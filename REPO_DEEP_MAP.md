# muonroi-building-block — Deep Map

> Complete file-level map. Agents should read this instead of exploring the repo.

---

## 1. Rule Engine

### RuleEngine.Abstractions (`src/Muonroi.RuleEngine.Abstractions/`)

| File | Class/Interface | Key Methods/Properties |
|------|----------------|----------------------|
| IRule.cs | `IRule<TContext>` | `EvaluateAsync(ctx, facts, ct) → RuleResult`, `ExecuteAsync(ctx, ct)`, Code, Order, DependsOn, HookPoint, Type |
| IMRuleOrchestrator.cs | `IMRuleOrchestrator<TContext>` | `ExecuteAsync(context, ct) → OrchestratorResult` |
| FactBag.cs | `FactBag` | `Get<T>(key)`, `Set<T>(key, value)`, `TryGet<T>(key, out T)`, `Remove(key)`, `AsReadOnly()` |
| MFactBagAwareRule.cs | `MFactBagAwareRule<TContext>` | Abstract base for FactBag-aware compiled rules. `ReadFact<T>(path)`, `WriteFact<T>(path, value)`, `NodePassed(nodeId)`, `NodeExecuted(nodeId)`, `NodeResult<T>(nodeId)`, `EvaluateCoreAsync(ctx, ct)` |
| RuleResult.cs | `RuleResult` | `Passed()`, `Success()`, `Failure(errors)` — static factories |
| ExtractAsRuleAttribute.cs | `MExtractAsRuleAttribute` | `(code)` — marker for rule extraction |
| HookPoint.cs | `HookPoint` enum | BeforeRule, AfterRule, BeforeValidation, AfterValidation |
| RuleType.cs | `RuleType` enum | Validation (read-only), Business (state-modifying) |
| ExecutionMode.cs | `ExecutionMode` enum | AllOrNothing, BestEffort, CompensateOnFailure |
| ICompensatableRule.cs | `ICompensatableRule<TContext>` | `CompensateAsync(ctx, ct)` |
| IRuleContext.cs | `IRuleContext` | Marker interface for context types |
| IRuleFactory.cs | `IRuleFactory` | Factory for rule instances |
| IRuleEventListener.cs | `IRuleEventListener<TContext>` | `OnRuleExecuted(rule, result, ct)` |
| ITenantQuotaTracker.cs | `ITenantQuotaTracker` | `CheckQuotaAsync(tenantId, type, requested, ct)`, `IncrementUsageAsync(...)` |
| OrchestratorResult.cs | `OrchestratorResult` | Execution summary record |
| TenantRuleGroupAttribute.cs | `TenantRuleGroupAttribute` | Multi-tenant rule grouping |
| Authoring/IRuleAuthoringManifestProvider.cs | `IRuleAuthoringManifestProvider` | Rule schema discovery |
| Authoring/MRuleAuthoringModels.cs | `RuleAuthoringManifest`, `RuleParameterInfo` | Metadata models |
| Adapters/IContextFactory.cs | `IContextFactory<TContext>` | Context creation from facts |
| Adapters/IContextProjector.cs | `IContextProjector` | Projects context to facts |

### RuleEngine.Core (`src/Muonroi.RuleEngine.Core/`)

