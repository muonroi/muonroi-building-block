param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,
    [Parameter(Mandatory = $true)]
    [string]$ActivationProofPath,
    [string]$PublicKeyPath,
    [string]$BaseUrl = "http://127.0.0.1:7310",
    [string]$TenantA = "tenant-a",
    [string]$TenantB = "tenant-b",
    [string]$OutputPath,
    [int]$StartupTimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param([string]$PathValue)
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return $null }
    return (Resolve-Path $PathValue).Path
}

function Find-PropertyValueRecursive {
    param(
        [object]$InputObject,
        [string]$PropertyName,
        [int]$Depth = 0
    )

    if ($null -eq $InputObject) { return $null }
    if ($Depth -gt 30) { return $null }

    if ($InputObject -is [System.Collections.IDictionary]) {
        foreach ($key in $InputObject.Keys) {
            if ([string]::Equals([string]$key, $PropertyName, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $InputObject[$key]
            }
        }

        foreach ($key in $InputObject.Keys) {
            $value = Find-PropertyValueRecursive -InputObject $InputObject[$key] -PropertyName $PropertyName -Depth ($Depth + 1)
            if ($null -ne $value) { return $value }
        }

        return $null
    }

    $props = $InputObject.PSObject.Properties
    if ($null -eq $props) { return $null }

    foreach ($prop in $props) {
        if ([string]::Equals($prop.Name, $PropertyName, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $prop.Value
        }
    }

    foreach ($prop in $props) {
        $value = Find-PropertyValueRecursive -InputObject $prop.Value -PropertyName $PropertyName -Depth ($Depth + 1)
        if ($null -ne $value) { return $value }
    }

    if ($InputObject -is [System.Collections.IEnumerable] -and -not ($InputObject -is [string])) {
        foreach ($item in $InputObject) {
            $value = Find-PropertyValueRecursive -InputObject $item -PropertyName $PropertyName -Depth ($Depth + 1)
            if ($null -ne $value) { return $value }
        }
    }

    return $null
}

function Wait-ForTcpReady {
    param(
        [string]$Url,
        [int]$TimeoutSeconds,
        [System.Diagnostics.Process]$Process,
        [string]$DisplayName
    )

    $uri = [Uri]$Url
    $targetHost = $uri.Host
    $targetPort = $uri.Port
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            throw "$DisplayName exited early with code $($Process.ExitCode)."
        }

        $client = $null
        try {
            $client = New-Object System.Net.Sockets.TcpClient
            $connect = $client.BeginConnect($targetHost, $targetPort, $null, $null)
            if ($connect.AsyncWaitHandle.WaitOne(1000, $false)) {
                $client.EndConnect($connect)
                if ($client.Connected) {
                    $client.Close()
                    return
                }
            }
        }
        catch {
        }
        finally {
            if ($null -ne $client) {
                try { $client.Close() } catch {}
            }
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for $DisplayName at $Url."
}

function Stop-ProcessSafe {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process) { return }
    if ($Process.HasExited) { return }

    try { Stop-Process -Id $Process.Id -Force -ErrorAction Stop } catch {}
}

function Invoke-HttpSafe {
    param(
        [string]$Url,
        [string]$Method = "Get",
        [hashtable]$Headers,
        [string]$Body,
        [Microsoft.PowerShell.Commands.WebRequestSession]$WebSession
    )

    try {
        if ([string]::IsNullOrWhiteSpace($Body)) {
            if ($null -ne $WebSession) {
                $response = Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers -WebSession $WebSession -UseBasicParsing -TimeoutSec 20
            }
            else {
                $response = Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers -UseBasicParsing -TimeoutSec 20
            }
        }
        else {
            if ($null -ne $WebSession) {
                $response = Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers -WebSession $WebSession -ContentType "application/json" -Body $Body -UseBasicParsing -TimeoutSec 20
            }
            else {
                $response = Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers -ContentType "application/json" -Body $Body -UseBasicParsing -TimeoutSec 20
            }
        }

        $json = $null
        if (-not [string]::IsNullOrWhiteSpace($response.Content)) {
            try { $json = $response.Content | ConvertFrom-Json } catch {}
        }

        return [ordered]@{
            StatusCode = [int]$response.StatusCode
            Body = [string]$response.Content
            Json = $json
            Error = $null
        }
    }
    catch {
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode.value__
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $content = $reader.ReadToEnd()

            $json = $null
            if (-not [string]::IsNullOrWhiteSpace($content)) {
                try { $json = $content | ConvertFrom-Json } catch {}
            }

            return [ordered]@{
                StatusCode = $status
                Body = $content
                Json = $json
                Error = $null
            }
        }

        return [ordered]@{
            StatusCode = -1
            Body = ""
            Json = $null
            Error = $_.Exception.Message
        }
    }
}

