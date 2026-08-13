# Muonroi.Pdf

## Description
A powerful PDF generation and manipulation library for the Muonroi ecosystem.

## Features
- HTML to PDF conversion.
- Document merging and splitting.
- Watermarking and security features.

## Minimal Usage
```csharp
var pdfService = serviceProvider.GetRequiredService<IPdfGenerator>();
var pdfBytes = await pdfService.GenerateFromHtmlAsync("<h1>Hello World</h1>");
```
