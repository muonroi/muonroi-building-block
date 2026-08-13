import os

def replace_in_file(path, old, new):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    content = content.replace(old, new)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)

# CanaryRolloutService.cs
replace_in_file('src/Muonroi.RuleEngine.EntityFrameworkCore/Rules/CanaryRolloutService.cs',
    'cancellationToken) ?? throw new MNotFoundException("RuleSetVersion", $"{workflow}/v{request.Version}");',
    'cancellationToken);\n        MGuard.Found(rule, "RuleSetVersion", $"{workflow}/v{request.Version}");')

replace_in_file('src/Muonroi.RuleEngine.EntityFrameworkCore/Rules/CanaryRolloutService.cs',
    'cancellationToken)\n            ?? throw new MNotFoundException("CanaryRollout", rolloutId);',
    'cancellationToken);\n        MGuard.Found(rollout, "CanaryRollout", rolloutId);')
    
# PostgresRuleSetStore.cs
replace_in_file('src/Muonroi.RuleEngine.EntityFrameworkCore/Rules/PostgresRuleSetStore.cs',
    '''        if (target is null)
        {
            throw new MNotFoundException("RuleSetVersion", $"{normalizedWorkflow}/v{version}");
        }''',
    '''        MGuard.Found(target, "RuleSetVersion", $"{normalizedWorkflow}/v{version}");''')

# RuleSetApprovalService.cs
replace_in_file('src/Muonroi.RuleEngine.EntityFrameworkCore/Rules/RuleSetApprovalService.cs',
    'cancellationToken)\n            ?? throw new MNotFoundException("RuleSetVersion", $"{workflow}/v{version}");',
    'cancellationToken);\n        MGuard.Found(target, "RuleSetVersion", $"{workflow}/v{version}");')
    
# ScenarioExecutor.cs
replace_in_file('src/Muonroi.RuleEngine.Proliferation/Execution/ScenarioExecutor.cs',
    '''            throw new MNotFoundException("Ruleset", workflowName);''',
    '''            MGuard.Found<object>(null, "Ruleset", workflowName);''')

# FileRuleSetStore.cs
replace_in_file('src/Muonroi.RuleEngine.Runtime/Rules/FileRuleSetStore.cs',
    '''        if (!File.Exists(path)) throw new MNotFoundException("RuleSetVersion", path);''',
    '''        MGuard.Found(File.Exists(path) ? new object() : null, "RuleSetVersion", path);''')
