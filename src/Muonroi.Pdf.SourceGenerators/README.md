# Muonroi.Pdf.SourceGenerators

## Description
Roslyn source generators for optimizing PDF template compilation and data binding at build time in the Muonroi ecosystem.

## Features
- Compile-time template validation.
- Zero-reflection data binding.
- Improved runtime performance for document generation.

## Minimal Usage
*Include this package in your project to automatically enable source generation for classes marked with `[PdfTemplate]`.*
```csharp
[PdfTemplate]
public partial class InvoiceTemplate { }
```
