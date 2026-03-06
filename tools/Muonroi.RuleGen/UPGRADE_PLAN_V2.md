# Muonroi.RuleGen Upgrade Plan V2 - Runtime Integration Edition

## Executive Summary

This plan extends the original RuleGen upgrade roadmap to support **bidirectional workflow** between runtime-generated rules (by BA/QC) and developer code. This enables a complete development lifecycle where:

1. **Code-first flow**: Developer writes handlers → Extract to rules → Deploy
2. **Runtime-first flow**: BA/QC create rules at runtime → Developer merges to handlers → Customize → Deploy
3. **Round-trip flow**: Developer splits handlers → BA/QC test/modify rules → Developer re-merges

## Critical User Persona Shift

**Original**: Developer-only toolkit (code → rules)
**Updated**: Multi-persona toolkit (BA/QC + Developer, bidirectional)

### New Personas
- **BA/QC**: Creates/edits rules at runtime during testing using Decision Table UI or Rule Designer
- **Developer**: Merges runtime rules into codebase, customizes logic, maintains code quality

---

## Phase 0: Foundation Assessment (Current State)

### Current Architecture
```
Developer Code (with [MExtractAsRule])
    ↓ extract command
Generated Rule Classes (*.g.cs with TODO)
    ↓ register command
DI Registration Code
```

### Runtime Architecture
```
BA/QC creates rules in UI
    ↓
FileRuleSetStore saves JSON
    ↓ (versioned, signed, multi-tenant)
Runtime execution via RuleOrchestrator
```

### **Critical Gap**: No bridge between runtime JSON rules and developer C# code

---

## Phase 1: Roslyn Foundation (Weeks 1-3) - 120 hours

### Goals
- Replace regex-based parsing with Microsoft.CodeAnalysis (Roslyn)
- Extract method bodies correctly (fix TODO placeholder issue)
- Prepare AST manipulation foundation for merge/split

### Tasks

#### 1.1 Roslyn Integration (40h)
- [ ] Add Microsoft.CodeAnalysis.CSharp package
- [ ] Build syntax tree parser for source files
- [ ] Extract method signatures with full semantic analysis
- [ ] Parse [MExtractAsRule] attributes using Roslyn AttributeSyntax

#### 1.2 Method Body Extraction (50h)
**Current blocker**: Line 213 in Program.cs has `// TODO: map method body`

- [ ] Extract complete method body from SyntaxTree
- [ ] Preserve statements, expressions, and control flow
- [ ] Handle async/await patterns correctly
- [ ] Map parameters (ctx → context, facts, cancellationToken)
- [ ] Preserve comments and documentation

**Example transformation**:
```csharp
// Source handler method
[MExtractAsRule("VAL-001")]
public async Task ValidateAge(OrderContext ctx)
{
    if (ctx.Customer.Age < 18)
        throw new ValidationException("Customer must be 18+");
}

// Generated rule (BEFORE - current)
public Task<RuleResult> EvaluateAsync(OrderContext ctx, FactBag facts, CancellationToken ct)
{
    // TODO: map method body from ValidateAge to generated evaluation logic.
    return Task.FromResult(RuleResult.Passed());
}

// Generated rule (AFTER - Roslyn)
public async Task<RuleResult> EvaluateAsync(OrderContext ctx, FactBag facts, CancellationToken ct)
{
    try
    {
        if (ctx.Customer.Age < 18)
            throw new ValidationException("Customer must be 18+");
        return RuleResult.Passed();
    }
    catch (Exception ex)
    {
        return RuleResult.Failure(ex.Message);
    }
}
```

#### 1.3 Code Generation with SyntaxFactory (30h)
- [ ] Use SyntaxFactory to build class declarations
- [ ] Generate properly formatted C# code
- [ ] Preserve original code style (braces, indentation)
- [ ] Add XML documentation comments

**Deliverables**:
- ✅ Roslyn-based extract command with full method body extraction
- ✅ Generated rules are fully functional (no TODO placeholders)
- ✅ 95%+ code coverage on extraction logic

---

## Phase 2: Multi-File & DI Awareness (Weeks 4-6) - 100 hours

### Goals
- Process multiple source files (handlers scattered across solution)
- Extract dependency injection patterns
- Generate comprehensive unit tests
- Add validation and linting

### Tasks

