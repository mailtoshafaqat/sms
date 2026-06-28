const MODEL_URL = '/models/face-api';
let mediaStream = null;
let modelsLoaded = false;
let scanTimer = null;
const useTinyDetector = /android|iphone|ipad|ipod/i.test(typeof navigator !== 'undefined' ? navigator.userAgent : '');

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

    const modelLoads = [
        faceapi.nets.faceLandmark68Net.loadFromUri(MODEL_URL),
        faceapi.nets.faceRecognitionNet.loadFromUri(MODEL_URL)
    ];

    if (useTinyDetector) {
        modelLoads.unshift(faceapi.nets.tinyFaceDetector.loadFromUri(MODEL_URL));
    } else {
        modelLoads.unshift(faceapi.nets.ssdMobilenetv1.loadFromUri(MODEL_URL));
    }

    await Promise.all(modelLoads);

    modelsLoaded = true;
    return true;
}

export async function startCamera(videoElementId, facingMode = 'user') {
    ensureCameraAccess();
    await stopCamera();

    const videoConstraints = {
        facingMode: { ideal: facingMode },
        width: { ideal: 640, min: 320 },
        height: { ideal: 480, min: 240 }
    };

    try {
        mediaStream = await navigator.mediaDevices.getUserMedia({
            video: videoConstraints,
            audio: false
        });
    } catch {
        mediaStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
    }

    const video = document.getElementById(videoElementId);
    if (!video) {
        throw new Error(`Video element '${videoElementId}' not found.`);
    }

    video.srcObject = mediaStream;
    await waitForVideoReady(video);
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

function getDetectorOptions() {
    if (useTinyDetector) {
        return new faceapi.TinyFaceDetectorOptions({ inputSize: 512, scoreThreshold: 0.3 });
    }

    return new faceapi.SsdMobilenetv1Options({ minConfidence: 0.35 });
}

function normalizeDescriptor(values) {
    let sum = 0;
    for (let i = 0; i < values.length; i += 1) {
        sum += values[i] * values[i];
    }

    if (sum <= 1e-12) {
        return values;
    }

    const inv = 1 / Math.sqrt(sum);
    return values.map((value) => value * inv);
}

function isFaceUsable(detection, video, relaxed = false) {
    const score = detection.detection?.score ?? 1;
    const minScore = relaxed ? 0.28 : (useTinyDetector ? 0.32 : 0.35);
    if (score < minScore) {
        return false;
    }

    const frameArea = video.videoWidth * video.videoHeight;
    const faceArea = detection.detection?.box?.area ?? 0;
    const minRatio = relaxed ? 0.035 : (useTinyDetector ? 0.045 : 0.06);
    if (frameArea > 0 && faceArea / frameArea < minRatio) {
        return false;
    }

    return true;
}

async function waitForVideoReady(video) {
    if (video.readyState >= 2 && video.videoWidth > 0) {
        return;
    }

    await new Promise((resolve) => {
        const timeout = setTimeout(resolve, 3000);
        const done = () => {
            clearTimeout(timeout);
            video.removeEventListener('loadeddata', done);
            resolve();
        };
        video.addEventListener('loadeddata', done);
    });
}

function sleep(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

function averageDescriptors(descriptors) {
    if (!descriptors.length) {
        return null;
    }

    if (descriptors.length === 1) {
        return descriptors[0];
    }

    const length = descriptors[0].length;
    const averaged = new Array(length).fill(0);
    for (const descriptor of descriptors) {
        for (let i = 0; i < length; i += 1) {
            averaged[i] += descriptor[i];
        }
    }

    for (let i = 0; i < length; i += 1) {
        averaged[i] /= descriptors.length;
    }

    return Array.from(normalizeDescriptor(averaged));
}

async function detectBestFace(video, options) {
    const single = await faceapi
        .detectSingleFace(video, options)
        .withFaceLandmarks()
        .withFaceDescriptor();

    if (single) {
        return single;
    }

    const all = await faceapi
        .detectAllFaces(video, options)
        .withFaceLandmarks()
        .withFaceDescriptors();

    if (!all || all.length === 0) {
        return null;
    }

    return all.sort((a, b) => b.detection.box.area - a.detection.box.area)[0];
}

async function captureFaceDetection(video, relaxed = false) {
    const options = getDetectorOptions();
    const detection = await detectBestFace(video, options);

    if (!detection?.descriptor) {
        return null;
    }

    if (!isFaceUsable(detection, video, relaxed)) {
        return null;
    }

    return {
        descriptor: Array.from(normalizeDescriptor(Array.from(detection.descriptor))),
        score: detection.detection?.score ?? 0
    };
}

export async function captureFaceDescriptor(videoElementId, relaxed = false) {
    ensureFaceApi();
    await loadFaceModels();

    const video = document.getElementById(videoElementId);
    if (!video) {
        throw new Error(`Video element '${videoElementId}' not found.`);
    }

    await waitForVideoReady(video);
    if (video.readyState < 2 || video.videoWidth === 0) {
        return null;
    }

    try {
        const result = await captureFaceDetection(video, relaxed);
        return result?.descriptor ?? null;
    } catch (error) {
        console.warn('Face detection failed:', error);
        return null;
    }
}

/** Gate scan: pick the single best frame (do not average — angles mix poorly). */
export async function captureGateScanDescriptor(videoElementId) {
    ensureFaceApi();
    await loadFaceModels();

    const video = document.getElementById(videoElementId);
    if (!video) {
        throw new Error(`Video element '${videoElementId}' not found.`);
    }

    await waitForVideoReady(video);
    if (video.readyState < 2 || video.videoWidth === 0) {
        return null;
    }

    let best = null;
    const attempts = useTinyDetector ? 5 : 4;
    for (let i = 0; i < attempts; i += 1) {
        try {
            const result = await captureFaceDetection(video, true);
            if (result && (!best || result.score > best.score)) {
                best = result;
            }
        } catch (error) {
            console.warn('Gate scan capture attempt failed:', error);
        }

        if (i < attempts - 1) {
            await sleep(100);
        }
    }

    return best?.descriptor ?? null;
}

/** Enrollment: capture several angles in one click. */
export async function captureEnrollmentBurst(videoElementId, sampleCount = 3) {
    ensureFaceApi();
    await loadFaceModels();

    const video = document.getElementById(videoElementId);
    if (!video) {
        throw new Error(`Video element '${videoElementId}' not found.`);
    }

    await waitForVideoReady(video);
    const descriptors = [];
    for (let i = 0; i < sampleCount; i += 1) {
        try {
            const result = await captureFaceDetection(video, true);
            if (result) {
                descriptors.push(result.descriptor);
            }
        } catch (error) {
            console.warn('Enrollment burst capture failed:', error);
        }

        if (i < sampleCount - 1) {
            await sleep(250);
        }
    }

    return descriptors;
}

export async function captureStableFaceDescriptor(videoElementId, sampleCount = 3) {
    const samples = [];
    for (let i = 0; i < sampleCount; i += 1) {
        const descriptor = await captureGateScanDescriptor(videoElementId);
        if (descriptor) {
            return descriptor;
        }

        if (i < sampleCount - 1) {
            await sleep(140);
        }
    }

    return averageDescriptors(samples);
}

export async function captureFaceDescriptorJson(videoElementId) {
    const descriptor = await captureGateScanDescriptor(videoElementId);
    return descriptor ? JSON.stringify(descriptor) : null;
}

function faceDistance(left, right) {
    const normalizedLeft = normalizeDescriptor(left);
    const normalizedRight = normalizeDescriptor(right);

    if (typeof faceapi?.euclideanDistance === 'function') {
        return faceapi.euclideanDistance(normalizedLeft, normalizedRight);
    }

    let sum = 0;
    for (let i = 0; i < normalizedLeft.length; i += 1) {
        const delta = normalizedLeft[i] - normalizedRight[i];
        sum += delta * delta;
    }

    return Math.sqrt(sum);
}

async function recordGateScan(descriptor) {
    const isMobile = /android|iphone|ipad|ipod/i.test(navigator.userAgent);
    const matchMode = isMobile ? 1 : 0;
    const scanResponse = await fetch('/attendance/gate/scan', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ descriptor, matchMode })
    });

    if (!scanResponse.ok) {
        throw new Error(`Gate scan failed (${scanResponse.status}).`);
    }

    return readApiResult(await scanResponse.json());
}

