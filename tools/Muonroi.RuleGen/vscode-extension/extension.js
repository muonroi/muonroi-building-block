const vscode = require('vscode');
const cp = require('child_process');
const path = require('path');

const RuleCodeLensProvider = require('./providers/RuleCodeLensProvider');
const RuleDiagnosticsProvider = require('./providers/RuleDiagnosticsProvider');
const RuleExplorerProvider = require('./providers/RuleExplorerProvider');
const { createLicenseValidator } = require('./licensing/LicenseValidator');
const { activateLicenseInteractive } = require('./licensing/LicenseActivationCommand');

function activate(context) {
    const outputChannel = vscode.window.createOutputChannel('Muonroi RuleGen');
    const diagnosticsProvider = new RuleDiagnosticsProvider();
    const licenseValidator = createLicenseValidator(context);

    context.subscriptions.push(vscode.languages.registerCodeLensProvider('csharp', new RuleCodeLensProvider()));

    const ruleExplorerProvider = new RuleExplorerProvider(licenseValidator, outputChannel);
    const treeView = vscode.window.createTreeView('muonroiRuleExplorer', {
        treeDataProvider: ruleExplorerProvider,
        showCollapseAll: true
    });
    context.subscriptions.push(treeView);

    async function requireFeature(capability, featureName) {
        const hasAccess = await licenseValidator.hasFeatureAccess(capability);
        if (hasAccess) {
            return true;
        }

        const action = await vscode.window.showErrorMessage(
            `Muonroi: "${featureName}" requires a Licensed or Enterprise plan.`,
            'Activate License',
            'Learn More'
        );

        if (action === 'Activate License') {
            await vscode.commands.executeCommand('muonroi.activateLicense');
        }

        if (action === 'Learn More') {
            await vscode.env.openExternal(vscode.Uri.parse('https://muonroi.com/commercial'));
        }

        return false;
    }

    async function refreshContexts() {
        const hasExplorer = await licenseValidator.hasFeatureAccess('vsix.explorer');
        await vscode.commands.executeCommand('setContext', 'muonroi.ruleExplorerEnabled', hasExplorer);
        ruleExplorerProvider.refresh();
    }

    context.subscriptions.push(vscode.commands.registerCommand('muonroi.rulegen.extractAll', () => {
        outputChannel.show();
        outputChannel.appendLine("Executing 'muonroi-rule extract'...");

        runCliCommand('muonroi-rule extract', outputChannel, () => {
            vscode.window.showInformationMessage('Rules extracted successfully.');
            ruleExplorerProvider.refresh();
        });
    }));

    context.subscriptions.push(vscode.commands.registerCommand('muonroi.rulegen.watch', () => {
        const terminal = vscode.window.createTerminal('Muonroi Rule Watcher');
        terminal.show();
        terminal.sendText('muonroi-rule watch');
    }));

    context.subscriptions.push(vscode.commands.registerCommand('muonroi.rulegen.gotoRule', async (ruleCode) => {
        const fileName = `${ruleCode}Rule.g.cs`;
        const files = await vscode.workspace.findFiles(`**/${fileName}`);

        if (files.length > 0) {
            const doc = await vscode.workspace.openTextDocument(files[0]);
            await vscode.window.showTextDocument(doc);
            return;
        }

        vscode.window.showWarningMessage(`Generated file '${fileName}' not found. Run extract command first.`);
    }));

    context.subscriptions.push(vscode.commands.registerCommand('muonroi.activateLicense', async () => {
        const activated = await activateLicenseInteractive(licenseValidator, context, outputChannel);
        if (activated) {
            await refreshContexts();
        }
    }));

    context.subscriptions.push(vscode.commands.registerCommand('muonroi.publishRuleset', async () => {
        const allowed = await requireFeature('vsix.publish', 'Publish Ruleset');
        if (!allowed) {
            return;
        }

        vscode.window.showInformationMessage(
            'Control Plane publish endpoint is coming soon. License is valid; integration will be enabled in Track 3.'
        );
    }));

    context.subscriptions.push(vscode.commands.registerCommand('muonroi.watchMode', async () => {
        const allowed = await requireFeature('vsix.watch', 'Watch Mode');
        if (!allowed) {
            return;
        }

        const terminal = vscode.window.createTerminal('Muonroi Premium Watch Mode');
        terminal.show();
        terminal.sendText('muonroi-rule watch --hot-reload');
    }));

    context.subscriptions.push(vscode.commands.registerCommand('muonroi.ruleExplorer', async () => {
        const allowed = await requireFeature('vsix.explorer', 'Rule Explorer');
        if (!allowed) {
            return;
        }

        await vscode.commands.executeCommand('workbench.view.explorer');
        ruleExplorerProvider.refresh();
    }));

    vscode.workspace.onDidSaveTextDocument((document) => {
        diagnosticsProvider.refreshDiagnostics(document);
        ruleExplorerProvider.refresh();
    });

    vscode.workspace.onDidOpenTextDocument((document) => {
        diagnosticsProvider.refreshDiagnostics(document);
    });

    vscode.workspace.onDidCloseTextDocument((document) => {
        diagnosticsProvider.clearDiagnostics(document);
    });

    refreshContexts().catch((error) => {
        outputChannel.appendLine(`[WARN] Failed to refresh premium contexts: ${error.message}`);
    });

    outputChannel.appendLine('Muonroi.RuleGen extension activated.');
}

function runCliCommand(command, outputChannel, onSuccess) {
    const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri?.fsPath || process.cwd();

    cp.exec(command, { cwd: workspacePath }, (error, stdout, stderr) => {
        if (stdout) {
            outputChannel.appendLine(stdout);
        }

        if (stderr) {
            outputChannel.appendLine(stderr);
        }

        if (error) {
            vscode.window.showErrorMessage(`Command failed: ${error.message}`);
            return;
        }

        if (onSuccess) {
            onSuccess();
        }
    });
}

function deactivate() {}

module.exports = {
    activate,
    deactivate
};