#### 2.1 Multi-File Processing (30h)
**Current limitation**: Processes only single `--source` file

- [ ] Accept `--source-dir` to scan entire directory tree
- [ ] Discover all classes with [MExtractAsRule] methods
- [ ] Handle namespace resolution across files
- [ ] Generate output organized by namespace/feature

```bash
# Before
muonroi-rulegen extract --source OrderHandler.cs --output rules/

# After
muonroi-rulegen extract --source-dir src/Handlers --output rules/ --pattern "**/*Handler.cs"
```

#### 2.2 Dependency Injection Extraction (40h)
- [ ] Detect constructor-injected services in handler classes
- [ ] Extract service dependencies (ILogger, IRepository, etc.)
- [ ] Generate rules with DI-aware constructors
- [ ] Update registration to include service dependencies

**Example**:
```csharp
// Handler with DI
public class OrderHandler
{
    private readonly IOrderRepository _repo;
    private readonly ILogger<OrderHandler> _logger;

    public OrderHandler(IOrderRepository repo, ILogger<OrderHandler> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    [MExtractAsRule("ORD-001")]
    public async Task ValidateInventory(OrderContext ctx)
    {
        var product = await _repo.GetProductAsync(ctx.ProductId);
        _logger.LogInformation("Validating inventory for {ProductId}", ctx.ProductId);
        // validation logic...
    }
}

// Generated rule
public class ORD_001Rule : IRule<OrderContext>
{
    private readonly IOrderRepository _repo;
    private readonly ILogger<ORD_001Rule> _logger;

    public ORD_001Rule(IOrderRepository repo, ILogger<ORD_001Rule> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<RuleResult> EvaluateAsync(OrderContext ctx, FactBag facts, CancellationToken ct)
    {
        try
        {
            var product = await _repo.GetProductAsync(ctx.ProductId);
            _logger.LogInformation("Validating inventory for {ProductId}", ctx.ProductId);
            // extracted validation logic...
            return RuleResult.Passed();
        }
        catch (Exception ex)
        {
            return RuleResult.Failure(ex.Message);
        }
    }
}
```

#### 2.3 Test Generation (20h)
- [ ] Generate xUnit test scaffolds for each rule
- [ ] Include arrange/act/assert structure
- [ ] Mock dependencies using NSubstitute
- [ ] Generate test data builders

#### 2.4 Validation & Linting (10h)
- [ ] Detect cyclic dependencies in DependsOn
- [ ] Validate Order conflicts
- [ ] Check for duplicate rule codes
- [ ] Warn on missing XML docs

**Deliverables**:
- ✅ Scalable multi-file extraction
- ✅ DI-aware rule generation
- ✅ Automated test generation
- ✅ Validation prevents common errors

---

## Phase 3: Developer Experience (Weeks 7-9) - 80 hours

### Goals
- Configuration file for team standards
- Watch mode for continuous regeneration
- Visual Studio / VS Code extension

### Tasks

#### 3.1 Configuration File (25h)
Create `.rulegenrc.json`:
```json
{
  "extract": {
    "sourceDir": "src/BusinessHandlers",
    "outputDir": "src/Generated/Rules",
    "namespace": "MyApp.Rules",
    "contextType": "MyApp.Domain.OrderContext",
    "filePattern": "**/*Handler.cs",
    "excludePatterns": ["**/obj/**", "**/bin/**"]
  },
  "conventions": {
    "ruleCodePrefix": "ORD",
    "defaultHookPoint": "BeforeRule",
    "generateTests": true,
    "testFramework": "xunit"
  },
  "validation": {
    "enforceCodeFormat": "^[A-Z]{3}-\\d{3}$",
    "requireXmlDocs": true,
    "detectCycles": true
  }
}
```

- [ ] Schema validation for config file
- [ ] Environment variable overrides
- [ ] Per-project vs global config

#### 3.2 Watch Mode (30h)
```bash
muonroi-rulegen watch --config .rulegenrc.json
```

- [ ] File system watcher on source directory
- [ ] Incremental regeneration on changes
- [ ] Debouncing to avoid over-triggering
- [ ] Live compilation feedback

#### 3.3 IDE Extension (25h)
**Visual Studio Code Extension**: `muonroi-rulegen-vscode`