| File | Class | Key Methods |
|------|-------|------------|
| RuleOrchestrator.cs | `RuleOrchestrator<TContext>` | `ExecuteAsync(context, filterPoint, ct) → FactBag` — creates FactBag, handles quota/tracing |
| DefaultRuleFactory.cs | `DefaultRuleFactory` | Default rule instantiation |
| MRuleEngineBuilder.cs | `MRuleEngineBuilder` | Fluent builder for engine setup |
| MRuleEngineOptions.cs | `MRuleEngineOptions` | Configuration options |
| IMRuleExecutionRouter.cs | `IMRuleExecutionRouter` | Routes to different rule types |
| MRuleExecutionRouter.cs | `MRuleExecutionRouter` | Implementation |
| Workflow/IMRuleWorkflowRunner.cs | `IMRuleWorkflowRunner` | Workflow contract |
| Workflow/MRuleWorkflowRunner.cs | `MRuleWorkflowRunner` | Workflow orchestration |
| Workflow/MRuleWorkflowDefinition.cs | `MRuleWorkflowDefinition` | Workflow definition |
| Workflow/MRuleWorkflowStep.cs | `MRuleWorkflowStep` | Step in workflow |
| Tracing/IRuleExecutionTracer.cs | `IRuleExecutionTracer` | `TraceAsync(entry, ct)`, `IsEnabled(tenantId)` |
| Tracing/IRuleTraceStore.cs | `IRuleTraceStore` | Trace persistence |
| Tracing/IRuleDebuggerModeService.cs | `IRuleDebuggerModeService` | Debugger control |
| RuleAuditLogger.cs | `RuleAuditLogger` | Audit trail |
| AuditTrailHook.cs | `AuditTrailHook<TContext>` | Lifecycle audit hook |

### RuleEngine.Runtime (`src/Muonroi.RuleEngine.Runtime/`)

**Rules subsystem** (`Runtime/Rules/`):

| File | Class | Purpose |
|------|-------|---------|
| RulesEngineService.cs | `RulesEngineService` | Main service facade |
| RuleEngine.cs | `RuleEngine<T>` | `AddRule()`, `RemoveRule()`, `ExecuteAsync()`, `GetCatalog()` |
| IRuleSetStore.cs | `IRuleSetStore` | RuleSet persistence |
| IRuleSetAuditStore.cs | `IRuleSetAuditStore` | Audit storage |
| IRuleSetApprovalService.cs | `IRuleSetApprovalService` | Approval workflow |
| ICanaryRolloutService.cs | `ICanaryRolloutService` | Canary deployment |
| IRuleActivationStrategy.cs | `IRuleActivationStrategy<T>` | Activation policies |
| IRuleSetSigner.cs | `IRuleSetSigner` | HMAC/RSA signing |
| PostgresRuleSetStore.cs | `PostgresRuleSetStore` | Postgres persistence |
| FileRuleSetStore.cs | `FileRuleSetStore` | File-based persistence |
| RuleEngineDbContext.cs | `RuleEngineDbContext` | EF DbContext |
| RuleSetRecord.cs | `RuleSetRecord` | Data model |
| RuleSetStatus.cs | `RuleSetStatus` enum | Active, Draft, Archived... |
| RuleSetRuntimeCache.cs | `RuleSetRuntimeCache` | In-memory cache |
| CanaryRolloutService.cs | `CanaryRolloutService` | Canary logic |
| RuleSetApprovalService.cs | `RuleSetApprovalService` | Approval impl |
| HmacSha256RuleSetSigner.cs | `HmacSha256RuleSetSigner` | HMAC signing |
| RsaRuleSetAuditSigner.cs | `RsaRuleSetAuditSigner` | RSA signing |
| ExternalJsonRule.cs | `ExternalJsonRule` | JSON-based rule |

**Adapters** (`Runtime/Adapters/`):

| File | Class | Purpose |
|------|-------|---------|
| FeelRuleAdapter.cs | `FeelRuleAdapter` | FEEL expression → FactBag output. Writes flat key + scoped `__node.{code}.{path}` |
| DecisionTableRuleAdapter.cs | `DecisionTableRuleAdapter` | Decision table integration |
| SubFlowRuleAdapter.cs | `SubFlowRuleAdapter` | Sub-workflow calls |
| GraphRuleDispatchAdapter.cs | `GraphRuleDispatchAdapter` | Flow graph dispatch + branching. Writes `__graph.node.{nodeId}.result` (scoped) + `result` (generic) |
| LiquidRuleAdapter.cs | `LiquidRuleAdapter` | Liquid template rules |
| RuleGraphParser.cs | `RuleGraphParser` | Kahn's sort → execution order |
| RuleFlowGraphModels.cs | `FlowGraph`, `FlowNode`, `FlowEdge` | Graph data structures |
| ReflectionContextFactory.cs | `ReflectionContextFactory` | Reflection-based context creation |

