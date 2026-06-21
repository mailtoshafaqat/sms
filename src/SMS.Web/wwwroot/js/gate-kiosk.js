(() => {
    let gateModulePromise = null;

    function getGateModule() {
        if (!gateModulePromise) {
            gateModulePromise = import('/js/local-biometric.js');
        }

        return gateModulePromise;
    }

    window.smsGateStart = async (dotNetRef, videoElementId, facingMode = 'user', intervalMs = 2000) => {
        const mod = await getGateModule();
        await mod.loadFaceModels();
        await mod.startCamera(videoElementId, facingMode);
        mod.startAutoFaceScan(videoElementId, dotNetRef, intervalMs, true);
    };

    window.smsGateStop = async () => {
        const mod = await getGateModule();
        mod.stopAutoFaceScan();
        await mod.stopCamera();
    };

    window.smsGateSwitchCamera = async (dotNetRef, videoElementId, facingMode, intervalMs = 2000) => {
        const mod = await getGateModule();
        await mod.startCamera(videoElementId, facingMode);
        mod.startAutoFaceScan(videoElementId, dotNetRef, intervalMs, true);
    };
})();
