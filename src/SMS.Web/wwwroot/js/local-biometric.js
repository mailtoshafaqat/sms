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

export async function startCamera(videoElementId) {
    await stopCamera();
    mediaStream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 480 } },
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

    const detection = await faceapi
        .detectSingleFace(video, new faceapi.SsdMobilenetv1Options({ minConfidence: 0.5 }))
        .withFaceLandmarks()
        .withFaceDescriptor();

    if (!detection) {
        return null;
    }

    return Array.from(detection.descriptor);
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

export function startAutoFaceScan(videoElementId, dotNetRef, intervalMs = 2500) {
    stopAutoFaceScan();
    scanTimer = setInterval(async () => {
        try {
            const descriptor = await captureFaceDescriptor(videoElementId);
            if (descriptor) {
                await dotNetRef.invokeMethodAsync('OnFaceDetectedAsync', descriptor);
            }
        } catch {
            // Ignore transient camera/model errors during polling.
        }
    }, intervalMs);
}

export function stopAutoFaceScan() {
    if (scanTimer) {
        clearInterval(scanTimer);
        scanTimer = null;
    }
}
