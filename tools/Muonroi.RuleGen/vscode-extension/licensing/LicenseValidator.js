const fs = require('fs');
const os = require('os');
const path = require('path');
const http = require('http');
const https = require('https');
const vscode = require('vscode');

function createLicenseValidator(context) {
    const proofCachePath = getProofCachePath();

    async function hasFeatureAccess(capability) {
        const offline = await checkOfflineProof(capability, proofCachePath);
        if (offline) {
            return true;
        }

        const config = vscode.workspace.getConfiguration('muonroi');
        const licenseKey = config.get('licenseKey');
        if (!licenseKey) {
            return false;
        }

        const serverUrl = config.get('licenseServerUrl') || 'https://license.muonroi.com';
        return checkOnline(serverUrl, licenseKey, capability);
    }

    return {
        hasFeatureAccess,
        getProofCachePath: () => proofCachePath,
        extensionContext: context
    };
}

async function checkOfflineProof(capability, proofPath) {
    if (!fs.existsSync(proofPath)) {
        return false;
    }

    try {
        const proof = JSON.parse(fs.readFileSync(proofPath, 'utf8'));
        const validUntil = new Date(proof.expiresAt || proof.validUntil || 0);
        if (!(validUntil instanceof Date) || Number.isNaN(validUntil.valueOf()) || validUntil < new Date()) {
            return false;
        }

        const features = proof.features || proof.allowedFeatures || proof?.signedLicensePayload?.allowedFeatures || [];
        if (!Array.isArray(features)) {
            return false;
        }

        return features.includes('*') || features.includes(capability);
    } catch {
        return false;
    }
}

function checkOnline(serverUrl, licenseKey, capability) {
    const payload = JSON.stringify({
        licenseKey,
        machineFingerprint: vscode.env.machineId,
        actionType: capability
    });

    return new Promise((resolve) => {
        const target = new URL('/api/v1/validate', serverUrl);
        const client = target.protocol === 'http:' ? http : https;

        const req = client.request(
            {
                hostname: target.hostname,
                port: target.port,
                path: target.pathname,
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Content-Length': Buffer.byteLength(payload)
                },
                timeout: 5000
            },
            (res) => {
                let data = '';
                res.on('data', (chunk) => {
                    data += chunk;
                });
                res.on('end', () => {
                    try {
                        const result = JSON.parse(data);
                        const features = result.allowedFeatures || [];
                        const isAllowed = Array.isArray(features) && (features.includes('*') || features.includes(capability));
                        resolve(Boolean(result.isValid) && isAllowed);
                    } catch {
                        resolve(false);
                    }
                });
            }
        );

        req.on('error', () => resolve(false));
        req.on('timeout', () => {
            req.destroy();
            resolve(false);
        });

        req.write(payload);
        req.end();
    });
}

function getProofCachePath() {
    const base = process.env.APPDATA || path.join(os.homedir(), '.config');
    return path.join(base, 'muonroi', 'activation_proof.json');
}

module.exports = {
    createLicenseValidator
};
