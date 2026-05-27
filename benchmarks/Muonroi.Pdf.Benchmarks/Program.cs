using BenchmarkDotNet.Running;
using Muonroi.Pdf.Benchmarks;

BenchmarkRunner.Run<PdfRenderBenchmarks>(args: args);
