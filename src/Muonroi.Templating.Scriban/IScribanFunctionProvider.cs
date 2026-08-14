namespace Muonroi.Templating.Scriban;

/// <summary>
/// Interface for registering custom Scriban functions.
/// Implementations are discovered via DI and their functions are registered
/// into the Scriban template context before rendering.
/// </summary>
public interface IScribanFunctionProvider
{
    /// <summary>
    /// Registers custom functions into the provided <see cref="ScriptObject"/>.
    /// </summary>
    /// <param name="scriptObject">The Scriban script object to register functions into.</param>
    void Register(ScriptObject scriptObject);
}
