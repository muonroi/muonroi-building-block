param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,
    [Parameter(Mandatory = $true)]
    [string]$ActivationProofPath,
    [string]$PublicKeyPath,
    [string]$BaseUrl = "http://127.0.0.1:7310",
    [string]$OutputPath,
    [int]$StartupTimeoutSeconds = 120,
    [string]$TenantA = "tenant-a",
    [string]$TenantB = "tenant-b"
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

function Wait-ForTcpReady {
    param(
        [string]$Url,
        [int]$TimeoutSeconds,
        [System.Diagnostics.Process]$Process,
        [string]$DisplayName
    )

    $uri = [Uri]$Url
    $targetHost = $uri.Host
    $port = $uri.Port
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            throw "$DisplayName exited early with code $($Process.ExitCode)."
        }

        $client = $null
        try {
            $client = New-Object System.Net.Sockets.TcpClient
            $connect = $client.BeginConnect($targetHost, $port, $null, $null)
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

    try {
        Stop-Process -Id $Process.Id -Force -ErrorAction Stop
    }
    catch {
        Write-Warning "Failed to stop process $($Process.Id): $($_.Exception.Message)"
    }
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
    $OutputPath = Join-Path $workspaceRoot "_tmp\${projectName}_enterprise_multitenant_e2e_$timestamp.json"
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
$env:LicenseConfigs__ProjectSeed = "MUONROI_LICENSE_MODE_TEST_SEED_20260303"
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
    $username = "ent_$suffix"
    $password = "P@ssw0rd!123"
    $webSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession

    $registerPayload = @{
        userName = $username
        password = $password
        email = "$username@example.com"
        phoneNumber = "0123456789"
        name = "Enterprise"
        surname = "Toolkit"
        isActive = $true
        isTwoFactorEnabled = $false
        isUseThirdPartyLogin = $false
        externalLoginProvider = ""
        externalLoginToken = ""
    } | ConvertTo-Json -Depth 10

    $registerResponse = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/Auth/register" `
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
        -Uri "$hostPort/api/v1/Auth/login" `
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

    $authHeaders = @{ Authorization = "Bearer $accessToken" }
    $authTenantAHeaders = @{ Authorization = "Bearer $accessToken"; "x-tenant-id" = $TenantA }
    $authTenantBHeaders = @{ Authorization = "Bearer $accessToken"; "x-tenant-id" = $TenantB }

    $verifyResponse = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/Auth/verify-token" `
        -Method Get `
        -Headers $authHeaders `
        -WebSession $webSession `
        -UseBasicParsing

    if ($verifyResponse.StatusCode -ne 200) {
        throw "Verify-token failed with HTTP $($verifyResponse.StatusCode)."
    }

    $decisionTableA = @{
        Name = "DT $TenantA"
        Description = "Rule table for $TenantA"
        HitPolicy = "First"
        TenantId = $TenantA
        InputColumns = @(@{ Id = "in_total"; Name = "total"; Label = "Total"; DataType = "number"; IsRequired = $true })
        OutputColumns = @(@{ Id = "out_discount"; Name = "discount"; Label = "Discount"; DataType = "number"; IsRequired = $true })
        Rows = @(@{
                Id = "row1"
                Order = 1
                InputCells = @(@{ ColumnId = "in_total"; Expression = ">= 1000" })
                OutputCells = @(@{ ColumnId = "out_discount"; Expression = "0.1" })
                IsEnabled = $true
            })
    } | ConvertTo-Json -Depth 20

    $createA = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/decision-tables" `
        -Method Post `
        -Headers $authTenantAHeaders `
        -ContentType "application/json" `
        -Body $decisionTableA `
        -UseBasicParsing
    if ($createA.StatusCode -ne 201) {
        throw "Create decision table for $TenantA failed with HTTP $($createA.StatusCode)."
    }

    $decisionTableB = @{
        Name = "DT $TenantB"
        Description = "Rule table for $TenantB"
        HitPolicy = "First"
        TenantId = $TenantB
        InputColumns = @(@{ Id = "in_total"; Name = "total"; Label = "Total"; DataType = "number"; IsRequired = $true })
        OutputColumns = @(@{ Id = "out_discount"; Name = "discount"; Label = "Discount"; DataType = "number"; IsRequired = $true })
        Rows = @(@{
                Id = "row1"
                Order = 1
                InputCells = @(@{ ColumnId = "in_total"; Expression = ">= 500" })
                OutputCells = @(@{ ColumnId = "out_discount"; Expression = "0.05" })
                IsEnabled = $true
            })
    } | ConvertTo-Json -Depth 20

    $createB = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/decision-tables" `
        -Method Post `
        -Headers $authTenantBHeaders `
        -ContentType "application/json" `
        -Body $decisionTableB `
        -UseBasicParsing
    if ($createB.StatusCode -ne 201) {
        throw "Create decision table for $TenantB failed with HTTP $($createB.StatusCode)."
    }

    $listA = Invoke-WebRequest -Uri "$hostPort/api/v1/decision-tables?tenantId=$TenantA" -Headers $authTenantAHeaders -UseBasicParsing
    $listAJson = $listA.Content | ConvertFrom-Json
    $countA = [int](Find-PropertyValueRecursive -InputObject $listAJson -PropertyName "total")
    if ($countA -lt 1) {
        throw "Expected at least 1 decision table for $TenantA."
    }

    $listB = Invoke-WebRequest -Uri "$hostPort/api/v1/decision-tables?tenantId=$TenantB" -Headers $authTenantBHeaders -UseBasicParsing
    $listBJson = $listB.Content | ConvertFrom-Json
    $countB = [int](Find-PropertyValueRecursive -InputObject $listBJson -PropertyName "total")
    if ($countB -lt 1) {
        throw "Expected at least 1 decision table for $TenantB."
    }

    $nrulesDefinition = @{
        Name = "NRules Demo"
        Description = "NRules test definition"
        RuleExpression = "amount > 100"
        ActionExpression = "approved=true"
    } | ConvertTo-Json -Depth 10

    $nrulesPut = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/rule-engine/nrules/nr-1" `
        -Method Put `
        -Headers $authTenantAHeaders `
        -ContentType "application/json" `
        -Body $nrulesDefinition `
        -UseBasicParsing
    if ($nrulesPut.StatusCode -ne 200) {
        throw "NRules PUT failed with HTTP $($nrulesPut.StatusCode)."
    }

    $nrulesTestPayload = @{
        RuleId = "nr-1"
        Facts = @(@{ amount = 150; customer = "vip" })
    } | ConvertTo-Json -Depth 10

    $nrulesTest = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/rule-engine/test" `
        -Method Post `
        -Headers $authTenantAHeaders `
        -ContentType "application/json" `
        -Body $nrulesTestPayload `
        -UseBasicParsing
    if ($nrulesTest.StatusCode -ne 200) {
        throw "NRules test failed with HTTP $($nrulesTest.StatusCode)."
    }

    $cepDefinition = @{
        Name = "CEP Demo"
        WindowType = "Sliding"
        WindowSizeSeconds = 60
        TimeToLiveSeconds = 300
    } | ConvertTo-Json -Depth 10

    $cepPut = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/rule-engine/cep/cep-1" `
        -Method Put `
        -Headers $authTenantAHeaders `
        -ContentType "application/json" `
        -Body $cepDefinition `
        -UseBasicParsing
    if ($cepPut.StatusCode -ne 200) {
        throw "CEP PUT failed with HTTP $($cepPut.StatusCode)."
    }

    $nowUtc = [DateTime]::UtcNow
    $cepSimulatePayload = @{
        Events = @(
            @{ Key = "order"; TimestampUtc = $nowUtc.ToString("o"); Payload = @{ amount = 120 } },
            @{ Key = "order"; TimestampUtc = $nowUtc.AddSeconds(5).ToString("o"); Payload = @{ amount = 180 } }
        )
    } | ConvertTo-Json -Depth 10

    $cepSimulate = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/rule-engine/cep/cep-1/simulate" `
        -Method Post `
        -Headers $authTenantAHeaders `
        -ContentType "application/json" `
        -Body $cepSimulatePayload `
        -UseBasicParsing
    if ($cepSimulate.StatusCode -ne 200) {
        throw "CEP simulate failed with HTTP $($cepSimulate.StatusCode)."
    }

    $decisionEvalFirstPayload = @{
        hitPolicy = "First"
        input = @{
            total = 1500
            status = "PAID"
        }
        rows = @(
            @{
                id = "row1"
                conditions = @{
                    total = ">= 1000"
                    status = "== PAID"
                }
                outputs = @{
                    discount = 0.1
                    risk = "low"
                }
            },
            @{
                id = "row2"
                conditions = @{
                    total = ">= 1200"
                    status = "== PAID"
                }
                outputs = @{
                    discount = 0.2
                    risk = "medium"
                }
            }
        )
    } | ConvertTo-Json -Depth 20

    $decisionEvalFirst = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/decision-tables/evaluate" `
        -Method Post `
        -Headers $authTenantAHeaders `
        -ContentType "application/json" `
        -Body $decisionEvalFirstPayload `
        -UseBasicParsing
    if ($decisionEvalFirst.StatusCode -ne 200) {
        throw "Decision table evaluate (first-match) failed with HTTP $($decisionEvalFirst.StatusCode)."
    }

    $decisionEvalFirstJson = $decisionEvalFirst.Content | ConvertFrom-Json
    $firstMatchedCount = [int](Find-PropertyValueRecursive -InputObject $decisionEvalFirstJson -PropertyName "matchedCount")
    if ($firstMatchedCount -ne 1) {
        throw "Decision table first-match expected matchedCount=1, actual=$firstMatchedCount."
    }

    $decisionEvalAllPayload = @{
        hitPolicy = "All"
        input = @{
            total = 1500
            status = "PAID"
        }
        rows = @(
            @{
                id = "row1"
                conditions = @{
                    total = ">= 1000"
                    status = "== PAID"
                }
                outputs = @{
                    discount = 0.1
                    risk = "low"
                }
            },
            @{
                id = "row2"
                conditions = @{
                    total = ">= 1200"
                    status = "== PAID"
                }
                outputs = @{
                    discount = 0.2
                    risk = "medium"
                }
            }
        )
    } | ConvertTo-Json -Depth 20

    $decisionEvalAll = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/decision-tables/evaluate" `
        -Method Post `
        -Headers $authTenantAHeaders `
        -ContentType "application/json" `
        -Body $decisionEvalAllPayload `
        -UseBasicParsing
    if ($decisionEvalAll.StatusCode -ne 200) {
        throw "Decision table evaluate (all-match) failed with HTTP $($decisionEvalAll.StatusCode)."
    }

    $decisionEvalAllJson = $decisionEvalAll.Content | ConvertFrom-Json
    $allMatchedCount = [int](Find-PropertyValueRecursive -InputObject $decisionEvalAllJson -PropertyName "matchedCount")
    if ($allMatchedCount -lt 2) {
        throw "Decision table all-match expected matchedCount>=2, actual=$allMatchedCount."
    }

    $feelExpression = 'order.amount > 100 and order.status == "PAID"'
    $encodedFeelExpression = [System.Uri]::EscapeDataString($feelExpression)
    $feelEval = Invoke-WebRequest `
        -Uri "$hostPort/api/v1/rule-engine/feel-eval?expression=$encodedFeelExpression&orderAmount=150&orderStatus=PAID" `
        -Method Get `
        -Headers $authTenantAHeaders `
        -UseBasicParsing
    if ($feelEval.StatusCode -ne 200) {
        throw "Rule-engine feel-eval failed with HTTP $($feelEval.StatusCode)."
    }

    $feelEvalJson = $feelEval.Content | ConvertFrom-Json
    $feelResult = [bool](Find-PropertyValueRecursive -InputObject $feelEvalJson -PropertyName "result")
    if (-not $feelResult) {
        throw "Rule-engine feel-eval expected result=true but received false."
    }

    $uiContract = Invoke-WebRequest -Uri "$hostPort/api/v1/Auth/ui-engine/contract-info" -UseBasicParsing
    if ($uiContract.StatusCode -ne 200) {
        throw "UI Engine contract-info failed with HTTP $($uiContract.StatusCode)."
    }

    $uiSchema = Invoke-WebRequest -Uri "$hostPort/api/v1/Auth/ui-engine/schema-hash" -UseBasicParsing
    if ($uiSchema.StatusCode -ne 200) {
        throw "UI Engine schema-hash failed with HTTP $($uiSchema.StatusCode)."
    }

    $evidence = [ordered]@{
        ProjectPath = $projectPath
        BaseUrl = $hostPort
        ActivationProofPath = $activationProofPath
        RegisteredUser = $username
        VerifyStatusCode = $verifyResponse.StatusCode
        DecisionTableTenantA_Total = $countA
        DecisionTableTenantB_Total = $countB
        DecisionTableEvaluateFirstMatchedCount = $firstMatchedCount
        DecisionTableEvaluateAllMatchedCount = $allMatchedCount
        NRulesStatusCode = $nrulesTest.StatusCode
        CepStatusCode = $cepSimulate.StatusCode
        FeelEvalStatusCode = $feelEval.StatusCode
        FeelEvalResult = $feelResult
        UiEngineContractStatusCode = $uiContract.StatusCode
        UiEngineSchemaStatusCode = $uiSchema.StatusCode
        CompletedAtUtc = [DateTime]::UtcNow.ToString("o")
    }
    $evidence | ConvertTo-Json -Depth 20 | Set-Content -Path $outputPath -Encoding UTF8

    Write-Host "Enterprise multi-tenant toolkit<->rule-engine<->ui-engine E2E: PASS" -ForegroundColor Green
    Write-Host "Evidence: $outputPath"
}
finally {
    Stop-ProcessSafe -Process $proc

    foreach ($k in $prevEnv.Keys) {
        $value = $prevEnv[$k]
        [Environment]::SetEnvironmentVariable($k, $value, "Process")
    }
}
