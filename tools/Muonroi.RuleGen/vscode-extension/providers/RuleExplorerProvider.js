const vscode = require('vscode');
const cp = require('child_process');

class RuleExplorerProvider {
    constructor(licenseValidator, outputChannel) {
        this.licenseValidator = licenseValidator;
        this.outputChannel = outputChannel;
        this._onDidChangeTreeData = new vscode.EventEmitter();
        this.onDidChangeTreeData = this._onDidChangeTreeData.event;
    }

    refresh() {
        this._onDidChangeTreeData.fire(undefined);
    }

    async getChildren(element) {
        if (element && Array.isArray(element.children)) {
            return element.children;
        }

        const hasAccess = await this.licenseValidator.hasFeatureAccess('vsix.explorer');
        if (!hasAccess) {
            return [
                new ExplorerNode(
                    'Rule Explorer is a premium feature. Activate license to unlock.',
                    vscode.TreeItemCollapsibleState.None
                )
            ];
        }

        const payload = await this.loadRuleGraph();
        if (payload.length === 0) {
            return [new ExplorerNode('No rules found in workspace.', vscode.TreeItemCollapsibleState.None)];
        }

        return payload;
    }

    getTreeItem(element) {
        return element;
    }

    loadRuleGraph() {
        const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri?.fsPath || process.cwd();

        return new Promise((resolve) => {
            cp.exec('muonroi-rule list --json', { cwd: workspacePath }, (error, stdout, stderr) => {
                if (error) {
                    if (stderr) {
                        this.outputChannel.appendLine(`[WARN] Rule explorer command error: ${stderr}`);
                    }
                    resolve([]);
                    return;
                }

                try {
                    const parsed = JSON.parse(stdout);
                    resolve(normalizeRuleTree(parsed));
                } catch (parseError) {
                    this.outputChannel.appendLine(`[WARN] Failed to parse rule explorer JSON: ${parseError.message}`);
                    resolve([]);
                }
            });
        });
    }
}

class ExplorerNode extends vscode.TreeItem {
    constructor(label, collapsibleState, children = []) {
        super(label, collapsibleState);
        this.children = children;
        this.contextValue = 'muonroiRuleExplorerItem';
    }
}

function normalizeRuleTree(input) {
    if (!input) {
        return [];
    }

    const groups = Array.isArray(input) ? input : input.groups || input.rules || [];
    if (!Array.isArray(groups)) {
        return [];
    }

    return groups.map((group) => {
        const groupName = group.name || group.workflow || 'workflow';
        const rules = Array.isArray(group.rules) ? group.rules : [];
        const children = rules.map((rule) => {
            const deps = Array.isArray(rule.dependencies) ? rule.dependencies : [];
            const depNodes = deps.map((dependency) =>
                new ExplorerNode(`depends on: ${dependency}`, vscode.TreeItemCollapsibleState.None));

            const hooks = Array.isArray(rule.hooks) ? rule.hooks : [];
            const hookNodes = hooks.map((hook) =>
                new ExplorerNode(`hook: ${hook}`, vscode.TreeItemCollapsibleState.None));

            return new ExplorerNode(
                rule.code || rule.name || 'rule',
                depNodes.length + hookNodes.length > 0
                    ? vscode.TreeItemCollapsibleState.Collapsed
                    : vscode.TreeItemCollapsibleState.None,
                [...hookNodes, ...depNodes]
            );
        });

        return new ExplorerNode(groupName, vscode.TreeItemCollapsibleState.Collapsed, children);
    });
}

module.exports = RuleExplorerProvider;
