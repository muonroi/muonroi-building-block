using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.Rules.Tests.Table;

public class DecisionTableImporterTests
{
    [Fact]
    public void ImportCsv_EmptyContent_ShouldThrow()
    {
        Action act = () => DecisionTableImporter.ImportCsv("");
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void ImportCsv_NullContent_ShouldThrow()
    {
        Action act = () => DecisionTableImporter.ImportCsv(null!);
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void ImportCsv_TooFewLines_ShouldThrow()
    {
        Action act = () => DecisionTableImporter.ImportCsv("HitPolicy,First\nAge,Result");
        act.Should().Throw<MInternalException>()
            .WithMessage("*hit policy*headers*rule*");
    }

    [Fact]
    public void ImportCsv_MissingHitPolicyDeclaration_ShouldThrow()
    {
        string csv = "NotHitPolicy,First\nAge,Result\n25,Pass";
        Action act = () => DecisionTableImporter.ImportCsv(csv);
        act.Should().Throw<MInternalException>().WithMessage("*HitPolicy*");
    }

    [Fact]
    public void ImportCsv_InvalidHitPolicy_ShouldThrow()
    {
        string csv = "HitPolicy,InvalidPolicy\nAge,Result\n25,Pass";
        Action act = () => DecisionTableImporter.ImportCsv(csv);
        act.Should().Throw<MInternalException>().WithMessage("*Invalid hit policy*");
    }

    [Fact]
    public void ImportCsv_ValidTable_ShouldParseCorrectly()
    {
        string csv = "HitPolicy,First\nAge,Result\n25,Pass\n15,Fail";

        var table = DecisionTableImporter.ImportCsv(csv);

        table.HitPolicy.Should().Be(HitPolicy.First);
        table.InputHeaders.Should().ContainSingle("Age");
        table.OutputHeaders.Should().ContainSingle("Result");
        table.Rules.Should().HaveCount(2);
        table.Rules[0].Inputs["Age"].Should().Be("25");
        table.Rules[0].Outputs["Result"].Should().Be("Pass");
    }

    [Fact]
    public void ImportCsv_UniqueHitPolicy_ShouldParse()
    {
        string csv = "HitPolicy,Unique\nAge,Result\n25,Pass";

        var table = DecisionTableImporter.ImportCsv(csv);

        table.HitPolicy.Should().Be(HitPolicy.Unique);
    }

    [Fact]
    public void ImportCsv_IncorrectColumnCount_ShouldThrow()
    {
        string csv = "HitPolicy,First\nAge,Result\n25";
        Action act = () => DecisionTableImporter.ImportCsv(csv);
        act.Should().Throw<MInternalException>().WithMessage("*incorrect*columns*");
    }

    [Fact]
    public void ImportCsv_OverlappingRanges_ShouldAddWarnings()
    {
        string csv = "HitPolicy,First\nAge,Result\n[0..10],Young\n[5..15],Teen";

        var table = DecisionTableImporter.ImportCsv(csv);

        table.Warnings.Should().ContainSingle(w => w.Contains("Overlap"));
    }

    [Fact]
    public void ImportCsv_NonOverlappingRanges_ShouldNotWarn()
    {
        string csv = "HitPolicy,First\nAge,Result\n[0..5],Young\n[10..15],Teen";

        var table = DecisionTableImporter.ImportCsv(csv);

        table.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ImportCsv_ComparisonExpressions_ShouldDetectOverlap()
    {
        string csv = "HitPolicy,First\nAge,Result\n>=10,Pass\n<=15,Also";

        var table = DecisionTableImporter.ImportCsv(csv);

        table.Warnings.Should().ContainSingle(w => w.Contains("Overlap"));
    }

    [Fact]
    public void ImportCsv_EqualValues_ShouldDetectOverlap()
    {
        string csv = "HitPolicy,First\nAge,Result\n10,Pass\n10,Fail";

        var table = DecisionTableImporter.ImportCsv(csv);

        table.Warnings.Should().ContainSingle(w => w.Contains("Overlap"));
    }

    [Fact]
    public void ImportCsv_AdjacentNonOverlappingRanges_ShouldNotWarn()
    {
        // >10 and <10 don't overlap; >10 starts at 10 exclusive, <10 ends at 10 exclusive
        string csv = "HitPolicy,First\nAge,Result\n>10,Pass\n<10,Fail";

        var table = DecisionTableImporter.ImportCsv(csv);

        table.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ImportCsv_MultipleOutputHeaders_ShouldParse()
    {
        string csv = "HitPolicy,First\nAge,Category,Risk\n25,Adult,Low\n15,Minor,Medium";

        var table = DecisionTableImporter.ImportCsv(csv);

        table.OutputHeaders.Should().HaveCount(2);
        table.OutputHeaders.Should().Contain("Category");
        table.OutputHeaders.Should().Contain("Risk");
        table.Rules[0].Outputs["Category"].Should().Be("Adult");
        table.Rules[0].Outputs["Risk"].Should().Be("Low");
    }

    [Fact]
    public void ImportCsv_SingleColumnOnly_ShouldThrow()
    {
        string csv = "HitPolicy,First\nAge\n25";
        Action act = () => DecisionTableImporter.ImportCsv(csv);
        act.Should().Throw<MInternalException>().WithMessage("*at least one input and one output*");
    }

    [Fact]
    public void HitPolicy_Enum_ShouldHaveExpectedValues()
    {
        HitPolicy.First.Should().BeDefined();
        HitPolicy.Unique.Should().BeDefined();
    }

    [Fact]
    public void DecisionRule_ShouldHaveInputsAndOutputs()
    {
        var rule = new DecisionRule(
            new Dictionary<string, string> { ["age"] = "25" },
            new Dictionary<string, string> { ["result"] = "pass" });

        rule.Inputs["age"].Should().Be("25");
        rule.Outputs["result"].Should().Be("pass");
    }

    [Fact]
    public void DecisionTable_Properties_ShouldBeInitializable()
    {
        var table = new DecisionTable { HitPolicy = HitPolicy.Unique };

        table.InputHeaders.Should().NotBeNull();
        table.OutputHeaders.Should().NotBeNull();
        table.Rules.Should().NotBeNull();
        table.Warnings.Should().NotBeNull();
    }
}
