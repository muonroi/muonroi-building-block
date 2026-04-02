using System.Collections;
using System.Runtime.CompilerServices;
using Muonroi.Core.Abstractions.Exceptions;

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
        [CallerArgumentExpression("value")] string? paramName = null,
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
        [CallerArgumentExpression("value")] string? paramName = null,
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
        [CallerArgumentExpression("value")] string? paramName = null,
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
        [CallerArgumentExpression("value")] string? paramName = null,
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
        [CallerArgumentExpression("value")] string? paramName = null,
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
            throw new MArgumentException("value", errorMessage, callerMember, callerFile, callerLine);
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
            throw new MArgumentException("condition", errorMessage, callerMember, callerFile, callerLine);
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
        [CallerArgumentExpression("value")] string? paramName = null,
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
        [CallerArgumentExpression("value")] string? paramName = null,
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
        [CallerArgumentExpression("value")] string? paramName = null,
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
        [CallerArgumentExpression("value")] string? paramName = null,
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
}