Features:
- [ ] Syntax highlighting for [MExtractAsRule]
- [ ] CodeLens: "Extract Rule" / "View Generated"
- [ ] Inline diagnostics for validation errors
- [ ] Command palette integration

**Deliverables**:
- ✅ Zero-config for teams (via .rulegenrc.json)
- ✅ Continuous workflow (watch mode)
- ✅ IDE-integrated experience

---

## **Phase 4: Merge/Split Workflow (NEW - Weeks 10-12) - 140 hours**

### 🎯 Goals
Enable bidirectional workflow between runtime-generated rules and developer code

### User Story
**As a BA/QC**, during testing I discover a new validation rule is needed. I create it using the Decision Table UI. The rule is saved to `FileRuleSetStore` and immediately active.

**As a Developer**, I want to:
1. **Merge** those runtime-generated rules into my `OrderHandler` class for code review and customization
2. **Split** my existing `OrderHandler` back into separate rules for BA/QC to test modifications

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Runtime Rule Generation                      │
│  BA/QC creates rules → FileRuleSetStore (JSON) → Execution     │
└────────────────────────┬────────────────────────────────────────┘
                         │
                    ┌────▼────┐
                    │  merge  │ Command
                    └────┬────┘
                         │
         ┌───────────────▼──────────────────┐
         │  Business Handler Class           │
         │  (Consolidated C# code)           │
         │  - Generated methods (partial)    │
         │  - Custom methods (partial)       │
         └───────────────┬──────────────────┘
                         │
                    ┌────▼────┐
                    │  split  │ Command
                    └────┬────┘
                         │
         ┌───────────────▼──────────────────┐
         │  Individual Rule Files            │
         │  *.g.cs (auto-generated)          │
         └───────────────────────────────────┘
```

### Tasks

#### 4.1 Runtime Rule Discovery (30h)
- [ ] Read rules from `FileRuleSetStore` JSON format
- [ ] Support multi-tenant rule isolation
- [ ] Handle versioned rulesets (active vs historical)
- [ ] Parse FEEL expressions from decision tables

**Input format** (FileRuleSetStore JSON):
```json
{
  "workflowName": "order-validation",
  "version": 3,
  "rules": [
    {
      "code": "VAL-001",
      "name": "ValidateCustomerAge",
      "hookPoint": "BeforeRule",
      "order": 10,
      "dependsOn": [],
      "condition": "customer.age >= 18",
      "action": "facts['isAdult'] = true",
      "type": "Validation"
    }
  ]
}
```

#### 4.2 Merge Command Implementation (60h)

**CLI Signature**:
```bash
muonroi-rulegen merge \
  --rules-json <path-to-runtime-rules.json> \
  --target <BusinessHandler.cs> \
  --strategy [append|replace|interactive] \
  --partial-class true
```

**Sub-tasks**:

**4.2.1 JSON to C# Conversion (25h)**
- [ ] Parse runtime rule JSON (from FileRuleSetStore)
- [ ] Convert FEEL expressions to C# code
- [ ] Map condition → if statements
- [ ] Map action → fact assignments
- [ ] Generate method signature from rule metadata

**Example**:
```json
{
  "code": "VAL-002",
  "condition": "order.total > 1000 and customer.vipStatus = 'Gold'",
  "action": "facts['discount'] = 0.15"
}
```

→ Generates:
```csharp
[MExtractAsRule("VAL-002", Order = 20)]
public Task ValidateVipDiscount(OrderContext ctx, FactBag facts)
{
    if (ctx.Order.Total > 1000 && ctx.Customer.VipStatus == "Gold")
    {
        facts["discount"] = 0.15;
        return Task.FromResult(RuleResult.Passed());
    }
    return Task.FromResult(RuleResult.Failure("VIP discount condition not met"));
}
```

**4.2.2 Conflict Resolution (20h)**
Merge strategies:

1. **Append** (default): Add new methods to end of class
2. **Replace**: Overwrite existing methods with same `[MExtractAsRule("code")]`
3. **Interactive**: Prompt developer for each conflict

- [ ] Detect existing methods with same rule code
- [ ] Three-way merge visualization (original, runtime, merged)
- [ ] Preserve developer customizations (comments, error handling)

**4.2.3 Partial Class Support (15h)**
Clean separation of generated vs custom code:

```csharp
// OrderHandler.Generated.cs (auto-generated, DO NOT EDIT)
namespace MyApp.Handlers;

public partial class OrderHandler
{
    [MExtractAsRule("VAL-001")]
    public Task ValidateAge(OrderContext ctx, FactBag facts)
    {
        // Generated from runtime rule
        if (ctx.Customer.Age < 18)
            return Task.FromResult(RuleResult.Failure("Must be 18+"));
        return Task.FromResult(RuleResult.Passed());
    }
}

// OrderHandler.cs (developer customizations)
namespace MyApp.Handlers;

public partial class OrderHandler
{
    private readonly IOrderRepository _repo;

    public OrderHandler(IOrderRepository repo) => _repo = repo;

    // Custom business logic here
    public async Task ProcessOrder(OrderContext ctx)
    {
        // Developer-written code
    }
}
```

- [ ] Generate `.Generated.cs` partial class for merged rules
- [ ] Preserve original `.cs` for developer customizations
- [ ] Add header comment: `// <auto-generated />`
- [ ] Support multiple partial class files

#### 4.3 Split Command Implementation (40h)

**CLI Signature**:
```bash
muonroi-rulegen split \
  --source <BusinessHandler.cs> \
  --output-dir <rules/> \
  --export-json <runtime-rules.json> \
  --tenant <tenant-id>
```

**Sub-tasks**:

**4.3.1 Method Extraction (20h)**
- [ ] Scan handler class for `[MExtractAsRule]` methods
- [ ] Extract method body using Roslyn
- [ ] Generate individual rule class files (*.g.cs)
- [ ] Preserve metadata (Order, DependsOn, HookPoint)

**4.3.2 JSON Export for Runtime (15h)**
Convert extracted rules back to FileRuleSetStore format:

```csharp
// Handler method
[MExtractAsRule("VAL-003", Order = 30)]
public Task ValidateInventory(OrderContext ctx, FactBag facts)
{
    if (ctx.Product.Stock < ctx.Order.Quantity)
        return Task.FromResult(RuleResult.Failure("Insufficient stock"));
    return Task.FromResult(RuleResult.Passed());
}
```

→ Exports to JSON:
```json
{
  "code": "VAL-003",
  "name": "ValidateInventory",
  "order": 30,
  "condition": "product.stock >= order.quantity",
  "action": "facts['stockAvailable'] = true",
  "type": "Validation"
}
```

**4.3.3 C# to FEEL Translation (5h)**
- [ ] Parse C# conditionals → FEEL expressions
- [ ] Handle simple comparisons (>, <, ==, !=)
- [ ] Preserve logical operators (&&, ||, !)
- [ ] Warn on complex C# that cannot be translated

**Limitations**: Complex C# (loops, LINQ, external service calls) cannot be fully translated to FEEL. Mark these as "custom" and exclude from JSON export.

#### 4.4 Workflow Integration (10h)

**4.4.1 IDE Commands**
Add to VS Code extension:
- [ ] "Merge Runtime Rules..." command
- [ ] "Split Handler to Rules..." command
- [ ] Preview diff before applying merge

**4.4.2 CI/CD Integration**
```yaml
# .github/workflows/rulegen.yml
- name: Merge runtime rules
  run: |
    dotnet tool install muonroi-rulegen
    muonroi-rulegen merge \
      --rules-json qa-env/runtime-rules.json \
      --target src/Handlers/OrderHandler.cs \
      --strategy replace
```

**Deliverables**:
- ✅ `merge` command: Runtime JSON → C# handler class
- ✅ `split` command: C# handler → Individual rules + JSON
- ✅ Conflict resolution with 3 strategies
- ✅ Partial class support for clean separation
- ✅ Round-trip capability (merge → customize → split)

---

## Phase 5: Enterprise Features (Weeks 13-16) - 100 hours

### Goals
- Multi-tenant rule generation
- Audit trail for rule changes
- Performance optimization
- Observability integration

### Tasks

#### 5.1 Multi-Tenant Support (30h)
- [ ] Tenant-scoped rule extraction
- [ ] Namespace isolation per tenant
- [ ] Quota enforcement during generation
- [ ] Merge rules from multiple tenants

**Use case**: SaaS platform with custom rules per customer

```bash
# Extract rules for specific tenant
muonroi-rulegen extract \
  --source-dir src/Handlers \
  --output rules/tenant-acme \
  --tenant acme-corp \
  --namespace Acme.Rules
```

#### 5.2 Audit Trail (25h)
Track who generated/merged rules and when:

```csharp
// Generated rule with audit metadata
/// <summary>
/// Generated from OrderHandler.ValidateAge
/// Created: 2026-02-28 14:30:00 UTC
/// Author: developer@company.com
/// Merged from runtime: true (source: qa-env/v3.json)
/// </summary>
public class VAL_001Rule : IRule<OrderContext>
{
    // ...
}
```

- [ ] Git commit hash tracking
- [ ] Author attribution from git config
- [ ] Merge history in XML docs
- [ ] Change log generation

#### 5.3 Performance Optimization (25h)
- [ ] Incremental compilation (cache syntax trees)
- [ ] Parallel processing for large codebases
- [ ] Benchmark: Extract 1000 rules in <10 seconds
- [ ] Memory optimization for large files

#### 5.4 Observability (20h)
- [ ] Structured logging (Serilog)
- [ ] Metrics: rules extracted, merge conflicts, generation time
- [ ] Health checks for watch mode
- [ ] OpenTelemetry integration

**Deliverables**:
- ✅ Multi-tenant capable
- ✅ Full audit trail
- ✅ Production-grade performance
- ✅ Observable and debuggable

---

## Timeline & Effort Summary

| Phase | Duration | Effort | Key Deliverable |
|-------|----------|--------|-----------------|
| Phase 1: Roslyn Foundation | 3 weeks | 120h | Full method body extraction |
| Phase 2: Multi-File & DI | 3 weeks | 100h | DI-aware rules + tests |
| Phase 3: Developer Experience | 3 weeks | 80h | Watch mode + IDE extension |
| **Phase 4: Merge/Split** | **3 weeks** | **140h** | **Runtime-to-code workflow** |
| Phase 5: Enterprise Features | 4 weeks | 100h | Multi-tenant + audit |
| **Total** | **16 weeks** | **540h** | **Production-ready toolkit** |

---

## Success Metrics

### Technical Metrics
- [ ] 95%+ code coverage on core extraction logic
- [ ] Extract 1000 rules in <10 seconds
- [ ] Zero manual edits needed for generated code
- [ ] 100% round-trip fidelity (code → split → merge → identical code)

### User Adoption Metrics
- [ ] 80% of developers use watch mode daily
- [ ] 90% of runtime rules successfully merged without conflicts
- [ ] 50% reduction in time to productionize BA/QC rules

### Quality Metrics
- [ ] Zero security vulnerabilities in generated code
- [ ] 100% of generated code passes SonarQube quality gate
- [ ] <1% regression rate after merge

---

## Risk Mitigation

### Phase 4 Specific Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Complex C# cannot translate to FEEL | High | High | Document limitations, provide custom escape hatch |
| Merge conflicts in production code | Critical | Medium | Interactive merge mode, 3-way diff preview |
| Runtime rules lack proper validation | High | Medium | Add pre-merge linting, FEEL validator |
| Partial class breaks build | Medium | Low | Template validation, integration tests |
| Loss of developer customizations | Critical | Low | Git safety checks, backup before merge |

---

## Market Comparison (Updated)

### New Competitive Advantage

| Feature | Muonroi RuleGen V2 | Drools Workbench | NRules | Microsoft RulesEngine |
|---------|-------------------|------------------|--------|----------------------|
| **Runtime-to-Code Merge** | ✅ Full support | ⚠️ Manual export | ❌ No | ❌ No |
| **Code-to-Runtime Split** | ✅ Full support | ⚠️ Limited | ❌ No | ❌ No |
| **BA/QC Workflow** | ✅ Integrated | ✅ Yes (Workbench) | ❌ Dev-only | ❌ Dev-only |
| **Partial Class Support** | ✅ Yes | N/A | N/A | N/A |
| **FEEL ↔ C# Translation** | ✅ Bidirectional | ⚠️ DRL only | ❌ No | ❌ JSON only |
| **Multi-tenant Generation** | ✅ Yes | ❌ No | ❌ No | ❌ No |

**Positioning**: Only .NET rule engine with **bidirectional code-runtime workflow** and **citizen developer integration**.

---

## Implementation Checklist

### Phase 4 Detailed Checklist

**Merge Command**:
- [ ] Parse FileRuleSetStore JSON format
- [ ] Convert FEEL expressions to C# AST
- [ ] Detect target class using Roslyn
- [ ] Implement append merge strategy
- [ ] Implement replace merge strategy
- [ ] Implement interactive merge (CLI prompts)
- [ ] Generate partial class for merged rules
- [ ] Add auto-generated header comments
- [ ] Preserve existing method comments
- [ ] Handle method parameter mapping
- [ ] Validate generated code compiles
- [ ] Add rollback on compilation failure
- [ ] Integration tests (20+ scenarios)

**Split Command**:
- [ ] Scan class for [MExtractAsRule] methods
- [ ] Extract method body via Roslyn
- [ ] Generate individual rule files
- [ ] Export to FileRuleSetStore JSON
- [ ] Translate C# conditionals to FEEL
- [ ] Handle unsupported C# constructs
- [ ] Preserve metadata (Order, DependsOn)
- [ ] Support multi-tenant export
- [ ] Add validation before export
- [ ] Integration tests (15+ scenarios)

**Workflow**:
- [ ] Document BA/QC → Developer handoff
- [ ] Create VS Code commands
- [ ] Add CI/CD examples
- [ ] Write comprehensive user guide
- [ ] Record demo video

---

## Appendix: Example Workflows

### Workflow 1: Runtime Rule to Production Code

**Step 1**: BA creates rule in Decision Table UI
```json
// Saved to FileRuleSetStore: qa-env/order-validation/v5.json
{
  "code": "ORD-DISC-001",
  "condition": "order.total > 500 and customer.tier = 'Premium'",
  "action": "facts['discountRate'] = 0.20"
}
```

**Step 2**: Developer merges to code
```bash
muonroi-rulegen merge \
  --rules-json qa-env/order-validation/v5.json \
  --target src/Handlers/OrderHandler.cs \
  --strategy interactive \
  --partial-class true
```

**Step 3**: Generated code
```csharp
// OrderHandler.Generated.cs
[MExtractAsRule("ORD-DISC-001", Order = 100)]
public Task ApplyPremiumDiscount(OrderContext ctx, FactBag facts)
{
    if (ctx.Order.Total > 500 && ctx.Customer.Tier == "Premium")
    {
        facts["discountRate"] = 0.20;
        return Task.FromResult(RuleResult.Passed());
    }
    return Task.FromResult(RuleResult.Failure("Premium discount not applicable"));
}
```

**Step 4**: Developer customizes
```csharp
// OrderHandler.cs
public partial class OrderHandler
{
    // Add business logic, error handling, logging
    private async Task LogDiscountApplication(OrderContext ctx, decimal rate)
    {
        await _auditService.LogAsync($"Applied {rate * 100}% discount to order {ctx.OrderId}");
    }
}
```

**Step 5**: Extract for deployment
```bash
muonroi-rulegen extract --source-dir src/Handlers --output rules/
```

### Workflow 2: Code to Runtime for Testing

**Step 1**: Developer writes handler
```csharp
[MExtractAsRule("INV-CHECK", Order = 10)]
public async Task CheckInventory(OrderContext ctx, FactBag facts)
{
    var stock = await _repo.GetStockAsync(ctx.ProductId);
    if (stock < ctx.Quantity)
        return RuleResult.Failure($"Only {stock} available");
    facts["inventoryReserved"] = true;
    return RuleResult.Passed();
}
```

**Step 2**: Split to runtime format
```bash
muonroi-rulegen split \
  --source OrderHandler.cs \
  --export-json staging/inventory-rules.json
```

**Step 3**: QA tests in staging environment
```json
// staging/inventory-rules.json loaded into FileRuleSetStore
{
  "code": "INV-CHECK",
  "condition": "product.stock >= order.quantity",
  "action": "facts['inventoryReserved'] = true"
}
```

**Step 4**: QA modifies threshold, developer re-merges changes

---

## Next Steps

1. **Immediate**: Implement Phase 1 (Roslyn) to fix TODO blocker
2. **Short-term**: Complete Phases 2-3 for developer productivity
3. **Critical**: Implement Phase 4 (merge/split) for BA/QC integration
4. **Long-term**: Add Phase 5 enterprise features for production scale

**Recommended Priority**: Phase 4 should be prioritized after Phase 1, as it delivers the most unique value proposition.
