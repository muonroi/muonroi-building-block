  📊 PHÂN TÍCH & UPGRADE PLAN: MUONROI.RULEGEN

  TRẠNG THÁI HIỆN TẠI

  ┌─────────────────────────────────────────────────────────────┐
  │           MUONROI.RULEGEN - CURRENT CAPABILITIES            │
  ├─────────────────────────────────────────────────────────────┤
  │  Regex-based parsing             [████░░░░░░░░]  30%        │
  │  Code generation (scaffold)      [██████░░░░░░]  50%        │
  │  Method body extraction          [░░░░░░░░░░░░]   0% (TODO) │
  │  Multi-file processing           [░░░░░░░░░░░░]   0%        │
  │  Validation & safety             [██░░░░░░░░░░]  15%        │
  │  Developer experience            [███░░░░░░░░░]  25%        │
  │  Documentation                   [██░░░░░░░░░░]  15%        │
  ├─────────────────────────────────────────────────────────────┤
  │  OVERALL MATURITY                [███░░░░░░░░░]  25%        │
  └─────────────────────────────────────────────────────────────┘

  Điểm mạnh:
  - ✅ Attribute system clean ([MExtractAsRule(...)])
  - ✅ 3 commands rõ ràng: extract, verify, register
  - ✅ Scaffold generation works

  Điểm yếu:
  - ❌ CRITICAL: TODO trong EvaluateAsync - không extract method body
  - ❌ Regex-based (fragile, không production-grade)
  - ❌ Single file processing only
  - ❌ No validation (circular deps, duplicates)
  - ❌ Minimal testing (3 tests)
  - ❌ No documentation/examples

  ---
  🏆 SO SÁNH VỚI MARKET COMPETITORS

  1. Drools Rule Language (DRL) + Compiler

  Drools Ecosystem:
  - drools-compiler - Compiles DRL → Java bytecode
  - kie-maven-plugin - Maven integration cho code gen
  - drools-wb (Workbench) - Visual rule authoring

  Capabilities:
  ┌─────────────────┬─────────────────────────┬─────────────────┬───────────────────┐
  │     Feature     │         Drools          │ Muonroi.RuleGen │        Gap        │
  ├─────────────────┼─────────────────────────┼─────────────────┼───────────────────┤
  │ Parsing         │ ANTLR-based grammar     │ ❌ Regex        │ 🔴 Critical       │
  ├─────────────────┼─────────────────────────┼─────────────────┼───────────────────┤
  │ Code Generation │ Full Java bytecode      │ ✅ C# scaffold  │ 🟡 Partial (TODO) │
  ├─────────────────┼─────────────────────────┼─────────────────┼───────────────────┤
  │ Multi-file      │ ✅ Full project scan    │ ❌ Single file  │ 🔴 Critical       │
  ├─────────────────┼─────────────────────────┼─────────────────┼───────────────────┤
  │ Validation      │ ✅ Compile-time         │ ❌ None         │ 🔴 Critical       │
  ├─────────────────┼─────────────────────────┼─────────────────┼───────────────────┤
  │ IDE Support     │ ✅ Eclipse plugin       │ ❌ None         │ 🟡 Medium         │
  ├─────────────────┼─────────────────────────┼─────────────────┼───────────────────┤
  │ Testing Tools   │ ✅ Decision tables test │ ❌ None         │ 🟡 Medium         │
  ├─────────────────┼─────────────────────────┼─────────────────┼───────────────────┤
  │ DSL Support     │ ✅ Custom DSL           │ ❌ None         │ 🟢 Low priority   │
  └─────────────────┴─────────────────────────┴─────────────────┴───────────────────┘
  Example Drools DRL:
  rule "Validate Order Price"
  when
      $order : Order( price > 1000 )
  then
      $order.setRequiresApproval(true);
      update($order);
  end

  Drools Code Generation:
  # Maven plugin auto-generates Java classes
  mvn clean compile
  # → generates: target/generated-sources/drools/*.java

  What Drools Does Better:
  1. Full method body extraction - Drools DRL rules are compiled to bytecode
  2. Type safety - Compile-time validation of field access
  3. IDE integration - Syntax highlighting, autocomplete
  4. Performance optimization - Rete algorithm compiled

  Muonroi.RuleGen Advantage:
  - ✅ Code-first (no new DSL to learn)
  - ✅ C# native (Drools is Java-only)
  - ✅ Attribute-based (familiar to .NET devs)

  ---
  2. NRules Fluent DSL + Source Generator

  NRules Approach:
  // NRules fluent DSL
  public class PreferredCustomerDiscountRule : Rule
  {
      public override void Define()
      {
          Customer customer = null;
          Order order = null;

          When()
              .Match<Customer>(() => customer, c => c.IsPreferred)
              .Match<Order>(() => order, o => o.Customer == customer);

          Then()
              .Do(ctx => ApplyDiscount(order, 10));
      }
  }

  Comparison:
  ┌───────────────────┬─────────────────────┬────────────────────┬────────────────────┐
  │      Feature      │       NRules        │  Muonroi.RuleGen   │        Gap         │
  ├───────────────────┼─────────────────────┼────────────────────┼────────────────────┤
  │ Approach          │ Fluent DSL          │ Attribute-based    │ Different paradigm │
  ├───────────────────┼─────────────────────┼────────────────────┼────────────────────┤
  │ Type Safety       │ ✅ Compile-time     │ ✅ Compile-time    │ ⚖️ Equal           │
  ├───────────────────┼─────────────────────┼────────────────────┼────────────────────┤
  │ Code Gen          │ ❌ Manual           │ ✅ Scaffold        │ 🟢 Advantage       │
  ├───────────────────┼─────────────────────┼────────────────────┼────────────────────┤
  │ Runtime Discovery │ ✅ Reflection scan  │ ✅ DI registration │ ⚖️ Equal           │
  ├───────────────────┼─────────────────────┼────────────────────┼────────────────────┤
  │ Fact Binding      │ ✅ Pattern matching │ ⚠️ Manual          │ 🟡 Medium          │
  ├───────────────────┼─────────────────────┼────────────────────┼────────────────────┤
  │ Business Logic    │ ✅ Inline lambdas   │ ❌ TODO            │ 🔴 Critical        │
  └───────────────────┴─────────────────────┴────────────────────┴────────────────────┘
  What NRules Does Better:
  1. Pattern matching - Declarative when/then
  2. Fact inference - Auto-binds objects from working memory
  3. Conflict resolution - Built-in salience/agenda

  Muonroi.RuleGen Advantage:
  - ✅ Less boilerplate (no fluent DSL)
  - ✅ Familiar attribute syntax
  - ✅ Hook points integration

  ---
  3. Easy Rules Annotation Processor

  Easy Rules (Java):
  @Rule(name = "weather rule", description = "if it rains then take an umbrella")
  public class WeatherRule {
      @Condition
      public boolean itRains(@Fact("rain") boolean rain) {
          return rain;
      }

      @Action
      public void takeAnUmbrella() {
          System.out.println("It rains, take an umbrella!");
      }
  }

  Comparison:
  ┌──────────────────┬─────────────────────┬──────────────────┬─────────────┐
  │     Feature      │     Easy Rules      │ Muonroi.RuleGen  │     Gap     │
  ├──────────────────┼─────────────────────┼──────────────────┼─────────────┤
  │ Annotation-based │ ✅ Java annotations │ ✅ C# attributes │ ⚖️ Equal    │
  ├──────────────────┼─────────────────────┼──────────────────┼─────────────┤
  │ Method as Rule   │ ✅ Direct use       │ ❌ Scaffold gen  │ 🔴 Critical │
  ├──────────────────┼─────────────────────┼──────────────────┼─────────────┤
  │ Fact Injection   │ ✅ @Fact params     │ ❌ Manual        │ 🟡 Medium   │
  ├──────────────────┼─────────────────────┼──────────────────┼─────────────┤
  │ Composite Rules  │ ✅ @And/@Or/@Not    │ ❌ None          │ 🟡 Medium   │
  ├──────────────────┼─────────────────────┼──────────────────┼─────────────┤
  │ MVEL Expressions │ ✅ Supported        │ ❌ None          │ 🟢 Low      │
  └──────────────────┴─────────────────────┴──────────────────┴─────────────┘
  What Easy Rules Does Better:
  1. No code generation - Annotations are runtime-processed
  2. Method IS the rule - No separate rule class needed
  3. Fact injection - Automatic parameter binding

  Muonroi.RuleGen Fundamental Difference:
  - Easy Rules: Annotated method IS the rule (runtime reflection)
  - Muonroi.RuleGen: Annotated method → GENERATES rule class (compile-time)

  ---
  4. Microsoft RulesEngine (JSON-based)

  RulesEngine Workflow:
  {
    "WorkflowName": "Discount",
    "Rules": [
      {
        "RuleName": "GiveDiscount10",
        "Expression": "input1.country == \"india\" AND input1.loyaltyFactor <= 2",
        "Actions": {
          "OnSuccess": {
            "Name": "OutputExpression",
            "Context": { "Result": "input1.TotalAmount * 0.9" }
          }
        }
      }
    ]
  }

  Comparison:
  ┌─────────────────────┬───────────────────┬─────────────────┬────────────────────┐
  │       Feature       │    RulesEngine    │ Muonroi.RuleGen │        Gap         │
  ├─────────────────────┼───────────────────┼─────────────────┼────────────────────┤
  │ Data-driven         │ ✅ JSON workflows │ ❌ Code-driven  │ Different paradigm │
  ├─────────────────────┼───────────────────┼─────────────────┼────────────────────┤
  │ Non-dev editing     │ ✅ Business users │ ❌ Devs only    │ 🟡 Medium          │
  ├─────────────────────┼───────────────────┼─────────────────┼────────────────────┤
  │ Versioning          │ ✅ JSON files     │ ⚠️ Code commits │ 🟡 Medium          │
  ├─────────────────────┼───────────────────┼─────────────────┼────────────────────┤
  │ Expression Language │ ✅ C# expressions │ ❌ None         │ 🟡 Medium          │
  ├─────────────────────┼───────────────────┼─────────────────┼────────────────────┤
  │ Code Generation     │ ❌ None           │ ✅ Scaffold     │ 🟢 Advantage       │
  └─────────────────────┴───────────────────┴─────────────────┴────────────────────┘
  What RulesEngine Does Better:
  1. Business user friendly - JSON editing
  2. Hot reload - Change rules without recompile
  3. Expression evaluation - Built-in C# expression parser

  Muonroi.RuleGen Advantage:
  - ✅ Type-safe at compile time
  - ✅ IDE support (IntelliSense)
  - ✅ Refactoring-friendly

  ---
  🏢 ENTERPRISE USE CASES & REQUIREMENTS

  Case 1: E-Commerce Order Validation

  Requirement:
  // Business logic method
  public async Task<ValidationResult> ValidateOrder(Order order)
  {
      if (order.TotalAmount > 10000 && !order.Customer.IsVerified)
          return ValidationResult.Fail("Large orders require verified customer");

      if (order.Items.Any(i => i.Quantity > 100))
          return ValidationResult.Fail("Bulk orders require approval");

      if (order.ShippingAddress.Country != order.BillingAddress.Country)
          return ValidationResult.Fail("Cross-border orders require customs declaration");

      return ValidationResult.Pass();
  }

  What Tool Needs:
  1. Extract method body logic into rule conditions
  2. Generate separate rules for each validation
  3. Preserve order of execution
  4. Support async/await patterns
  5. Map return types (ValidationResult → RuleResult)

  Current Muonroi.RuleGen Output:
  public sealed class ValidateOrderRule : IRule<Order>
  {
      public async Task<RuleResult> EvaluateAsync(Order ctx, FactBag facts, CancellationToken ct)
      {
          // TODO: map method body from ValidateOrder to generated evaluation logic. ❌
          return RuleResult.Passed();
      }
  }

  Gap: ❌ Cannot extract method body → Developer must manually copy logic

  ---
  Case 2: Multi-Tenant Pricing Rules

  Requirement:
  // Tenant-specific pricing
  [MExtractAsRule("PRICING_TIER_1", Order = 10)]
  [TenantScope("tenant-a")]
  public decimal CalculatePrice(Product product, int quantity)
  {
      if (quantity >= 100) return product.BasePrice * 0.8m; // 20% discount
      if (quantity >= 50) return product.BasePrice * 0.9m;  // 10% discount
      return product.BasePrice;
  }

  [MExtractAsRule("PRICING_TIER_2", Order = 10)]
  [TenantScope("tenant-b")]
  public decimal CalculatePrice(Product product, int quantity)
  {
      if (quantity >= 200) return product.BasePrice * 0.75m; // 25% discount
      return product.BasePrice;
  }

  What Tool Needs:
  1. Handle duplicate method names (same signature, different tenants)
  2. Generate unique rule codes per tenant
  3. Preserve tenant metadata
  4. Extract if/else logic into separate rule instances

  Current Gap: ❌ Cannot handle duplicate method names → Name collision

  ---
  Case 3: Financial Compliance Rules

  Requirement:
  // SOX compliance rule
  [MExtractAsRule("SOX_SEGREGATION_OF_DUTIES")]
  [AuditRequired]
  [ComplianceLevel("Critical")]
  public async Task<ComplianceResult> CheckSegregationOfDuties(Transaction tx)
  {
      var initiator = await _userService.GetUserAsync(tx.InitiatedBy);
      var approver = await _userService.GetUserAsync(tx.ApprovedBy);

      if (initiator.Department == approver.Department)
          return ComplianceResult.Violation("Same department approval not allowed");

      if (tx.Amount > 50000 && approver.Level < ManagerLevel.Director)
          return ComplianceResult.Violation("High-value transactions require director approval");

      return ComplianceResult.Compliant();
  }

  What Tool Needs:
  1. Preserve custom attributes ([AuditRequired], [ComplianceLevel])
  2. Extract async service calls
  3. Generate dependency injection for _userService
  4. Map return types (ComplianceResult → RuleResult)
  5. Generate audit logging boilerplate

  Current Gap: ❌ No DI extraction → Cannot generate service dependencies

  ---
  Case 4: Workflow State Transitions

  Requirement:
  // State machine rule
  [MExtractAsRule("APPROVE_PURCHASE_REQUEST", HookPoint = HookPoint.BeforeUpdate)]
  public bool CanTransition(PurchaseRequest request, WorkflowState targetState)
  {
      return (request.Status, targetState) switch
      {
          (Status.Draft, Status.PendingApproval) => request.TotalAmount > 0,
          (Status.PendingApproval, Status.Approved) => request.ApprovedBy != null,
          (Status.Approved, Status.Completed) => request.Items.All(i => i.IsDelivered),
          _ => false
      };
  }

  What Tool Needs:
  1. Parse pattern matching expressions
  2. Extract switch expressions (C# 8+)
  3. Handle tuple deconstruction
  4. Generate equivalent if/else or match expression

  Current Gap: ❌ Regex cannot parse modern C# syntax → Pattern matching fails

  ---
  🚀 UPGRADE ROADMAP

  PHASE 1: Foundation (4 weeks) - Roslyn Integration

  Week 1-2: Replace Regex with Roslyn

  Goal: Proper C# syntax tree analysis

  Implementation:
  // tools/Muonroi.RuleGen/Analyzers/RoslynRuleExtractor.cs
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  public class RoslynRuleExtractor
  {
      public async Task<List<ExtractedRuleDefinition>> ExtractRulesAsync(string sourceFilePath)
      {
          var code = await File.ReadAllTextAsync(sourceFilePath);
          var tree = CSharpSyntaxTree.ParseText(code);
          var root = await tree.GetRootAsync();

          var rules = new List<ExtractedRuleDefinition>();

          // Find all methods with ExtractAsRule attribute
          var methods = root.DescendantNodes()
              .OfType<MethodDeclarationSyntax>()
              .Where(m => HasExtractAsRuleAttribute(m));

          foreach (var method in methods)
          {
              var rule = await ExtractRuleDefinitionAsync(method, tree);
              rules.Add(rule);
          }

          return rules;
      }

      private bool HasExtractAsRuleAttribute(MethodDeclarationSyntax method)
      {
          return method.AttributeLists
              .SelectMany(al => al.Attributes)
              .Any(attr => attr.Name.ToString().Contains("ExtractAsRule"));
      }

      private async Task<ExtractedRuleDefinition> ExtractRuleDefinitionAsync(
          MethodDeclarationSyntax method,
          SyntaxTree tree)
      {
          // Extract attribute arguments
          var attr = method.AttributeLists
              .SelectMany(al => al.Attributes)
              .First(a => a.Name.ToString().Contains("ExtractAsRule"));

          var code = attr.ArgumentList.Arguments[0].Expression.ToString().Trim('"');
          var order = GetAttributeProperty(attr, "Order") ?? 0;
          var hookPoint = GetAttributeProperty(attr, "HookPoint") ?? "BeforeRule";
          var dependsOn = GetAttributeArray(attr, "DependsOn") ?? Array.Empty<string>();

          // Extract method signature
          var returnType = method.ReturnType.ToString();
          var parameters = method.ParameterList.Parameters
              .Select(p => new ParameterInfo(p.Type.ToString(), p.Identifier.Text))
              .ToList();

          // Extract method body (NEW!)
          var body = await ExtractMethodBodyAsync(method);

          return new ExtractedRuleDefinition(
              Code: code,
              MethodName: method.Identifier.Text,
              Order: order,
              HookPoint: hookPoint,
              DependsOn: dependsOn,
              ReturnType: returnType,
              Parameters: parameters,
              MethodBody: body // ✅ NEW
          );
      }

      private async Task<MethodBodyInfo> ExtractMethodBodyAsync(MethodDeclarationSyntax method)
      {
          var body = method.Body ?? method.ExpressionBody?.Expression;
          if (body == null) return MethodBodyInfo.Empty;

          return new MethodBodyInfo(
              Statements: ExtractStatements(body),
              LocalVariables: ExtractLocalVariables(body),
              ServiceCalls: ExtractServiceCalls(body),
              ReturnStatements: ExtractReturnStatements(body)
          );
      }
  }

  Benefits:
  - ✅ Handles all C# syntax (switch expressions, pattern matching, etc.)
  - ✅ Type information available via semantic model
  - ✅ Robust to whitespace/formatting changes
  - ✅ Can extract dependencies, parameters, return types

  ---
  Week 3-4: Method Body Translation

  Goal: Translate C# method body → Rule evaluation logic

  Strategy 1: Statement-by-Statement Translation
  // Original method
  public ValidationResult ValidateOrder(Order order)
  {
      if (order.TotalAmount > 10000 && !order.Customer.IsVerified)
          return ValidationResult.Fail("Large orders require verified customer");

      return ValidationResult.Pass();
  }

  // Generated rule (BEFORE - current)
  public async Task<RuleResult> EvaluateAsync(Order ctx, FactBag facts, CancellationToken ct)
  {
      // TODO: map method body ❌
      return RuleResult.Passed();
  }

  // Generated rule (AFTER - with extraction)
  public async Task<RuleResult> EvaluateAsync(Order ctx, FactBag facts, CancellationToken ct)
  {
      // Translated from ValidateOrder method body
      if (ctx.TotalAmount > 10000 && !ctx.Customer.IsVerified)
      {
          return RuleResult.Failure("Large orders require verified customer");
      }

      return RuleResult.Passed();
  }

  Implementation:
  public class MethodBodyTranslator
  {
      public string TranslateToRuleBody(MethodBodyInfo body, ParameterInfo contextParam)
      {
          var statements = new List<string>();

          foreach (var stmt in body.Statements)
          {
              var translated = stmt switch
              {
                  IfStatementSyntax ifStmt => TranslateIfStatement(ifStmt, contextParam),
                  ReturnStatementSyntax retStmt => TranslateReturnStatement(retStmt),
                  LocalDeclarationStatementSyntax localDecl => TranslateLocalDeclaration(localDecl),
                  ExpressionStatementSyntax exprStmt => TranslateExpressionStatement(exprStmt),
                  _ => $"// TODO: Translate {stmt.Kind()}"
              };

              statements.Add(translated);
          }

          return string.Join("\n    ", statements);
      }

      private string TranslateIfStatement(IfStatementSyntax ifStmt, ParameterInfo contextParam)
      {
          // Replace first parameter with 'ctx'
          var condition = ifStmt.Condition.ToString()
              .Replace(contextParam.Name, "ctx");

          var thenBlock = TranslateBlock(ifStmt.Statement);
          var elseBlock = ifStmt.Else != null
              ? $"else\n    {TranslateBlock(ifStmt.Else.Statement)}"
              : "";

          return $"if ({condition})\n    {thenBlock}\n    {elseBlock}";
      }

      private string TranslateReturnStatement(ReturnStatementSyntax retStmt)
      {
          var expr = retStmt.Expression.ToString();

          // Map return type transformations
          if (expr.Contains("ValidationResult.Fail"))
          {
              var message = ExtractStringLiteral(expr);
              return $"return RuleResult.Failure({message});";
          }
          else if (expr.Contains("ValidationResult.Pass"))
          {
              return "return RuleResult.Passed();";
          }

          return $"return RuleResult.Passed(); // TODO: Map {expr}";
      }
  }

  Strategy 2: Pattern-Based Templates
  // Detect common patterns and use templates
  public class PatternBasedTranslator
  {
      public string Translate(MethodBodyInfo body)
      {
          // Pattern 1: Single if-return validation
          if (IsSingleIfReturnPattern(body))
          {
              return GenerateSingleIfReturnRule(body);
          }

          // Pattern 2: Multiple if-return validations
          if (IsMultipleIfReturnPattern(body))
          {
              return GenerateMultipleIfReturnRule(body);
          }

          // Pattern 3: Switch expression
          if (IsSwitchExpressionPattern(body))
          {
              return GenerateSwitchExpressionRule(body);
          }

          // Fallback: Statement-by-statement
          return FallbackTranslation(body);
      }

      private string GenerateSingleIfReturnRule(MethodBodyInfo body)
      {
          var ifStmt = body.Statements.OfType<IfStatementSyntax>().First();
          var condition = ifStmt.Condition.ToString();
          var errorMessage = ExtractErrorMessage(ifStmt.Statement);

          return $$"""
          if ({{condition}})
          {
              return RuleResult.Failure("{{errorMessage}}");
          }
          return RuleResult.Passed();
          """;
      }
  }

  Benefits:
  - ✅ Removes TODO - Actual business logic extracted
  - ✅ Supports simple → complex patterns
  - ✅ Fallback for unknown patterns
  - ✅ Incremental improvement (start with simple patterns)

  ---
  PHASE 2: Advanced Features (4 weeks)

  Week 1: Multi-File Processing

  Goal: Process entire projects, not just single files

  Implementation:
  // tools/Muonroi.RuleGen/Commands/ExtractCommand.cs (NEW)
  public class ExtractCommand
  {
      public async Task ExecuteAsync(ExtractOptions options)
      {
          var sourceFiles = options.Source.EndsWith(".cs")
              ? new[] { options.Source }
              : Directory.GetFiles(options.Source, "*.cs", SearchOption.AllDirectories);

          var allRules = new List<ExtractedRuleDefinition>();

          foreach (var file in sourceFiles)
          {
              var extractor = new RoslynRuleExtractor();
              var rules = await extractor.ExtractRulesAsync(file);
              allRules.AddRange(rules);
          }

          // Validate uniqueness
          var duplicates = allRules
              .GroupBy(r => r.Code)
              .Where(g => g.Count() > 1)
              .ToList();

          if (duplicates.Any())
          {
              Console.Error.WriteLine("ERROR: Duplicate rule codes found:");
              foreach (var dup in duplicates)
              {
                  Console.Error.WriteLine($"  - {dup.Key} ({dup.Count()} occurrences)");
              }
              return 1;
          }

          // Generate rules
          foreach (var rule in allRules)
          {
              await GenerateRuleFileAsync(rule, options.OutputDir, options.Namespace);
          }

          return 0;
      }
  }

  New CLI Options:
  # Process single file (existing)
  muonroi-rule extract --source MyRules.cs --output ./Generated

  # Process directory (NEW)
  muonroi-rule extract --source ./src/Business --output ./Generated

  # Process project file (NEW)
  muonroi-rule extract --project MyProject.csproj --output ./Generated

  ---
  Week 2: Dependency Injection Extraction

  Goal: Auto-detect services and generate constructor injection

  Implementation:
  public class DependencyExtractor
  {
      public List<ServiceDependency> ExtractDependencies(MethodBodyInfo body)
      {
          var dependencies = new List<ServiceDependency>();

          // Find field references (e.g., _userService, _orderRepo)
          var fieldAccesses = body.ServiceCalls
              .Select(sc => sc.Target)
              .Where(t => t.StartsWith("_"))
              .Distinct();

          foreach (var field in fieldAccesses)
          {
              var serviceType = InferServiceType(field); // e.g., _userService → IUserService
              dependencies.Add(new ServiceDependency(serviceType, field));
          }

          return dependencies;
      }

      private string InferServiceType(string fieldName)
      {
          // _userService → IUserService
          var name = fieldName.TrimStart('_');
          var pascalCase = char.ToUpper(name[0]) + name.Substring(1);
          return $"I{pascalCase}";
      }
  }

  // Generated rule with DI
  public sealed class CheckSegregationOfDutiesRule : IRule<Transaction>
  {
      private readonly IUserService _userService; // ✅ Injected

      public CheckSegregationOfDutiesRule(IUserService userService)
      {
          _userService = userService;
      }

      public async Task<RuleResult> EvaluateAsync(Transaction ctx, FactBag facts, CancellationToken ct)
      {
          var initiator = await _userService.GetUserAsync(ctx.InitiatedBy);
          var approver = await _userService.GetUserAsync(ctx.ApprovedBy);

          if (initiator.Department == approver.Department)
              return RuleResult.Failure("Same department approval not allowed");

          return RuleResult.Passed();
      }
  }

  ---
  Week 3: Validation & Safety

  Goal: Detect errors before generation

  Implementation:
  public class RuleValidator
  {
      public ValidationReport Validate(List<ExtractedRuleDefinition> rules)
      {
          var report = new ValidationReport();

          // 1. Check duplicate codes
          var duplicates = rules.GroupBy(r => r.Code).Where(g => g.Count() > 1);
          foreach (var dup in duplicates)
          {
              report.AddError($"Duplicate rule code: {dup.Key}");
          }

          // 2. Check circular dependencies
          var graph = BuildDependencyGraph(rules);
          var cycles = DetectCycles(graph);
          foreach (var cycle in cycles)
          {
              report.AddError($"Circular dependency: {string.Join(" -> ", cycle)}");
          }

          // 3. Check missing dependencies
          var allCodes = rules.Select(r => r.Code).ToHashSet();
          foreach (var rule in rules)
          {
              foreach (var dep in rule.DependsOn)
              {
                  if (!allCodes.Contains(dep))
                  {
                      report.AddWarning($"Rule '{rule.Code}' depends on missing '{dep}'");
                  }
              }
          }

          // 4. Check hook point validity
          foreach (var rule in rules)
          {
              if (!Enum.TryParse<HookPoint>(rule.HookPoint, out _))
              {
                  report.AddError($"Invalid HookPoint '{rule.HookPoint}' in rule '{rule.Code}'");
              }
          }

          return report;
      }
  }

  Output:
  muonroi-rule extract --source ./src --output ./Generated --validate

  Validating extracted rules...
  ✓ Found 15 rules
  ✗ ERROR: Duplicate rule code: ORDER_VALIDATE (2 occurrences)
    - src/Orders/OrderRules.cs:42
    - src/Legacy/OldOrderRules.cs:18
  ⚠ WARNING: Rule 'SHIPPING_VALIDATE' depends on missing 'ADDRESS_VALIDATE'
  ✗ ERROR: Circular dependency: RULE_A -> RULE_B -> RULE_C -> RULE_A

  Validation failed with 2 errors, 1 warning.

  ---
  Week 4: Test Generation

  Goal: Auto-generate test scaffolds for extracted rules

  Implementation:
  public class TestGenerator
  {
      public string GenerateTest(ExtractedRuleDefinition rule)
      {
          return $$"""
          using Xunit;
          using Muonroi.RuleEngine.Abstractions;

          namespace {{rule.Namespace}}.Tests;

          public class {{rule.Code}}RuleTests
          {
              private readonly {{rule.Code}}Rule _rule;

              public {{rule.Code}}RuleTests()
              {
                  _rule = new {{rule.Code}}Rule();
              }

              [Fact]
              public async Task EvaluateAsync_WhenConditionMet_ReturnsPassed()
              {
                  // Arrange
                  var context = new {{rule.ContextType}}
                  {
                      // TODO: Setup test context
                  };
                  var facts = new FactBag();

                  // Act
                  var result = await _rule.EvaluateAsync(context, facts, CancellationToken.None);

                  // Assert
                  Assert.True(result.IsSuccess);
              }

              [Fact]
              public async Task EvaluateAsync_WhenConditionNotMet_ReturnsFailure()
              {
                  // Arrange
                  var context = new {{rule.ContextType}}
                  {
                      // TODO: Setup failing context
                  };
                  var facts = new FactBag();

                  // Act
                  var result = await _rule.EvaluateAsync(context, facts, CancellationToken.None);

                  // Assert
                  Assert.False(result.IsSuccess);
                  Assert.NotEmpty(result.Errors);
              }
          }
          """;
      }
  }

  New Command:
  muonroi-rule generate-tests --rules ./Generated --output ./Generated.Tests

  ---
  PHASE 3: Developer Experience (3 weeks)

  Week 1: Configuration File Support

  .rulegen.json:
  {
    "$schema": "https://muonroi.com/schemas/rulegen-config.schema.json",
    "version": "1.0",
    "source": {
      "include": ["src/**/*.cs"],
      "exclude": ["src/Generated/**", "**/*.g.cs"]
    },
    "output": {
      "directory": "./Generated/Rules",
      "namespace": "MyProject.Rules.Generated",
      "contextType": "MyBusinessContext"
    },
    "generation": {
      "extractMethodBodies": true,
      "extractDependencies": true,
      "generateTests": true,
      "validateCircularDeps": true
    },
    "templates": {
      "ruleClass": "./templates/rule-class.cs.tmpl",
      "registration": "./templates/registration.cs.tmpl"
    }
  }

  Usage:
  # Use config file (NEW)
  muonroi-rule extract --config .rulegen.json

  # Override config
  muonroi-rule extract --config .rulegen.json --namespace CustomNamespace

  ---
  Week 2: Watch Mode

  Goal: Auto-regenerate on file changes

  Implementation:
  public class WatchCommand
  {
      public async Task ExecuteAsync(WatchOptions options)
      {
          var watcher = new FileSystemWatcher(options.SourceDir, "*.cs");

          watcher.Changed += async (sender, e) =>
          {
              Console.WriteLine($"File changed: {e.FullPath}");
              await RegenerateRuleAsync(e.FullPath, options);
          };

          watcher.EnableRaisingEvents = true;

          Console.WriteLine($"Watching {options.SourceDir} for changes... (Press Ctrl+C to exit)");
          await Task.Delay(Timeout.Infinite);
      }
  }

  Usage:
  muonroi-rule watch --config .rulegen.json

  ---
  Week 3: IDE Integration (VS Code Extension)

  Extension Features:
  1. Syntax Highlighting - Highlight [ExtractAsRule] attributes
  2. Code Actions - Right-click → "Generate Rule"
  3. Quick Fix - Auto-add missing dependencies
  4. Inline Errors - Show validation errors in editor

  package.json (VS Code Extension):
  {
    "name": "muonroi-rulegen",
    "displayName": "Muonroi RuleGen",
    "description": "Code-first rule generation for Muonroi Rule Engine",
    "version": "1.0.0",
    "engines": {
      "vscode": "^1.80.0"
    },
    "activationEvents": [
      "onLanguage:csharp"
    ],
    "contributes": {
      "commands": [
        {
          "command": "muonroi.generateRule",
          "title": "Muonroi: Generate Rule from Method"
        },
        {
          "command": "muonroi.extractAll",
          "title": "Muonroi: Extract All Rules"
        }
      ],
      "configuration": {
        "title": "Muonroi RuleGen",
        "properties": {
          "muonroi.rulegen.configFile": {
            "type": "string",
            "default": ".rulegen.json",
            "description": "Path to RuleGen configuration file"
          }
        }
      }
    }
  }

  ---
  PHASE 4: Enterprise Features (3 weeks)

  Week 1: Multi-Tenant Support

  Goal: Handle tenant-specific rules

  Attributes:
  [MExtractAsRule("PRICING_CALCULATION")]
  [TenantScope("tenant-a", "tenant-b")]
  public decimal CalculatePrice(Product product, int quantity)
  {
      // Pricing logic for tenants A and B
  }

  [MExtractAsRule("PRICING_CALCULATION")]
  [TenantScope("tenant-c")]
  public decimal CalculatePrice(Product product, int quantity)
  {
      // Different pricing logic for tenant C
  }

  Generated:
  public sealed class PricingCalculation_TenantA_Rule : IRule<Product>
  {
      public string Code => "PRICING_CALCULATION";
      public string TenantId => "tenant-a";
      // ...
  }

  public sealed class PricingCalculation_TenantC_Rule : IRule<Product>
  {
      public string Code => "PRICING_CALCULATION";
      public string TenantId => "tenant-c";
      // ...
  }

  ---
  Week 2: Audit & Compliance

  Attributes:
  [MExtractAsRule("SOX_APPROVAL_CHECK")]
  [AuditRequired]
  [ComplianceLevel(ComplianceLevel.Critical)]
  [RegulatoryFramework("SOX", "GDPR")]
  public async Task<bool> CheckApprovalCompliance(Transaction tx)
  {
      // Compliance logic
  }

  Generated:
  public sealed class SoxApprovalCheckRule : IRule<Transaction>
  {
      private readonly IAuditLogger _auditLogger;

      public async Task<RuleResult> EvaluateAsync(Transaction ctx, FactBag facts, CancellationToken ct)
      {
          // Auto-generated audit logging
          await _auditLogger.LogRuleExecutionAsync(new AuditEntry
          {
              RuleCode = Code,
              ComplianceLevel = ComplianceLevel.Critical,
              RegulatoryFrameworks = new[] { "SOX", "GDPR" },
              Timestamp = DateTime.UtcNow,
              Context = ctx
          });

          try
          {
              // Extracted business logic
              // ...

              return RuleResult.Passed();
          }
          catch (Exception ex)
          {
              await _auditLogger.LogRuleFailureAsync(Code, ex);
              throw;
          }
      }
  }

  ---
  Week 3: Performance Optimization

  Features:
  1. Parallel Extraction - Process multiple files concurrently
  2. Incremental Generation - Only regenerate changed rules
  3. Caching - Cache parsed syntax trees

  Implementation:
  public class IncrementalExtractor
  {
      private readonly Dictionary<string, (DateTime Modified, List<ExtractedRuleDefinition> Rules)> _cache = new();

      public async Task<List<ExtractedRuleDefinition>> ExtractAsync(List<string> sourceFiles)
      {
          var changedFiles = sourceFiles
              .Where(f => HasFileChanged(f))
              .ToList();

          var results = await Task.WhenAll(
              changedFiles.Select(f => ExtractFileAsync(f))
          );

          // Update cache
          foreach (var (file, rules) in results)
          {
              _cache[file] = (File.GetLastWriteTimeUtc(file), rules);
          }

          return _cache.Values.SelectMany(v => v.Rules).ToList();
      }

      private bool HasFileChanged(string file)
      {
          if (!_cache.TryGetValue(file, out var cached))
              return true;

          return File.GetLastWriteTimeUtc(file) > cached.Modified;
      }
  }

  ---
  📊 FINAL COMPARISON: BEFORE vs AFTER
  ┌───────────────┬──────────────────┬───────────────────────────┬─────────────┐
  │    Feature    │ Before (Current) │     After (Upgraded)      │   Impact    │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ Parsing       │ Regex            │ Roslyn AST                │ 🔴 Critical │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ Method Body   │ TODO placeholder │ ✅ Extracted              │ 🔴 Critical │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ Multi-File    │ Single file      │ Directory/Project         │ 🟡 High     │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ Validation    │ None             │ Circular deps, duplicates │ 🟡 High     │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ Dependencies  │ Not extracted    │ ✅ Auto DI                │ 🟡 High     │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ Testing       │ None             │ Auto-generated scaffolds  │ 🟢 Medium   │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ Config File   │ CLI args only    │ .rulegen.json             │ 🟢 Medium   │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ Watch Mode    │ None             │ Auto-regenerate           │ 🟢 Medium   │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ IDE Extension │ None             │ VS Code extension         │ 🟢 Low      │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ Multi-Tenant  │ None             │ Tenant-scoped rules       │ 🟢 Medium   │
  ├───────────────┼──────────────────┼───────────────────────────┼─────────────┤
  │ Audit         │ None             │ Auto audit logging        │ 🟢 Low      │
  └───────────────┴──────────────────┴───────────────────────────┴─────────────┘
  Overall Maturity:
  - Before: 25% → After: 85%

  ---
  🎯 SUMMARY & RECOMMENDATION

  Critical Gaps to Fix:

  1. 🔴 Method body extraction (PHASE 1, Week 3-4) - Removes TODO blocker
  2. 🔴 Roslyn parsing (PHASE 1, Week 1-2) - Production-grade code analysis
  3. 🟡 Multi-file processing (PHASE 2, Week 1) - Enterprise scalability

  Quick Wins:

  1. Config file support (PHASE 3, Week 1) - 2 days work, huge DX improvement
  2. Validation (PHASE 2, Week 3) - 3 days work, prevents errors early
  3. Watch mode (PHASE 3, Week 2) - 2 days work, iterative development

  Recommended Priority:

  1. PHASE 1 (4 weeks) - Foundation → Unblocks code-first vision
  2. PHASE 2 Week 1-3 (3 weeks) - Multi-file + DI + Validation → Enterprise-ready
  3. PHASE 3 Week 1 (1 week) - Config file → Better DX
  4. PHASE 2 Week 4 (1 week) - Test generation → Quality
  5. PHASE 3-4 (remaining) - Nice-to-haves

  Total Timeline: ~14 weeks (3.5 months) để đạt 85% maturity