# Agent Runbook: Generate Paid/Enterprise Keys Fast

This runbook is for agents verifying template-generated projects with paid/enterprise tier features.

## 1. Quick Start (one command)

From `D:\Personal\Project\MuonroiBuildingBlock`:

```powershell
.\scripts\flow-license-server.ps1 -Organization "Muonroi Local Verify" -NoRunServer
```

Run full mode matrix (`Free/Paid/Enterprise`) against 3 template projects:

```powershell
.\scripts\flow-license-modes.ps1 -Organization "Muonroi Local Verify"
```

`flow-license-modes.ps1` verifies per mode:
- app startup + swagger reachability
- `[License] Verified tier: ...` in runtime logs
- auth API flow: `register -> login -> GET /api/v1/Auth/verify-token` with bearer token header

Outputs:

- `tools\MockLicenseServer\generated-licenses\paid-license.key`
- `tools\MockLicenseServer\generated-licenses\enterprise-license.key`
- `tools\MockLicenseServer\generated-licenses\generated-license-keys.json`

Use key string from `enterprise-license.key` when testing enterprise-tier rule engine.

## 2. Start Mock License Server

```powershell
cd D:\Personal\Project\MuonroiBuildingBlock\tools\MockLicenseServer
dotnet run --project .\MockLicenseServer.csproj
```

Default base URL: `http://localhost:6000`

## 3. Generate key + activation proof (recommended flow)

1. Generate an enterprise key via API:

```powershell
$body = @{ tier = "enterprise"; organizationName = "Muonroi Verify"; validDays = 365 } | ConvertTo-Json
$generatedRaw = Invoke-WebRequest -Uri "http://localhost:6000/api/v1/keys/generate" -Method Post -ContentType "application/json" -Body $body
$generatedDoc = [System.Text.Json.JsonDocument]::Parse($generatedRaw.Content)
$enterpriseKey = $generatedDoc.RootElement.GetProperty("childKey").GetProperty("licenseKey").GetString()
```

2. Activate key and get signed proof:

```powershell
$activateBody = @{
  licenseKey = $enterpriseKey
  machineFingerprint = "local-dev"
  productVersion = "1.0.0"
  activationTime = (Get-Date).ToUniversalTime().ToString("o")
  environment = "Development"
} | ConvertTo-Json

$activationRaw = Invoke-WebRequest -Uri "http://localhost:6000/api/v1/activate" -Method Post -ContentType "application/json" -Body $activateBody
$activationDoc = [System.Text.Json.JsonDocument]::Parse($activationRaw.Content)
if (-not $activationDoc.RootElement.GetProperty("success").GetBoolean()) { throw "Activation failed" }
$proofRaw = $activationDoc.RootElement.GetProperty("proof").GetRawText()
```

Important:
- do not round-trip proof via `ConvertTo-Json` from a parsed object because timezone normalization can break signature validation.
- always persist proof using raw JSON text (`$proofRaw`) from server response.

## 4. Apply to generated project(s)

For each generated app:

- write `licenses\license.key` with enterprise key.
- write `licenses\activation_proof.json` with `activation.Proof`.
- copy `tools\MockLicenseServer\server_public_key.pem` to `licenses\public.pem`.
- ensure `LicenseConfigs.Mode = Offline` and paths point to those files.

## 5. Apply to 3 template outputs quickly

```powershell
$projects = @(
  "D:\Personal\Project\_tmp\verify-runs\rule-engine-verify-20260228-01\base\src\VerifyBase.API",
  "D:\Personal\Project\_tmp\verify-runs\rule-engine-verify-20260228-01\microservices\src\Services\VerifyMicro.Catalog",
  "D:\Personal\Project\_tmp\verify-runs\rule-engine-verify-20260228-01\modular\src\Host\VerifyModular.Host"
)

$publicPem = "D:\Personal\Project\MuonroiBuildingBlock\tools\MockLicenseServer\server_public_key.pem"

foreach ($p in $projects) {
  $licenses = Join-Path $p "licenses"
  New-Item -ItemType Directory -Path $licenses -Force | Out-Null
  Set-Content -Path (Join-Path $licenses "license.key") -Value $enterpriseKey -Encoding UTF8
  Set-Content -Path (Join-Path $licenses "activation_proof.json") -Value $proofRaw -Encoding UTF8
  Copy-Item $publicPem (Join-Path $licenses "public.pem") -Force
}
```

## 6. Verification checklist

- App log contains: `[License] Verified tier: Enterprise`
- Rule engine protected endpoints return success under enterprise key.
- `activation_proof.json` signature validates with `public.pem`.
- No fallback/workaround flags required for normal runtime path.

## 7. Required CI secrets (names only)

Set these in GitHub repository settings (Actions secrets and variables):

- `NUGET_ORG_API_KEY`
  - Used by `.github/workflows/publish-oss.yml`.
- `VSCE_PAT`
  - Used by `.github/workflows/publish-vsix.yml`.

Do not commit secret values to source control. Only store secret names and setup steps in docs.