**FEEL compilation** (`Runtime/Compilation/Feel/`):

| File | Class | Purpose |
|------|-------|---------|
| FeelExpressionCompiler.cs | `FeelExpressionCompiler` | FEEL → executable |
| ExpressionTreeVisitor.cs | `ExpressionTreeVisitor` | FEEL AST visitor |

### RuleEngine.Runtime.Web (`src/Muonroi.RuleEngine.Runtime.Web/`)

| File | Class | Routes |
|------|-------|--------|
| Controllers/RuntimeRuleSetController.cs | REST | `GET/POST/PUT/DELETE /api/v1/rule-sets`, `GET /approve`, `POST /canary` |
| Controllers/MRuleFlowContractController.cs | REST | `GET /api/v1/rule-flow/contract` |
| Hubs/RuleSetChangeHub.cs | SignalR | Real-time rule notifications |
| Services/RuleDryRunService.cs | `RuleDryRunService` | Test execution without persistence |
| Services/IMRuleFlowContractProvider.cs | Interface | Flow schema provider |

### RuleEngine.DecisionTable (`src/Muonroi.RuleEngine.DecisionTable/`)

| File | Class | Key Methods |
|------|-------|------------|
| IDecisionTableExecutor.cs | `IDecisionTableExecutor` | `ExecuteAsync(table, inputFacts, ct) → DecisionTableExecutionResult` |
| DecisionTableExecutor.cs | `DecisionTableExecutor` | Hit policies: Unique, Any, First, All, Collect, RuleOrder |
| Models/DecisionTable.cs | `DecisionTable` | Table definition |
| Models/HitPolicy.cs | `HitPolicy` enum | Unique, Any, First, All, Collect, RuleOrder |
| Models/DecisionTableExecutionResult.cs | Result | Matched rows + outputs |
| Stores/IDecisionTableStore.cs | `IDecisionTableStore` | `CreateAsync`, `ReadAsync`, `UpdateAsync`, `DeleteAsync`, `ListAsync` |
| Stores/InMemoryDecisionTableStore.cs | `InMemoryDecisionTableStore` | Volatile storage |
| Stores/EfCoreDecisionTableStore.cs | `EfCoreDecisionTableStore` | Postgres/SQL Server |
| Stores/Persistence/DecisionTableDbContext.cs | `DecisionTableDbContext` | EF context |
| Feel/IFeelCellEvaluator.cs | `IFeelCellEvaluator` | `Evaluate(expr, actual, dataType) → bool` |
| Feel/FullFeelCellEvaluator.cs | `FullFeelCellEvaluator` | Complete FEEL support |
| Feel/SimplifiedFeelCellEvaluator.cs | `SimplifiedFeelCellEvaluator` | Simplified dialect |
| Validators/DecisionTableValidator.cs | `DecisionTableValidator` | Structural validation |
| Validators/OverlapDetector.cs | `OverlapDetector` | Condition overlap |
| Validators/MultiColumnOverlapDetector.cs | `MultiColumnOverlapDetector` | Multi-column conflicts |
| Validators/GapDetector.cs | `GapDetector` | Input coverage gaps |
| Converters/ExcelToDecisionTableConverter.cs | Converter | Excel import |

### RuleEngine.DecisionTable.Web (`src/Muonroi.RuleEngine.DecisionTable.Web/`)

| File | Routes |
|------|--------|
| Controllers/DecisionTableController.cs | `GET/POST/PUT/DELETE /api/v1/decision-tables` |
| Controllers/DecisionTableFeelController.cs | `POST /api/v1/decision-tables/{id}/feel` |
| Controllers/DecisionTableValidationController.cs | `POST /api/v1/decision-tables/validate` |
| Controllers/DecisionTableExportController.cs | `GET /export` |

### RuleEngine.CEP (`src/Muonroi.RuleEngine.CEP/`)

| File | Class | Purpose |
|------|-------|---------|
| CepEngine.cs | `CepEngine` | Event aggregation and windowing |
| WindowType.cs | `WindowType` enum | Tumbling, Sliding, Session |
| Builder/CepWindowBuilder.cs | `CepWindowBuilder` | Fluent window config |
| Controllers/CepController.cs | REST | Event stream management |

