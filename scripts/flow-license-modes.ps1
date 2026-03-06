param(
    [string]$Organization = "Muonroi Local Test",
    [string]$OutputRoot,
    [string[]]$ProjectPaths,
    [string[]]$Modes = @("Free", "Paid", "Enterprise"),
    [int]$MockServerPort = 6060,
    [int]$AppStartPort = 7301,
    [int]$StartupTimeoutSeconds = 120,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-ProjectPaths {
    param(
        [string]$WorkspaceRoot,
        [string[]]$ExplicitProjectPaths
    )

    if ($ExplicitProjectPaths -and $ExplicitProjectPaths.Count -gt 0) {
        $resolved = New-Object System.Collections.Generic.List[string]
        foreach ($path in $ExplicitProjectPaths) {
            if ([string]::IsNullOrWhiteSpace($path)) { continue }
            if (-not (Test-Path $path)) {
                throw "Project path not found: $path"
            }

            $fullPath = (Resolve-Path $path).Path
            if (-not $fullPath.EndsWith(".csproj", [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Project path must be a .csproj file: $fullPath"
            }

            $resolved.Add($fullPath)
        }

        return $resolved.ToArray()
    }

    $candidates = @(
        (Join-Path $WorkspaceRoot "Muonroi.BaseTemplate\src\Muonroi.BaseTemplate.API\Muonroi.BaseTemplate.API.csproj"),
        (Join-Path $WorkspaceRoot "Muonroi.Microservices.Template\src\Services\Muonroi.Microservices.Catalog\Muonroi.Microservices.Catalog.csproj"),
        (Join-Path $WorkspaceRoot "Muonroi.Modular.Template\src\Host\Muonroi.Modular.Host\Muonroi.Modular.Host.csproj")
    )

    $projects = New-Object System.Collections.Generic.List[string]
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            $projects.Add((Resolve-Path $candidate).Path)
        }
    }

    if ($projects.Count -eq 0) {
        throw "No template projects found to test. Provide -ProjectPaths explicitly."
    }

    return $projects.ToArray()
}

function Normalize-Modes {
    param([string[]]$InputModes)

    $valid = @{
        "free" = "Free"
        "paid" = "Paid"
        "enterprise" = "Enterprise"
    }

    $result = New-Object System.Collections.Generic.List[string]
    foreach ($modeItem in $InputModes) {
        if ([string]::IsNullOrWhiteSpace($modeItem)) { continue }

        $tokens = @($modeItem -split "," | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        foreach ($token in $tokens) {
            $normalized = $token.Trim().ToLowerInvariant()
            if (-not $valid.ContainsKey($normalized)) {
                throw "Unsupported mode '$token'. Supported values: Free, Paid, Enterprise."
            }

            $canonical = $valid[$normalized]
            if (-not $result.Contains($canonical)) {
                $result.Add($canonical)
            }
        }
    }

    if ($result.Count -eq 0) {
        throw "No mode selected. Use -Modes Free,Paid,Enterprise."
    }

    return $result.ToArray()
}

function Wait-ForHttpOk {
    param(
        [string]$Url,
        [int]$TimeoutSeconds,
        [System.Diagnostics.Process]$Process,
        [string]$DisplayName
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            throw "$DisplayName process exited early with code $($Process.ExitCode)."
        }

        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -eq 200) {
                return
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    throw "Timed out waiting for $DisplayName at $Url."
}

function Start-DotnetProcess {
    param(
        [string]$ProjectPath,
        [int]$Port,
        [string]$OutputLogPath,
        [string]$ErrorLogPath,
        [hashtable]$EnvironmentOverrides,
        [switch]$NoBuild
    )

    $backup = @{}
    foreach ($pair in $EnvironmentOverrides.GetEnumerator()) {
        $backup[$pair.Key] = [Environment]::GetEnvironmentVariable($pair.Key, "Process")
        [Environment]::SetEnvironmentVariable($pair.Key, [string]$pair.Value, "Process")
    }

    try {
        $arguments = @("run")
        if ($NoBuild) {
            $arguments += "--no-build"
        }
        $arguments += @("--project", $ProjectPath, "--urls", "http://127.0.0.1:$Port")

        return Start-Process dotnet `
            -ArgumentList $arguments `
            -WorkingDirectory (Split-Path -Parent $ProjectPath) `
            -RedirectStandardOutput $OutputLogPath `
            -RedirectStandardError $ErrorLogPath `
            -PassThru
    }
    finally {
        foreach ($key in $backup.Keys) {
            [Environment]::SetEnvironmentVariable($key, $backup[$key], "Process")
        }
    }
}

function Stop-ProcessSafe {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process) { return }
    if ($Process.HasExited) { return }

    try {
        Stop-Process -Id $Process.Id -Force -ErrorAction Stop
    }
    catch {
        Write-Warning "Failed to stop process $($Process.Id): $($_.Exception.Message)"
    }
}

function Get-DetectedTier {
    param(
        [string]$OutputLogPath,
        [string]$ErrorLogPath
    )

    $buffer = ""
    if (Test-Path $OutputLogPath) {
        $buffer += Get-Content -Path $OutputLogPath -Raw
    }
    if (Test-Path $ErrorLogPath) {
        $buffer += "`n" + (Get-Content -Path $ErrorLogPath -Raw)
    }

    $match = [regex]::Match($buffer, "Verified tier:\s*(Free|Licensed|Enterprise)", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($match.Success) {
        return $match.Groups[1].Value
    }

    return $null
}

function Find-PropertyValueRecursive {
    param(
        [object]$InputObject,
        [string]$PropertyName
    )

    if ($null -eq $InputObject) {
        return $null
    }

    if ($InputObject -is [System.Collections.IDictionary]) {
        foreach ($key in $InputObject.Keys) {
            if ([string]::Equals([string]$key, $PropertyName, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $InputObject[$key]
            }
        }

        foreach ($key in $InputObject.Keys) {
            $value = Find-PropertyValueRecursive -InputObject $InputObject[$key] -PropertyName $PropertyName
            if ($null -ne $value) {
                return $value
            }
        }

        return $null
    }

    if ($InputObject -is [System.Collections.IEnumerable] -and -not ($InputObject -is [string])) {
        foreach ($item in $InputObject) {
            $value = Find-PropertyValueRecursive -InputObject $item -PropertyName $PropertyName
            if ($null -ne $value) {
                return $value
            }
        }

        return $null
    }

    $props = $InputObject.PSObject.Properties
    if ($null -eq $props) {
        return $null
    }

    foreach ($prop in $props) {
        if ([string]::Equals($prop.Name, $PropertyName, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $prop.Value
        }
    }

    foreach ($prop in $props) {
        $value = Find-PropertyValueRecursive -InputObject $prop.Value -PropertyName $PropertyName
        if ($null -ne $value) {
            return $value
        }
    }

    return $null
}

function Invoke-AuthFlowVerification {
    param(
        [string]$BaseUrl,
        [string]$EvidencePath
    )

    $suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
    $username = "user_$suffix"
    $password = "P@ssw0rd!123"
    $webSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession

    $registerPayload = @{
        userName = $username
        password = $password
        email = "$username@example.com"
        phoneNumber = "0123456789"
        name = "Auto"
        surname = "Verify"
        isActive = $true
        isTwoFactorEnabled = $false
        isUseThirdPartyLogin = $false
        externalLoginProvider = ""
        externalLoginToken = ""
    } | ConvertTo-Json -Depth 10

    $registerResponse = Invoke-WebRequest `
        -Uri "$BaseUrl/api/v1/Auth/register" `
        -Method Post `
        -ContentType "application/json" `
        -Body $registerPayload `
        -WebSession $webSession `
        -UseBasicParsing

    if ($registerResponse.StatusCode -lt 200 -or $registerResponse.StatusCode -ge 300) {
        throw "Register failed with HTTP $($registerResponse.StatusCode)."
    }

    $loginPayload = @{
        username = $username
        password = $password
    } | ConvertTo-Json -Depth 5

    $loginResponse = Invoke-WebRequest `
        -Uri "$BaseUrl/api/v1/Auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body $loginPayload `
        -WebSession $webSession `
        -UseBasicParsing

    if ($loginResponse.StatusCode -lt 200 -or $loginResponse.StatusCode -ge 300) {
        throw "Login failed with HTTP $($loginResponse.StatusCode)."
    }

    $loginJson = $loginResponse.Content | ConvertFrom-Json
    $accessTokenValue = Find-PropertyValueRecursive -InputObject $loginJson -PropertyName "accessToken"
    $accessToken = [string]$accessTokenValue
    if ([string]::IsNullOrWhiteSpace($accessToken)) {
        throw "Login succeeded but accessToken was not found in response."
    }

    $debugSnapshot = [ordered]@{
        Username = $username
        RegisterStatusCode = $registerResponse.StatusCode
        LoginStatusCode = $loginResponse.StatusCode
        LoginBody = $loginResponse.Content
        LoginSetCookie = $loginResponse.Headers["Set-Cookie"]
        AccessTokenLength = $accessToken.Length
    }
    $debugSnapshot | ConvertTo-Json -Depth 10 | Set-Content -Path $EvidencePath -Encoding UTF8

    $verifyResponse = Invoke-WebRequest `
        -Uri "$BaseUrl/api/v1/Auth/verify-token" `
        -Method Get `
        -Headers @{ Authorization = "Bearer $accessToken" } `
        -WebSession $webSession `
        -UseBasicParsing

    if ($verifyResponse.StatusCode -ne 200) {
        throw "Token verification GET failed with HTTP $($verifyResponse.StatusCode)."
    }

    $verifyJson = $verifyResponse.Content | ConvertFrom-Json
    $hasAuthorizationHeader = [System.Convert]::ToBoolean(
        (Find-PropertyValueRecursive -InputObject $verifyJson -PropertyName "hasAuthorizationHeader"))
    if (-not $hasAuthorizationHeader) {
        throw "Token verification GET did not receive Authorization header."
    }

    $evidence = [ordered]@{
        Username = $username
        RegisterStatusCode = $registerResponse.StatusCode
        LoginStatusCode = $loginResponse.StatusCode
        LoginBody = $loginResponse.Content
        LoginSetCookie = $loginResponse.Headers["Set-Cookie"]
        AccessTokenLength = $accessToken.Length
        VerifyStatusCode = $verifyResponse.StatusCode
        VerifyBody = $verifyResponse.Content
        HasAuthorizationHeader = $hasAuthorizationHeader
    }
    $evidence | ConvertTo-Json -Depth 10 | Set-Content -Path $EvidencePath -Encoding UTF8
}

function New-ActivationProofFile {
    param(
        [string]$ServerBaseUrl,
        [string]$LicenseKey,
        [string]$OutputProofPath
    )

    $request = @{
        licenseKey = $LicenseKey
        machineFingerprint = "local-dev"
        productVersion = "1.0.0"
        activationTime = (Get-Date).ToUniversalTime().ToString("o")
        environment = "Development"
    } | ConvertTo-Json -Depth 5

    $response = Invoke-RestMethod `
        -Uri "$ServerBaseUrl/api/v1/activate" `
        -Method Post `
        -ContentType "application/json" `
        -Body $request

    if (-not $response.success) {
        throw "Activation API failed: $($response.error)"
    }

    $proofRaw = $response.proof | ConvertTo-Json -Depth 20
    $proofDir = Split-Path -Parent $OutputProofPath
    New-Item -ItemType Directory -Path $proofDir -Force | Out-Null
    Set-Content -Path $OutputProofPath -Value $proofRaw -Encoding UTF8
}

$buildingBlockRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$workspaceRoot = (Resolve-Path (Join-Path $buildingBlockRoot "..")).Path
$mockServerRoot = Join-Path $buildingBlockRoot "tools\MockLicenseServer"
$generateScriptPath = Join-Path $mockServerRoot "Generate-MockLicenseKeys.ps1"
$generatedSummaryPath = Join-Path $mockServerRoot "generated-licenses\generated-license-keys.json"
$mockServerPublicKeyPath = Join-Path $mockServerRoot "server_public_key.pem"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $workspaceRoot "_tmp\flow_license_modes_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
}
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

$projects = Resolve-ProjectPaths -WorkspaceRoot $workspaceRoot -ExplicitProjectPaths $ProjectPaths
$selectedModes = Normalize-Modes -InputModes $Modes

$expectedTierByMode = @{
    "Free" = "Free"
    "Paid" = "Licensed"
    "Enterprise" = "Enterprise"
}

Write-Host "=== FLOW 3: FREE/PAID/ENTERPRISE MODE E2E ===" -ForegroundColor Cyan
Write-Host "Output root: $OutputRoot"
Write-Host ""

Write-Host "[1/4] Generate Paid/Enterprise keys" -ForegroundColor Yellow
powershell -ExecutionPolicy Bypass -File $generateScriptPath -Organization $Organization
if (-not (Test-Path $generatedSummaryPath)) {
    throw "Generated key summary not found: $generatedSummaryPath"
}

$keySummary = Get-Content -Path $generatedSummaryPath -Raw | ConvertFrom-Json
$paidKey = [string]$keySummary.PaidKey
$enterpriseKey = [string]$keySummary.EnterpriseKey
if ([string]::IsNullOrWhiteSpace($paidKey) -or [string]::IsNullOrWhiteSpace($enterpriseKey)) {
    throw "Generated keys are missing in summary: $generatedSummaryPath"
}

$serverOutLog = Join-Path $OutputRoot "mock-license-server.out.log"
$serverErrLog = Join-Path $OutputRoot "mock-license-server.err.log"
$mockServerProcess = $null

try {
    Write-Host "[2/4] Start MockLicenseServer at port $MockServerPort" -ForegroundColor Yellow
    $mockServerProcess = Start-Process dotnet `
        -ArgumentList @("run", "--project", ".\MockLicenseServer.csproj", "--urls", "http://127.0.0.1:$MockServerPort") `
        -WorkingDirectory $mockServerRoot `
        -RedirectStandardOutput $serverOutLog `
        -RedirectStandardError $serverErrLog `
        -PassThru

    Wait-ForHttpOk -Url "http://127.0.0.1:$MockServerPort/health" -TimeoutSeconds $StartupTimeoutSeconds -Process $mockServerProcess -DisplayName "MockLicenseServer"
    if (-not (Test-Path $mockServerPublicKeyPath)) {
        throw "Mock server public key not found after startup: $mockServerPublicKeyPath"
    }

    Write-Host "[3/4] Build target projects" -ForegroundColor Yellow
    if (-not $SkipBuild) {
        foreach ($project in $projects) {
            Write-Host "  Building: $project"
            dotnet build $project
        }
    }
    else {
        Write-Host "  Skipped build by -SkipBuild."
    }

    Write-Host "[4/4] Run mode verification matrix" -ForegroundColor Yellow
    $results = New-Object System.Collections.Generic.List[object]
    $port = $AppStartPort
    $serverBaseUrl = "http://127.0.0.1:$MockServerPort"

    foreach ($project in $projects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
        foreach ($mode in $selectedModes) {
            $modeFolder = Join-Path $OutputRoot (Join-Path $projectName $mode.ToLowerInvariant())
            New-Item -ItemType Directory -Path $modeFolder -Force | Out-Null

            $appOutLog = Join-Path $modeFolder "app.out.log"
            $appErrLog = Join-Path $modeFolder "app.err.log"
            $proofPath = Join-Path $modeFolder "activation_proof.json"
            $authEvidencePath = Join-Path $modeFolder "auth-flow-evidence.json"

            if (Test-Path $proofPath) {
                Remove-Item -Path $proofPath -Force
            }

            if ($mode -eq "Paid") {
                New-ActivationProofFile -ServerBaseUrl $serverBaseUrl -LicenseKey $paidKey -OutputProofPath $proofPath
            }
            elseif ($mode -eq "Enterprise") {
                New-ActivationProofFile -ServerBaseUrl $serverBaseUrl -LicenseKey $enterpriseKey -OutputProofPath $proofPath
            }

            $envOverrides = @{
                "ASPNETCORE_ENVIRONMENT" = "Development"
                "LicenseConfigs__Mode" = "Offline"
                "LicenseConfigs__ActivationProofPath" = $proofPath
                "LicenseConfigs__PublicKeyPath" = if ($mode -eq "Free") { "" } else { $mockServerPublicKeyPath }
                "LicenseConfigs__ProjectSeed" = "MUONROI_LICENSE_MODE_TEST_SEED_20260303"
            }

            Write-Host "  [$projectName][$mode] Running on port $port..."
            $appProcess = $null
            try {
                $appProcess = Start-DotnetProcess `
                    -ProjectPath $project `
                    -Port $port `
                    -OutputLogPath $appOutLog `
                    -ErrorLogPath $appErrLog `
                    -EnvironmentOverrides $envOverrides `
                    -NoBuild

                Wait-ForHttpOk `
                    -Url "http://127.0.0.1:$port/swagger/index.html" `
                    -TimeoutSeconds $StartupTimeoutSeconds `
                    -Process $appProcess `
                    -DisplayName "$projectName $mode"

                Invoke-AuthFlowVerification `
                    -BaseUrl "http://127.0.0.1:$port" `
                    -EvidencePath $authEvidencePath

                Stop-ProcessSafe -Process $appProcess
                Start-Sleep -Seconds 1

                $detectedTier = Get-DetectedTier -OutputLogPath $appOutLog -ErrorLogPath $appErrLog
                $expectedTier = $expectedTierByMode[$mode]
                $isPass = $detectedTier -eq $expectedTier

                if (-not $isPass) {
                    $detectedDisplay = if ([string]::IsNullOrWhiteSpace($detectedTier)) { "<not found>" } else { $detectedTier }
                    throw "Tier assertion failed. Expected '$expectedTier' but detected '$detectedDisplay'. Logs: $appOutLog"
                }

                $results.Add([pscustomobject]@{
                        Project      = $projectName
                        Mode         = $mode
                        ExpectedTier = $expectedTier
                        DetectedTier = $detectedTier
                        Status       = "PASS"
                        AuthFlow     = "PASS"
                        LogPath      = $appOutLog
                    })
            }
            catch {
                Stop-ProcessSafe -Process $appProcess
                $results.Add([pscustomobject]@{
                        Project      = $projectName
                        Mode         = $mode
                        ExpectedTier = $expectedTierByMode[$mode]
                        DetectedTier = $null
                        Status       = "FAIL"
                        AuthFlow     = "FAIL"
                        LogPath      = $appOutLog
                        Error        = $_.Exception.Message
                    })
            }
            finally {
                Stop-ProcessSafe -Process $appProcess
            }

            $port++
        }
    }

    Write-Host ""
    Write-Host "Mode verification summary:" -ForegroundColor Yellow
    $results | Format-Table -AutoSize

    $failed = @($results | Where-Object { $_.Status -eq "FAIL" })
    if ($failed.Count -gt 0) {
        throw "Mode verification failed: $($failed.Count) case(s). Inspect logs under $OutputRoot."
    }

    Write-Host ""
    Write-Host "Flow license modes completed successfully." -ForegroundColor Green
}
finally {
    Stop-ProcessSafe -Process $mockServerProcess
}
