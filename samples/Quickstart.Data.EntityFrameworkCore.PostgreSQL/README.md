> Demonstrates PostgreSQL Entity Framework Core integration.

## What This Sample Demonstrates
- Wiring up AppDbContext derived from MDbContext with PostgreSQL
- Standard DatabaseConfigs usage in ppsettings.json
- Simple API interacting with DbSet<T>

## Prerequisites
- .NET 8 SDK
- A running PostgreSQL instance (optional if you just inspect the setup)

## Run

`ash
cd samples/Quickstart.Data.EntityFrameworkCore.PostgreSQL/src/Quickstart.Data.EntityFrameworkCore.PostgreSQL.Api
dotnet run
`

Then open http://localhost:5000/swagger.

## Key Files
- Program.cs — registration of AddDbContextConfigure<...>
- ppsettings.json — Database connection configuration