### RuleEngine.Testing (`src/Muonroi.RuleEngine.Testing/`)

| File | Class | Purpose |
|------|-------|---------|
| MRuleTestBuilder.cs | `MRuleTestBuilder` | Test scaffolding |
| MRuleOrchestratorSpy.cs | `MRuleOrchestratorSpy` | Orchestrator mock |
| MFactBagAssertions.cs | `MFactBagAssertions` | Fluent assertions for FactBag |

### RuleEngine.SourceGenerators (`src/Muonroi.RuleEngine.SourceGenerators/`) — netstandard2.0

| File | Class | Purpose |
|------|-------|---------|
| ExtractAsRuleGenerator.cs | `IIncrementalGenerator` | Generates rule classes from `[MExtractAsRule]` |
| RuleRegistrationGenerator.cs | Generator | Generates DI registration code |
| RuleCatalogRegistrationGenerator.cs | Generator | Generates rule catalog |
| Analyzers/MBB001–MBB007 | Roslyn analyzers | Ecosystem closure enforcement |
| CodeFixes/MBB001_*.cs, MBB002_*.cs | Code fixes | Auto-fix for analyzer violations |
| FeelExpressionSyntaxValidator.cs | Validator | FEEL syntax validation |
| Polyfills.cs | `IsExternalInit` | netstandard2.0 record polyfill |

---

## 2. Multi-Tenancy

### Tenancy.Abstractions (`src/Muonroi.Tenancy.Abstractions/`)

| File | Class/Interface | Purpose |
|------|----------------|---------|
| ITenantContext.cs | `ITenantContext` | Get/set current TenantId |
| ITenantConnectionStringFactory.cs | `ITenantConnectionStringFactory` | Resolve connection string by tenant |
| ITenantIdResolver.cs | `ITenantIdResolver` | Extract tenant ID from request |
| ITenantScoped.cs | `ITenantScoped` | Marker for tenant-scoped services |
| MultiTenantOptions.cs | `MultiTenantOptions` | Configuration |
| TenantConnectionStringsOptions.cs | `TenantConnectionStringsOptions` | Connection strings map |
| InMemoryTenantQuotaStore.cs | `InMemoryTenantQuotaStore` | Volatile quota storage |
| InMemoryTenantQuotaTracker.cs | `InMemoryTenantQuotaTracker` | Quota enforcement |
| Interfaces/ITenantQuotaStore.cs | `ITenantQuotaStore` | Persistent quota |
| Models/TenantQuota.cs | `TenantQuota` | Quota definition |
| Licensing/ITenantLicenseFeatureGate.cs | `ITenantLicenseFeatureGate` | Tier-based access |

### Tenancy.Core (`src/Muonroi.Tenancy.Core/`)

| File | Class | Purpose |
|------|-------|---------|
| TenantContext.cs | `TenantContext` | Static `CurrentTenantId` via `AsyncLocal<string>` |
| DefaultTenantIdResolver.cs | `DefaultTenantIdResolver` | Header-based resolution |
| DefaultTenantConnectionStringFactory.cs | Factory | Default connection factory |
| MappingTenantConnectionStringFactory.cs | Factory | Custom mapping |
| TenantSchemaSelector.cs | `TenantSchemaSelector` | Schema-per-tenant EF |
| TenantSecurityValidator.cs | `TenantSecurityValidator` | XSS/injection protection |
| Shared/TenantQuotaTracker.cs | `TenantQuotaTracker` | Quota impl |
| ContextMirrorScope.cs | `ContextMirrorScope` | Mirrors execution context to log scopes |

### Tenancy (`src/Muonroi.Tenancy/`)

| File | Class | Purpose |
|------|-------|---------|
| TenantResolutionMiddleware.cs | `TenantResolutionMiddleware` | ASP.NET middleware |
| Cache/RedisTenantCache.cs | `RedisTenantCache` | Redis tenant cache |

---

## 3. Core Abstractions (Ecosystem Wrappers)

