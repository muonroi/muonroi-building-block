const fs = require('fs');
const path = require('path');
const http = require('http');
const https = require('https');
const vscode = require('vscode');

async function activateLicenseInteractive(licenseValidator, context, outputChannel) {
    const config = vscode.workspace.getConfiguration('muonroi');
    const existingKey = config.get('licenseKey') || '';

    const licenseKey = await vscode.window.showInputBox({
        title: 'Muonroi License Activation',
        prompt: 'Enter your Muonroi license key (MRR-...)',
        value: existingKey,
        ignoreFocusOut: true
    });

    if (!licenseKey) {
        return false;
    }

    const serverUrl = config.get('licenseServerUrl') || 'https://license.muonroi.com';
    const payload = {
        licenseKey,
        machineFingerprint: vscode.env.machineId,
        environment: 'vscode',
        productVersion: context.extension.packageJSON.version || 'unknown'
    };

    try {
        const result = await postJson(new URL('/api/v1/activate', serverUrl), payload);
        if (!result || result.success !== true) {
            vscode.window.showErrorMessage(`Activation failed: ${result?.error || 'unknown error'}`);
            return false;
        }

        const proofPath = licenseValidator.getProofCachePath();
        const proofJson = decodeProofPayload(result);
        await writeProofFile(proofPath, proofJson);

        await config.update('licenseKey', licenseKey, vscode.ConfigurationTarget.Global);

        vscode.window.showInformationMessage('License activated. Premium features unlocked.');
        outputChannel.appendLine(`[INFO] Activation proof saved: ${proofPath}`);
        return true;
    } catch (error) {
        vscode.window.showErrorMessage(`Activation request failed: ${error.message}`);
        return false;
    }
}

function postJson(url, body) {
    const client = url.protocol === 'http:' ? http : https;
    const payload = JSON.stringify(body);

    return new Promise((resolve, reject) => {
        const req = client.request(
            {
                hostname: url.hostname,
                port: url.port,
                path: url.pathname,
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Content-Length': Buffer.byteLength(payload)
                },
                timeout: 10000
            },
            (res) => {
                let data = '';
                res.on('data', (chunk) => {
                    data += chunk;
                });
                res.on('end', () => {
                    try {
                        resolve(JSON.parse(data));
                    } catch (error) {
                        reject(error);
                    }
                });
            }
        );

        req.on('error', reject);
        req.on('timeout', () => {
            req.destroy();
            reject(new Error('Activation request timed out.'));
        });

        req.write(payload);
        req.end();
    });
}

function decodeProofPayload(response) {
    if (response.activationProof) {
        return Buffer.from(response.activationProof, 'base64').toString('utf8');
    }

    if (response.proof) {
        return JSON.stringify(response.proof, null, 2);
    }

    throw new Error('Activation response does not contain proof payload.');
}

async function writeProofFile(filePath, content) {
    const directory = path.dirname(filePath);
    await fs.promises.mkdir(directory, { recursive: true });
    await fs.promises.writeFile(filePath, content, 'utf8');
}

module.exports = {
    activateLicenseInteractive
};