function Add-Case {
    param(
        [System.Collections.Generic.List[object]]$Cases,
        [string]$Id,
        [string]$Name,
        [bool]$Pass,
        [string]$Expected,
        [string]$Actual,
        [object]$Detail
    )

    $Cases.Add([ordered]@{
            Id = $Id
            Name = $Name
            Pass = $Pass
            Expected = $Expected
            Actual = $Actual
            Detail = $Detail
        })
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Resolve-AbsolutePath -PathValue $ProjectPath
$activationProofPath = Resolve-AbsolutePath -PathValue $ActivationProofPath

if (-not $projectPath.EndsWith(".csproj", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "ProjectPath must be a .csproj file."
}

if (-not (Test-Path $projectPath)) {
    throw "ProjectPath not found: $projectPath"
}

if (-not (Test-Path $activationProofPath)) {
    throw "ActivationProofPath not found: $activationProofPath"
}

if ([string]::IsNullOrWhiteSpace($PublicKeyPath)) {
    $PublicKeyPath = Join-Path $workspaceRoot "tools\MockLicenseServer\server_public_key.pem"
}
$publicKeyPath = Resolve-AbsolutePath -PathValue $PublicKeyPath
if (-not (Test-Path $publicKeyPath)) {
    throw "PublicKeyPath not found: $publicKeyPath"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
    $OutputPath = Join-Path $workspaceRoot "_tmp\${projectName}_multitenant_rule_isolation_$timestamp.json"
}
$outputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDir = Split-Path -Parent $outputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$stdoutLog = [System.IO.Path]::ChangeExtension($outputPath, ".app.out.log")
$stderrLog = [System.IO.Path]::ChangeExtension($outputPath, ".app.err.log")

$baseUri = [Uri]$BaseUrl
$hostPort = "$($baseUri.Scheme)://$($baseUri.Host):$($baseUri.Port)"

$prevEnv = @{
    ASPNETCORE_ENVIRONMENT = $env:ASPNETCORE_ENVIRONMENT
    LicenseConfigs__Mode = $env:LicenseConfigs__Mode
    LicenseConfigs__ActivationProofPath = $env:LicenseConfigs__ActivationProofPath
    LicenseConfigs__PublicKeyPath = $env:LicenseConfigs__PublicKeyPath
    LicenseConfigs__ProjectSeed = $env:LicenseConfigs__ProjectSeed
    MultiTenantConfigs__Enabled = $env:MultiTenantConfigs__Enabled
    MultiTenantConfigs__RequireTenantClaimForAuthenticatedUser = $env:MultiTenantConfigs__RequireTenantClaimForAuthenticatedUser
    TokenConfigs__MultiTenantEnabled = $env:TokenConfigs__MultiTenantEnabled
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:LicenseConfigs__Mode = "Offline"
$env:LicenseConfigs__ActivationProofPath = $activationProofPath
$env:LicenseConfigs__PublicKeyPath = $publicKeyPath
$env:LicenseConfigs__ProjectSeed = "MUONROI_MULTITENANT_SUPPLEMENT_TEST_SEED_20260303"
$env:MultiTenantConfigs__Enabled = "true"
$env:MultiTenantConfigs__RequireTenantClaimForAuthenticatedUser = "false"
$env:TokenConfigs__MultiTenantEnabled = "true"

$proc = $null
try {
    $proc = Start-Process dotnet `
        -ArgumentList @("run", "--project", $projectPath, "--urls", $hostPort) `
        -WorkingDirectory (Split-Path -Parent $projectPath) `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError $stderrLog `
        -PassThru

    Wait-ForTcpReady -Url $hostPort -TimeoutSeconds $StartupTimeoutSeconds -Process $proc -DisplayName "API"

    $suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
    $username = "tenant_$suffix"
    $password = "P@ssw0rd!123"
    $webSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession

    $registerPayload = @{
        userName = $username
        password = $password
        email = "$username@example.com"
        phoneNumber = "0123456789"
        name = "Tenant"
        surname = "Isolation"
        isActive = $true
        isTwoFactorEnabled = $false
        isUseThirdPartyLogin = $false
        externalLoginProvider = ""
        externalLoginToken = ""
    } | ConvertTo-Json -Depth 10

    $registerResponse = Invoke-HttpSafe -Url "$hostPort/api/v1/Auth/register" -Method "Post" -Body $registerPayload -WebSession $webSession
    if ($registerResponse.StatusCode -lt 200 -or $registerResponse.StatusCode -ge 300) {
        throw "Register failed with HTTP $($registerResponse.StatusCode)."
    }

    $loginPayload = @{ username = $username; password = $password } | ConvertTo-Json -Depth 5
    $loginResponse = Invoke-HttpSafe -Url "$hostPort/api/v1/Auth/login" -Method "Post" -Body $loginPayload -WebSession $webSession
    if ($loginResponse.StatusCode -lt 200 -or $loginResponse.StatusCode -ge 300) {
        throw "Login failed with HTTP $($loginResponse.StatusCode)."
    }

    $accessTokenValue = Find-PropertyValueRecursive -InputObject $loginResponse.Json -PropertyName "accessToken"
    $accessToken = [string]$accessTokenValue
    if ([string]::IsNullOrWhiteSpace($accessToken)) {
        throw "Login succeeded but accessToken was not found in response."
    }

    $tenantAHeaders = @{ Authorization = "Bearer $accessToken"; "x-tenant-id" = $TenantA }
    $tenantBHeaders = @{ Authorization = "Bearer $accessToken"; "x-tenant-id" = $TenantB }

    $reset = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/reset" -Method "Post" -Headers $tenantAHeaders -Body "{}" -WebSession $webSession
    if ($reset.StatusCode -ne 200) {
        throw "Cannot reset supplement state. HTTP $($reset.StatusCode)."
    }

    $cases = New-Object System.Collections.Generic.List[object]

    $workflow = "order-validation"

    $registerA = @{ tenantId = $TenantA; workflow = $workflow; ruleCode = "TA_RULE"; outputKey = "tenant.marker"; outputValue = "A" } | ConvertTo-Json -Depth 10
    $registerAResp = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/tenant-rules/register" -Method "Post" -Headers $tenantAHeaders -Body $registerA -WebSession $webSession

    $registerB = @{ tenantId = $TenantB; workflow = $workflow; ruleCode = "TB_RULE"; outputKey = "tenant.marker"; outputValue = "B" } | ConvertTo-Json -Depth 10
    $registerBResp = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/tenant-rules/register" -Method "Post" -Headers $tenantBHeaders -Body $registerB -WebSession $webSession

    $registerPass = $registerAResp.StatusCode -eq 200 -and $registerBResp.StatusCode -eq 200
    Add-Case -Cases $cases -Id "G09_REG" -Name "Register tenant rules" -Pass $registerPass -Expected "Both tenant registrations return HTTP 200" -Actual "A=$($registerAResp.StatusCode); B=$($registerBResp.StatusCode)" -Detail @($registerAResp.Json, $registerBResp.Json)

    $evaluateBody = @{ workflow = $workflow; input = @{ amount = 100 } } | ConvertTo-Json -Depth 15
    $evalA = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/tenant-rules/evaluate" -Method "Post" -Headers $tenantAHeaders -Body $evaluateBody -WebSession $webSession
    $evalB = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/tenant-rules/evaluate" -Method "Post" -Headers $tenantBHeaders -Body $evaluateBody -WebSession $webSession

    $rulesA = @()
    $rulesB = @()
    $markerA = $null
    $markerB = $null

    $appliedA = Find-PropertyValueRecursive -InputObject $evalA.Json -PropertyName "appliedRules"
    if ($null -ne $appliedA) { $rulesA = @($appliedA | ForEach-Object { [string]$_ }) }
    $appliedB = Find-PropertyValueRecursive -InputObject $evalB.Json -PropertyName "appliedRules"
    if ($null -ne $appliedB) { $rulesB = @($appliedB | ForEach-Object { [string]$_ }) }

    $markerA = Find-PropertyValueRecursive -InputObject $evalA.Json -PropertyName "tenant.marker"
    $markerB = Find-PropertyValueRecursive -InputObject $evalB.Json -PropertyName "tenant.marker"

    $isolationPass =
        $evalA.StatusCode -eq 200 -and
        $evalB.StatusCode -eq 200 -and
        ($rulesA -contains "TA_RULE") -and
        (-not ($rulesA -contains "TB_RULE")) -and
        ($rulesB -contains "TB_RULE") -and
        (-not ($rulesB -contains "TA_RULE")) -and
        ([string]$markerA -eq "A") -and
        ([string]$markerB -eq "B")

    Add-Case -Cases $cases -Id "G09" -Name "Multi-tenant rule isolation" -Pass $isolationPass -Expected "tenant-a only sees TA_RULE; tenant-b only sees TB_RULE" -Actual "A=[$($rulesA -join ',')], marker=$markerA; B=[$($rulesB -join ',')], marker=$markerB" -Detail @($evalA.Json, $evalB.Json)

    $quotaConcurrentBody = @{ scenario = "quota-concurrent"; tenantId = $TenantA; concurrentLimit = 1; rateLimitPerSecond = 100; artificialDelayMs = 900 } | ConvertTo-Json -Depth 10

    $jobScript = {
        param($Url, $Token, $Tenant, $Body)
        $headers = @{ Authorization = "Bearer $Token"; "x-tenant-id" = $Tenant }
        try {
            $resp = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -ContentType "application/json" -Body $Body -UseBasicParsing -TimeoutSec 30
            return [pscustomobject]@{ StatusCode = [int]$resp.StatusCode; Body = $resp.Content }
        }
        catch {
            if ($_.Exception.Response) {
                $status = [int]$_.Exception.Response.StatusCode.value__
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $content = $reader.ReadToEnd()
                return [pscustomobject]@{ StatusCode = $status; Body = $content }
            }

            return [pscustomobject]@{ StatusCode = -1; Body = $_.Exception.Message }
        }
    }

    $job1 = Start-Job -ScriptBlock $jobScript -ArgumentList "$hostPort/api/v1/rule-engine/supplement/test", $accessToken, $TenantA, $quotaConcurrentBody
    $job2 = Start-Job -ScriptBlock $jobScript -ArgumentList "$hostPort/api/v1/rule-engine/supplement/test", $accessToken, $TenantA, $quotaConcurrentBody
    Wait-Job -Job $job1, $job2 | Out-Null
    $tenantAQuotaResults = @()
    $tenantAQuotaResults += Receive-Job -Job $job1
    $tenantAQuotaResults += Receive-Job -Job $job2
    Remove-Job -Job $job1, $job2 -Force

    $tenantAStatusCodes = @($tenantAQuotaResults | ForEach-Object { [int]$_.StatusCode })
    $tenantAHas429 = $tenantAStatusCodes -contains 429

    $quotaBBody = @{ scenario = "quota-concurrent"; tenantId = $TenantB; concurrentLimit = 1; rateLimitPerSecond = 100; artificialDelayMs = 100 } | ConvertTo-Json -Depth 10
    $tenantBQuotaResp = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/test" -Method "Post" -Headers $tenantBHeaders -Body $quotaBBody -WebSession $webSession

    $quotaIsolationPass = $tenantAHas429 -and $tenantBQuotaResp.StatusCode -eq 200
    Add-Case -Cases $cases -Id "G09_QUOTA" -Name "Tenant quota isolation" -Pass $quotaIsolationPass -Expected "tenant-a overload gets 429; tenant-b remains 200" -Actual "tenant-a statuses=$($tenantAStatusCodes -join ','); tenant-b=$($tenantBQuotaResp.StatusCode)" -Detail @($tenantAQuotaResults, $tenantBQuotaResp.Json)

    $overallPass = @($cases | Where-Object { -not $_.Pass }).Count -eq 0

    $evidence = [ordered]@{
        OverallStatus = if ($overallPass) { "PASS" } else { "FAIL" }
        ProjectPath = $projectPath
        BaseUrl = $hostPort
        ActivationProofPath = $activationProofPath
        TenantA = $TenantA
        TenantB = $TenantB
        RegisteredUser = $username
        Cases = $cases
        CompletedAtUtc = [DateTime]::UtcNow.ToString("o")
    }

    $evidence | ConvertTo-Json -Depth 50 | Set-Content -Path $outputPath -Encoding UTF8

    if ($overallPass) {
        Write-Host "Multi-tenant rule isolation supplement flow: PASS" -ForegroundColor Green
    }
    else {
        Write-Host "Multi-tenant rule isolation supplement flow: FAIL" -ForegroundColor Red
    }

    Write-Host "Evidence: $outputPath"

    if (-not $overallPass) {
        exit 1
    }
}
finally {
    Stop-ProcessSafe -Process $proc

    foreach ($k in $prevEnv.Keys) {
        [Environment]::SetEnvironmentVariable($k, $prevEnv[$k], "Process")
    }
}
