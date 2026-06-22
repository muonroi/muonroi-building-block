# Muonroi.Kubernetes

> Thin configuration model for Kubernetes-aware Muonroi workloads — bind cluster type and API endpoint once, inject everywhere.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Kubernetes.svg)](https://www.nuget.org/packages/Muonroi.Kubernetes/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Kubernetes` is a zero-dependency options package that describes which Kubernetes cluster variant a Muonroi workload targets (upstream K8s, K3s, or Amazon EKS) and where its API server lives. Services that need cluster awareness — choosing in-cluster vs. external endpoints, routing health probes, or selecting cluster-specific behaviour — bind `KubernetesConfigs` via the standard .NET options system and read it through `IOptions<KubernetesConfigs>`. The package ships no middleware, no DI extension method, and no runtime Kubernetes SDK dependency.

## Installation

```bash
dotnet add package Muonroi.Kubernetes --prerelease
```

## Quick Start

Bind the configuration section in `Program.cs`:

```csharp
using Muonroi.Kubernetes.Kubernetes;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KubernetesConfigs>(
    builder.Configuration.GetSection(KubernetesConfigs.SectionName));
```

Add the matching section to `appsettings.json`:

```json
{
  "KubernetesConfigs": {
    "ClusterType": "K8S",
    "ClusterEndpoint": "https://kubernetes.default.svc"
  }
}
```

Inject the options where cluster awareness is needed:

```csharp
public class ClusterInfoService(IOptions<KubernetesConfigs> configs)
{
    public string GetEndpoint() => configs.Value.ClusterEndpoint
        ?? "(in-cluster service-account config)";
}
```

## Features

- `KubernetesConfigs` options class bound from the `"KubernetesConfigs"` configuration section
- `KubernetesClusterType` enum covering the three supported distributions: `K8S`, `K3S`, `Eks`
- `ClusterEndpoint` property for the API server URL (`null` signals in-cluster service-account config)
- No transitive Kubernetes SDK dependency — pure POCO, compatible with any host type

## Configuration

### `appsettings.json`

```json
{
  "KubernetesConfigs": {
    "ClusterType": "K8S",
    "ClusterEndpoint": "https://kubernetes.default.svc"
  }
}
```

`ClusterEndpoint` is nullable. When omitted or `null`, services that consume this config can fall back to in-cluster credentials (e.g. the Kubernetes client SDK's `InClusterConfig`).

### Supported `ClusterType` values

| Value | Distribution |
|-------|-------------|
| `K8S` | Upstream Kubernetes (default) |
| `K3S` | K3s lightweight distribution |
| `Eks` | Amazon EKS managed Kubernetes |

## API Reference

| Type | Purpose |
|------|---------|
| `KubernetesConfigs` | Options class; bound from `KubernetesConfigs.SectionName` (`"KubernetesConfigs"`). Exposes `ClusterType` and `ClusterEndpoint`. |
| `KubernetesConfigs.SectionName` | `const string` — configuration section key (`"KubernetesConfigs"`). |
| `KubernetesClusterType` | Enum — `K8S`, `K3S`, `Eks`. |

## Samples

- [Quickstart.Kubernetes](../../samples/Quickstart.Kubernetes/) — binds `KubernetesConfigs` from `appsettings.json` and exposes the resolved values via a minimal ASP.NET Core API.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.BuildingBlock.All`](../Muonroi.BuildingBlock.All/) — meta package that bundles all OSS building blocks, including this one.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) in the repository root.
