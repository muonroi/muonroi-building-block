using Muonroi.Core.Abstractions.Exceptions;
using System.Collections;
using System.Runtime.CompilerServices;

namespace Muonroi.Core.Abstractions.Guards;

/// <summary>
/// Static guard utility for common precondition validations.
/// </summary>
public static class MGuard
{
    /// <summary>
    /// Ensures the specified value is not null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <returns>The non-null value.</returns>
    /// <exception cref="MArgumentException">Thrown when value is null.</exception>
    public static T NotNull<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        where T : class
    {
        if (value is null)
        {
            throw new MArgumentException(paramName ?? "value", $"{paramName} cannot be null.", callerMember, callerFile, callerLine);
        }

        return value;
    }

    /// <summary>
    /// Ensures the specified nullable struct value is not null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <returns>The non-null value.</returns>
    /// <exception cref="MArgumentException">Thrown when value is null.</exception>
    public static T NotNull<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        where T : struct
    {
        if (!value.HasValue)
        {
            throw new MArgumentException(paramName ?? "value", $"{paramName} cannot be null.", callerMember, callerFile, callerLine);
        }

        return value.Value;
    }

    /// <summary>
    /// Ensures the specified string is not null or whitespace.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <returns>The non-empty string.</returns>
    /// <exception cref="MArgumentException">Thrown when value is null or whitespace.</exception>
    public static string NotEmpty(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new MArgumentException(paramName ?? "value", $"{paramName} cannot be null or empty.", callerMember, callerFile, callerLine);
        }

