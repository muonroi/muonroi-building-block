param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,
    [Parameter(Mandatory = $true)]
    [string]$ActivationProofPath,
    [string]$PublicKeyPath,
    [string]$BaseUrl = "http://127.0.0.1:7310",
    [string]$TenantId = "tenant-a",
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

function Get-CollectionCount {
    param([object]$Value)

    if ($null -eq $Value) { return 0 }
    if ($Value -is [System.Collections.IDictionary]) { return $Value.Count }
    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) { return @($Value).Count }

    $props = @($Value.PSObject.Properties)
    if ($props.Length -gt 0) { return $props.Length }

    return 0
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
    $OutputPath = Join-Path $workspaceRoot "_tmp\${projectName}_rule_engine_behaviors_$timestamp.json"
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
$env:LicenseConfigs__ProjectSeed = "MUONROI_RULE_ENGINE_SUPPLEMENT_TEST_SEED_20260303"
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
    $username = "rule_$suffix"
    $password = "P@ssw0rd!123"
    $webSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession

    $registerPayload = @{
        userName = $username
        password = $password
        email = "$username@example.com"
        phoneNumber = "0123456789"
        name = "Rule"
        surname = "Supplement"
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

    $authTenantHeaders = @{ Authorization = "Bearer $accessToken"; "x-tenant-id" = $TenantId }

    $reset = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/reset" -Method "Post" -Headers $authTenantHeaders -Body "{}" -WebSession $webSession
    if ($reset.StatusCode -ne 200) {
        throw "Cannot reset supplement state. HTTP $($reset.StatusCode)."
    }

    $cases = New-Object System.Collections.Generic.List[object]

    $allBody = @{ scenario = "allornothing"; executionMode = "AllOrNothing"; tenantId = $TenantId } | ConvertTo-Json -Depth 10
    $allResp = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/test" -Method "Post" -Headers $authTenantHeaders -Body $allBody -WebSession $webSession
    $allRuleResults = Find-PropertyValueRecursive -InputObject $allResp.Json -PropertyName "ruleResults"
    $allRuleCount = Get-CollectionCount -Value $allRuleResults
    $allErrors = Find-PropertyValueRecursive -InputObject $allResp.Json -PropertyName "errors"
    $allErrorCount = Get-CollectionCount -Value $allErrors
    $allPass = $allResp.StatusCode -eq 200 -and $allRuleCount -lt 3 -and $allErrorCount -ge 1
    Add-Case -Cases $cases -Id "G01" -Name "ExecutionMode.AllOrNothing" -Pass $allPass -Expected "HTTP 200; stop early (<3 rules); errors>=1" -Actual "HTTP $($allResp.StatusCode); ruleCount=$allRuleCount; errors=$allErrorCount" -Detail $allResp.Json

    $bestBody = @{ scenario = "besteffort"; executionMode = "BestEffort"; tenantId = $TenantId } | ConvertTo-Json -Depth 10
    $bestResp = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/test" -Method "Post" -Headers $authTenantHeaders -Body $bestBody -WebSession $webSession
    $bestRuleResults = Find-PropertyValueRecursive -InputObject $bestResp.Json -PropertyName "ruleResults"
    $bestRuleCount = Get-CollectionCount -Value $bestRuleResults
    $bestErrors = Find-PropertyValueRecursive -InputObject $bestResp.Json -PropertyName "errors"
    $bestErrorCount = Get-CollectionCount -Value $bestErrors
    $bestPass = $bestResp.StatusCode -eq 200 -and $bestRuleCount -ge 3 -and $bestErrorCount -ge 1
    Add-Case -Cases $cases -Id "G02" -Name "ExecutionMode.BestEffort" -Pass $bestPass -Expected "HTTP 200; executes all rules (>=3); errors aggregated" -Actual "HTTP $($bestResp.StatusCode); ruleCount=$bestRuleCount; errors=$bestErrorCount" -Detail $bestResp.Json

    $compBody = @{ scenario = "compensateonfailure"; executionMode = "CompensateOnFailure"; tenantId = $TenantId } | ConvertTo-Json -Depth 10
    $compResp = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/test" -Method "Post" -Headers $authTenantHeaders -Body $compBody -WebSession $webSession
    $compErrors = Find-PropertyValueRecursive -InputObject $compResp.Json -PropertyName "compensationErrors"
    $compErrorCount = Get-CollectionCount -Value $compErrors
    $compRules = Find-PropertyValueRecursive -InputObject $compResp.Json -PropertyName "compensatedRules"
    $compRulesCount = Get-CollectionCount -Value $compRules
    $compPass = $compResp.StatusCode -eq 200 -and $compRulesCount -ge 1 -and $compErrorCount -eq 0
    Add-Case -Cases $cases -Id "G03_G04" -Name "CompensateOnFailure + ICompensatableRule" -Pass $compPass -Expected "HTTP 200; compensatedRules>=1; compensationErrors=0" -Actual "HTTP $($compResp.StatusCode); compensatedRules=$compRulesCount; compensationErrors=$compErrorCount" -Detail $compResp.Json

    $factBody = @{ scenario = "factbag"; tenantId = $TenantId } | ConvertTo-Json -Depth 10
    $factResp = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/test" -Method "Post" -Headers $authTenantHeaders -Body $factBody -WebSession $webSession
    $factValue = Find-PropertyValueRecursive -InputObject $factResp.Json -PropertyName "order.validated"
    $factPass = $factResp.StatusCode -eq 200 -and [bool]$factValue
    Add-Case -Cases $cases -Id "G05" -Name "FactBag propagation" -Pass $factPass -Expected "HTTP 200; facts['order.validated']=true" -Actual "HTTP $($factResp.StatusCode); order.validated=$factValue" -Detail $factResp.Json

    $depBody = @{ scenario = "dependson"; tenantId = $TenantId } | ConvertTo-Json -Depth 10
    $depResp = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/test" -Method "Post" -Headers $authTenantHeaders -Body $depBody -WebSession $webSession
    $depRuleResults = Find-PropertyValueRecursive -InputObject $depResp.Json -PropertyName "ruleResults"
    $orderA = $null
    $orderB = $null
    if ($depRuleResults -is [System.Collections.IDictionary]) {
        if ($depRuleResults.Contains("RULE_A")) { $orderA = $depRuleResults["RULE_A"].executionOrder }
        if ($depRuleResults.Contains("RULE_B")) { $orderB = $depRuleResults["RULE_B"].executionOrder }
    }
    elseif ($null -ne $depRuleResults) {
        $propA = $depRuleResults.PSObject.Properties["RULE_A"]
        $propB = $depRuleResults.PSObject.Properties["RULE_B"]
        if ($null -ne $propA) { $orderA = $propA.Value.executionOrder }
        if ($null -ne $propB) { $orderB = $propB.Value.executionOrder }
    }
    $depPass = $depResp.StatusCode -eq 200 -and $null -ne $orderA -and $null -ne $orderB -and ([int]$orderB -gt [int]$orderA)
    Add-Case -Cases $cases -Id "G06" -Name "DependsOn ordering" -Pass $depPass -Expected "RULE_B.executionOrder > RULE_A.executionOrder" -Actual "HTTP $($depResp.StatusCode); RULE_A=$orderA; RULE_B=$orderB" -Detail $depResp.Json

    $quotaConcurrentBody = @{ scenario = "quota-concurrent"; tenantId = $TenantId; concurrentLimit = 1; rateLimitPerSecond = 100; artificialDelayMs = 900 } | ConvertTo-Json -Depth 10
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

    $job1 = Start-Job -ScriptBlock $jobScript -ArgumentList "$hostPort/api/v1/rule-engine/supplement/test", $accessToken, $TenantId, $quotaConcurrentBody
    $job2 = Start-Job -ScriptBlock $jobScript -ArgumentList "$hostPort/api/v1/rule-engine/supplement/test", $accessToken, $TenantId, $quotaConcurrentBody
    Wait-Job -Job $job1, $job2 | Out-Null
    $jobResults = @()
    $jobResults += Receive-Job -Job $job1
    $jobResults += Receive-Job -Job $job2
    Remove-Job -Job $job1, $job2 -Force
    $statusCodes = @($jobResults | ForEach-Object { [int]$_.StatusCode })
    $has429 = $statusCodes -contains 429
    $g07Pass = $has429
    Add-Case -Cases $cases -Id "G07" -Name "Quota ConcurrentExecutions" -Pass $g07Pass -Expected "At least one concurrent request returns 429" -Actual "StatusCodes=$($statusCodes -join ',')" -Detail $jobResults

    Start-Sleep -Seconds 2
    $rateTenantId = "$TenantId-rate"
    $quotaRateBody = @{ scenario = "quota-rate"; tenantId = $rateTenantId; concurrentLimit = 5; rateLimitPerSecond = 1; artificialDelayMs = 0 } | ConvertTo-Json -Depth 10
    $rateResp1 = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/test" -Method "Post" -Headers $authTenantHeaders -Body $quotaRateBody -WebSession $webSession
    $rateResp2 = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/test" -Method "Post" -Headers $authTenantHeaders -Body $quotaRateBody -WebSession $webSession
    $g08Pass = $rateResp1.StatusCode -eq 200 -and $rateResp2.StatusCode -eq 429
    Add-Case -Cases $cases -Id "G08" -Name "Quota RuleEvaluationsPerSecond" -Pass $g08Pass -Expected "First call 200; second call 429 with rateLimitPerSecond=1" -Actual "First=$($rateResp1.StatusCode); Second=$($rateResp2.StatusCode)" -Detail @($rateResp1, $rateResp2)

    $hookBody = @{ scenario = "hooks"; executionMode = "BestEffort"; tenantId = $TenantId } | ConvertTo-Json -Depth 10
    $hookResp = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/test" -Method "Post" -Headers $authTenantHeaders -Body $hookBody -WebSession $webSession
    $hookTrace = Find-PropertyValueRecursive -InputObject $hookResp.Json -PropertyName "hookTrace"
    $hookText = ($hookTrace | ForEach-Object { [string]$_ }) -join "|"
    $g10Pass = $hookResp.StatusCode -eq 200 -and $hookText.Contains("BeforeRule:HOOK_OK") -and $hookText.Contains("AfterRule:HOOK_OK") -and $hookText.Contains("OnError:HOOK_FAIL")
    Add-Case -Cases $cases -Id "G10" -Name "HookPoint execution order" -Pass $g10Pass -Expected "Hook trace contains Before/After/OnError markers" -Actual "HTTP $($hookResp.StatusCode); Trace=$hookText" -Detail $hookResp.Json

    $metricsResp = Invoke-HttpSafe -Url "$hostPort/api/v1/rule-engine/supplement/metrics" -Method "Get" -Headers $authTenantHeaders -WebSession $webSession
    $matched = [int](Find-PropertyValueRecursive -InputObject $metricsResp.Json -PropertyName "matched")
    $fired = [int](Find-PropertyValueRecursive -InputObject $metricsResp.Json -PropertyName "fired")
    $g11Pass = $metricsResp.StatusCode -eq 200 -and $matched -gt 0 -and $fired -gt 0
    Add-Case -Cases $cases -Id "G11" -Name "IRuleEventListener events" -Pass $g11Pass -Expected "metrics.rules.matched>0 and fired>0" -Actual "HTTP $($metricsResp.StatusCode); matched=$matched; fired=$fired" -Detail $metricsResp.Json

    $requiredFields = @("isSuccess", "executionMode", "ruleResults", "errors", "compensationErrors", "facts")
    $missing = New-Object System.Collections.Generic.List[string]
    foreach ($field in $requiredFields) {
        if (-not $allResp.Body.Contains('"' + $field + '"')) {
            $missing.Add($field)
        }
    }
    $g12Pass = $missing.Count -eq 0
    Add-Case -Cases $cases -Id "G12" -Name "OrchestratorResult structure" -Pass $g12Pass -Expected "Response contains isSuccess/executionMode/ruleResults/errors/compensationErrors/facts" -Actual "Missing=$($missing -join ',')" -Detail $allResp.Json

    $overallPass = @($cases | Where-Object { -not $_.Pass }).Count -eq 0

    $evidence = [ordered]@{
        OverallStatus = if ($overallPass) { "PASS" } else { "FAIL" }
        ProjectPath = $projectPath
        BaseUrl = $hostPort
        ActivationProofPath = $activationProofPath
        TenantId = $TenantId
        RegisteredUser = $username
        Cases = $cases
        CompletedAtUtc = [DateTime]::UtcNow.ToString("o")
    }

    $evidence | ConvertTo-Json -Depth 50 | Set-Content -Path $outputPath -Encoding UTF8

    if ($overallPass) {
        Write-Host "Rule engine behaviors supplement flow: PASS" -ForegroundColor Green
    }
    else {
        Write-Host "Rule engine behaviors supplement flow: FAIL" -ForegroundColor Red
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
