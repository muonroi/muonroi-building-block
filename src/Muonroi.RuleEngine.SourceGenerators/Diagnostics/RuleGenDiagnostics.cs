

namespace Muonroi.RuleEngine.SourceGenerators.Diagnostics;

internal static class RuleGenDiagnostics
{
    private const string RuleGenName = "RuleGen";
    public static readonly DiagnosticDescriptor DuplicateRuleCode = new(
        id: "MRG001",
        title: "Duplicate Rule Code",
        messageFormat: "Rule code '{0}' is already used in class '{1}'",
        category: RuleGenName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidHookPoint = new(
        id: "MRG002",
        title: "Invalid Hook Point",
        messageFormat: "Hook point '{0}' is not a valid value for HookPoint enum",
        category: RuleGenName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NonInterfaceDependency = new(
        id: "MRG003",
        title: "Non-Interface Dependency",
        messageFormat: "Field '{0}' has type '{1}' which is not an interface. Rule dependencies should ideally be interfaces for better DI support.",
        category: RuleGenName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor HelperMethodExtractionFailed = new(
        id: "MRG004",
        title: "Helper Method Extraction Failed",
        messageFormat: "Failed to extract helper method '{0}'. Only private methods in the same class are supported.",
        category: RuleGenName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingDependencyReference = new(
        id: "MRG005",
        title: "Missing DependsOn Reference",
        messageFormat: "Rule '{0}' declares DependsOn '{1}' but no rule with that code was found",
        category: RuleGenName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OrderWithoutDependsOn = new(
        id: "MRG006",
        title: "Order Without DependsOn",
        messageFormat: "Rule '{0}' has Order={1} but no DependsOn. Rule orchestrator sorts by DependsOn graph, not Order.",
        category: RuleGenName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FactConsumptionWithoutDependency = new(
        id: "MRG007",
        title: "FactBag Dependency Risk",
        messageFormat: "Rule '{0}' reads fact key '{1}' but has no dependency path to producer rule(s): {2}",
        category: RuleGenName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NullableAssignedToNonNullableString = new(
        id: "MRG008",
        title: "Nullable To Non-Nullable String Assignment",
        messageFormat: "Assignment to '{0}' may pass nullable value '{1}'. Add null-coalescing (?? string.Empty) or explicit guard.",
        category: RuleGenName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FactGuardThrowsInvalidOperation = new(
        id: "MRG009",
        title: "Fact Guard Throws InvalidOperationException",
        messageFormat: "Rule '{0}' uses throw InvalidOperationException for missing fact key '{1}'. Prefer returning RuleResult.Failure to avoid first-chance exception noise.",
        category: RuleGenName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidFeelExpression = new(
        id: "MRG010",
        title: "Invalid FEEL Expression",
        messageFormat: "Rule '{0}': Expression='{1}' failed FEEL validation: {2}",
        category: RuleGenName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
