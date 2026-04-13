# Host Tenant Multi-Site Service Templates

`dotnet new` templates for scaffolding multi-site tenant service projects with per-site DI, shared/per-site proto, and site-resolved services.

## Install

```bash
dotnet new install Host.Tenant.Templates
```

## Templates

### tenant-service — Full multi-site service (Host + Core + Sites.Default)

```bash
# EF Core + shared proto (default)
dotnet new tenant-service -n Acme.Billing

# Dapper + custom SiteCode key
dotnet new tenant-service -n Acme.Shipping --dataAccess dapper --siteCodeKey x-site-code

# Per-site proto mode (sites have different RPC contracts)
dotnet new tenant-service -n Acme.Customs --protoMode perSite
```

### tenant-site-module — Add a new site to existing service

```bash
# Shared proto (site uses Host's proto, optional fields for differences)
dotnet new tenant-site-module -n Acme.Billing.Sites.Hph --siteId Hph --dataAccess ef

# Per-site proto (site has its own .proto with different RPCs)
dotnet new tenant-site-module -n Acme.Billing.Sites.Tci --siteId Tci --dataAccess ef --protoMode perSite
```

After creating a site, add to Host:
1. Add `<ProjectReference>` in Host .csproj
2. Add assembly to `AddSiteServices()` in Program.cs
3. If perSite proto: add `app.MapSiteGrpcServices(typeof(XxxGrpcService).Assembly)`

## Parameters

### tenant-service

| Parameter | Values | Default | Description |
|-----------|--------|---------|-------------|
| `--name` | string | required | Service name prefix |
| `--dataAccess` | ef, dapper | ef | Data access pattern |
| `--protoMode` | shared, perSite | shared | Proto architecture |
| `--siteCodeKey` | string | SiteCode | gRPC metadata key for site resolution |

### tenant-site-module

| Parameter | Values | Default | Description |
|-----------|--------|---------|-------------|
| `--name` | string | required | Full project name |
| `--siteId` | string | NewSite | Site identifier (class prefix) |
| `--dataAccess` | ef, dapper | ef | Data access pattern |
| `--protoMode` | shared, perSite | shared | Include per-site .proto + GrpcService |

## Proto Modes

### Shared (default) — 1 proto, optional fields

```
Host/v1/Protos/service.proto  ← one file for all sites
  message CreateRequest {
    string name = 1;
    optional string tci_field = 100;  // TCI-specific
    optional string ctl_field = 101;  // CTL-specific
  }
```

- Site differences via C# service inheritance
- Caller refs shared proto for all sites
- Set site via gRPC metadata (configurable key)

### Per-Site — each site has own .proto

```
Host/v1/Protos/service.proto       ← shared (default sites)
Sites.Tci/Protos/service.tci.proto ← TCI-specific RPCs
Sites.Ctl/Protos/service.ctl.proto ← CTL-specific RPCs
```

- Each site's proto compiled by its own project
- Separate gRPC endpoints per site
- Caller refs the specific site's proto
- Marked with `[SiteGrpcService("TCI")]` for auto-discovery

## Uninstall

```bash
dotnet new uninstall Host.Tenant.Templates
```
