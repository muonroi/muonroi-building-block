using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Security;

namespace Muonroi.Core.Tests.Security;

/// <summary>
/// Unit tests for MSecureFileReader: path traversal validation and secure reading.
/// </summary>
public class MSecureFileReaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFile;

    public MSecureFileReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"msrf-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempFile = Path.Combine(_tempDir, "test-key.pem");
        File.WriteAllText(_tempFile, "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQ==\n-----END RSA PRIVATE KEY-----");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // Test 1: ReadKeyFile with valid absolute path to existing file returns file content
    [Fact]
    public void ReadKeyFile_ValidAbsolutePath_ReturnsFileContent()
    {
        string content = MSecureFileReader.ReadKeyFile(_tempFile);
        Assert.Contains("BEGIN RSA PRIVATE KEY", content);
    }

    // Test 2: ReadKeyFile with relative path resolves against base directory and returns content
    [Fact]
    public void ReadKeyFile_RelativePath_ResolvesAgainstBaseDirectoryAndReturnsContent()
    {
        string fileName = Path.GetFileName(_tempFile);
        string content = MSecureFileReader.ReadKeyFile(fileName, _tempDir);
        Assert.Contains("BEGIN RSA PRIVATE KEY", content);
    }

    // Test 3: ReadKeyFile with path containing ".." throws MConfigurationException
    [Fact]
    public void ReadKeyFile_PathWithDotDot_ThrowsMConfigurationException()
    {
        var ex = Assert.Throws<MConfigurationException>(
            () => MSecureFileReader.ReadKeyFile("../secret.key", _tempDir));
        Assert.Contains("traversal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Test 4: ReadKeyFile with path containing "..\" (Windows) throws MConfigurationException
    [Fact]
    public void ReadKeyFile_PathWithDotDotBackslash_ThrowsMConfigurationException()
    {
        var ex = Assert.Throws<MConfigurationException>(
            () => MSecureFileReader.ReadKeyFile(@"foo\..\..\etc\passwd", _tempDir));
        Assert.Contains("traversal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Test 5: ReadKeyFile with null/empty path throws MConfigurationException
    [Fact]
    public void ReadKeyFile_NullPath_ThrowsMConfigurationException()
    {
        Assert.Throws<MConfigurationException>(() => MSecureFileReader.ReadKeyFile(null));
    }

    [Fact]
    public void ReadKeyFile_EmptyPath_ThrowsMConfigurationException()
    {
        Assert.Throws<MConfigurationException>(() => MSecureFileReader.ReadKeyFile("   "));
    }

    // Test 6: ReadKeyFile with nonexistent file throws MConfigurationException (not FileNotFoundException)
    [Fact]
    public void ReadKeyFile_NonexistentFile_ThrowsMConfigurationException_NotFileNotFoundException()
    {
        string nonexistent = Path.Combine(_tempDir, "does-not-exist.pem");
        var ex = Assert.Throws<MConfigurationException>(
            () => MSecureFileReader.ReadKeyFile(nonexistent));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Test 7: ReadKeyFile with valid path and Logging capability logs access audit entry
    [Fact]
    public void ReadKeyFile_WithLoggingCapability_LogsAuditEntry()
    {
        // Capture console output to verify audit logging
        using var sw = new StringWriter();
        Console.SetOut(sw);

        var registry = new FakeEcosystemRegistry(MCapability.Logging);
        string content = MSecureFileReader.ReadKeyFile(_tempFile, null, registry);

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

        string output = sw.ToString();
        Assert.Contains("AUDIT", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN RSA PRIVATE KEY", content);
    }

    // Test 8: ResolveAndValidate returns normalized absolute path for valid input
    [Fact]
    public void ResolveAndValidate_ValidPath_ReturnsNormalizedAbsolutePath()
    {
        string resolved = MSecureFileReader.ResolveAndValidate(_tempFile);
        Assert.True(Path.IsPathRooted(resolved));
        Assert.Equal(Path.GetFullPath(_tempFile), resolved, StringComparer.OrdinalIgnoreCase);
    }

    // Additional: nested traversal pattern
    [Fact]
    public void ReadKeyFile_NestedTraversalPattern_ThrowsMConfigurationException()
    {
        var ex = Assert.Throws<MConfigurationException>(
            () => MSecureFileReader.ReadKeyFile("foo/../../etc/passwd", _tempDir));
        Assert.Contains("traversal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Fake ecosystem registry for testing capability detection.
/// </summary>
file sealed class FakeEcosystemRegistry(MCapability active) : IMEcosystemRegistry
{
    private MCapability _active = active;

    public MCapability Active => _active;

    public bool Has(MCapability capability) => (_active & capability) == capability;

    public void Register(MCapability capability) => _active |= capability;
}
