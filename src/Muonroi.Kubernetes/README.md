# Muonroi.Kubernetes
> Kubernetes runtime configurations and environment utilities for the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Kubernetes.svg)](https://www.nuget.org/packages/Muonroi.Kubernetes/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

`Muonroi.Kubernetes` provides foundational configuration primitives for Muonroi Building Block applications running within Kubernetes environments. It is designed to act as a central source of truth for runtime orchestration parameters, allowing services to adapt their behaviors based on the specific flavor or configuration of the Kubernetes cluster they are deployed to.

While currently lightweight, this package establishes the configuration contracts (`KubernetesConfigs` and `KubernetesClusterType`) necessary for downstream integrationsâ€”such as dynamic service discovery, Kubernetes-native leader election, and automated health check bindings.

## Features

- **Standardized Configuration Section**: Exposes `KubernetesConfigs` which binds directly to the standard `KubernetesConfigs` section in `appsettings.json`.
- **Cluster Type Awareness**: Includes the `KubernetesClusterType` enumeration to differentiate between standard Upstream Kubernetes (`K8S`), lightweight Edge Kubernetes (`K3S`), and managed Amazon EKS (`Eks`), enabling environment-specific optimizations.

## Installation

```bash
dotnet add package Muonroi.Kubernetes
```

## Quick Start

### 1. Configuration Setup

Add the configuration section to your `appsettings.json` or environment variables:

```json
{
  "KubernetesConfigs": {
    "ClusterType": "Eks",
    "ClusterEndpoint": "https://<eks-id>.gr7.us-east-1.eks.amazonaws.com"
  }
}
```

Or via environment variables:
`KubernetesConfigs__ClusterType=K3S`

### 2. Binding Configuration

Bind the configuration model during your application startup:

```csharp
using Muonroi.Kubernetes.Kubernetes;

var builder = WebApplication.CreateBuilder(args);

// Bind the configuration to the DI container via IOptions
builder.Services.Configure<KubernetesConfigs>(
    builder.Configuration.GetSection(KubernetesConfigs.SectionName));
```

### 3. Usage

Inject `IOptions<KubernetesConfigs>` into your services to adapt logic based on the cluster type.

```csharp
using Microsoft.Extensions.Options;
using Muonroi.Kubernetes.Kubernetes;

public class ClusterAwareMetricsPublisher
{
    private readonly KubernetesConfigs _k8sConfigs;

    public ClusterAwareMetricsPublisher(IOptions<KubernetesConfigs> options)
    {
        _k8sConfigs = options.Value;
    }

    public async Task PublishMetricsAsync()
    {
        if (_k8sConfigs.ClusterType == KubernetesClusterType.Eks)
        {
            // Publish to AWS CloudWatch via IAM Roles for Service Accounts (IRSA)
            await PublishToCloudWatchAsync();
        }
        else
        {
            // Fallback to standard Prometheus scraping format
            ExposePrometheusEndpoint();
        }
    }
    
    private Task PublishToCloudWatchAsync() => Task.CompletedTask;
    private void ExposePrometheusEndpoint() { }
}
```

## API Reference

### `KubernetesConfigs`

The central configuration record for Kubernetes-specific settings.

- `SectionName`: Constant `"KubernetesConfigs"` used for configuration binding.
- `ClusterType`: Enum indicating the distribution of Kubernetes (`KubernetesClusterType`). Defaults to `K8S`.
- `ClusterEndpoint`: Optional API server endpoint URL string.

### `KubernetesClusterType`

Enum defining supported Kubernetes distributions:
- `K8S`: Standard upstream Kubernetes (Default).
- `K3S`: Lightweight Kubernetes distribution (e.g., for edge or local environments).
- `Eks`: Amazon Elastic Kubernetes Service.

## Ecosystem Combinations

> Works great standalone. Becomes **significantly more powerful** when combined.

### + ServiceDiscovery.Consul -> K8s cluster type drives which discovery backend is active
Switch between native K8s DNS and Consul depending on the environment.

### + Http -> HttpClient service discovery resolves addresses from K8s DNS
Extend HTTP clients to resolve http://my-service directly to K8s service boundaries.

### + Resilience -> K8s pod restarts trigger circuit-breaker half-open retry automatically
Tune Polly resilience strategies based on known Kubernetes deployment and rollout behavior.

### + Observability -> K8s pod/namespace metadata added to all OTel resource attributes
Enrich your telemetry spans with k8s.pod.name and k8s.namespace.name automatically.

### + Secrets -> KubernetesSecretProvider reads from mounted K8s secret volumes
Read infrastructure secrets directly from /etc/secrets rather than environment variables.

### Full Stack
`csharp
// combined registration
builder.Services.Configure<KubernetesConfigs>(builder.Configuration.GetSection(KubernetesConfigs.SectionName));
builder.Services.AddServiceDiscovery();
builder.Services.AddMuonroiObservability(options => options.EnrichWithKubernetes());
`

## Samples
- samples/KubernetesDeployment/
- samples/ServiceDiscovery/


## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
