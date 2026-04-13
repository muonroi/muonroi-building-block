using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace Muonroi.RuleGen.Mcp.Prompts;

[McpServerPromptType]
public sealed class CreateRuleFromRequirementsPrompt
{
    [McpServerPrompt(Name = "muonroi_create_rule_from_requirements")]
    public ChatMessage GetPrompt(string requirement, string contextType, string @namespace)
    {
        return new ChatMessage(ChatRole.User,
            $"Requirement: {requirement}\n" +
            $"Context type: {contextType}\n" +
            $"Namespace: {@namespace}\n\n" +
            "Workflow:\n" +
            "1. Read muonroi://ecosystem/rules.\n" +
            "2. Call muonroi_rulegen_translate_feel if the requirement contains FEEL-like conditions.\n" +
            "3. Call muonroi_scaffold_rule_class.\n" +
            "4. Fill business logic only inside the generated skeleton.\n" +
            "5. Call muonroi_compliance_check.\n" +
            "6. If [MExtractAsRule] is present, call muonroi_rulegen_extract and muonroi_rulegen_register.\n" +
            "7. Return the final code only if compliance passes.");
    }
}

[McpServerPromptType]
public sealed class FixMbbViolationsPrompt
{
    [McpServerPrompt(Name = "muonroi_fix_mbb_violations")]
    public ChatMessage GetPrompt(string[] paths)
    {
        string joined = string.Join(", ", paths);
        return new ChatMessage(ChatRole.User,
            $"Scan and fix Muonroi ecosystem violations for: {joined}.\n" +
            "1. Call muonroi_compliance_check.\n" +
            "2. For each violation call muonroi_compliance_suggest_wrapper.\n" +
            "3. Apply fixes.\n" +
            "4. Re-run muonroi_compliance_check.\n" +
            "5. Report fixed and remaining violations.");
    }
}

[McpServerPromptType]
public sealed class ScaffoldNewFeaturePrompt
{
    [McpServerPrompt(Name = "muonroi_scaffold_new_feature")]
    public ChatMessage GetPrompt(string featureName, string contextType, string @namespace)
    {
        return new ChatMessage(ChatRole.User,
            $"Scaffold a new Muonroi feature named {featureName} in namespace {@namespace} for context {contextType}.\n" +
            "1. Read muonroi://ecosystem/rules and muonroi://ecosystem/patterns.\n" +
            "2. Call muonroi_scaffold_service.\n" +
            "3. Call muonroi_scaffold_repository.\n" +
            "4. Call muonroi_scaffold_rule_class as needed.\n" +
            "5. Call muonroi_rulegen_extract and muonroi_rulegen_register for generated rule files.\n" +
            "6. Run muonroi_compliance_check before returning code.");
    }
}
