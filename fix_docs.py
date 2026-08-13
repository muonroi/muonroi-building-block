import os
import re

file_path = 'src/Muonroi.Core.Abstractions/Guards/MGuard.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

new_found_with_docs = """    /// <summary>
    /// Ensures the entity was found. Returns the non-null value or throws <see cref="MNotFoundException"/>.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="value">The entity value to check.</param>
    /// <param name="entityName">The name of the entity for diagnostics.</param>
    /// <param name="key">The key of the entity.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    /// <returns>The non-null entity value.</returns>
    /// <exception cref="MNotFoundException">Thrown when <paramref name="value"/> is null.</exception>
    public static T Found<T>(
        T? value,
        string entityName,
        object key,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0) where T : class
    {
        if (value is null)
        {
            throw new MNotFoundException(entityName, key)
            {
                CallerMethod = callerMember,
                CallerFile = callerFile,
                CallerLine = callerLine,
                SourcePackage = MException.ExtractPackageName(callerFile)
            };
        }

        return value;
    }

    public static T Found<T>("""

content = content.replace(
"""    public static T Found<T>(
        T? value,
        string entityName,
        object key,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0) where T : class
    {
        if (value is null)
        {
            throw new MNotFoundException(entityName, key)
            {
                CallerMethod = callerMember,
                CallerFile = callerFile,
                CallerLine = callerLine,
                SourcePackage = MException.ExtractPackageName(callerFile)
            };
        }

        return value;
    }

    public static T Found<T>(""",
new_found_with_docs
)

with open(file_path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)
