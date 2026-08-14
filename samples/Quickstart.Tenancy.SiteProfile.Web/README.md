# Quickstart.Tenancy.SiteProfile.Web
> Web integrations and repository base classes for Site Profiles.

## What This Sample Demonstrates
- AddSiteInfrastructure() setup (registers profiles, controllers, configuration)
- SiteProfileStateMiddleware (halts requests if site is disabled)
- Per-site EF Core DbContext with AddSiteDbInfrastructure<TContext>()
- MSiteRepository<TContext, T> implementation

## Prerequisites
- .NET 8 SDK

## Run

`ash
cd samples/Quickstart.Tenancy.SiteProfile.Web/src/Quickstart.Tenancy.SiteProfile.Web.Api
dotnet run
`

Then open http://localhost:5000/swagger.
Call /api/samples with X-Site-Code: sg01.

## Key Files
- Program.cs — DB, Repository, and site configuration.
