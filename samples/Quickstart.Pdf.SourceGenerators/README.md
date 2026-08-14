# Quickstart.Pdf.SourceGenerators
> Demonstrates the compile-time PDF renderer source generator.

## What This Sample Demonstrates
- Using the `[PdfTemplate]` attribute on a model class.
- Configuring `AdditionalFiles` in the project file to embed HTML templates.
- Resolving the generated `IMPdfRenderer<TModel>` via DI.
- Zero-overhead rendering with interpolated strings (no runtime reflection or external file reads).

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Pdf.SourceGenerators/src/Quickstart.Pdf.SourceGenerators.Api
dotnet run
```

Then open [http://localhost:5000/swagger](http://localhost:5000/swagger).

You can test the `POST /pdf/report` endpoint with a payload like:

```json
{
  "id": "RPT-2026-08",
  "title": "August Financials",
  "totalSales": 12500.50
}
```

## Key Files
- `Models/ReportModel.cs` — model with `[PdfTemplate]` attribute
- `Templates/ReportTemplate.html` — the HTML template file loaded at compile time
- `Program.cs` — registration of the generated `AddPdfRendererReportModel()` extension

## How It Works
The `Muonroi.Pdf.SourceGenerators` package runs during compilation. It inspects classes marked with `[PdfTemplate]` and reads the specified HTML template file (which must be included as an `AdditionalFiles` in `.csproj`). It generates a strongly typed implementation of `IMPdfRenderer<TModel>` where the HTML string is an inlined C# string interpolation. This avoids runtime template parsing and IO overhead.
