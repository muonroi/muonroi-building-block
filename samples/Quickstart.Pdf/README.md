# Quickstart.Pdf
> Demonstrates the basic HTML to PDF rendering pipeline using Muonroi.Pdf.

## What This Sample Demonstrates
- Registering the `Muonroi.Pdf` engine using `AddPdf()`.
- Resolving `IMPdfService` from DI.
- Rendering a simple HTML string to a PDF byte array (`RenderToBytesAsync`).
- Configuring `PdfRenderOptions` like margins and template IDs.

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Pdf/src/Quickstart.Pdf.Api
dotnet run
```

Then open [http://localhost:5000/swagger](http://localhost:5000/swagger) and execute the `/pdf/invoice` endpoint to receive a generated PDF file.

## Key Files
- `Program.cs` — service registration and endpoint wiring

## How It Works
The `Muonroi.Pdf` engine processes standard HTML and CSS into PDF documents. It works fully offline with no external dependencies (like Chrome or wkhtmltopdf). In `Program.cs`, we use `AddPdf()` to register all internal layout and rendering services. Our minimal API endpoint injects `IMPdfService` to perform the conversion directly from an HTML string to PDF bytes.
