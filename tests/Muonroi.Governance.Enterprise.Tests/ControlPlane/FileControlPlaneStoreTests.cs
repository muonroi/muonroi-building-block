namespace Muonroi.Governance.Enterprise.Tests.ControlPlane;

public class FileControlPlaneStoreTests : IDisposable
{
    private readonly IMJsonSerializeService _jsonSerializeService;
    private readonly string _tempFile;
    private readonly MFileControlPlaneStore _store;

    public FileControlPlaneStoreTests()
    {
        _jsonSerializeService = Substitute.For<IMJsonSerializeService>();
        _tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        _store = new MFileControlPlaneStore(_tempFile, _jsonSerializeService);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ShouldReturnNewRegistry()
    {
        // Act
        var result = _store.Load();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Licenses);
    }

    [Fact]
    public void Save_ShouldCreateFile()
    {
        // Arrange
        var registry = new MControlPlaneRegistry();

        // Act
        _store.Save(registry);

        // Assert
        Assert.True(File.Exists(_tempFile));
    }

    [Fact]
    public void Load_WhenFileExists_ShouldDeserialize()
    {
        // Arrange
        var registry = new MControlPlaneRegistry();
        File.WriteAllText(_tempFile, "{}");
        _jsonSerializeService.Deserialize<MControlPlaneRegistry>(Arg.Any<string>()).Returns(registry);

        // Act
        var result = _store.Load();

        // Assert
        Assert.Same(registry, result);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}
