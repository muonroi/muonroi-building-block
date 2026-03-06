param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,
    [string]$ActivationProofPath,
    [string]$PublicKeyPath,
    [string]$LicenseMode = "Enterprise",
    [string]$BaseUrl = "http://127.0.0.1:7310",
    [string]$TenantId = "tenant-a",
    [string]$AdminUsername = "admin",
    [string]$AdminPassword = "sysadmin",
    [switch]$AssignAdminRoleToNewUser,
    [string]$OutputPath,
    [int]$StartupTimeoutSeconds = 120,
    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param([string]$PathValue)
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return $null }
    return (Resolve-Path $PathValue).Path
}

function Stop-ProcessSafe {
    param([System.Diagnostics.Process]$Process)
    if ($null -eq $Process) { return }
    if ($Process.HasExited) { return }
    try { Stop-Process -Id $Process.Id -Force -ErrorAction Stop } catch {}
}

function Wait-ForTcpReady {
    param(
        [string]$Url,
        [int]$TimeoutSeconds,
        [System.Diagnostics.Process]$Process
    )
    $uri = [Uri]$Url
    $targetHost = $uri.Host
    $targetPort = $uri.Port
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            throw "API exited early with code $($Process.ExitCode)."
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

    throw "Timed out waiting for API at $Url."
}

function Get-StatusCodeSafe {
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
                $response = Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers -WebSession $WebSession -UseBasicParsing -TimeoutSec 15
            }
            else {
                $response = Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers -UseBasicParsing -TimeoutSec 15
            }
        }
        else {
            if ($null -ne $WebSession) {
                $response = Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers -ContentType "application/json" -Body $Body -WebSession $WebSession -UseBasicParsing -TimeoutSec 15
            }
            else {
                $response = Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers -ContentType "application/json" -Body $Body -UseBasicParsing -TimeoutSec 15
            }
        }

        return [ordered]@{
            StatusCode = [int]$response.StatusCode
            Body = $response.Content
            Error = $null
        }
    }
    catch {
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode.value__
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $content = $reader.ReadToEnd()
            return [ordered]@{
                StatusCode = $status
                Body = $content
                Error = $null
            }
        }

        return [ordered]@{
            StatusCode = -1
            Body = ""
            Error = $_.Exception.Message
        }
    }
}