### Core.Abstractions (`src/Muonroi.Core.Abstractions/`)

| File | Interface | Methods |
|------|-----------|---------|
| Interfaces/IMDateTimeService.cs | `IMDateTimeService` | `Now()`, `UtcNow()`, `Today()`, `UtcToday()`, `NowTs()`, `UtcNowTs()` |
| Interfaces/IMJsonSerializeService.cs | `IMJsonSerializeService` | `Serialize<T>(obj)`, `Deserialize<T>(text)` |
| Context/ISystemExecutionContextAccessor.cs | `ISystemExecutionContextAccessor` | `Get() → ISystemExecutionContext` |
| Context/ISystemExecutionContext.cs | `ISystemExecutionContext` | TenantId, UserId, CorrelationId |
| Context/ILogScopeFactory.cs | `ILogScopeFactory` | Log scope creation |
| Diagnostics/IMTraceContext.cs | `IMTraceContext` | Tracing facade |

---

## 4. Governance & License

### Governance.Abstractions (`src/Muonroi.Governance.Abstractions/`)

| File | Class/Interface | Purpose |
|------|----------------|---------|
| License/ILicenseGuard.cs | `ILicenseGuard` | `Tier`, `EnsureValid()`, `HasFeature()`, `EnsureFeature()` |
| License/LicensePayload.cs | `LicensePayload` | JWT claims: tier, features, expiry, seats |
| License/LicenseEnums.cs | `LicenseTier` enum | Free=0, Licensed=1, Enterprise=2 |
| License/ActivationProof.cs | `ActivationProof` | RSA-signed offline proof |
| License/ILicenseGuardEnhancer.cs | `ILicenseGuardEnhancer` | Extended guard logic |
| License/ILicenseFingerprintProvider.cs | `ILicenseFingerprintProvider` | Device fingerprint |
| License/IFingerprintChainStore.cs | `IFingerprintChainStore` | Chain persistence |
| License/LicenseConfigs.cs | `LicenseConfigs` | Mode (Offline/Online), FilePath, PublicKeyPath |

### Governance.Enterprise (`src/Muonroi.Governance.Enterprise/`)

| File | Class | Notes |
|------|-------|-------|
| License/AntiTamperDetector.cs | `AntiTamperDetector` | **BUG**: 64-bit disables checking |
| License/EnterpriseLicenseGuardEnhancer.cs | `EnterpriseLicenseGuardEnhancer` | **BUG**: uses static TenantContext |
| License/LicenseActivator.cs | `LicenseActivator` | **BUG**: 5 IMLog violations |
| License/CodeIntegrityVerifier.cs | `CodeIntegrityVerifier` | Assembly checksum |
| License/FingerprintProvider.cs | `FingerprintProvider` | Hardware fingerprint |
| License/LicenseHeartbeatService.cs | `LicenseHeartbeatService` | Heartbeat keep-alive |
| License/HmacFingerprintSigner.cs | `HmacFingerprintSigner` | HMAC chain signing |
| ControlPlane/EnterpriseControlPlaneService.cs | Service | Approval + canary workflow |
| Compliance/MComplianceExportService.cs | Service | Compliance export |

---

## 5. RuleGen CLI & Tools

### RuleGen CLI (`tools/Muonroi.RuleGen/`)

**Commands** (`Commands/`):

| File | Command | Purpose |
|------|---------|---------|
| ExtractCommand.cs | `extract` | Extract `[MExtractAsRule]` methods → `.g.cs` rules |
| VerifyCommand.cs | `verify` | Verify extracted rules match generated files |
| RegisterCommand.cs | `register` | Register rules in rule store |
| GenerateTestsCommand.cs | `generate-tests` | Generate test scaffolds |
| MergeCommand.cs | `merge` | Merge rule sets |
| SplitCommand.cs | `split` | Split rule sets by namespace/tenant |
| WatchCommand.cs | `watch` | File watcher for re-extraction |

**Services** (`Services/`):

