# Muonroi.Data.EntityFrameworkCore.Events

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.Events.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.Events/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.Data.EntityFrameworkCore.Events.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.Events/)

> Event-driven inbox and outbox mechanisms using EF Core.

## Overview
Defines outbox and inbox mechanisms via `MEventOutboxDbContext` and `MessageInbox`, ensuring reliable messaging integration through `MDbContextOutboxExtensions`.

## Features
- **Event Outbox**: Utilizes `MEventOutboxDbContext` and `MDbContextOutboxExtensions` for reliable transactional messaging.
- **Message Inbox**: Employs `MessageInbox` and `EfCoreMessageInboxStore` for idempotent message consumption.
- **Saga Support**: Integrates `MSagaDbContext` and `MuonroiSagaServiceCollectionExtensions` for long-running processes.

## Installation
```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.Events
```

## Quick Start
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMuonroiSagaServices();
```

## Ecosystem Combinations
- **With Muonroi.Data.EntityFrameworkCore**: Extends standard `MDbContext` with `MDbContextOutboxExtensions`.
- **Full Stack Example**:
```csharp
builder.Services.AddMuonroiSagaServices()
                .AddDbContext<MEventOutboxDbContext>();
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
