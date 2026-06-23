# Muonroi.Pdf — Samples

Runnable, copy-paste examples for the Muonroi PDF engine. Read [`Program.cs`](./Program.cs)
top-to-bottom to learn the engine: register once with `AddPdf()`, inject `IMPdfService`, build a
`PdfRenderOptions`, call `RenderAsync`.

## Run

```bash
dotnet run --project samples/Muonroi.Pdf.Samples
```

PDFs are written to `bin/Debug/net8.0/pdf-output/`. Expected console output:

```
  01-minimal.pdf               1p    ~27 KB
  02-invoice.pdf               1p    ~30 KB
  03-report-header-footer.pdf  2p    ~54 KB
  04-watermark-gradient.pdf    1p    ~27 KB
  05-flexbox.pdf               1p    ~27 KB
  06-grid.pdf                  1p    ~27 KB
  07-multipage.pdf             3p    ~25 KB
  policy-rejection             rejected 1 violation(s): ...
```

## Scenarios

| File | Demonstrates |
|------|--------------|
| `01-minimal.pdf` | Smallest end-to-end render. |
| `02-invoice.pdf` | Tables, floats, `%` column widths, `border-collapse`, totals box. |
| `03-report-header-footer.pdf` | Programmatic 3-column running header/footer + `counter(page)`/`counter(pages)`, multi-page. |
| `04-watermark-gradient.pdf` | `transform: rotate()` watermark + `linear-gradient`/`radial-gradient` shading. |
| `05-flexbox.pdf` | Real Flexbox — **requires `PdfConfigs:Policy:AllowModernLayout=true`**. |
| `06-grid.pdf` | Real CSS Grid (`repeat(auto-fill, minmax())`, named areas) — **requires `AllowModernLayout`**. |
| `07-multipage.pdf` | `RenderMultiPageAsync` — several HTML fragments into one PDF. |
| `policy-rejection` | `PdfPolicyException` thrown fail-loud for forbidden CSS (`position:fixed`). |

## Notes

- `BuildPdf(allowModernLayout)` builds the pipeline via the Generic Host so `IHostEnvironment`
  (needed by the default font resolver) is present. Scenarios 5 and 6 use a second provider with
  `AllowModernLayout=true`; everything else uses the strict `legacy-print-v1` default.
- Bundled Liberation fonts load automatically — no `@font-face` or OS fonts required.
- For the full supported subset and more template snippets, see the docs:
  **Guides → PDF Engine → Supported HTML / CSS** and **PDF Examples / Sample Templates**.
