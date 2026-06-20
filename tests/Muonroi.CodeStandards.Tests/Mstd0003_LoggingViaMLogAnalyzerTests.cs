namespace Muonroi.CodeStandards.Tests;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.CodeStandards.Analyzers;
using Xunit;

public class Mstd0003_LoggingViaMLogAnalyzerTests
{
    // Fake logging infrastructure compiled into every test source so the analyzer can resolve
    // Microsoft.Extensions.Logging.ILogger, Muonroi.Logging.Abstractions.IMLog and Serilog.Log
    // without referencing the real assemblies (which would conflict with these declarations).
    private const string Prelude = @"
namespace Microsoft.Extensions.Logging
{
    public interface ILogger { void Log(string message); }
    public interface ILogger<out T> : ILogger { }
    public static class LoggerExtensions
    {
        public static void LogInformation(this ILogger logger, string message) { }
        public static void LogError(this ILogger logger, string message) { }
        public static void LogWarning(this ILogger logger, string message) { }
        public static void LogDebug(this ILogger logger, string message) { }
    }
}
namespace Muonroi.Logging.Abstractions
{
    public interface IMLog : Microsoft.Extensions.Logging.ILogger { void Info(string message); }
    public interface IMLog<out T> : IMLog, Microsoft.Extensions.Logging.ILogger<T> { }
}
namespace Serilog
{
    public static class Log
    {
        public static void Error(string message) { }
        public static void Information(string message) { }
    }
}
";

    [Fact]
    public void Mstd0003_ConsoleWriteLine_InMuonroiNamespace_ShouldError()
    {
        string source = Prelude + @"
namespace Muonroi.MyService
{
    public class Worker
    {
        public void Run() { System.Console.WriteLine(""hello""); }
    }
}";
        Assert.Contains(GetDiagnostics(source), d => d.Id == "MSTD0003");
    }

    [Fact]
    public void Mstd0003_ConsoleErrorWriteLine_ShouldError()
    {
        string source = Prelude + @"
namespace Muonroi.MyService
{
    public class Worker
    {
        public void Run() { System.Console.Error.WriteLine(""boom""); }
    }
}";
        Assert.Contains(GetDiagnostics(source), d => d.Id == "MSTD0003");
    }

    [Fact]
    public void Mstd0003_DebugWriteLine_ShouldError()
    {
        string source = Prelude + @"
namespace Muonroi.MyService
{
    public class Worker
    {
        public void Run() { System.Diagnostics.Debug.WriteLine(""x""); }
    }
}";
        Assert.Contains(GetDiagnostics(source), d => d.Id == "MSTD0003");
    }

    [Fact]
    public void Mstd0003_SerilogStaticLog_ShouldError()
    {
        string source = Prelude + @"
namespace Muonroi.MyService
{
    public class Worker
    {
        public void Run() { Serilog.Log.Error(""x""); }
    }
}";
        Assert.Contains(GetDiagnostics(source), d => d.Id == "MSTD0003");
    }

    [Fact]
    public void Mstd0003_RawILoggerLogCall_ShouldError()
    {
        string source = Prelude + @"
namespace Muonroi.MyService
{
    using Microsoft.Extensions.Logging;
    public class Worker
    {
        private readonly ILogger<Worker> _logger;
        public Worker(ILogger<Worker> logger) { _logger = logger; }
        public void Run() { _logger.LogInformation(""x""); }
    }
}";
        Assert.Contains(GetDiagnostics(source), d => d.Id == "MSTD0003");
    }

    [Fact]
    public void Mstd0003_IMLogUsage_ShouldNotError()
    {
        string source = Prelude + @"
namespace Muonroi.MyService
{
    using Microsoft.Extensions.Logging;
    using Muonroi.Logging.Abstractions;
    public class Worker
    {
        private readonly IMLog<Worker> _log;
        public Worker(IMLog<Worker> log) { _log = log; }
        public void Run()
        {
            _log.Info(""x"");
            _log.LogInformation(""y"");
        }
    }
}";
        Assert.DoesNotContain(GetDiagnostics(source), d => d.Id == "MSTD0003");
    }

    [Fact]
    public void Mstd0003_LoggingInfrastructureNamespace_ShouldNotError()
    {
        string source = Prelude + @"
namespace Muonroi.Logging
{
    using Microsoft.Extensions.Logging;
    public class Wrapper
    {
        private readonly ILogger<Wrapper> _inner;
        public Wrapper(ILogger<Wrapper> inner) { _inner = inner; }
        public void Run() { _inner.LogInformation(""x""); System.Console.WriteLine(""y""); }
    }
}";
        Assert.DoesNotContain(GetDiagnostics(source), d => d.Id == "MSTD0003");
    }

    [Fact]
    public void Mstd0003_NonMuonroiNamespace_ShouldNotError()
    {
        string source = Prelude + @"
namespace MyApp.Services
{
    public class Worker
    {
        public void Run() { System.Console.WriteLine(""x""); }
    }
}";
        Assert.DoesNotContain(GetDiagnostics(source), d => d.Id == "MSTD0003");
    }

    [Fact]
    public void Mstd0003_InTestAssembly_ShouldNotError()
    {
        string source = Prelude + @"
namespace Muonroi.MyService
{
    public class Worker
    {
        public void Run() { System.Console.WriteLine(""x""); }
    }
}";
        Assert.DoesNotContain(
            GetDiagnostics(source, assemblyName: "MyProject.Tests"),
            d => d.Id == "MSTD0003");
    }

    [Fact]
    public void Mstd0003_ConsoleReadLine_ShouldNotError()
    {
        string source = Prelude + @"
namespace Muonroi.MyService
{
    public class Worker
    {
        public string Run() { return System.Console.ReadLine(); }
    }
}";
        Assert.DoesNotContain(GetDiagnostics(source), d => d.Id == "MSTD0003");
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(string source, string assemblyName = "TestCompilation")
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest);

        // Use the full trusted-platform-assembly set so framework types referenced by the
        // analyzer (System.Console, System.Diagnostics.Debug, System.IO.TextWriter, ...) resolve.
        // The real Microsoft.Extensions.Logging / Serilog / Muonroi.Logging assemblies are NOT
        // in this set, so the in-source fakes above are unambiguous.
        string tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        List<MetadataReference> references = tpa
            .Split(Path.PathSeparator)
            .Where(p => p.Length > 0)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mstd0003_LoggingViaMLogAnalyzer());
        return compilation.WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }
}
