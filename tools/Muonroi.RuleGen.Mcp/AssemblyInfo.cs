// Muonroi.RuleGen.Mcp is a developer MCP (Model Context Protocol) tool server.
// It throws standard BCL exceptions (InvalidOperationException, FileNotFoundException,
// DirectoryNotFoundException) as tool-error responses, not as Muonroi service exceptions.
// MException types are inappropriate here — this is a CLI-adjacent tool, not a Muonroi service.
// Null-forgiving operators in internal Roslyn/Spectre utility code are post-null-check patterns.
[assembly: SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "MCP tool server: BCL exceptions are appropriate tool-error responses; MException is for Muonroi service boundaries.",
    Scope = "module")]
[assembly: SuppressMessage("Muonroi.CodeStandards", "MSTD0002",
    Justification = "MCP tool server: null-forgiving operators in internal utility code are post-null-check narrowing patterns.",
    Scope = "module")]