| File | Class | Purpose |
|------|-------|---------|
| RoslynRuleExtractor.cs | `RoslynRuleExtractor` | `ExtractAsync(files, ns, contextOverride, parallel, ct)` — semantic analysis |
| CompileCheckService.cs | `CompileCheckService` | Compilation verification |
| AuditMetadataService.cs | `AuditMetadataService` | Git commit/author resolution |
| FeelCSharpTranslator.cs | `FeelCSharpTranslator` | FEEL → C# transpilation |
| RuntimeRuleJsonService.cs | `RuntimeRuleJsonService` | Runtime rule serialization |

**Writers** (`Writers/`):

| File | Class | Purpose |
|------|-------|---------|
| RuleClassWriter.cs | `RuleClassWriter` | Generates `.g.cs` rule class |
| RegistrationWriter.cs | `RegistrationWriter` | Generates DI registration code |
| DispatcherWriter.cs | `DispatcherWriter` | Generates dispatch table |
| TestScaffoldWriter.cs | `TestScaffoldWriter` | Generates unit test template |

### Other Tools

| Tool | Location | Purpose |
|------|----------|---------|
| DecisionTableGen | `tools/Muonroi.DecisionTableGen/` | Decision table code gen CLI |
| RuleGen MCP | `tools/Muonroi.RuleGen.Mcp/` | MCP server for IDE integration |
| RuleGen VSIX | `tools/Muonroi.RuleGen.VisualStudio/` | VS extension: CodeLens + diagnostics |
| VS Code Extension | `tools/Muonroi.RuleGen/vscode-extension/` | CodeLens, diagnostics, snippets |

---

## 6. FEEL Engine (`src/Muonroi.Rules/`)

| File | Class | Purpose |
|------|-------|---------|
| Feel/FeelEvaluator.cs | `FeelEvaluator` | FEEL expression evaluation |
| Feel/FeelParser.cs | `FeelParser` | FEEL AST parser |
| Feel/FeelStandardLibrary.cs | `FeelStandardLibrary` | Built-in functions |
| Flags/FeatureFlagEvaluator.cs | `FeatureFlagEvaluator` | Feature flag logic |
| Controllers/FeelController.cs | REST | `POST /api/v1/feel` |

---

## 7. Samples (`samples/`)

| Sample | Purpose |
|--------|---------|
| FraudDetection/ | Fraud detection rules |
| LoanApproval/ | Loan approval workflow |
| MultiTenantSaaS/ | Multi-tenant pattern |
| Quickstart.DecisionTable/ | Decision table quickstart |
| Quickstart.RuleEngine/ | Rule engine quickstart |
| RuleSourceGen/ | Source generation example |

---

## 8. Key Pipelines

### Rule Execution
```
IMRuleOrchestrator.ExecuteAsync() → RuleOrchestrator creates FactBag
  → resolves rules (compiled + FEEL + flow graph)
  → ExecutionMode: AllOrNothing | BestEffort | CompensateOnFailure
  → each rule: EvaluateAsync(ctx, factBag, ct) → RuleResult
  → aggregate → OrchestratorResult
```

### Flow Graph
```
RuleGraphParser (Kahn's sort) → RuleGraphEntry[]
  → GraphRuleDispatchAdapter executes nodes
  → FactBag keys: __graph.node.{nodeId}.executed/passed/errored/result
  → FEEL outputField keys: flat + __node.{code}.{path} (scoped)
  → Branching: on-true/on-false read FactBag state
```

### Code Generation
```
Source with [MExtractAsRule("CODE")] → RoslynRuleExtractor
  → ExtractedRuleDefinition → RuleClassWriter → {CODE}.g.cs
  → RegistrationWriter → MGeneratedRuleRegistrationExtensions.g.cs
  → DispatcherWriter → {Context}GeneratedRuleEngineDispatcher.g.cs
  → Consumer: services.AddMGeneratedRules()
```

### Decision Table Execution
```
IDecisionTableStore.ReadAsync(id) → DecisionTable
  → DecisionTableExecutor.ExecuteAsync(table, inputFacts, ct)
  → IFeelCellEvaluator per cell → HitPolicy selection
  → DecisionTableExecutionResult (matched rows + outputs)
```
