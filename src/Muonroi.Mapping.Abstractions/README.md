# Muonroi.Mapping.Abstractions

## Description
Contains the core interfaces and abstractions for mapping operations in the Muonroi ecosystem.

## Features
- Standardized mapping interfaces (`IMapper`).
- Decouples mapping logic from specific implementations.
- Lightweight and dependency-free.

## Minimal Usage
```csharp
public class MyService
{
    private readonly IMapper _mapper;
    public MyService(IMapper mapper) => _mapper = mapper;
}
```
