# Muonroi.Pdf.Enterprise

## Description
Enterprise-grade PDF features for the Muonroi ecosystem, including advanced security, digital signatures, and high-volume generation optimizations.

## Features
- Digital signatures and certificate management.
- PDF/A compliance for archiving.
- Advanced encryption and access control.

## Minimal Usage
```csharp
var pdfOptions = new PdfEnterpriseOptions
{
    EnableDigitalSignatures = true,
    ComplianceLevel = PdfComplianceLevel.PdfA
};
```
