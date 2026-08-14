[assembly: InternalsVisibleTo("Muonroi.RuleGen.Mcp")]
[assembly: InternalsVisibleTo("muonroi-mcp-dev")]
[assembly: InternalsVisibleTo("Muonroi.RuleGen.Mcp.Tests")]
[assembly: InternalsVisibleTo("Muonroi.RuleGen.Tests")]

// Muonroi.RuleGen is a developer CLI tool. It throws standard BCL exceptions
// (InvalidOperationException, FileNotFoundException, DirectoryNotFoundException, InvalidDataException)
// as user-visible CLI error messages caught by the entry-point error handler.
// MException types are inappropriate here: this tool is not a Muonroi service —
// it is a standalone .NET tool that consumers invoke from the command line.
[assembly: SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "CLI tool: BCL exceptions are the correct user-visible error type; MException is inappropriate for a standalone developer tool.",
    Scope = "module")]
[assembly: SuppressMessage("Muonroi.CodeStandards", "MSTD0002",
    Justification = "CLI tool: null-forgiving operators in internal Roslyn/Spectre utility code are post-null-check narrowing patterns.",
    Scope = "module")]
