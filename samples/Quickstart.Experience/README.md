> Quickstart demonstrating the Experience Engine capabilities.

## What This Sample Demonstrates
- Using `IExperienceStore` with a file-based backing (`FileExperienceStore`)
- Mocking `IExperienceBrain` to simulate extraction and abstraction
- `NeuronExperience` entity handling
- Utilizing `MistakeDetector` to identify patterns in logs

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Experience/src/Quickstart.Experience.Api
dotnet run
```

Then open http://localhost:5000/swagger.

## Key Files
- `Program.cs` — Configures file paths and registers the mock brain and file store
- `Controllers/ExperienceController.cs` — API to trigger mistake detection and search

## How It Works
The engine stores experiences in the `data/experiences` folder separated by tiers. A mock brain provides deterministic extracted experiences for a given trajectory.
