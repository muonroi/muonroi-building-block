# Quickstart.Tenancy.SiteProfile
> Demonstrates the Site Profile pattern for per-site service resolution.

## What This Sample Demonstrates
- ISiteProfile marker interface for distinct site configurations
- ISiteProfileResolver for per-request site resolution
- AddMultiSiteProfiles and AddSiteResolvedService<T> for registering per-site services
- SiteProfileScope for temporarily overriding the site context (e.g. background jobs)

## Prerequisites
- .NET 8 SDK

## Run

`ash
cd samples/Quickstart.Tenancy.SiteProfile/src/Quickstart.Tenancy.SiteProfile.Api
dotnet run
`

Then open http://localhost:5000/swagger.
Try calling /api/welcome and passing X-Site-Code header sg01 or us01.

## Key Files
- Program.cs — service registration and endpoint wiring