let gateEnrollments = null;
let scanPausedUntil = 0;
let lastNoFaceHintAt = 0;

function formatGateMessage(raw, success) {
    if (!raw) {
        return '';
    }

    if (success) {
        const parts = raw.split(' — ');
        const name = parts[0]?.trim() || 'Student';
        const detail = (parts[1] || raw).toLowerCase();
        if (detail.includes('out')) {
            return `${name} — Checked OUT`;
        }

        if (detail.includes('in') || detail.includes('present') || detail.includes('late')) {
            return `${name} — Checked IN`;
        }

        return name;
    }

    const lower = raw.toLowerCase();
    if (lower.includes('not recognized') || lower.includes('enroll') || lower.includes('ambiguous')) {
        return 'Face not recognized. Enroll at Biometric Test first.';
    }

    if (lower.includes('duplicate') || lower.includes('already') || lower.includes('wait')) {
        return 'Already scanned. Please wait a moment.';
    }

    if (lower.includes('no enrolled')) {
        return 'No students enrolled. Ask admin to enroll faces.';
    }

    if (lower.includes('blocked') || lower.includes('scan failed')) {
        return 'Could not record attendance. Try again.';
    }

    return raw.length > 80 ? 'Could not record attendance. Try again.' : raw;
}

function setGateFrameVisual(state, label = '') {
    if (typeof window.smsGateSetFrameVisual === 'function') {
        window.smsGateSetFrameVisual(state, label);
    }
}

