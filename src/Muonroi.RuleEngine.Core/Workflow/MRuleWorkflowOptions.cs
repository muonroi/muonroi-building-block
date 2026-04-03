namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Runtime options for workflow execution safeguards.
/// </summary>
public sealed class MRuleWorkflowOptions
{
    /// <summary>
    /// Maximum number of steps that can execute in a single workflow run.
    /// Used to prevent infinite loops caused by cyclic transitions.
    /// </summary>
    public int MaxSteps { get; set; } = 256;
}

