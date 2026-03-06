# Implementation Plan: 3-Tier Security Enhancement for License System

## Overview
Implement comprehensive security enhancements based on expert analysis (Defensive + Offensive perspectives) with 3 progressive tiers.

## Expert Recommendations Summary

### Defensive Expert (Phòng thủ)
1. **License Tier as Source of Truth** - Don't trust `ASPNETCORE_ENVIRONMENT`
2. **Signed Policy Files** - RSA-signed JSON policies that can't be downgraded
3. **Server Nonce + TPM/DPAPI** - Prevent chain replay attacks
4. **TagWith() + AsyncLocal** - Prevent EF interceptor loops

### Offensive Expert (Tấn công)
1. **Acknowledgment**: Software protection has limits against admin access
2. **Hardware Breakpoints**: Detection requires GetThreadContext checks
3. **Server-Side Execution**: Ultimate protection for critical business logic
4. **Key Management**: Encrypted state can be compromised via hook attacks

## Target Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ TIER 1: Enhanced Client Protection (95% honest users)      │
│ - Fix EF loop risk with AsyncLocal bypass                  │
│ - Add SavingChangesAsync interceptor override              │
│ - Improve anti-tampering with GetThreadContext checks      │
│ - License Tier drives enforcement (not env var)            │
├─────────────────────────────────────────────────────────────┤
│ TIER 2: Signed Policy System (Enterprise compliance)       │
│ - RSA-signed policy files (can't be modified)              │
│ - Policy contains enforcement rules, quotas, features      │
│ - Server issues policies, client verifies signature        │
│ - Policy tier overrides config-based settings              │
├─────────────────────────────────────────────────────────────┤
│ TIER 3: Server-Side Validation (Maximum security)          │
│ - Chain submission to server for audit                     │
│ - Server-side nonce rotation (anti-replay)                 │
│ - TPM/DPAPI anchor for chain integrity                     │
│ - Remote policy revocation                                 │
│ - Critical business logic runs server-side                 │
└─────────────────────────────────────────────────────────────┘
```

## Implementation Strategy

### Phased Rollout (3 Tiers, 7 weeks total)

**TIER 1: Enhanced Client Protection** (Weeks 1-2)
- Fix EF interceptor loop with AsyncLocal bypass context
- Add missing SavingChangesAsync override
- License Tier drives enforcement (not env var)
- Hardware breakpoint detection (optional)

**TIER 2: Signed Policy System** (Weeks 3-4)
- RSA-signed policy files that override config
- Server issues policies, client verifies signature
- Quotas and rate limiting per feature
- PolicySigner CLI tool for generating policies

**TIER 3: Server-Side Validation** (Weeks 5-6)
- Chain submission to server for audit
- Server nonce rotation (anti-replay)
- TPM/DPAPI anchoring (Windows only)
- Background service for periodic submission

**Integration & Testing** (Week 7)
- Cross-tier testing
- Performance benchmarks
- Security audit
- Documentation

### Key Design Decisions

1. **Backward Compatible**: Free mode continues working without changes
2. **Opt-In**: Each tier is optional, users adopt incrementally
3. **DX First**: Development mode bypasses aggressive checks
4. **Production Hardened**: Full enforcement only in Production + Paid license
5. **No Breaking Changes**: Existing LicenseConfigs remain valid

## TIER 1: Enhanced Client Protection (Weeks 1-2)

### Objectives
- **Fix**: EF interceptor can cause infinite loop if RecordAction triggers SaveChanges
- **Fix**: Missing SavingChangesAsync override in interceptor
- **Improve**: Make License Tier the source of truth (not ASPNETCORE_ENVIRONMENT)
- **Enhance**: Add hardware breakpoint detection (optional, Windows-only)

### New Files to Create

#### 1. `LicenseExecutionContext.cs`
**Path**: `src/Muonroi.BuildingBlock/Shared/License/`
**Purpose**: AsyncLocal-based flag to prevent re-entry in interceptor
```csharp
public sealed class LicenseExecutionContext
{
    private static readonly AsyncLocal<bool> _isInLicenseCheck = new();

    public static bool IsInLicenseCheck { get; set; }

    public static IDisposable BeginScope() => new Scope();

    private sealed class Scope : IDisposable { ... }
}
```

#### 2. `AntiTamperDetector.cs`
**Path**: `src/Muonroi.BuildingBlock/Shared/License/`
**Purpose**: Centralized detection for debuggers, hardware breakpoints, method hooks
- Uses P/Invoke `GetThreadContext()` to check DR0-DR7 registers
- Detects JMP (0xE9) and INT3 (0xCC) at method entry points
- Only runs when `EnableAntiTampering=true` and not Free tier

#### 3. `LicenseEnforcementMode.cs`
**Path**: `src/Muonroi.BuildingBlock/Shared/License/`
**Purpose**: Enum for enforcement behavior (FreeTier, Development, Production)

### Files to Modify

#### 1. `LicenseSaveChangesInterceptor.cs`
**Changes**:
- Add `SavingChangesAsync` override (currently missing!)
- Wrap `guard.EnsureValid()` in `LicenseExecutionContext.BeginScope()`
- Check `IsInLicenseCheck` flag before executing

#### 2. `LicenseGuard.cs`
**Changes**:
- Constructor: Initialize `AntiTamperDetector`
- `RecordAction()`: Wrap in `LicenseExecutionContext` to prevent recursion
- Remove env var checks, use tier-based logic

#### 3. `LicenseConfigs.cs`
**Changes**:
- Add `EnforcementMode?` property (optional override)
- Add `GetEnforcementMode(LicenseTier)` method for auto-detection

#### 4. `LicenseVerifier.cs`
**Changes**:
- Log enforcement mode based on tier (not environment)
- Make tier the source of truth

### Configuration Example

**`appsettings.Development.json`**:
```json
{
  "LicenseConfigs": {
    "EnforcementMode": "Development",
    "SkipSignatureVerification": true,
    "EnableAntiTampering": false,
    "FailMode": "Soft"
  }
}
```

**`appsettings.Production.json`**:
```json
{
  "LicenseConfigs": {
    "EnforcementMode": "Production",
    "SkipSignatureVerification": false,
    "EnableAntiTampering": true,
    "FailMode": "Hard"
  }
}
```

### Testing Strategy
- **Unit**: Test `LicenseExecutionContext.BeginScope()` prevents recursion
- **Integration**: Trigger nested SaveChanges, verify no loop
- **Manual**: Attach debugger, verify detection works

---

## TIER 2: Signed Policy System (Weeks 3-4)

### Objectives
- Prevent config downgrade attacks (attacker can modify appsettings.json)
- Separate policy from license (policy can be updated without new license)
- Enable enterprise compliance (quotas, rate limits, feature gates)
- Server issues policies, client verifies RSA signature

### New Files to Create

#### 1. `Policy/LicensePolicy.cs`
**Purpose**: Policy data structure
```csharp
public sealed class LicensePolicy
{
    public string PolicyId { get; set; }
    public string Version { get; set; }
    public string LicenseId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public PolicyEnforcementRules Enforcement { get; set; }
    public Dictionary<string, FeatureQuota> FeatureQuotas { get; set; }
    public string Signature { get; set; }  // RSA-SHA256
}

public sealed class PolicyEnforcementRules
{
    public bool EnforceOnDatabase { get; set; }
    public bool EnableAntiTampering { get; set; }
    public LicenseFailMode FailMode { get; set; }
    public int MaxApiRequestsPerMinute { get; set; }
    public int MaxDbOperationsPerMinute { get; set; }
}
```

#### 2. `Policy/PolicyVerifier.cs`
**Purpose**: Verify RSA signature on policy files
- Uses same public key as license verification
- Signs all policy fields except Signature itself
- Returns `PolicyVerificationResult` with validation status

#### 3. `Policy/IPolicyStore.cs` + `FilePolicyStore.cs`
**Purpose**: Load/save/cache policy files
- Similar to `ILicenseStore` pattern
- Loads from `PolicyFilePath` config

#### 4. `Policy/PolicyEnforcer.cs`
**Purpose**: Apply policy rules at runtime
- **Rate limiting**: In-memory tracking (reset on restart)
- **Feature quotas**: Check usage against limits
- **Override config**: Policy takes precedence over LicenseConfigs

### Files to Modify

#### 1. `LicenseConfigs.cs`
**Changes**:
- Add `string? PolicyFilePath`
- Add `bool RequireSignedPolicy` (default: false)

#### 2. `LicenseServiceCollectionExtensions.cs`
**Changes**:
- Register `IPolicyStore`, `PolicyVerifier`, `PolicyEnforcer`
- Load and verify policy during startup
- Throw `SecurityException` if `RequireSignedPolicy=true` and signature invalid

#### 3. `LicenseGuard.cs`
**Changes**:
- Inject `PolicyEnforcer?` (optional)
- Use `_policyEnforcer.CheckOperation()` for rate limits
- Use policy settings over config when available

#### 4. `LicenseSaveChangesInterceptor.cs`
**Changes**:
- Inject `PolicyEnforcer?`
- Use `_policyEnforcer.ShouldEnforceOnDatabase()` instead of config

### Policy File Example

**`policy.json` (server-generated)**:
```json
{
  "PolicyId": "pol_abc123",
  "Version": "1.0.0",
  "LicenseId": "lic_enterprise_001",
  "IssuedAt": "2026-02-07T00:00:00Z",
  "ExpiresAt": "2027-02-07T00:00:00Z",
  "Enforcement": {
    "EnforceOnDatabase": true,
    "EnableAntiTampering": true,
    "FailMode": "Hard",
    "MaxApiRequestsPerMinute": 1000,
    "MaxDbOperationsPerMinute": 500
  },
  "FeatureQuotas": {
    "rule-engine": {
      "MaxUsagePerDay": 10000,
      "MaxConcurrentUsage": 50
    }
  },
  "Signature": "BASE64_RSA_SIGNATURE"
}
```

### PolicySigner CLI Tool

Create `tools/PolicySigner/Program.cs` to generate signed policies:
```csharp
// Read private key
var rsa = RSA.Create();
rsa.ImportFromPem(File.ReadAllText("private.pem").ToCharArray());

// Create policy
var policy = new LicensePolicy { ... };

// Sign
var signingData = new { policy.PolicyId, ... };
var json = JsonSerializer.Serialize(signingData);
var signature = rsa.SignData(Encoding.UTF8.GetBytes(json), ...);
policy.Signature = Convert.ToBase64String(signature);

// Save
File.WriteAllText("policy.json", JsonSerializer.Serialize(policy));
```

### Testing Strategy
- **Unit**: Test `PolicyVerifier` with valid/invalid signatures
- **Integration**: Load policy, verify rate limits enforced
- **Security**: Attempt to modify policy signature, verify rejection

---

## TIER 3: Server-Side Validation (Weeks 5-6)

### Objectives
- Submit action chains to server for remote audit
- Rotate server nonce to prevent replay attacks
- Optional TPM/DPAPI anchoring (Windows-only, machine-bound license)
- Guidance for critical business logic on server-side

### New Files to Create

#### 1. `ServerValidation/ChainSubmitter.cs`
**Purpose**: HTTP client to submit chains to license server
- Posts `ChainSubmissionRequest` to server endpoint
- Receives `ChainSubmissionResponse` with new nonce
- Handles timeouts and failures gracefully

#### 2. `ServerValidation/NonceRotator.cs`
**Purpose**: Rotate server nonce after each submission
- Calls `ChainSubmitter` with recent chain entries
- Updates license file with new nonce from server
- Prevents replay attacks (old chains can't be reused)

#### 3. `ServerValidation/TpmAnchor.cs`
**Purpose**: Windows DPAPI/TPM anchoring (optional)
- `Protect()`: Encrypt data with machine-level DPAPI
- `Unprotect()`: Decrypt data (only works on same machine)
- Makes license file non-transferable between machines

#### 4. `ServerValidation/ChainSubmissionHostedService.cs`
**Purpose**: Background service for periodic submission
- Runs every N minutes (configured via `ChainSubmissionIntervalMinutes`)
- Submits recent chain entries in batches
- Rotates nonce after successful submission

### Files to Modify

#### 1. `LicenseConfigs.cs`
**Changes**:
- Add `bool EnableServerValidation` (default: false)
- Add `int ChainSubmissionIntervalMinutes` (default: 60)
- Add `int ChainSubmissionBatchSize` (default: 100)
- Add `bool EnableTpmAnchoring` (default: false)
- Add `OnlineLicenseConfigs.ChainSubmissionEndpoint`

#### 2. `LicenseServiceCollectionExtensions.cs`
**Changes**:
- Register `ChainSubmitter`, `NonceRotator` if enabled
- Add `ChainSubmissionHostedService` as hosted service
- Configure `HttpClient` for license server

#### 3. `LicenseStore.cs`
**Changes**:
- `Load()`: Call `TpmAnchor.Unprotect()` if enabled
- `Save()`: Call `TpmAnchor.Protect()` if enabled

#### 4. `IFingerprintChainStore.cs` + `FileFingerprintChainStore.cs`
**Changes**:
- Add `GetRecentEntries(int count, long? afterSequence)` method
- Implementation reads chain log file, returns recent entries

### Server-Side API Requirements

The license server must implement:

**POST `/api/v1/chain/submit`**

Request:
```json
{
  "LicenseId": "lic_enterprise_001",
  "Entries": [ ... chain entries ... ],
  "SubmittedAt": "2026-02-07T11:00:00Z"
}
```

Response (success):
```json
{
  "Accepted": true,
  "NewNonce": "nonce_rotated_456",
  "Error": null
}
```

Server logic:
1. Verify chain signatures using stored license secret
2. Check for sequence gaps or duplicates (detect replay)
3. Generate new nonce (random GUID or timestamp-based)
4. Return nonce to client
5. Store audit trail in database

### Configuration Example

**`appsettings.Production.json` (TIER 3)**:
```json
{
  "LicenseConfigs": {
    "Mode": "Online",
    "EnableServerValidation": true,
    "ChainSubmissionIntervalMinutes": 60,
    "ChainSubmissionBatchSize": 100,
    "EnableTpmAnchoring": true,

    "Online": {
      "Endpoint": "https://license.muonroi.com/api/v1",
      "ChainSubmissionEndpoint": "/chain/submit",
      "TimeoutSeconds": 30
    }
  }
}
```

### Testing Strategy
- **Unit**: Test `ChainSubmitter` with mocked HttpClient
- **Integration**: Mock license server, verify submission flow
- **E2E**: Deploy test server, run client, verify chains appear in audit log
- **Security**: Test replay attack (submit same chain twice), verify detection

---

## Critical Files

### Tier 1 (Client Protection)
- `src/Muonroi.BuildingBlock/Shared/License/LicenseBypassContext.cs` (NEW)
- `src/Muonroi.BuildingBlock/External/Entity/LicenseSaveChangesInterceptor.cs` (MODIFY)
- `src/Muonroi.BuildingBlock/External/Entity/DataSample/MigrationManager.cs` (MODIFY)
- `src/Muonroi.BuildingBlock/Shared/License/LicenseGuard.cs` (MODIFY - add hardware breakpoint detection)
- `src/Muonroi.BuildingBlock/External/InfrastructureExtensions.cs` (MODIFY - license tier enforcement)

### Tier 2 (Signed Policies)
- `src/Muonroi.BuildingBlock/Shared/Policy/PolicyPayload.cs` (NEW)
- `src/Muonroi.BuildingBlock/Shared/Policy/PolicyVerifier.cs` (NEW)
- `src/Muonroi.BuildingBlock/Shared/Policy/PolicyConfigs.cs` (NEW)
- `src/Muonroi.BuildingBlock/Shared/Policy/RsaPolicySigner.cs` (NEW)
- `src/Muonroi.BuildingBlock/Shared/License/LicenseConfigs.cs` (MODIFY)
- `src/Muonroi.BuildingBlock/Shared/License/LicenseGuard.cs` (MODIFY - check policies)

### Tier 3 (Server Validation)
- `src/Muonroi.BuildingBlock/Shared/License/LicenseActivationService.cs` (MODIFY - add endpoints)
- `src/Muonroi.BuildingBlock/Shared/License/ChainSubmissionService.cs` (NEW)
- `src/Muonroi.BuildingBlock/Shared/License/TpmAnchorProvider.cs` (NEW - Windows TPM/DPAPI)
- `src/Muonroi.BuildingBlock/Shared/Policy/PolicyRevocationService.cs` (NEW)

## Verification Strategy

### TIER 1 Verification

**Unit Tests**:
- `LicenseExecutionContext_BeginScope_PreventsRecursion()`
- `AntiTamperDetector_CheckHardwareBreakpoints_Windows()`
- `LicenseGuard_UsesT ierNotEnvironment()`

**Integration Tests**:
- Trigger SaveChanges → guard.RecordAction → (internal SaveChanges) → verify no loop
- Test Free/Development/Production enforcement modes
- Attach debugger, verify detection (if enabled)

**Manual Testing**:
```bash
# Test with Free tier
dotnet run --project Muonroi.BaseTemplate.API
# Should work without restrictions

# Test with Licensed tier + Development mode
# Modify license.json to Licensed tier
dotnet run --project Muonroi.BaseTemplate.API
# Should have relaxed checks

# Test with Enterprise tier + Production mode
export ASPNETCORE_ENVIRONMENT=Production
dotnet run --no-launch-profile --project Muonroi.BaseTemplate.API
# Should enforce all security checks
```

### TIER 2 Verification

**Unit Tests**:
- `PolicyVerifier_ValidSignature_ReturnsSuccess()`
- `PolicyVerifier_InvalidSignature_ReturnsFailure()`
- `PolicyEnforcer_CheckRateLimit_BlocksExcess()`

**Integration Tests**:
- Load policy.json, verify signature validation
- Make 1001 API requests when MaxApiRequestsPerMinute=1000
- Verify 1001st request is denied

**Policy Generation Test**:
```bash
# Build PolicySigner tool
cd tools/PolicySigner
dotnet build

# Generate policy
dotnet run -- lic_enterprise_001 > policy.json

# Verify policy loads in app
cp policy.json ../../Muonroi.BaseTemplate.API/
dotnet run --project ../../Muonroi.BaseTemplate.API
# Check logs for "[Policy] Loaded policy pol_xxx vX.X.X"
```

### TIER 3 Verification

**Mock Server Setup**:
Create a simple mock license server for testing:
```csharp
// tests/MockLicenseServer/Program.cs
app.MapPost("/api/v1/chain/submit", async (ChainSubmissionRequest req) =>
{
    // Verify chain signatures
    // Check for duplicates
    // Return new nonce
    return new ChainSubmissionResponse
    {
        Accepted = true,
        NewNonce = Guid.NewGuid().ToString()
    };
});
```

**Integration Tests**:
- Start mock server on localhost:5000
- Configure client to use mock endpoint
- Verify chain submission after N minutes
- Check nonce rotation in license.json

**TPM Anchoring Test (Windows only)**:
```csharp
// Test DPAPI protection
var original = File.ReadAllText("license.json");
var protected = TpmAnchor.Protect(original);
File.WriteAllText("license.protected.json", protected);

// Try to unprotect on SAME machine
var unprotected = TpmAnchor.Unprotect(protected);
Assert.Equal(original, unprotected);

// Copy to DIFFERENT machine
// Should throw CryptographicException
```

---

## Implementation Sequence

### Phase 1: TIER 1 Foundation (Weeks 1-2)
1. Create `LicenseExecutionContext.cs`
2. Create `AntiTamperDetector.cs` with P/Invoke
3. Create `LicenseEnforcementMode.cs` enum
4. Modify `LicenseSaveChangesInterceptor.cs`:
   - Add `SavingChangesAsync` override
   - Wrap in `LicenseExecutionContext.BeginScope()`
5. Modify `LicenseGuard.cs`:
   - Initialize `AntiTamperDetector`
   - Wrap `RecordAction` in context
6. Modify `LicenseConfigs.cs` (add EnforcementMode)
7. Modify `LicenseVerifier.cs` (tier-based logging)
8. Write unit tests
9. Write integration tests
10. Update README.md with TIER 1 docs

### Phase 2: TIER 2 Policies (Weeks 3-4)
1. Create `Policy/` namespace directory
2. Create `LicensePolicy.cs`, `PolicyEnforcementRules.cs`, `FeatureQuota.cs`
3. Create `PolicyVerifier.cs` (RSA verification)
4. Create `IPolicyStore.cs` + `FilePolicyStore.cs`
5. Create `PolicyEnforcer.cs` (rate limiting, quotas)
6. Create `tools/PolicySigner` CLI project
7. Modify `LicenseConfigs.cs` (add PolicyFilePath, RequireSignedPolicy)
8. Modify `LicenseServiceCollectionExtensions.cs` (register policy services)
9. Modify `LicenseGuard.cs` (inject PolicyEnforcer)
10. Modify `LicenseSaveChangesInterceptor.cs` (use policy enforcer)
11. Generate sample policy.json with PolicySigner
12. Write unit tests
13. Write integration tests
14. Update README.md with TIER 2 docs

### Phase 3: TIER 3 Server Validation (Weeks 5-6)
1. Create `ServerValidation/` namespace directory
2. Create `ChainSubmitter.cs` (HTTP client)
3. Create `NonceRotator.cs`
4. Create `TpmAnchor.cs` (Windows DPAPI)
5. Create `ChainSubmissionHostedService.cs`
6. Create mock license server for testing
7. Modify `LicenseConfigs.cs` (add TIER 3 settings)
8. Modify `LicenseServiceCollectionExtensions.cs` (register TIER 3 services)
9. Modify `LicenseStore.cs` (TPM protect/unprotect)
10. Modify `IFingerprintChainStore.cs` + implementation (GetRecentEntries)
11. Write unit tests
12. Deploy mock server, run integration tests
13. Update README.md with TIER 3 docs

### Phase 4: Integration & Polish (Week 7)
1. Test all 3 tiers together
2. Performance benchmarks (measure overhead)
3. Security audit (internal review)
4. Documentation review (README, code comments)
5. Migration guide for existing users
6. Release notes
7. Bump version to 2.0.0
8. Build and publish NuGet package

---

## Success Criteria

### TIER 1
✅ EF interceptor loop issue is completely fixed
✅ SavingChangesAsync override is present and tested
✅ License tier drives enforcement (not env var)
✅ Hardware breakpoint detection works on Windows
✅ All tests pass
✅ No regressions in Free mode

### TIER 2
✅ Policy files can be loaded and verified
✅ Invalid signatures are rejected
✅ Rate limits are enforced correctly
✅ PolicySigner tool generates valid policies
✅ Policy overrides config settings
✅ All tests pass

### TIER 3
✅ Chains submit to server successfully
✅ Nonce rotation works correctly
✅ TPM anchoring prevents license transfer (Windows)
✅ Background service runs without issues
✅ Server API endpoints implemented
✅ All tests pass

---

## Migration Guide for Existing Users

### Upgrading from 1.x to 2.0

**For FREE users**:
No changes required. Everything continues to work.

**For Licensed/Enterprise users wanting TIER 1 only**:
```json
{
  "LicenseConfigs": {
    "EnforcementMode": "Production",  // NEW: explicit mode
    "EnableAntiTampering": true       // Enhanced detection
  }
}
```

**For Enterprise users wanting TIER 2**:
1. Obtain signed policy from Muonroi server
2. Save as `policy.json` in app directory
3. Update config:
```json
{
  "LicenseConfigs": {
    "PolicyFilePath": "policy.json",
    "RequireSignedPolicy": true  // Enforce signature check
  }
}
```

**For Enterprise users wanting TIER 3**:
1. Contact Muonroi to set up server infrastructure
2. Obtain server endpoint URL
3. Update config:
```json
{
  "LicenseConfigs": {
    "EnableServerValidation": true,
    "ChainSubmissionIntervalMinutes": 60,
    "EnableTpmAnchoring": true,  // Optional, Windows only
    "Online": {
      "Endpoint": "https://license.muonroi.com/api/v1",
      "ChainSubmissionEndpoint": "/chain/submit"
    }
  }
}
```

---

## Risk Assessment

### TIER 1 Risks
- **P/Invoke compatibility**: GetThreadContext may fail on some Windows versions
  - **Mitigation**: Wrap in try-catch, gracefully degrade
- **Performance overhead**: Anti-tampering checks add latency
  - **Mitigation**: Only run when EnableAntiTampering=true, cache results

### TIER 2 Risks
- **Key management**: Private keys must be secured on server
  - **Mitigation**: Use HSM or key vault in production
- **Policy expiration**: Expired policies need renewal process
  - **Mitigation**: Send renewal reminders 30 days before expiry

### TIER 3 Risks
- **Server availability**: If server is down, validation fails
  - **Mitigation**: Graceful degradation, cached nonces remain valid
- **Network latency**: Chain submission adds network overhead
  - **Mitigation**: Async background service, doesn't block user operations
- **TPM compatibility**: Not all machines have TPM
  - **Mitigation**: Make TPM optional, document requirements

---

## Performance Considerations

### Expected Overhead

**TIER 1**:
- AsyncLocal check: ~0.01ms (negligible)
- Anti-tampering: ~5-10ms per check (only at startup)
- Overall: <1% overhead

**TIER 2**:
- Policy signature verification: ~10ms (only at startup)
- Rate limit check: ~0.1ms (in-memory dictionary lookup)
- Overall: <1% overhead

**TIER 3**:
- Chain submission: ~100-500ms (async, doesn't block)
- TPM protect/unprotect: ~10-50ms (only during license save/load)
- Overall: <5% overhead

### Optimization Opportunities
- Cache policy verification result
- Batch chain submissions
- Use async/await for all I/O operations
- Lazy load anti-tampering detector

---

## Summary

This comprehensive 3-tier security enhancement plan addresses all expert recommendations:

**✅ Defensive Expert's Concerns Addressed**:
- License Tier as source of truth (TIER 1)
- Signed policy files prevent config downgrade (TIER 2)
- Server nonce + TPM anchoring prevent replay (TIER 3)
- AsyncLocal prevents EF interceptor loops (TIER 1)

**✅ Offensive Expert's Warnings Acknowledged**:
- Hardware breakpoint detection added (TIER 1) - but acknowledged as cat-and-mouse game
- Strong name signing limitations understood - `sn -Vr` bypass noted in docs
- Encrypted state limitations understood - focus on server-side validation (TIER 3)
- Ultimate protection via server-side execution (TIER 3 guidance)

**🎯 Key Strengths**:
- **Backward Compatible**: Free mode untouched, existing users unaffected
- **Incremental Adoption**: Implement TIER 1 first, add TIER 2/3 later
- **DX First**: Development mode bypasses aggressive checks
- **Production Hardened**: Full enforcement only when needed
- **Well Tested**: Comprehensive test strategy for each tier

**📦 Deliverables**:
- 3 new namespaces: `License/Policy`, `License/ServerValidation`, plus core files
- ~15 new files, ~10 modified files
- PolicySigner CLI tool
- Mock license server for testing
- Comprehensive documentation

**⏱️ Timeline**: 7 weeks total (2 weeks per tier + 1 week integration)

**🚀 Ready to Implement**: This plan is ready for execution.
