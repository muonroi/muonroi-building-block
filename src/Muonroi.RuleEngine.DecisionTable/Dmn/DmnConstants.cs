using System.Xml.Linq;

namespace Muonroi.RuleEngine.DecisionTable.Dmn;

/// <summary>
/// DMN 1.3 XML namespace constants and element/attribute name tokens.
/// Reference: https://www.omg.org/spec/DMN/20191111/MODEL/
/// </summary>
public static class DmnConstants
{
    // ── Namespaces ───────────────────────────────────────────────────────────

    /// <summary>Primary DMN 1.3 model namespace (OMG 20191111).</summary>
    public static readonly XNamespace DmnNamespace = "https://www.omg.org/spec/DMN/20191111/MODEL/";

    /// <summary>Legacy DMN 1.1/1.2 namespace (OMG 20180521) — supported on import.</summary>
    public static readonly XNamespace DmnLegacyNamespace = "https://www.omg.org/spec/DMN/20180521/MODEL/";

    /// <summary>Default namespace value placed in the exported definitions/@namespace attribute.</summary>
    public const string MuonroiDmnNamespace = "https://muonroi.dev/dmn";

    // ── Element local names ──────────────────────────────────────────────────

    /// <summary>DMN root element: <c>&lt;definitions&gt;</c>.</summary>
    public const string Definitions = "definitions";

    /// <summary>DMN decision element: <c>&lt;decision&gt;</c>.</summary>
    public const string Decision = "decision";

    /// <summary>DMN decision table element: <c>&lt;decisionTable&gt;</c>.</summary>
    public const string DecisionTable = "decisionTable";

    /// <summary>DMN input column element: <c>&lt;input&gt;</c>.</summary>
    public const string Input = "input";

    /// <summary>DMN output column element: <c>&lt;output&gt;</c>.</summary>
    public const string Output = "output";

    /// <summary>DMN rule (row) element: <c>&lt;rule&gt;</c>.</summary>
    public const string Rule = "rule";

    /// <summary>DMN input expression element: <c>&lt;inputExpression&gt;</c>.</summary>
    public const string InputExpression = "inputExpression";

    /// <summary>DMN input cell element: <c>&lt;inputEntry&gt;</c>.</summary>
    public const string InputEntry = "inputEntry";

    /// <summary>DMN output cell element: <c>&lt;outputEntry&gt;</c>.</summary>
    public const string OutputEntry = "outputEntry";

    /// <summary>DMN FEEL expression text element: <c>&lt;text&gt;</c>.</summary>
    public const string Text = "text";

    /// <summary>DMN description element: <c>&lt;description&gt;</c>.</summary>
    public const string Description = "description";

    // ── Attribute local names ────────────────────────────────────────────────

    /// <summary>XML id attribute name: <c>id</c>.</summary>
    public const string AttrId = "id";

    /// <summary>XML name attribute name: <c>name</c>.</summary>
    public const string AttrName = "name";

    /// <summary>DMN definitions namespace attribute: <c>namespace</c>.</summary>
    public const string AttrNamespace = "namespace";

    /// <summary>DMN column label attribute: <c>label</c>.</summary>
    public const string AttrLabel = "label";

    /// <summary>DMN type reference attribute: <c>typeRef</c>.</summary>
    public const string AttrTypeRef = "typeRef";

    /// <summary>DMN hit policy attribute: <c>hitPolicy</c>.</summary>
    public const string AttrHitPolicy = "hitPolicy";

    /// <summary>DMN aggregation attribute (Collect variants): <c>aggregation</c>.</summary>
    public const string AttrAggregation = "aggregation";
}
