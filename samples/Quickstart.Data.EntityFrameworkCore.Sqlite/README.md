> Demonstrates Sqlite Entity Framework Core integration.

## What This Sample Demonstrates
- Wiring up AppDbContext derived from MDbContext with Sqlite
- Standard DatabaseConfigs usage in ppsettings.json
- Simple API interacting with DbSet<T>

## Prerequisites
- .NET 8 SDK
- None! Runs entirely in-memory.

## Run

`ash
cd samples/Quickstart.Data.EntityFrameworkCore.Sqlite/src/Quickstart.Data.EntityFrameworkCore.Sqlite.Api
dotnet run
`

Then open http://localhost:5000/swagger.

## Key Files
- Program.cs — registration of AddDbContextConfigure<...>
- ppsettings.json — Database connection configuration
