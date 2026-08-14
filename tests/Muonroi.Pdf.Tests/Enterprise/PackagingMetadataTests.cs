namespace Muonroi.Pdf.Tests.Enterprise;

/// <summary>
/// Smoke tests for F7 dual-NuGet packaging signing strategy.
///
/// Strategy: Enterprise assembly is strong-named (Muonroi.snk); OSS engine is unsigned.
/// This asymmetry keeps OSS contributor friction minimal while protecting the commercial surface.
///
/// NOTE: If you want both assemblies signed, <see cref="OssAssemblyIsUnsigned"/> will fail
/// and signal that the signing strategy needs revisiting.
/// </summary>
public sealed class PackagingMetadataTests
{
    /// <summary>
    /// Verifies that the Enterprise assembly carries a strong-name public key.
    /// Signing is applied via SignAssembly=true + Muonroi.snk in Muonroi.Pdf.Enterprise.csproj.
    /// </summary>
    [Fact]
    public void EnterpriseAssemblyIsStrongNamed()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // OpenSSL 3 on Linux rejects SHA-1 strong-name signing; SignAssembly=false on non-Windows CI
            return;
        }

        byte[] publicKey = typeof(IFeatureGate).Assembly.GetName().GetPublicKey() ?? [];
        Assert.True(publicKey.Length > 0,
            "Muonroi.Pdf.Enterprise must be strong-named (SignAssembly=true + Muonroi.snk). " +
            "Public key length was 0 — check AssemblyOriginatorKeyFile in the .csproj.");
    }

    /// <summary>
    /// Verifies that the OSS engine assembly is NOT strong-named.
    /// This is intentional: unsigned OSS reduces contributor friction (no snk required to build).
    /// If this test fails and you want both assemblies signed, update the signing strategy docs.
    /// </summary>
    [Fact]
    public void OssAssemblyIsUnsigned()
    {
        byte[] publicKey = typeof(PdfServiceCollectionExtensions).Assembly.GetName().GetPublicKey() ?? [];
        Assert.True(publicKey.Length == 0,
            "Muonroi.Pdf (OSS engine) should NOT be strong-named — asymmetric signing strategy. " +
            "If you want both assemblies signed, remove this assertion and update signing docs.");
    }
}
