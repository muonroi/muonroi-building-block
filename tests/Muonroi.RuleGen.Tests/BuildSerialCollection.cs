namespace Muonroi.RuleGen.Tests;

/// <summary>
/// Serializes every test that shells out to <c>dotnet build</c> — the one-shot RuleGen tool build in
/// <see cref="CliProcess"/> and the <c>--compile-target</c> compile-checks driven through the CLI, plus
/// the standalone <see cref="Services.CompileCheckService"/> build test.
/// <para>
/// The tool build and the <c>--compile-target</c> builds both transitively rebuild the shared in-repo
/// project intermediate output under <c>src/*/obj</c> (Muonroi.RuleEngine.Abstractions and its
/// dependency chain). When two such builds run concurrently — which only happens during a full-solution
/// <c>dotnet test</c>, where CPU saturation stretches each build to 20-40s so they overlap — MSBuild
/// races on the generated <c>ref/*.dll</c> reference assemblies and <c>*.GeneratedMSBuildEditorConfig</c>
/// files in those obj directories, producing transient build failures:
/// <c>CS0006: Metadata file '...\Muonroi.Core.Abstractions\obj\Debug\net8.0\ref\...dll' could not be found</c>
/// and <c>CS2001: Source file '...GeneratedMSBuildEditorConfig.editorconfig' could not be found</c>.
/// </para>
/// <para>
/// In isolation (this assembly alone) the builds are fast, never overlap, and all tests pass — which is
/// why the flakiness only surfaces in the full suite. Marking these tests as a single
/// non-parallelized collection guarantees at most one <c>dotnet build</c> runs at a time, so the shared
/// obj directories are never written by two processes concurrently.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BuildSerialCollection
{
    public const string Name = "RuleGen build-spawning (serialized)";
}
