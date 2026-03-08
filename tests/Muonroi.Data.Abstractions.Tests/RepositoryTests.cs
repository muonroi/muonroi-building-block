namespace Muonroi.Data.Abstractions.Tests;

using System;
using System.Threading.Tasks;
using Muonroi.Data.Abstractions.Repositories;
using Muonroi.Core.Abstractions.SeedWorks;
using Moq;
using Xunit;

public class RepositoryTests
{
    public class TestEntity : MEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void IMRepository_Add_ShouldReturnEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "Test" };
        var repoMock = new Mock<IMRepository<TestEntity>>();
        repoMock.Setup(x => x.Add(It.IsAny<TestEntity>())).Returns(entity);

        // Act
        var result = repoMock.Object.Add(entity);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
    }
}
