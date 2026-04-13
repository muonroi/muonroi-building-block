using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators.Tests;

/// <summary>
/// Tests for MSP040 ColumnMapDriftAnalyzer.
///
/// Strategy: each test includes minimal inline source definitions for:
///   - ISiteColumnMap interface (so the analyzer can resolve the type by metadata name)
///   - DefaultSiteColumnMap base class (so skip rule D-04 can be applied)
///   - An entity class with [Column]-attributed properties (EF-configured)
///   - A concrete ISiteColumnMap subclass with a Column() switch expression
///
/// Positive tests (MSP040 fired): concrete ISiteColumnMap has a switch that is missing
/// at least one [Column]-attributed entity property that it covers (association heuristic).
///
/// Negative tests (no diagnostic): all properties covered, abstract class, DefaultSiteColumnMap,
/// or entity not associated with this column map.
/// </summary>
public sealed class ColumnMapDriftAnalyzerTests
{
    // ---------------------------------------------------------------------------
    // Shared infrastructure — minimal inline stubs
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Minimal ISiteColumnMap interface replica needed so the analyzer can resolve
    /// "Muonroi.Tenancy.SiteProfile.Web.Dapper.ISiteColumnMap" in the test compilation.
    /// </summary>
    private const string ISiteColumnMapSource = """
        namespace Muonroi.Tenancy.SiteProfile.Web.Dapper
        {
            public interface ISiteColumnMap
            {
                string Column(string propertyName);
                string Column(string propertyName, string tableName) => Column(propertyName);
                bool HasColumn(string propertyName) => true;
            }
        }
        """;

    /// <summary>
    /// Minimal DefaultSiteColumnMap class replica so the analyzer can resolve
    /// "Muonroi.Tenancy.SiteProfile.Web.Dapper.DefaultSiteColumnMap" and apply D-04 skip rule.
    /// </summary>
    private const string DefaultSiteColumnMapSource = """
        namespace Muonroi.Tenancy.SiteProfile.Web.Dapper
        {
            public class DefaultSiteColumnMap : ISiteColumnMap
            {
                public virtual string Column(string propertyName) => propertyName.ToUpperInvariant();
                public virtual string Column(string propertyName, string tableName) => Column(propertyName);
                public virtual bool HasColumn(string propertyName) => true;
            }
        }
        """;