async function notifyGate(dotNetRef, faceDetected, success, statusMessage) {
    const friendly = formatGateMessage(statusMessage, success);

    if (success) {
        setGateFrameVisual('success', friendly);
        scanPausedUntil = Date.now() + 2500;
    } else if (faceDetected && friendly) {
        setGateFrameVisual('error', friendly);
        scanPausedUntil = Date.now() + 2500;
    }

    if (!dotNetRef) {
        return;
    }

    try {
        await dotNetRef.invokeMethodAsync(
            'OnGateScanStatusAsync',
            faceDetected,
            success,
            friendly || statusMessage || ''
        );
    } catch (error) {
        console.warn('Gate Blazor callback failed (attendance may still be recorded):', error);
    }

    if (success && navigator.vibrate) {
        navigator.vibrate(200);
    }
}

async function runGateScanTick(videoElementId, dotNetRef, forceReload) {
    if (Date.now() < scanPausedUntil) {
        return;
    }

    if (!gateEnrollments || forceReload) {
        await loadGateEnrollments(forceReload);
        forceReload = false;
    }

    if (!gateEnrollments || gateEnrollments.length === 0) {
        setGateFrameVisual('error', 'No students enrolled. Ask admin to enroll faces.');
        await notifyGate(dotNetRef, false, false, 'No enrolled students.');
        return;
    }

    setGateFrameVisual('scanning');

    const descriptor = await captureGateScanDescriptor(videoElementId);
    if (!descriptor) {
        const now = Date.now();
        if (now - lastNoFaceHintAt > 4000) {
            lastNoFaceHintAt = now;
            setGateFrameVisual('error', 'Face not detected');
            await notifyGate(dotNetRef, false, false, 'Face not detected. Stand closer and look at the camera.');
        } else {
            setGateFrameVisual('scanning');
        }
        return;
    }

    const result = await recordGateScan(descriptor);
    await notifyGate(dotNetRef, true, result.success, result.message);
}

export async function scanGateOnce(videoElementId, dotNetRef) {
    await loadGateEnrollments(true);
    await runGateScanTick(videoElementId, dotNetRef, false);
}

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

export async function enrollGateFaceSample(videoElementId, studentId) {
    const descriptor = await captureGateScanDescriptor(videoElementId);
    if (!descriptor) {
        return { success: false, message: 'No face detected. Stand closer, face the camera, and try again.' };
    }

    const response = await fetch('/attendance/gate/enroll', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ studentId, descriptor })
    });

    if (!response.ok) {
        return { success: false, message: `Enroll failed (${response.status}).` };
    }

    const result = await response.json();
    await loadGateEnrollments(true);
    return {
        success: !!(result?.success ?? result?.Success),
        message: result?.message ?? result?.Message ?? '',
        sampleCount: result?.sampleCount ?? result?.SampleCount ?? 0,
        gateReady: !!(result?.gateReady ?? result?.GateReady)
    };
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
            await runGateScanTick(videoElementId, dotNetRef, forceReload);
            forceReload = false;
        } catch (error) {
            console.warn('Gate face scan error:', error);
            await notifyGate(
                dotNetRef,
                false,
                false,
                error.message ?? 'Gate scan failed.'
            );
        } finally {
            busy = false;
        }
    };

    scanTimer = setTimeout(tick, 250);
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
