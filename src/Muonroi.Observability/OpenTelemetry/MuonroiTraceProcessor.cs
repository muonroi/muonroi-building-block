using System.Diagnostics;
using Muonroi.Core.Abstractions.Exceptions;
using OpenTelemetry;

namespace Muonroi.Observability.OpenTelemetry;

/// <summary>
/// Custom OpenTelemetry processor for tagging spans with Muonroi-specific exception data.
/// </summary>
public sealed class MuonroiTraceProcessor : BaseProcessor<Activity>
{
    /// <inheritdoc />
    public override void OnEnd(Activity activity)
    {
        // Try to find MException in activity tags or events if possible,
        // but typically we tag manually where the exception is caught.
        // This processor can also be used for general enrichment.
        base.OnEnd(activity);
    }

    /// <summary>
    /// Helper to tag an activity with MException details.
    /// </summary>
    /// <param name="activity">The activity to tag.</param>
    /// <param name="ex">The exception.</param>
    public static void TagException(Activity? activity, Exception ex)
    {
        if (activity == null) return;

        if (ex is MException mex)
        {
            activity.SetTag("exception.category", mex.Category.ToString());
            activity.SetTag("exception.error_code", mex.ErrorCode);
        }
    }
}
