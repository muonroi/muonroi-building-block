const vscode = require('vscode');
const cp = require('child_process');
const path = require('path');

class RuleDiagnosticsProvider {
    constructor() {
        this.collection = vscode.languages.createDiagnosticCollection('muonroi-rulegen');
    }

    async refreshDiagnostics(document) {
        if (document.languageId !== 'csharp') return;

        const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        if (!workspaceRoot) return;

        const diagnostics = [];

        // Layer 1: In-file duplicate rule code detection (instant, no CLI needed)
        const text = document.getText();
        const regex = /\[MExtractAsRule\("([^"]+)"/g;
        const codes = new Map();
        let match;
        while ((match = regex.exec(text)) !== null) {
            const code = match[1];
            if (codes.has(code)) {
                const range = new vscode.Range(
                    document.positionAt(match.index),
                    document.positionAt(match.index + match[0].length)
                );
                diagnostics.push(new vscode.Diagnostic(
                    range,
                    `[MRG001] Duplicate rule code: '${code}'`,
                    vscode.DiagnosticSeverity.Error
                ));
            }
            codes.set(code, match.index);
        }

        // Layer 2: CLI verify — runs muonroi-rule verify on the source dir for cross-file issues
        try {
            const sourceDir = path.dirname(document.uri.fsPath);
            const output = await this._runVerify(workspaceRoot, sourceDir);
            const cliDiagnostics = this._parseVerifyOutput(output, document);
            diagnostics.push(...cliDiagnostics);
        } catch {
            // CLI not installed or timed out — silently skip
        }

        this.collection.set(document.uri, diagnostics);
    }

    clearDiagnostics(document) {
        this.collection.delete(document.uri);
    }

    /** Runs `muonroi-rule verify --source-dir <dir>` and returns stdout+stderr */
    _runVerify(cwd, sourceDir) {
        return new Promise((resolve, reject) => {
            const proc = cp.spawn(
                'muonroi-rule',
                ['verify', '--source-dir', sourceDir],
                { cwd, shell: true, timeout: 5000 }
            );
            let out = '';
            proc.stdout.on('data', d => { out += d.toString(); });
            proc.stderr.on('data', d => { out += d.toString(); });
            proc.on('close', () => resolve(out));
            proc.on('error', reject);
        });
    }

    /**
     * Parses CLI verify output lines of the form:
     *   ERROR [MRG001] file.cs:42 — Duplicate rule code 'ORDER_VALIDATE'
     *   WARN  [MRG003] file.cs:10 — Non-interface dependency '_repo'
     */
    _parseVerifyOutput(output, document) {
        const diagnostics = [];
        const linePattern = /^(ERROR|WARN)\s+\[MRG\d+\]\s+.*?:(\d+)\s+[—-]\s+(.+)$/gm;
        let m;
        while ((m = linePattern.exec(output)) !== null) {
            const severity = m[1] === 'ERROR'
                ? vscode.DiagnosticSeverity.Error
                : vscode.DiagnosticSeverity.Warning;
            const lineNum = Math.max(0, parseInt(m[2], 10) - 1);
            const message = m[3].trim();
            const range = new vscode.Range(lineNum, 0, lineNum, Number.MAX_SAFE_INTEGER);
            diagnostics.push(new vscode.Diagnostic(range, message, severity));
        }
        return diagnostics;
    }
}

module.exports = RuleDiagnosticsProvider;
