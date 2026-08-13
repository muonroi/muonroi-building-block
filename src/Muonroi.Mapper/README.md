# Muonroi.Mapper

## Description
Provides high-performance object-to-object mapping capabilities for the Muonroi ecosystem, allowing seamless translation between domain models, DTOs, and view models.

## Features
- Fast and reliable object mapping.
- Integration with `Muonroi.Mapping.Abstractions`.
- Support for complex nested mappings and custom resolvers.

## Minimal Usage
```csharp
var mapper = serviceProvider.GetRequiredService<IMapper>();
var userDto = mapper.Map<UserDto>(userEntity);
```