        return value;
    }

    /// <summary>
    /// Ensures the specified collection is not null or empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection.</typeparam>
    /// <param name="value">The collection to check.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <returns>The non-empty collection.</returns>
    /// <exception cref="MArgumentException">Thrown when value is null or empty.</exception>
    public static T NotEmpty<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        where T : IEnumerable
    {
        if (value is null)
        {
            throw new MArgumentException(paramName ?? "value", $"{paramName} cannot be null.", callerMember, callerFile, callerLine);
        }

        var enumerator = value.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new MArgumentException(paramName ?? "value", $"{paramName} cannot be empty.", callerMember, callerFile, callerLine);
        }

        return value;
    }

    /// <summary>
    /// Ensures the specified value is not the default value of its type.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <returns>The non-default value.</returns>
    /// <exception cref="MArgumentException">Thrown when value is the default for its type.</exception>
    public static T NotDefault<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
        {
            throw new MArgumentException(paramName ?? "value", $"{paramName} cannot be its default value.", callerMember, callerFile, callerLine);
        }

        return value;
    }

    /// <summary>
    /// Ensures the specified value satisfies a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="predicate">The predicate to satisfy.</param>
    /// <param name="errorMessage">The error message if the predicate is not satisfied.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <exception cref="MArgumentException">Thrown when the predicate returns false.</exception>
    public static void Satisfies<T>(
        T value,
        Func<T, bool> predicate,
        string errorMessage,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (!predicate(value))
        {
            throw new MArgumentException(nameof(value), errorMessage, callerMember, callerFile, callerLine);
        }
    }

    /// <summary>
    /// Guards against a condition being true.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="errorMessage">The error message if the condition is true.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <exception cref="MArgumentException">Thrown when the condition is true.</exception>
    public static void Against(
        bool condition,
        string errorMessage,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (condition)
        {
            throw new MArgumentException(nameof(condition), errorMessage, callerMember, callerFile, callerLine);
        }
    }

    /// <summary>
    /// Ensures an integer value is within a specified range.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="min">The minimum allowed value.</param>
    /// <param name="max">The maximum allowed value.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <returns>The value if it is within range.</returns>
    /// <exception cref="MArgumentException">Thrown when the value is outside the range.</exception>
    public static int InRange(
        int value,
        int min,
        int max,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (value < min || value > max)
        {
            throw new MArgumentException(paramName ?? "value", $"{paramName} must be between {min} and {max}, but was {value}.", callerMember, callerFile, callerLine);
        }

        return value;
    }

    /// <summary>
    /// Ensures a long value is within a specified range.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="min">The minimum allowed value.</param>
    /// <param name="max">The maximum allowed value.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <returns>The value if it is within range.</returns>
    /// <exception cref="MArgumentException">Thrown when the value is outside the range.</exception>
    public static long InRange(
        long value,
        long min,
        long max,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (value < min || value > max)
        {
            throw new MArgumentException(paramName ?? "value", $"{paramName} must be between {min} and {max}, but was {value}.", callerMember, callerFile, callerLine);
        }

        return value;
    }

    /// <summary>
    /// Ensures a string's length does not exceed a maximum length.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="maxLength">The maximum allowed length.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <returns>The string value.</returns>
    /// <exception cref="MArgumentException">Thrown when the string length exceeds the maximum length.</exception>
    public static string? MaxLength(
        string? value,
        int maxLength,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (value != null && value.Length > maxLength)
        {
            throw new MArgumentException(paramName ?? "value", $"{paramName} length cannot exceed {maxLength}.", callerMember, callerFile, callerLine);
        }

        return value;
    }

    /// <summary>
    /// Ensures an enum value is valid for its type.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The enum value to check.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    /// <returns>The valid enum value.</returns>
    /// <exception cref="MArgumentException">Thrown when the enum value is not defined.</exception>
    public static TEnum ValidEnum<TEnum>(
        TEnum value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            throw new MArgumentException(paramName ?? "value", $"{value} is not a valid value for enum {typeof(TEnum).Name}.", callerMember, callerFile, callerLine);
        }

        return value;
    }

    /// <summary>
    /// Asserts an internal state invariant. Throws <see cref="MInternalException"/> when the condition is false.
    /// Use for "should never happen" checks and internal assertions.
    /// Caller context (method, file, line) is automatically captured and propagated to the exception.
    /// </summary>
    /// <param name="condition">The condition that must be true.</param>
    /// <param name="errorMessage">The error message if the condition is false.</param>
    /// <param name="errorCode">Optional specific error code (defaults to INTERNAL_ERROR).</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    /// <exception cref="MInternalException">Thrown when the condition is false.</exception>
    public static void State(
        bool condition,
        string errorMessage,
        string? errorCode = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (!condition)
        {
            throw new MInternalException(errorMessage, errorCode, callerMember, callerFile, callerLine);
        }
    }

    /// <summary>
    /// Ensures a required configuration value is present.
    /// Throws <see cref="MConfigurationException"/> when the value is null.
    /// Caller context (method, file, line) is automatically captured and propagated to the exception.
    /// </summary>
    /// <typeparam name="T">The type of the configuration value.</typeparam>
    /// <param name="value">The configuration value to check.</param>
    /// <param name="configKey">The configuration key or description for diagnostics.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    /// <returns>The non-null configuration value.</returns>
    /// <exception cref="MConfigurationException">Thrown when the value is null.</exception>
    public static T Configured<T>(
        T? value,
        string configKey,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0) where T : class
    {
        if (value is null)
        {
            throw new MConfigurationException($"Required configuration '{configKey}' is missing or null.", configKey, callerMember, callerFile, callerLine);
        }

        return value;
    }

    /// <summary>
    /// Ensures a required configuration string is present and not empty.
    /// Throws <see cref="MConfigurationException"/> when the value is null or whitespace.
    /// Caller context (method, file, line) is automatically captured and propagated to the exception.
    /// </summary>
    /// <param name="value">The configuration string to check.</param>
    /// <param name="configKey">The configuration key or description for diagnostics.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    /// <returns>The non-empty configuration string.</returns>
    /// <exception cref="MConfigurationException">Thrown when the value is null or whitespace.</exception>
    public static string Configured(
        string? value,
        string configKey,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new MConfigurationException($"Required configuration '{configKey}' is missing or empty.", configKey, callerMember, callerFile, callerLine);
        }

        return value;
    }

    /// <summary>
    /// Ensures the caller is authenticated. Throws <see cref="MUnauthorizedException"/> when not.
    /// </summary>
    /// <param name="isAuthenticated">Whether the caller is authenticated.</param>
    /// <param name="message">The error message if not authenticated.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    /// <exception cref="MUnauthorizedException">Thrown when <paramref name="isAuthenticated"/> is false.</exception>
    public static void Authorized(
        bool isAuthenticated,
        string message,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (!isAuthenticated)
        {
            throw new MUnauthorizedException(message, callerMember, callerFile, callerLine);
        }
    }

    /// <summary>
    /// Ensures the caller has required permissions. Throws <see cref="MForbiddenException"/> when not.
    /// </summary>
    /// <param name="hasPermission">Whether the caller has permission.</param>
    /// <param name="message">The error message if not permitted.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    /// <exception cref="MForbiddenException">Thrown when <paramref name="hasPermission"/> is false.</exception>
    public static void Permitted(
        bool hasPermission,
        string message,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (!hasPermission)
        {
            throw new MForbiddenException(message, callerMember, callerFile, callerLine);
        }
    }

    /// <summary>
    /// Ensures the entity was found. Returns the non-null value or throws <see cref="MNotFoundException"/>.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="value">The entity value to check.</param>
    /// <param name="entityName">The name of the entity for diagnostics.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    /// <returns>The non-null entity value.</returns>
    /// <exception cref="MNotFoundException">Thrown when <paramref name="value"/> is null.</exception>
    public static T Found<T>(
        T? value,
        string entityName,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0) where T : class
    {
        if (value is null)
        {
            throw new MNotFoundException(entityName, "(not specified)")
            {
                CallerMethod = callerMember,
                CallerFile = callerFile,
                CallerLine = callerLine,
                SourcePackage = MException.ExtractPackageName(callerFile)
            };
        }

        return value;
    }

    /// <summary>
    /// Ensures no conflict exists. Throws <see cref="MConflictException"/> when conflict is true.
    /// </summary>
    /// <param name="isConflict">Whether a conflict exists.</param>
    /// <param name="message">The error message if conflict exists.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    /// <exception cref="MConflictException">Thrown when <paramref name="isConflict"/> is true.</exception>
    public static void NotConflict(
        bool isConflict,
        string message,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (isConflict)
        {
            throw new MConflictException(message)
            {
                CallerMethod = callerMember,
                CallerFile = callerFile,
                CallerLine = callerLine,
                SourcePackage = MException.ExtractPackageName(callerFile)
            };
        }
    }

    /// <summary>
    /// Ensures a tenant ID is present and valid. Returns the non-empty value or throws <see cref="MArgumentException"/>.
    /// </summary>
    /// <param name="tenantId">The tenant ID to validate.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    /// <returns>The validated tenant ID.</returns>
    /// <exception cref="MArgumentException">Thrown when <paramref name="tenantId"/> is null, empty, or whitespace.</exception>
    public static string ValidTenant(
        string? tenantId,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new MArgumentException("tenantId", "Tenant ID is required and cannot be empty.", callerMember, callerFile, callerLine);
        }

        return tenantId;
    }
}
