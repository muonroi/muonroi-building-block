# Muonroi.Pdf.Governance

## Description
Provides governance, auditing, and compliance tracking for PDF generation in the Muonroi ecosystem.

## Features
- Detailed audit logs of document generation.
- Policy enforcement (e.g., mandatory watermarking).
- Compliance reporting integrations.

## Minimal Usage
```csharp
services.AddMuonroiPdfGovernance(options => 
{
    options.RequireWatermark = true;
    options.EnableAuditLogging = true;
});
```
