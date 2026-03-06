# Contributing to Muonroi Building Block

Thank you for taking the time to contribute! This guide helps you set up the project locally and submit changes following the project's conventions.

## Fork and Clone
1. Fork the repository on GitHub and clone your fork:
   ```bash
   git clone https://github.com/<your-username>/MuonroiBuildingBlock.git
   cd MuonroiBuildingBlock
   ```
2. Install the .NET SDK 9.0 or later if you haven't already.
3. Restore dependencies and build the solution:
   ```bash
   dotnet restore
   dotnet build
   ```

## Run the Project Locally
You can explore the library using the samples in the `Samples` folder. For example:
```bash
dotnet run --project Samples/Samples.csproj
```
Feel free to adapt the command to any sample you are interested in.

## Running Tests
Before submitting changes, ensure all tests pass:
```bash
dotnet test
```
This command runs the full test suite. No additional configuration is required.

## Code Style
The repository uses an `.editorconfig` to define coding conventions. Please format your code before committing:
```bash
dotnet format Muonroi.BuildingBlock.sln
```

## Pull Request Process
1. Create a topic branch from `main` and make your changes there.
2. Run `dotnet format` and `dotnet test` before committing.
3. Push your branch and open a Pull Request.
4. Ensure your PR description explains the motivation and links any related issues.
5. All commits must pass CI and at least one code review before being merged.

## Feature Ideas
Check the issue tracker for items labeled `help wanted` or `good first issue`. If you have an idea for a new feature or enhancement, please open an issue to discuss it before starting work.

Happy coding!
