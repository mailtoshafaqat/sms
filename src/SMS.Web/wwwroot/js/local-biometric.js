const MODEL_URL = 'https://cdn.jsdelivr.net/npm/@vladmandic/face-api/model';
let mediaStream = null;
let modelsLoaded = false;
let scanTimer = null;

function ensureFaceApi() {
    if (typeof faceapi === 'undefined') {
        throw new Error('face-api library is not loaded.');
    }
}

function randomChallenge() {
    const bytes = new Uint8Array(32);
    crypto.getRandomValues(bytes);
    return bytes;
}

function bytesToBase64(bytes) {
    let binary = '';
    bytes.forEach((b) => { binary += String.fromCharCode(b); });
    return btoa(binary);
}

function base64ToBytes(value) {
    const binary = atob(value);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i += 1) {
        bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
}

function ensureCameraAccess() {
    if (!window.isSecureContext) {
        throw new Error(
            'Camera is blocked on HTTP from a phone. Use the HTTPS link shown above (tap Advanced → Proceed), then allow camera.'
        );
    }

    if (!navigator.mediaDevices?.getUserMedia) {
        throw new Error('Camera is not available in this browser. Use Chrome or Safari and allow camera permission.');
    }
}

export async function loadFaceModels() {
    ensureFaceApi();
    if (modelsLoaded) {
        return true;
    }

    await Promise.all([
        faceapi.nets.ssdMobilenetv1.loadFromUri(MODEL_URL),
        faceapi.nets.faceLandmark68Net.loadFromUri(MODEL_URL),
        faceapi.nets.faceRecognitionNet.loadFromUri(MODEL_URL)
    ]);

    modelsLoaded = true;
    return true;
}

export async function startCamera(videoElementId, facingMode = 'user') {
    ensureCameraAccess();
    await stopCamera();
    mediaStream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: { ideal: facingMode }, width: { ideal: 640 }, height: { ideal: 480 } },
        audio: false
    });

    const video = document.getElementById(videoElementId);
    if (!video) {
        throw new Error(`Video element '${videoElementId}' not found.`);
    }

    video.srcObject = mediaStream;
    await video.play();
    return true;
}

export async function stopCamera() {
    stopAutoFaceScan();

    if (mediaStream) {
        mediaStream.getTracks().forEach((track) => track.stop());
        mediaStream = null;
    }

    return true;
}

export async function captureFaceDescriptor(videoElementId) {
    ensureFaceApi();
    await loadFaceModels();

    const video = document.getElementById(videoElementId);
    if (!video) {
        throw new Error(`Video element '${videoElementId}' not found.`);
    }

    if (video.readyState < 2 || video.videoWidth === 0) {
        return null;
    }

    const detection = await faceapi
        .detectSingleFace(video, new faceapi.SsdMobilenetv1Options({ minConfidence: 0.35 }))
        .withFaceLandmarks()
        .withFaceDescriptor();

    if (!detection) {
        return null;
    }

    return Array.from(detection.descriptor);
}

export async function captureFaceDescriptorJson(videoElementId) {
    const descriptor = await captureFaceDescriptor(videoElementId);
    return descriptor ? JSON.stringify(descriptor) : null;
}

function faceDistance(left, right) {
    if (typeof faceapi?.euclideanDistance === 'function') {
        return faceapi.euclideanDistance(left, right);
    }

    let sum = 0;
    for (let i = 0; i < left.length; i += 1) {
        const delta = left[i] - right[i];
        sum += delta * delta;
    }

    return Math.sqrt(sum);
}

async function recordGateScan(descriptor) {
    const scanResponse = await fetch('/attendance/gate/scan', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ descriptor })
    });

    if (!scanResponse.ok) {
        throw new Error(`Gate scan failed (${scanResponse.status}).`);
    }

    return readApiResult(await scanResponse.json());
}

const GATE_MATCH_THRESHOLD = 0.7;
let gateEnrollments = null;

function readApiResult(result) {
    return {
        success: !!(result?.success ?? result?.Success),
        message: result?.message ?? result?.Message ?? ''
    };
}

export async function loadGateEnrollments(force = false) {
    if (gateEnrollments && !force) {
        return gateEnrollments;
    }

    const response = await fetch('/attendance/gate/enrollments', { credentials: 'include' });
    if (!response.ok) {
        throw new Error(`Failed to load gate enrollments (${response.status}).`);
    }

    gateEnrollments = await response.json();
    return gateEnrollments;
}

