using System.ComponentModel.DataAnnotations;

namespace Muonroi.BuildingBlock.Test;

public class MValidationObjectTests
{
    private class TestValidationObject : MValidationObject
    {
        [Required]
        public string? Name { get; set; }
    }

    [Fact]
    public void IsValid_Returns_True_When_Object_Is_Valid()
    {
        var obj = new TestValidationObject { Name = "test" };
        Assert.True(obj.IsValid());
    }

    [Fact]
    public void IsValid_Returns_False_When_Object_Is_Invalid()
    {
        var obj = new TestValidationObject();
        Assert.False(obj.IsValid());
    }
}
