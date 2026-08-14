namespace Muonroi.RuleEngine.Runtime.Compilation.JavaScript;

/// <summary>
/// Compiles JavaScript expressions into cached delegates using the Jint interpreter.
/// Sandboxed: no require, no filesystem, no network access.
/// </summary>
public static class JintExpressionCompiler
{
    private static readonly ConcurrentDictionary<string, Func<IDictionary<string, object?>, CancellationToken, object?>> Cache =
        new(StringComparer.Ordinal);

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Compiles a JavaScript expression into a cached delegate that accepts a CancellationToken
    /// and returns the evaluation result. FactBag variables are injected as global scope.
    /// </summary>
    /// <remarks>
    /// D-01: The delegate creates a linked CancellationTokenSource combining the caller's token
    /// with a 5-second hard deadline. This ensures both caller-initiated cancellation (e.g.,
    /// request abort) and the budget timeout stop JS execution promptly.
    /// </remarks>
    public static Func<IDictionary<string, object?>, CancellationToken, object?> Compile(string jsExpression, TimeSpan? timeout = null)
    {
        string normalized = (jsExpression ?? string.Empty).Trim();
        TimeSpan effectiveTimeout = timeout ?? DefaultTimeout;
        return Cache.GetOrAdd(normalized, expr => BuildDelegate(expr, effectiveTimeout));
    }

    /// <summary>
    /// Compiles a JavaScript expression into a cached delegate that returns a boolean result.
    /// </summary>
    public static Func<IDictionary<string, object>, bool> CompileBoolean(string jsExpression, TimeSpan? timeout = null)
    {
        Func<IDictionary<string, object?>, CancellationToken, object?> inner = Compile(jsExpression, timeout);
        return variables =>
        {
            Dictionary<string, object?> nullable = new(variables.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> kv in variables)
            {
                nullable[kv.Key] = kv.Value;
            }
            return ToBoolean(inner(nullable, CancellationToken.None));
        };
    }

    private static Func<IDictionary<string, object?>, CancellationToken, object?> BuildDelegate(string expression, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return (_, _) => null;
        }

        return (variables, ct) =>
        {
            // D-01: Link caller CT with a hard timeout budget — whichever fires first wins
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            Engine engine = CreateSandboxedEngine(timeout, cts.Token);

            foreach (KeyValuePair<string, object?> kv in variables)
            {
                if (kv.Key.StartsWith("__", StringComparison.Ordinal))
                {
                    continue;
                }

                engine.SetValue(kv.Key, kv.Value ?? JsValue.Null);
            }

            JsValue result = engine.Evaluate(expression);
            return ConvertJsValue(result);
        };
    }

    private static Engine CreateSandboxedEngine(TimeSpan timeout, CancellationToken ct)
    {
        return new Engine(options =>
        {
            options.Strict();
            options.TimeoutInterval(timeout);
            options.MaxStatements(10_000);
            options.LimitMemory(16_000_000); // 16 MB
            options.LimitRecursion(64);
            options.CancellationToken(ct);   // D-01: propagate caller + budget CT to Jint
        });
    }

    private static object? ConvertJsValue(JsValue value)
    {
        if (value.IsNull() || value.IsUndefined())
        {
            return null;
        }

        if (value.IsBoolean())
        {
            return value.AsBoolean();
        }

        if (value.IsNumber())
        {
            return value.AsNumber();
        }

        if (value.IsString())
        {
            return value.AsString();
        }

        if (value.IsArray())
        {
            Jint.Native.Array.ArrayInstance array = value.AsArray();
            List<object?> list = [];
            foreach (JsValue item in array)
            {
                list.Add(ConvertJsValue(item));
            }
            return list;
        }

        if (value.IsObject())
        {
            ObjectInstance obj = value.AsObject();
            Dictionary<string, object?> dict = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<JsValue, PropertyDescriptor> prop in obj.GetOwnProperties())
            {
                string key = prop.Key.AsString();
                dict[key] = ConvertJsValue(obj.Get(prop.Key));
            }
            return dict;
        }

        return value.ToString();
    }

    private static bool ToBoolean(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            double d => Math.Abs(d) > double.Epsilon,
            string s => !string.IsNullOrEmpty(s),
            _ => true
        };
    }
}
