(() => {
    const MODULE_PATH = '/js/local-biometric.js';
    let gateModulePromise = null;

    function getGateModule() {
        if (!gateModulePromise) {
            gateModulePromise = import(MODULE_PATH);
        }

        return gateModulePromise;
    }

    window.smsReloadBiometricModule = () => {
        gateModulePromise = import(`${MODULE_PATH}?reload=${Date.now()}`);
        return gateModulePromise;
    };

    let frameResetTimer = null;

    window.smsGateSetFrameVisual = (state, label = '') => {
        const wrap = document.getElementById('gate-video-wrap');
        const icon = document.getElementById('gate-frame-icon');
        const frameLabel = document.getElementById('gate-frame-label');
        const errorBox = document.getElementById('gate-error-only');

        if (!wrap) {
            return;
        }

        wrap.className = `gate-kiosk-video-wrap ${state}`;

        if (icon) {
            if (state === 'success') {
                icon.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/></svg>';
            } else if (state === 'error') {
                icon.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M18.3 5.71 12 12.01l-6.29-6.3-1.42 1.41 6.3 6.29-6.3 6.29 1.42 1.42 6.29-6.3 6.29 6.3 1.42-1.42-6.3-6.29 6.3-6.29z"/></svg>';
            } else {
                icon.innerHTML = '';
            }
        }

        if (frameLabel) {
            frameLabel.textContent = state === 'success' ? label : '';
        }

        if (errorBox) {
            if (state === 'error' && label) {
                errorBox.textContent = label;
                errorBox.hidden = false;
            } else if (state !== 'error') {
                errorBox.hidden = true;
            }
        }

        if (frameResetTimer) {
            clearTimeout(frameResetTimer);
            frameResetTimer = null;
        }

        if (state === 'success' || state === 'error') {
            frameResetTimer = setTimeout(() => {
                window.smsGateSetFrameVisual('idle');
            }, 2500);
        }
    };

    window.smsGateUpdateStatus = (text, kind = 'info') => {
        const el = document.getElementById('gate-js-status');
        if (el && text) {
            el.textContent = text;
            el.className = `gate-kiosk-js-status ${kind === 'error' ? 'gate-status-error' : kind === 'success' ? 'gate-status-ok' : 'muted'}`;
        }
    };

    window.smsGateStart = async (dotNetRef, videoElementId, facingMode = 'user', intervalMs = 2000) => {
        const mod = await getGateModule();
        window.smsGateSetFrameVisual('scanning');
        await mod.loadFaceModels();
        await mod.startCamera(videoElementId, facingMode);
        const scanInterval = /android|iphone|ipad|ipod/i.test(navigator.userAgent)
            ? Math.max(intervalMs, 1600)
            : intervalMs;
        mod.startAutoFaceScan(videoElementId, dotNetRef, scanInterval, true);
    };

    window.smsGateStop = async () => {
        const mod = await getGateModule();
        mod.stopAutoFaceScan();
        await mod.stopCamera();
    };

    window.smsGateSwitchCamera = async (dotNetRef, videoElementId, facingMode, intervalMs = 2000) => {
        const mod = await getGateModule();
        await mod.startCamera(videoElementId, facingMode);
        const scanInterval = /android|iphone|ipad|ipod/i.test(navigator.userAgent)
            ? Math.max(intervalMs, 1600)
            : intervalMs;
        mod.startAutoFaceScan(videoElementId, dotNetRef, scanInterval, true);
    };

    window.smsGateScanOnce = async (dotNetRef, videoElementId) => {
        const mod = await getGateModule();
        await mod.loadFaceModels();
        await mod.scanGateOnce(videoElementId, dotNetRef);
    };

    // Window globals for Local Biometric Test (reliable on mobile PWA; avoids stale ES module interop).
    window.smsLocalBioLoadModels = async () => (await getGateModule()).loadFaceModels();

    window.smsLocalBioStartCamera = async (videoElementId, facingMode = 'user') => {
        const mod = await getGateModule();
        await mod.loadFaceModels();
        await mod.startCamera(videoElementId, facingMode);
    };

    window.smsLocalBioStopCamera = async () => {
        const mod = await getGateModule();
        mod.stopAutoFaceScan();
        await mod.stopCamera();
    };

    window.smsLocalBioCaptureJson = async (videoElementId) => {
        const mod = await getGateModule();
        await mod.loadFaceModels();
        return mod.captureFaceDescriptorJson(videoElementId);
    };

    window.smsLocalBioStartAutoScan = async (videoElementId, dotNetRef, intervalMs = 2500) => {
        const mod = await getGateModule();
        await mod.loadFaceModels();
        mod.startAutoFaceScan(videoElementId, dotNetRef, intervalMs);
    };

    window.smsLocalBioStopAutoScan = async () => {
        const mod = await getGateModule();
        mod.stopAutoFaceScan();
    };

    window.smsLocalBioEnrollFingerprint = async (userId, displayName, rpName) => {
        const mod = await getGateModule();
        return mod.enrollFingerprint(userId, displayName, rpName);
    };

    window.smsLocalBioVerifyFingerprint = async (credentialId) => {
        const mod = await getGateModule();
        return mod.verifyFingerprint(credentialId);
    };
})();
