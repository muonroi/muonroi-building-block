# Rule Engine Upgrade Report

## 1. Pham vi da nang cap

Dot nay tap trung vao 4 nhom:
- storage security hardening cho ruleset artifacts,
- governance validation cho workflow payload,
- license gate `rule-engine` theo tier,
- bo sung test bao phu cac case bao mat/chong sai cau hinh.

Bo sung them foundation cho "Dynamic Business Logic Cloud":
- runtime ruleset cache,
- distributed ruleset change notification,
- hot-reload qua cache invalidation.

## 2. Thay doi chinh

### 2.1. Security hardening cho File-backed ruleset store
- `FileRuleSetStore` da duoc harden:
  - validate tenant/workflow path segments theo regex.
  - chong path traversal bang `GetFullPath + ensure-under-root`.
  - parse version defensive (bo qua file version malformed thay vi crash).
  - gioi han kich thuoc artifact theo `MaxRuleSetSizeBytes`.
  - lock ghi version theo workflow de tranh race khi ghi dong thoi.

### 2.2. RuleStore security knobs
- `RuleStoreConfigs` bo sung:
  - `MaxRuleSetSizeBytes`
  - `RequireSignature`
  - `AllowedPathSegmentPattern`

### 2.3. Integrity & governance
- `HmacSha256RuleSetSigner.Verify` xu ly malformed base64 an toan (`false` thay vi throw FormatException).
- `RulesEngineService.SaveRuleSetAsync` bo sung validation:
  - JSON phai parse duoc.
  - `WorkflowName` trong payload phai khop workflow key.
  - `Rules` neu ton tai phai la non-empty array.

### 2.4. License gate cho Rule Engine
- Gate `FreeTierFeatures.Premium.RuleEngine` da duoc add vao:
  - `RulesEngineService` (`SaveRuleSetAsync`, `SetActiveVersionAsync`, `ExecuteAsync`)
  - `RuleEngine<T>` execution path

### 2.5. Dynamic hot-reload foundation
- Them notifier abstraction:
  - `IRuleSetChangeNotifier`
  - `InMemoryRuleSetChangeNotifier`
  - `RedisRuleSetChangeNotifier`
- Them runtime cache:
  - `IRuleSetRuntimeCache`
  - `RuleSetRuntimeCache`
- `RulesEngineService`:
  - cache read path cho ruleset payload
  - publish event khi save/activate
  - invalidate cache local + cross-node (neu Redis notifier bat)

## 3. File da thay doi

- `RULE_ENGINE_UPGRADE_PLAN.md`
- `src/Muonroi.BuildingBlock/Shared/Rules/RuleStoreConfigs.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/RuleEngineServiceCollectionExtensions.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/FileRuleSetStore.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/HmacSha256RuleSetSigner.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/RulesEngineService.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/RuleEngine.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/RuleSetChangeEvent.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/IRuleSetChangeNotifier.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/InMemoryRuleSetChangeNotifier.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/RedisRuleSetChangeNotifier.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/IRuleSetRuntimeCache.cs`
- `src/Muonroi.BuildingBlock/Shared/Rules/RuleSetRuntimeCache.cs`
- `tests/Muonroi.BuildingBlock.Test/FileRuleSetStoreSecurityTests.cs`
- `tests/Muonroi.BuildingBlock.Test/RulesEngineServiceSecurityTests.cs`
- `tests/Muonroi.BuildingBlock.Test/RuleEngineLicenseGateTests.cs`
- `tests/Muonroi.BuildingBlock.Test/RuleSetSigningTests.cs`
- `tests/Muonroi.BuildingBlock.Test/RuleSetRuntimeCacheTests.cs`
- `tests/Muonroi.BuildingBlock.Test/RulesEngineServiceHotReloadTests.cs`
- `DYNAMIC_BUSINESS_LOGIC_CLOUD_ROADMAP.md`

## 4. Test evidence

Da chay:

```bash
dotnet test tests/Muonroi.BuildingBlock.Test/Muonroi.BuildingBlock.Test.csproj -v minimal
dotnet test tests/Muonroi.RuleEngine.Core.Tests/Muonroi.RuleEngine.Core.Tests.csproj -v minimal
dotnet test tests/Rules/Muonroi.Rules.Tests.csproj -v minimal
```

Ket qua:
- `Muonroi.BuildingBlock.Test`: Passed `1427/1427`
- `Muonroi.RuleEngine.Core.Tests`: Passed `166/166`
- `Muonroi.Rules.Tests`: hien co issue duplicate assembly attributes trong local build (`CS0579`) nen khong su dung lam gate cho dot nay.

Ghi chu known issue (pre-existing, khong do dot upgrade nay):
- `tests/Rules/Lint/Rules.Lint.Tests.csproj` dang tham chieu sai path `src/Rules/Rules.csproj` (khong ton tai), nen khong build duoc.

## 5. Migration note

- Neu dang dung ruleset artifacts lon, can check lai `RuleStore:MaxRuleSetSizeBytes`.
- Neu muon bat governance chat, bat:
  - `RuleStore:RequireSignature = true`
  - va register `IRuleSetSigner`.
- Neu Free tier, duong Rule Engine se bi chan boi feature gate `rule-engine`.