function Decode-JwtPayload {
    param([string]$Jwt)
    if ([string]::IsNullOrWhiteSpace($Jwt)) { return $null }
    $parts = $Jwt.Split(".")
    if ($parts.Length -lt 2) { return $null }

    $payload = $parts[1].Replace('-', '+').Replace('_', '/')
    switch ($payload.Length % 4) {
        2 { $payload += "==" }
        3 { $payload += "=" }
        0 { }
        default { return $null }
    }

    try {
        $bytes = [Convert]::FromBase64String($payload)
        $json = [System.Text.Encoding]::UTF8.GetString($bytes)
        return $json | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Get-SqliteDbPath {
    param([string]$ProjectFilePath)

    $projectDir = Split-Path -Parent $ProjectFilePath
    $candidates = @(
        (Join-Path $projectDir "app_dev.db"),
        (Join-Path $projectDir "app.db")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $candidates[0]
}

function Invoke-SqliteScalar {
    param(
        [string]$DbPath,
        [string]$Sql
    )

    if (-not (Test-Path $DbPath)) {
        return ""
    }

    $value = sqlite3 $DbPath $Sql
    if ($null -eq $value) { return "" }
    return [string]$value
}

function Assign-AdminRoleForUser {
    param(
        [string]$DbPath,
        [string]$Username
    )

    $safeUserName = $Username.Replace("'", "''")
    $userId = (Invoke-SqliteScalar -DbPath $DbPath -Sql "select EntityId from MUsers where UserName='$safeUserName' order by CreationTime desc limit 1;").Trim()
    $adminRoleId = (Invoke-SqliteScalar -DbPath $DbPath -Sql "select EntityId from MRoles where Name='Admin' and IsDeleted=0 limit 1;").Trim()

    if ([string]::IsNullOrWhiteSpace($userId) -or [string]::IsNullOrWhiteSpace($adminRoleId)) {
        return [ordered]@{
            Assigned = $false
            Reason = "Cannot resolve user or admin role in DB."
            UserId = $userId
            AdminRoleId = $adminRoleId
        }
    }

    $exists = (Invoke-SqliteScalar -DbPath $DbPath -Sql "select count(*) from MUserRoles where UserId='$userId' and RoleId='$adminRoleId' and IsDeleted=0;").Trim()
    if ($exists -ne "0") {
        return [ordered]@{
            Assigned = $true
            Reason = "Already assigned."
            UserId = $userId
            AdminRoleId = $adminRoleId
        }
    }

    $entityId = [Guid]::NewGuid().ToString()
    $now = [DateTime]::UtcNow.ToString("o")
    $ts = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $sql = "insert into MUserRoles (EntityId,UserId,RoleId,CreatedDateTs,IsDeleted,CreationTime,CreatorUserId) values ('$entityId','$userId','$adminRoleId',$ts,0,'$now','$userId');"
    $null = sqlite3 $DbPath $sql

    return [ordered]@{
        Assigned = $true
        Reason = "Assigned admin role."
        UserId = $userId
        AdminRoleId = $adminRoleId
    }
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Resolve-AbsolutePath -PathValue $ProjectPath
if (-not $projectPath.EndsWith(".csproj", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "ProjectPath must be a .csproj file."
}

$modeValue = $LicenseMode.Trim()
if ([string]::IsNullOrWhiteSpace($modeValue)) { $modeValue = "Enterprise" }

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
    $OutputPath = Join-Path $workspaceRoot "_tmp\${projectName}_ui_engine_current_matrix_$timestamp.json"
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
$dbPath = Get-SqliteDbPath -ProjectFilePath $projectPath

$activationProofPath = Resolve-AbsolutePath -PathValue $ActivationProofPath
if ($modeValue -in @("Enterprise", "Paid")) {
    if ([string]::IsNullOrWhiteSpace($activationProofPath) -or -not (Test-Path $activationProofPath)) {
        throw "ActivationProofPath is required for mode $modeValue."
    }

    if ([string]::IsNullOrWhiteSpace($PublicKeyPath)) {
        $PublicKeyPath = Join-Path $workspaceRoot "tools\MockLicenseServer\server_public_key.pem"
    }
}

$publicKeyPath = Resolve-AbsolutePath -PathValue $PublicKeyPath
if ($modeValue -in @("Enterprise", "Paid") -and -not (Test-Path $publicKeyPath)) {
    throw "PublicKeyPath not found: $publicKeyPath"
}

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
$env:LicenseConfigs__Mode = if ($modeValue -eq "Free") { "Free" } else { "Offline" }
$env:LicenseConfigs__ActivationProofPath = if ($modeValue -eq "Free") { "" } else { $activationProofPath }
$env:LicenseConfigs__PublicKeyPath = if ($modeValue -eq "Free") { "" } else { $publicKeyPath }
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

    Wait-ForTcpReady -Url $hostPort -TimeoutSeconds $StartupTimeoutSeconds -Process $proc

    $suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
    $username = "ui_$suffix"
    $password = "P@ssw0rd!123"
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

    $registerPayload = @{
        userName = $username
        password = $password
        email = "$username@example.com"
        phoneNumber = "0123456789"
        name = "Ui"
        surname = "Matrix"
        isActive = $true
        isTwoFactorEnabled = $false
        isUseThirdPartyLogin = $false
        externalLoginProvider = ""
        externalLoginToken = ""
    } | ConvertTo-Json -Depth 10

    $register = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/register" -Method "Post" -Body $registerPayload -WebSession $session
    if ($register.StatusCode -lt 200 -or $register.StatusCode -ge 300) {
        throw "Register failed with HTTP $($register.StatusCode)."
    }

    $loginPayload = @{ username = $username; password = $password } | ConvertTo-Json -Depth 5
    $login = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/login" -Method "Post" -Body $loginPayload -WebSession $session
    if ($login.StatusCode -lt 200 -or $login.StatusCode -ge 300) {
        throw "Login failed with HTTP $($login.StatusCode)."
    }

    $loginJson = $login.Body | ConvertFrom-Json
    $newUserToken = [string]$loginJson.result.accessToken
    if ([string]::IsNullOrWhiteSpace($newUserToken)) {
        throw "Login succeeded but accessToken missing."
    }

    $adminLoginPayload = @{ username = $AdminUsername; password = $AdminPassword } | ConvertTo-Json -Depth 5
    $adminSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $adminLogin = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/login" -Method "Post" -Body $adminLoginPayload -WebSession $adminSession
    $adminToken = ""
    if ($adminLogin.StatusCode -ge 200 -and $adminLogin.StatusCode -lt 300) {
        try {
            $adminLoginJson = $adminLogin.Body | ConvertFrom-Json
            $adminToken = [string]$adminLoginJson.result.accessToken
        }
        catch {
            $adminToken = ""
        }
    }

    $newBearer = @{ Authorization = "Bearer $newUserToken" }
    $newBearerTenant = @{ Authorization = "Bearer $newUserToken"; "x-tenant-id" = $TenantId }
    $adminBearer = if ([string]::IsNullOrWhiteSpace($adminToken)) { $null } else { @{ Authorization = "Bearer $adminToken" } }
    $adminBearerTenant = if ([string]::IsNullOrWhiteSpace($adminToken)) { $null } else { @{ Authorization = "Bearer $adminToken"; "x-tenant-id" = $TenantId } }

    $matrix = [ordered]@{
        UiCurrent_NoToken = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/ui-engine/current"
        UiCurrent_NewUser_Bearer = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/ui-engine/current" -Headers $newBearer
        UiCurrent_NewUser_BearerTenant = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/ui-engine/current" -Headers $newBearerTenant
        UiCurrent_NewUser_SessionOnly = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/ui-engine/current" -WebSession $session
        AuthUsers_NewUser_Bearer = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/users" -Headers $newBearer
        AuthUsers_NewUser_BearerTenant = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/users" -Headers $newBearerTenant
        ProfileCurrent_NewUser_Bearer = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/profile/current" -Headers $newBearer
        ProfileCurrent_NewUser_BearerTenant = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/profile/current" -Headers $newBearerTenant
        VerifyToken_NewUser_Bearer = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/verify-token" -Headers $newBearer
        UiContractInfo_Anonymous = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/ui-engine/contract-info"
        UiSchemaHash_Anonymous = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/ui-engine/schema-hash"
    }

    $roleAssignment = $null
    if ($AssignAdminRoleToNewUser) {
        $roleAssignment = Assign-AdminRoleForUser -DbPath $dbPath -Username $username

        $relogin = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/login" -Method "Post" -Body $loginPayload -WebSession $session
        if ($relogin.StatusCode -ge 200 -and $relogin.StatusCode -lt 300) {
            $reloginJson = $relogin.Body | ConvertFrom-Json
            $reloginToken = [string]$reloginJson.result.accessToken

            if (-not [string]::IsNullOrWhiteSpace($reloginToken)) {
                $newBearerAfterRole = @{ Authorization = "Bearer $reloginToken" }
                $newBearerTenantAfterRole = @{ Authorization = "Bearer $reloginToken"; "x-tenant-id" = $TenantId }

                $matrix["UiCurrent_NewUser_AfterAdminRole_Bearer"] = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/ui-engine/current" -Headers $newBearerAfterRole
                $matrix["UiCurrent_NewUser_AfterAdminRole_BearerTenant"] = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/ui-engine/current" -Headers $newBearerTenantAfterRole
                $matrix["AuthUsers_NewUser_AfterAdminRole_Bearer"] = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/users" -Headers $newBearerAfterRole
                $matrix["AuthUsers_NewUser_AfterAdminRole_BearerTenant"] = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/users" -Headers $newBearerTenantAfterRole
                $matrix["ProfileCurrent_NewUser_AfterAdminRole_Bearer"] = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/profile/current" -Headers $newBearerAfterRole
                $matrix["ProfileCurrent_NewUser_AfterAdminRole_BearerTenant"] = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/profile/current" -Headers $newBearerTenantAfterRole

                $decodedRelogin = Decode-JwtPayload -Jwt $reloginToken
                $roleAssignment["PermissionClaimAfterRole"] = if ($null -ne $decodedRelogin) { [string]$decodedRelogin.permission } else { "" }
            }
        }
    }

    if ($null -ne $adminBearer) {
        $matrix["UiCurrent_Admin_Bearer"] = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/ui-engine/current" -Headers $adminBearer
        $matrix["UiCurrent_Admin_BearerTenant"] = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/ui-engine/current" -Headers $adminBearerTenant
        $matrix["AuthUsers_Admin_Bearer"] = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/users" -Headers $adminBearer
        $matrix["AuthUsers_Admin_BearerTenant"] = Get-StatusCodeSafe -Url "$hostPort/api/v1/Auth/users" -Headers $adminBearerTenant
    }

    $decodedNewUser = Decode-JwtPayload -Jwt $newUserToken
    $decodedAdmin = if ([string]::IsNullOrWhiteSpace($adminToken)) { $null } else { Decode-JwtPayload -Jwt $adminToken }

    $authUsersNewStatus = [int]$matrix.AuthUsers_NewUser_Bearer.StatusCode
    $uiCurrentNewStatus = [int]$matrix.UiCurrent_NewUser_Bearer.StatusCode
    $authUsersNewTenantStatus = [int]$matrix.AuthUsers_NewUser_BearerTenant.StatusCode
    $uiCurrentNewTenantStatus = [int]$matrix.UiCurrent_NewUser_BearerTenant.StatusCode

    $analysis = [ordered]@{
        AuthPipelineForNewUser = if ($authUsersNewStatus -eq 200) { "OK" } else { "FAILED" }
        UiCurrentForNewUser = if ($uiCurrentNewStatus -eq 200) { "OK" } else { "FAILED" }
        AuthPipelineForNewUserWithTenantHeader = if ($authUsersNewTenantStatus -eq 200) { "OK" } else { "FAILED" }
        UiCurrentForNewUserWithTenantHeader = if ($uiCurrentNewTenantStatus -eq 200) { "OK" } else { "FAILED" }
        SuspectedIssue = if ($authUsersNewStatus -eq 401 -and $uiCurrentNewStatus -eq 401 -and $authUsersNewTenantStatus -eq 200 -and $uiCurrentNewTenantStatus -eq 200) {
            "Tenant header is required in multi-tenant mode; no-tenant requests are rejected by design."
        }
        elseif ($authUsersNewStatus -ne 200 -and $uiCurrentNewStatus -eq 401 -and $authUsersNewTenantStatus -ne 200) {
            "Global auth session/token validation issue before ui-engine/current."
        }
        elseif ($authUsersNewStatus -eq 200 -and $uiCurrentNewStatus -eq 401) {
            "ui-engine/current specific auth/policy/claim mapping issue."
        }
        else {
            ""
        }
    }

    $evidence = [ordered]@{
        ProjectPath = $projectPath
        Mode = $modeValue
        BaseUrl = $hostPort
        TenantId = $TenantId
        RegisteredUser = $username
        AdminLoginStatus = $adminLogin.StatusCode
        NewUserJwtClaims = $decodedNewUser
        AdminJwtClaims = $decodedAdmin
        Matrix = $matrix
        RoleAssignment = $roleAssignment
        Analysis = $analysis
        OutputLogs = [ordered]@{
            Stdout = $stdoutLog
            Stderr = $stderrLog
        }
        CompletedAtUtc = [DateTime]::UtcNow.ToString("o")
    }
    $evidence | ConvertTo-Json -Depth 20 | Set-Content -Path $outputPath -Encoding UTF8

    if ($Strict) {
        $hasUiCurrentSuccess = @($matrix.GetEnumerator() | Where-Object { $_.Key -like "UiCurrent_*" -and $_.Value.StatusCode -eq 200 }).Count -gt 0
        if (-not $hasUiCurrentSuccess) {
            throw "Strict mode failed: no UiCurrent_* case returned HTTP 200."
        }
    }

    Write-Host "UI Engine current matrix test completed." -ForegroundColor Green
    Write-Host "Evidence: $outputPath"
}
finally {
    Stop-ProcessSafe -Process $proc
    foreach ($k in $prevEnv.Keys) {
        [Environment]::SetEnvironmentVariable($k, $prevEnv[$k], "Process")
    }
}
