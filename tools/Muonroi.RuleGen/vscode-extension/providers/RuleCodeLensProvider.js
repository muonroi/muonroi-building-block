const vscode = require('vscode');

class RuleCodeLensProvider {
    provideCodeLenses(document, token) {
        this.codeLenses = [];
        const text = document.getText();
        const regex = /\[MExtractAsRule\("([^"]+)"/g;
        let matches;

        while ((matches = regex.exec(text)) !== null) {
            const line = document.lineAt(document.positionAt(matches.index).line);
            const range = new vscode.Range(line.range.start, line.range.end);
            const ruleCode = matches[1];

            if (range) {
                this.codeLenses.push(new vscode.CodeLens(range, {
                    title: `⚡ Go to Generated Rule (${ruleCode})`,
                    tooltip: `Jump to the generated ${ruleCode}Rule.g.cs file`,
                    command: "muonroi.rulegen.gotoRule",
                    arguments: [ruleCode]
                }));

                this.codeLenses.push(new vscode.CodeLens(range, {
                    title: "🔄 Re-extract",
                    tooltip: "Manually trigger rule extraction for this file",
                    command: "muonroi.rulegen.extractAll"
                }));
            }
        }
        return this.codeLenses;
    }

    resolveCodeLens(codeLens, token) {
        return codeLens;
    }
}

module.exports = RuleCodeLensProvider;
