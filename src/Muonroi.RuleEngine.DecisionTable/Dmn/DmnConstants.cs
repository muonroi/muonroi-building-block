using System.Xml.Linq;

namespace Muonroi.RuleEngine.DecisionTable.Dmn;

/// <summary>
/// DMN 1.3 XML namespace constants and element name tokens.
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

    public const string Definitions    = "definitions";
    public const string Decision       = "decision";
    public const string DecisionTable  = "decisionTable";
    public const string Input          = "input";
    public const string Output         = "output";
    public const string Rule           = "rule";
    public const string InputExpression = "inputExpression";
    public const string InputEntry     = "inputEntry";
    public const string OutputEntry    = "outputEntry";
    public const string Text           = "text";
    public const string Description    = "description";

    // ── Attribute local names ────────────────────────────────────────────────

    public const string AttrId          = "id";
    public const string AttrName        = "name";
    public const string AttrNamespace   = "namespace";
    public const string AttrLabel       = "label";
    public const string AttrTypeRef     = "typeRef";
    public const string AttrHitPolicy   = "hitPolicy";
    public const string AttrAggregation = "aggregation";
}