function matchGateEnrollment(descriptor) {
    ensureFaceApi();

    if (!gateEnrollments || gateEnrollments.length === 0) {
        return null;
    }

    let best = null;
    let bestDistance = Number.POSITIVE_INFINITY;

    for (const entry of gateEnrollments) {
        const descriptors = entry.descriptors ?? entry.Descriptors ?? [];
        for (const stored of descriptors) {
            const distance = faceDistance(descriptor, stored);
            if (distance < bestDistance) {
                bestDistance = distance;
                best = entry;
            }
        }
    }

    if (!best || bestDistance > GATE_MATCH_THRESHOLD) {
        return null;
    }

    return best;
}

async function postGateRecord(externalId) {
    const response = await fetch('/attendance/gate/record', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ externalId })
    });

    if (!response.ok) {
        throw new Error(`Gate record failed (${response.status}).`);
    }

    return readApiResult(await response.json());
}

export function startAutoFaceScan(videoElementId, dotNetRef, intervalMs = 2000, forceReload = false) {
    stopAutoFaceScan();
    let busy = false;

    const tick = async () => {
        scanTimer = setTimeout(tick, intervalMs);

        if (busy) {
            return;
        }

        busy = true;
        try {
            if (!gateEnrollments || forceReload) {
                await loadGateEnrollments(true);
                forceReload = false;
            }

            if (!gateEnrollments || gateEnrollments.length === 0) {
                await dotNetRef.invokeMethodAsync(
                    'OnGateScanStatusAsync',
                    false,
                    false,
                    'No enrolled students. Admin: enroll faces at Local Biometric Test.');
                return;
            }

            const descriptor = await captureFaceDescriptor(videoElementId);
            if (!descriptor) {
                await dotNetRef.invokeMethodAsync('OnGateScanStatusAsync', false, false, '');
                return;
            }

            // Same path as Local Biometric Test → Manual Scan (server-side match).
            const result = await recordGateScan(descriptor);
            if (!result.success) {
                const clientMatch = matchGateEnrollment(descriptor);
                if (clientMatch) {
                    const externalId = clientMatch.externalId ?? clientMatch.ExternalId;
                    const recordResult = await postGateRecord(externalId);
                    await dotNetRef.invokeMethodAsync(
                        'OnGateScanStatusAsync',
                        true,
                        recordResult.success,
                        recordResult.message);
                    return;
                }
            }

            await dotNetRef.invokeMethodAsync(
                'OnGateScanStatusAsync',
                true,
                result.success,
                result.message);
        } catch (error) {
            console.warn('Gate face scan error:', error);
            await dotNetRef.invokeMethodAsync(
                'OnGateScanStatusAsync',
                false,
                false,
                error.message ?? 'Gate scan failed.');
        } finally {
            busy = false;
        }
    };

    scanTimer = setTimeout(tick, intervalMs);
}

export function stopAutoFaceScan() {
    if (scanTimer) {
        clearTimeout(scanTimer);
        scanTimer = null;
    }
}

export async function enrollFingerprint(userId, displayName, rpName = 'SMS') {
    if (!window.PublicKeyCredential) {
        throw new Error('WebAuthn is not supported in this browser.');
    }

    const credential = await navigator.credentials.create({
        publicKey: {
            challenge: randomChallenge(),
            rp: { name: rpName },
            user: {
                id: new TextEncoder().encode(String(userId)),
                name: displayName,
                displayName: displayName
            },
            pubKeyCredParams: [{ alg: -7, type: 'public-key' }],
            authenticatorSelection: {
                authenticatorAttachment: 'platform',
                residentKey: 'preferred',
                userVerification: 'required'
            },
            timeout: 120000
        }
    });

    if (!credential) {
        return null;
    }

    return bytesToBase64(new Uint8Array(credential.rawId));
}

export async function verifyFingerprint(credentialId) {
    if (!window.PublicKeyCredential) {
        throw new Error('WebAuthn is not supported in this browser.');
    }

    const allowCredentials = credentialId
        ? [{ id: base64ToBytes(credentialId), type: 'public-key', transports: ['internal'] }]
        : [];

    const assertion = await navigator.credentials.get({
        publicKey: {
            challenge: randomChallenge(),
            allowCredentials,
            userVerification: 'required',
            timeout: 120000
        }
    });

    if (!assertion) {
        return null;
    }

    return bytesToBase64(new Uint8Array(assertion.rawId));
}
