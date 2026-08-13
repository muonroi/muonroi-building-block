# Muonroi.Pdf.Abstractions

## Description
Contains the core interfaces and abstractions for PDF generation and manipulation in the Muonroi ecosystem.

## Features
- Standardized `IPdfGenerator` interface.
- Core document models and layout abstractions.
- Lightweight and dependency-free for easy integration.

## Minimal Usage
```csharp
public class MyPdfService
{
    private readonly IPdfGenerator _generator;
    public MyPdfService(IPdfGenerator generator) => _generator = generator;
}
```
