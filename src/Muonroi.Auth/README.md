# Muonroi.Auth
> Core primitives for Muonroi.Auth in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Auth.svg)](https://www.nuget.org/packages/Muonroi.Auth/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.Auth provides comprehensive authentication mechanisms including JWT generation, token revocation, and WebAuthn support. It features core services like `JwtService` and `WebAuthnService`, alongside security components like `BCryptPasswordHasher`.

## Features

- **Token Management**: Issue and validate tokens with `JwtService` and track revocation via `RedisTokenRevocationStore`.
- **Modern Authentication**: Support passkeys and biometric logins through `WebAuthnService`.
- **Advanced Security**: Implement Demonstrating Proof-of-Possession (DPoP) using `DPoPBindingService`.

## Quick Start

```csharp
using Muonroi.Auth;

builder.Services.AddAuthServices();
```

## Installation

```bash
dotnet add package Muonroi.Auth
```

## Ecosystem Combinations

Combine with `Muonroi.Bff` to implement secure session-based authentication for Single Page Applications, storing JWTs issued by `JwtService` securely server-side instead of in browser storage.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.Auth components.
