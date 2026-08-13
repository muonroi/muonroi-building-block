using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Messaging.Abstractions.Contracts;
using Muonroi.Core.Abstractions.Exceptions;
using Headers = MassTransit.Headers;

namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Applies rule-driven routing decisions to consumed messages before the consumer pipeline continues.
/// </summary>
/// <typeparam name="T">The consumed message type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="RuleEngineRoutingFilter{T}"/> class.
/// </remarks>
/// <param name="routers">The DI-registered explicit message routers.</param>
/// <param name="legacyRoutingRules">The legacy pass/fail routing rules.</param>
/// <param name="sendEndpointProvider">The send endpoint provider used for redirects.</param>
/// <param name="configs">The message bus routing configuration.</param>
/// <param name="routingExpressionEvaluator">The routing expression evaluator.</param>
/// <param name="executionContextAccessor">The execution context accessor for tenant fallback.</param>
/// <param name="dynamicRoutingTableStore">The dynamic routing table store to look up routing definitions.</param>
public sealed class RuleEngineRoutingFilter<T>(
    IEnumerable<IMessageRouter<T>> routers,
    IEnumerable<IMessageRoutingRule<T>> legacyRoutingRules,
    ISendEndpointProvider sendEndpointProvider,
    IOptions<MessageBusConfigs> configs,
    IRoutingExpressionEvaluator? routingExpressionEvaluator = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null,
    IDynamicRoutingTableStore? dynamicRoutingTableStore = null) : IFilter<ConsumeContext<T>>
    where T : class
{
    private static readonly string[] FallbackHeaderKeys =
    [
        CustomHeader.TenantId,
        CustomHeader.CorrelationId,
        CustomHeader.ApiKey,
        CustomHeader.SourceType,
        CustomHeader.SentAt,
        ClaimConstants.UserIdentifier,
        ClaimConstants.Username,
        ClaimConstants.AccessToken
    ];

    private readonly IEnumerable<IMessageRouter<T>> _routers = MGuard.NotNull(routers);
    private readonly IEnumerable<IMessageRoutingRule<T>> _legacyRoutingRules = MGuard.NotNull(legacyRoutingRules);
    private readonly ISendEndpointProvider _sendEndpointProvider = MGuard.NotNull(sendEndpointProvider);
    private readonly MessageBusConfigs _configs = MGuard.NotNull(configs).Value;
    private readonly IRoutingExpressionEvaluator? _routingExpressionEvaluator = routingExpressionEvaluator;
    private readonly ISystemExecutionContextAccessor? _executionContextAccessor = executionContextAccessor;
    private readonly IDynamicRoutingTableStore? _dynamicRoutingTableStore = dynamicRoutingTableStore;

    /// <inheritdoc />
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (!_configs.EnableRuleEngineRouting)
        {
            await next.Send(context);
            return;
        }

        RoutingContext routingContext = BuildRoutingContext(context);
        IReadOnlyList<KeyValuePair<string, object?>> headerPairs = ExtractHeaderPairs(context.Headers);
        IReadOnlyList<IMessageRouter<T>> routers = await ResolveRoutersAsync(context.Message, routingContext, context.CancellationToken);

        foreach (IMessageRouter<T> router in routers)
        {
            IRoutingDecision decision = await router.RouteAsync(context.Message, routingContext, context.CancellationToken)
                ?? RoutingDecision.PassThrough;

            if (decision.Reject)
            {
                RoutingRejectedException exception = new(decision.Reason ?? "Message rejected by routing rule.");
                await context.NotifyFaulted(TimeSpan.Zero, nameof(RuleEngineRoutingFilter<T>), exception);
                throw exception;
            }

            if (!string.IsNullOrWhiteSpace(decision.TargetAddress))
            {
                ISendEndpoint endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri(decision.TargetAddress));
                await endpoint.Send(
                    context.Message,
                    sendContext => CopyHeaders(headerPairs, sendContext.Headers),
                    context.CancellationToken);
                return;
            }
        }

        await next.Send(context);
    }

    /// <inheritdoc />
    public void Probe(ProbeContext context)
    {
    }

    private async Task<IReadOnlyList<IMessageRouter<T>>> ResolveRoutersAsync(
        T message,
        RoutingContext routingContext,
        CancellationToken cancellationToken)
    {
        List<IMessageRouter<T>> routers = [];

        if (_configs.EnableRedisRoutingTable &&
            _dynamicRoutingTableStore != null &&
            !string.IsNullOrWhiteSpace(routingContext.TenantId))
        {
            IReadOnlyList<RoutingTableEntry> routes = await _dynamicRoutingTableStore.GetRoutesAsync(
                routingContext.MessageType,
                routingContext.TenantId,
                cancellationToken);
            routers.AddRange(routes.Select(route => new RedisRoutingTableRouter(route, message, routingContext, _routingExpressionEvaluator)));
        }

        routers.AddRange(_routers);
        routers.AddRange(_legacyRoutingRules.Select(static rule => new LegacyRoutingRuleAdapter(rule)));

        return routers
            .OrderBy(static router => router.Order)
            .ThenBy(static router => router.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private RoutingContext BuildRoutingContext(ConsumeContext<T> context)
    {
        IReadOnlyList<KeyValuePair<string, object?>> headerPairs = ExtractHeaderPairs(context.Headers);
        IReadOnlyDictionary<string, string> headers = headerPairs
            .Where(static pair => pair.Value != null)
            .GroupBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => Convert.ToString(group.Last().Value) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        string? tenantId = ResolveHeaderValue(headers, CustomHeader.TenantId)
            ?? _executionContextAccessor?.Get().TenantId;
        string correlationId = ResolveHeaderValue(headers, CustomHeader.CorrelationId)
            ?? _executionContextAccessor?.Get().CorrelationId
            ?? Guid.NewGuid().ToString("N");
        string messageType = context.Message.GetType().FullName ?? typeof(T).Name;

        return new RoutingContext(tenantId, correlationId, messageType, headers);
    }

    private static string? ResolveHeaderValue(IReadOnlyDictionary<string, string> headers, string key)
    {
        return headers.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> ExtractHeaderPairs(Headers? headers)
    {
        if (headers == null)
        {
            return [];
        }

        List<KeyValuePair<string, object?>> pairs = [];
        if (headers is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                if (TryCreatePair(item, out KeyValuePair<string, object?> pair))
                {
                    pairs.Add(pair);
                }
            }
        }

        if (pairs.Count > 0)
        {
            return pairs;
        }

        foreach (string key in FallbackHeaderKeys)
        {
            if (headers.TryGetHeader(key, out object? value) && value != null)
            {
                pairs.Add(new KeyValuePair<string, object?>(key, value));
            }
        }

        return pairs;
    }

    private static bool TryCreatePair(object? item, out KeyValuePair<string, object?> pair)
    {
        pair = default;
        if (item == null)
        {
            return false;
        }

        Type itemType = item.GetType();
        PropertyInfo? keyProperty = itemType.GetProperty("Key");
        PropertyInfo? valueProperty = itemType.GetProperty("Value");
        if (keyProperty == null || valueProperty == null)
        {
            return false;
        }

        string? key = keyProperty.GetValue(item)?.ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        pair = new KeyValuePair<string, object?>(key, valueProperty.GetValue(item));
        return true;
    }

    private static void CopyHeaders(
        IReadOnlyList<KeyValuePair<string, object?>> headerPairs,
        SendHeaders headers)
    {
        foreach (KeyValuePair<string, object?> pair in headerPairs)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
            {
                headers.Set(pair.Key, pair.Value);
            }
        }
    }

    /// <summary>
    /// Runtime routing context built from the MassTransit consume envelope.
    /// </summary>
    /// <param name="TenantId">The tenant identifier.</param>
    /// <param name="CorrelationId">The correlation identifier.</param>
    /// <param name="MessageType">The CLR message type name.</param>
    /// <param name="Headers">The normalized string headers.</param>
    private sealed record RoutingContext(
        string? TenantId,
        string CorrelationId,
        string MessageType,
        IReadOnlyDictionary<string, string> Headers) : IRoutingContext;

    /// <summary>
    /// Adapts legacy routing rules into the new decision-based routing contract.
    /// </summary>
    /// <remarks>
    /// Initializes a new adapter instance.
    /// </remarks>
    /// <param name="rule">The legacy routing rule.</param>
    private sealed class LegacyRoutingRuleAdapter(IMessageRoutingRule<T> rule) : IMessageRouter<T>
    {
        private readonly IMessageRoutingRule<T> _rule = MGuard.NotNull(rule);

        /// <inheritdoc />
        public int Order => _rule.Order;

        /// <inheritdoc />
        public string Code => _rule.Code;

        /// <inheritdoc />
        public async Task<IRoutingDecision> RouteAsync(T message, IRoutingContext context, CancellationToken ct = default)
        {
            await _rule.ExecuteAsync(message, ct);
            return RoutingDecision.PassThrough;
        }
    }

    /// <summary>
    /// Adapts Redis routing table entries into executable routers.
    /// </summary>
    /// <remarks>
    /// Initializes a new adapter instance.
    /// </remarks>
    /// <param name="entry">The Redis routing entry.</param>
    /// <param name="message">The message used to build FEEL variables.</param>
    /// <param name="context">The routing context used to build FEEL variables.</param>
    /// <param name="evaluator">The FEEL expression evaluator.</param>
    private sealed class RedisRoutingTableRouter(RoutingTableEntry entry, T message, IRoutingContext context, IRoutingExpressionEvaluator? evaluator) : IMessageRouter<T>
    {
        private readonly RoutingTableEntry _entry = MGuard.NotNull(entry);
        private readonly Func<IDictionary<string, object>, bool>? _predicate = string.IsNullOrWhiteSpace(entry.FeelExpression) || evaluator == null ? null : evaluator.Compile(entry.FeelExpression);
        private readonly IDictionary<string, object> _variables = BuildFeelVariables(message, context);

        /// <inheritdoc />
        public int Order => _entry.Order;

        /// <inheritdoc />
        public string Code => _entry.RuleCode;

        /// <inheritdoc />
        public Task<IRoutingDecision> RouteAsync(T message, IRoutingContext context, CancellationToken ct = default)
        {
            if (!_entry.IsActive)
            {
                return Task.FromResult(RoutingDecision.PassThrough);
            }

            if (_predicate != null && !_predicate(_variables))
            {
                return Task.FromResult(RoutingDecision.PassThrough);
            }

            return Task.FromResult(RoutingDecision.RedirectTo(_entry.TargetAddress, $"Dynamic route '{_entry.RuleCode}' matched."));
        }

        private static IDictionary<string, object> BuildFeelVariables(T message, IRoutingContext context)
        {
            Dictionary<string, object> variables = new(StringComparer.OrdinalIgnoreCase)
            {
                ["tenantId"] = context.TenantId ?? string.Empty,
                ["correlationId"] = context.CorrelationId,
                ["messageType"] = context.MessageType,
                ["headers"] = context.Headers
            };

            if (message is IDictionary<string, object> dictionary)
            {
                foreach (KeyValuePair<string, object> pair in dictionary)
                {
                    variables[pair.Key] = pair.Value;
                }
            }

            foreach (PropertyInfo property in message.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    variables[property.Name] = property.GetValue(message) ?? string.Empty;
                }
            }

            return variables;
        }
    }
}
