using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Services;
using Muonroi.RuleGen.Writers;

namespace Muonroi.RuleGen.Commands;

internal static class RegisterCommand
{
    public static async Task<int> RunAsync(CommandContext context)
    {
        string? rules = OptionReader.GetString(context, "rules");
        if (string.IsNullOrWhiteSpace(rules))
        {
            throw new MInternalException("Missing required option --rules.");
        }

        string? output = OptionReader.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new MInternalException("Missing required option --output.");
        }

        string ns = OptionReader.GetString(context, "namespace", context.Config.Extract.Namespace) ?? "Generated.Rules";
        string registrationClassName = OptionReader.GetString(
            context,
            "registration-class",
            "MGeneratedRuleRegistrationExtensions") ?? "MGeneratedRuleRegistrationExtensions";
        string dispatcherNamespace = OptionReader.GetString(context, "dispatcher-namespace", ns) ?? ns;
        bool generateDispatchers = OptionReader.GetBool(context, "generate-dispatchers", true);
        bool registerDispatchers = OptionReader.GetBool(context, "register-dispatchers", generateDispatchers);
        bool overwriteDispatchers = OptionReader.GetBool(context, "dispatcher-overwrite", false);
        bool includeRuleEngineRegistration = OptionReader.GetBool(
            context,
            "include-rule-engine",
            generateDispatchers);
        string dispatcherSuffix = OptionReader.GetString(context, "dispatcher-suffix", "GeneratedRuleEngineDispatcher")
            ?? "GeneratedRuleEngineDispatcher";
        string? workflowName = OptionReader.GetString(context, "workflow-name", context.Config.Extract.WorkflowName);
        string rulesDir = Path.GetFullPath(rules, context.WorkingDirectory);
        string outputFile = Path.GetFullPath(output, context.WorkingDirectory);
        string dispatcherDir = Path.GetFullPath(
            OptionReader.GetString(context, "dispatcher-output", Path.GetDirectoryName(outputFile))
                ?? Path.GetDirectoryName(outputFile)
                ?? context.WorkingDirectory,
            context.WorkingDirectory);

        IReadOnlyList<Models.DiscoveredRuleType> discovered = RuleTypeDiscoveryService.Discover(rulesDir);
        IReadOnlyList<Models.DiscoveredDispatcherContext> dispatchers = DispatcherWriter.BuildContexts(
            discovered,
            dispatcherSuffix,
            workflowName);
        IReadOnlyList<Models.DiscoveredDispatcherContext> dispatchersForRegistration =
            registerDispatchers ? dispatchers : [];
        string rendered = RegistrationWriter.Render(
            ns,
            discovered,
            dispatchersForRegistration,
            includeRuleEngineRegistration,
            registrationClassName);

        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        await File.WriteAllTextAsync(outputFile, rendered);

        int generatedDispatcherCount = 0;
        int skippedDispatcherCount = 0;
        if (generateDispatchers)
        {
            Directory.CreateDirectory(dispatcherDir);
            foreach (Models.DiscoveredDispatcherContext dispatcher in dispatchers)
            {
                string dispatcherFile = Path.Combine(dispatcherDir, dispatcher.FileName);
                if (File.Exists(dispatcherFile) && !overwriteDispatchers)
                {
                    skippedDispatcherCount++;
                    continue;
                }

                string content = DispatcherWriter.Render(dispatcherNamespace, dispatcher);
                await File.WriteAllTextAsync(dispatcherFile, content);
                generatedDispatcherCount++;
            }
        }

        Console.WriteLine(
            $"Generated registration: '{outputFile}' ({discovered.Count} rules, {dispatchers.Count} contexts).");
        if (generateDispatchers)
        {
            Console.WriteLine(
                $"Generated dispatchers: {generatedDispatcherCount}, skipped existing: {skippedDispatcherCount}, output: '{dispatcherDir}'.");
        }

        return 0;
    }
}
