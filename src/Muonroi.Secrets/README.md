# Muonroi.Secrets
> Lightweight abstraction for secret retrieval in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Secrets.svg)](https://www.nuget.org/packages/Muonroi.Secrets/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

`Muonroi.Secrets` provides a simple, standard abstraction for reading secure configuration values across Muonroi Building Block applications. It standardizes secret retrieval through the `ISecretProvider` interface, allowing application logic to depend on an agnostic source of truth for sensitive information like connection strings, API keys, and cryptographic salts.

By default, this package includes a `ConfigurationSecretProvider` that reads directly from the ASP.NET Core `IConfiguration` root. In more complex environments (like Kubernetes or AWS), this interface allows easy swapping to dedicated providers (like HashiCorp Vault, AWS Secrets Manager, or Azure Key Vault) without rewriting consumer logic.

## Features

- **Standard Abstraction**: Clean `ISecretProvider` contract for reading named secrets.
- **Built-in Implementation**: Includes `ConfigurationSecretProvider` that bridges `IConfiguration` natively out of the box.
- **Environment Agnostic**: Perfect for transitioning an application from local `secrets.json` or environment variables to a cloud-native secret store.

## Installation

```bash
dotnet add package Muonroi.Secrets
```

## Quick Start

### 1. Registration

Register the built-in `ConfigurationSecretProvider` in your Dependency Injection container.

```csharp
using Muonroi.Secrets.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Register the provider, passing in the existing IConfiguration
builder.Services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
```

### 2. Usage

Inject `ISecretProvider` wherever you need to access secure configuration data securely.

```csharp
using Muonroi.Secrets.Secrets;

public class PaymentGatewayClient
{
    private readonly string _apiKey;

    public PaymentGatewayClient(ISecretProvider secretProvider)
    {
        // Fetch the secret key safely
        _apiKey = secretProvider.GetSecret("PaymentGateway:ApiKey") 
            ?? throw new InvalidOperationException("API key is missing!");
    }

    public async Task ProcessPaymentAsync(decimal amount)
    {
        // Use _apiKey for authentication...
    }
}
```

## API Reference

### `ISecretProvider`

The core contract for the package.

```csharp
namespace Muonroi.Secrets.Secrets
{
    public interface ISecretProvider
    {
        string? GetSecret(string name);
    }
}
```

### `ConfigurationSecretProvider`

A lightweight implementation that simply delegates the secret lookup to the `IConfiguration` indexer.

```csharp
public class ConfigurationSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public string? GetSecret(string name)
    {
        return configuration[name];
    }
}
```

## Advanced Use Cases

If you need to fetch secrets from an external cloud provider (e.g., AWS Secrets Manager), you can easily implement a custom `ISecretProvider` and replace the DI registration.

```csharp
public class AwsSecretProvider : ISecretProvider 
{
    public string? GetSecret(string name)
    {
        // Call AWS SDK to fetch secret synchronously, or implement caching for async fetching
    }
}
```

## Ecosystem Combinations

> Works great standalone. Becomes **significantly more powerful** when combined.

### + Auth -> JWT signing keys fetched via ISecretProvider (not hardcoded in config)
Securely fetch your signing keys dynamically.

### + Governance -> License activation keys resolved via ISecretProvider
Retrieve the RSA public keys or activation tokens needed for ILicenseGuard.

### + Tenancy -> Per-tenant secrets: provider.GetAsync($"tenants/{tenantId}/db-password")
Safely isolate and fetch database credentials or API keys on a per-tenant basis.

### + Kubernetes -> KubernetesSecretProvider reads from K8s secrets volume
When running in Kubernetes, map the secret provider to native K8s secret volumes.

### + Data.EntityFrameworkCore -> Connection strings fetched from secrets, not appsettings
Inject the secret provider into your DbContext factory to resolve database connection strings securely.

### Full Stack
`csharp
// combined registration
builder.Services.AddSingleton<ISecretProvider, KubernetesSecretProvider>();
builder.Services.AddMuonroiAuth();
builder.Services.AddTenantContext();
`

## Samples
- samples/MultiTenantSaaS/
- samples/KubernetesDeployment/


## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