    // ---------------------------------------------------------------------------
    // Test 1 — POSITIVE: concrete ISiteColumnMap with Column() switch missing a property
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConcreteColumnMap_MissingPropertyInSwitch_Reports_MSP040()
    {
        // Entity with 2 [Column]-attributed properties
        const string entitySource = """
            using System.ComponentModel.DataAnnotations.Schema;
            namespace MyApp.Entities
            {
                public class OrderEntity
                {
                    [Column("ORDER_NO")] public string OrderNo { get; set; } = "";
                    [Column("BOOKING_NO")] public string BookingNo { get; set; } = "";
                }
            }
            """;

        // Column map covers OrderNo but NOT BookingNo — MSP040 should fire for BookingNo
        const string columnMapSource = """
            using Muonroi.Tenancy.SiteProfile.Web.Dapper;
            namespace MyApp.Sites
            {
                public class {|#0:MySiteColumnMap|} : ISiteColumnMap
                {
                    public string Column(string propertyName)
                        => propertyName switch
                        {
                            "OrderNo" => "ORDER_NO",
                            _ => propertyName.ToUpperInvariant()
                        };
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<ColumnMapDriftAnalyzer>
            .Diagnostic(ColumnMapDriftAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("MySiteColumnMap", "BookingNo", "OrderEntity");

        var test = new CSharpAnalyzerTest<ColumnMapDriftAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { ISiteColumnMapSource, DefaultSiteColumnMapSource, entitySource, columnMapSource },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 2 — NEGATIVE: all [Column] properties covered in switch — no diagnostic
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConcreteColumnMap_AllPropertiesCovered_NoDiagnostic()
    {
        const string entitySource = """
            using System.ComponentModel.DataAnnotations.Schema;
            namespace MyApp.Entities
            {
                public class OrderEntity
                {
                    [Column("ORDER_NO")] public string OrderNo { get; set; } = "";
                    [Column("BOOKING_NO")] public string BookingNo { get; set; } = "";
                }
            }
            """;

        // Column map covers BOTH properties — no MSP040
        const string columnMapSource = """
            using Muonroi.Tenancy.SiteProfile.Web.Dapper;
            namespace MyApp.Sites
            {
                public class MySiteColumnMap : ISiteColumnMap
                {
                    public string Column(string propertyName)
                        => propertyName switch
                        {
                            "OrderNo" => "ORDER_NO",
                            "BookingNo" => "BOOKING_NO",
                            _ => propertyName.ToUpperInvariant()
                        };
                }
            }
            """;

        var test = new CSharpAnalyzerTest<ColumnMapDriftAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { ISiteColumnMapSource, DefaultSiteColumnMapSource, entitySource, columnMapSource },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // No expected diagnostics
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 3 — NEGATIVE: DefaultSiteColumnMap itself — skip per D-04
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DefaultSiteColumnMap_Itself_NoDiagnostic()
    {
        const string entitySource = """
            using System.ComponentModel.DataAnnotations.Schema;
            namespace MyApp.Entities
            {
                public class OrderEntity
                {
                    [Column("ORDER_NO")] public string OrderNo { get; set; } = "";
                }
            }
            """;

        // DefaultSiteColumnMap itself — should never fire MSP040
        // It's included inline as a stub matching the exact metadata name.
        // The analyzer skips it via SymbolEqualityComparer check.
        var test = new CSharpAnalyzerTest<ColumnMapDriftAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { ISiteColumnMapSource, DefaultSiteColumnMapSource, entitySource },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // No expected diagnostics — DefaultSiteColumnMap uses convention, not validated
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 4 — POSITIVE: Column(string, string) overload missing mapping — reports MSP040
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConcreteColumnMap_TableOverload_MissingProperty_Reports_MSP040()
    {
        const string entitySource = """
            using System.ComponentModel.DataAnnotations.Schema;
            namespace MyApp.Entities
            {
                public class OrderEntity
                {
                    [Column("ORDER_NO")] public string OrderNo { get; set; } = "";
                    [Column("BOOKING_NO")] public string BookingNo { get; set; } = "";
                }
            }
            """;

        // Column(string, string) overload covers OrderNo in TABLE_A but BookingNo is missing
        const string columnMapSource = """
            using Muonroi.Tenancy.SiteProfile.Web.Dapper;
            namespace MyApp.Sites
            {
                public class {|#0:MySiteColumnMap|} : ISiteColumnMap
                {
                    public string Column(string propertyName) => propertyName.ToUpperInvariant();

                    public string Column(string propertyName, string tableName)
                        => (propertyName, tableName) switch
                        {
                            ("OrderNo", "TABLE_A") => "ORDER_NO_EXT",
                            _ => Column(propertyName)
                        };
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<ColumnMapDriftAnalyzer>
            .Diagnostic(ColumnMapDriftAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("MySiteColumnMap", "BookingNo", "OrderEntity");

        var test = new CSharpAnalyzerTest<ColumnMapDriftAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { ISiteColumnMapSource, DefaultSiteColumnMapSource, entitySource, columnMapSource },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 5 — NEGATIVE: abstract ISiteColumnMap implementation — no diagnostic
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AbstractColumnMap_NoDiagnostic()
    {
        const string entitySource = """
            using System.ComponentModel.DataAnnotations.Schema;
            namespace MyApp.Entities
            {
                public class OrderEntity
                {
                    [Column("ORDER_NO")] public string OrderNo { get; set; } = "";
                    [Column("BOOKING_NO")] public string BookingNo { get; set; } = "";
                }
            }
            """;

        // Abstract column map — skip rule applies, no MSP040
        const string columnMapSource = """
            using Muonroi.Tenancy.SiteProfile.Web.Dapper;
            namespace MyApp.Sites
            {
                public abstract class AbstractSiteColumnMap : ISiteColumnMap
                {
                    public abstract string Column(string propertyName);
                }
            }
            """;

        var test = new CSharpAnalyzerTest<ColumnMapDriftAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { ISiteColumnMapSource, DefaultSiteColumnMapSource, entitySource, columnMapSource },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // No expected diagnostics — abstract class is skipped
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 6 — NEGATIVE: Column map with no switch at all (pure delegation) — no diagnostic
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConcreteColumnMap_NoDelegationToSwitch_NoAssociation_NoDiagnostic()
    {
        const string entitySource = """
            using System.ComponentModel.DataAnnotations.Schema;
            namespace MyApp.Entities
            {
                public class OrderEntity
                {
                    [Column("ORDER_NO")] public string OrderNo { get; set; } = "";
                }
            }
            """;

        // Column map with no switch — fully delegates, no association to any entity
        const string columnMapSource = """
            using Muonroi.Tenancy.SiteProfile.Web.Dapper;
            namespace MyApp.Sites
            {
                public class PureDelegateColumnMap : ISiteColumnMap
                {
                    public string Column(string propertyName) => propertyName.ToUpperInvariant();
                }
            }
            """;

        var test = new CSharpAnalyzerTest<ColumnMapDriftAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { ISiteColumnMapSource, DefaultSiteColumnMapSource, entitySource, columnMapSource },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // No expected diagnostics — no switch means no association heuristic triggers
        await test.RunAsync();
    }
}
