const CACHE_NAME = 'sms-gate-v11';
const PRECACHE_URLS = [
    '/app.css',
    '/js/pwa-register.js',
    '/js/gate-kiosk.js',
    '/branding/company-logo.svg',
    '/branding/gate-icon-192.png',
    '/branding/gate-icon-512.png',
    '/manifest.webmanifest'
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => cache.addAll(PRECACHE_URLS))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(
                keys
                    .filter((key) => key !== CACHE_NAME)
                    .map((key) => caches.delete(key))))
            .then(() => self.clients.claim())
    );
});

function isJsRequest(url) {
    return url.pathname.endsWith('.js') && !url.pathname.startsWith('/_framework/');
}

async function networkFirst(request) {
    const cache = await caches.open(CACHE_NAME);

    try {
        const response = await fetch(request);
        if (response && response.status === 200 && response.type !== 'opaque') {
            cache.put(request, response.clone());
        }

        return response;
    } catch (error) {
        const cached = await cache.match(request);
        if (cached) {
            return cached;
        }

        throw error;
    }
}

self.addEventListener('fetch', (event) => {
    const { request } = event;
    if (request.method !== 'GET') {
        return;
    }

    const url = new URL(request.url);

    if (url.pathname.startsWith('/_framework/') || url.pathname.startsWith('/_blazor')) {
        return;
    }

    if (request.mode === 'navigate') {
        event.respondWith(
            fetch(request).catch(() => caches.match('/attendance/gate'))
        );
        return;
    }

    if (isJsRequest(url)) {
        event.respondWith(networkFirst(request));
        return;
    }

    const isStaticAsset =
        url.pathname.endsWith('.css') ||
        url.pathname.endsWith('.svg') ||
        url.pathname.endsWith('.png') ||
        url.pathname.endsWith('.webmanifest') ||
        url.hostname === 'cdn.jsdelivr.net';

    if (!isStaticAsset) {
        return;
    }

    event.respondWith(
        caches.match(request).then((cached) => {
            if (cached) {
                return cached;
            }

            return fetch(request).then((response) => {
                if (!response || response.status !== 200 || response.type === 'opaque') {
                    return response;
                }

                const copy = response.clone();
                caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
                return response;
            });
        })
    );
});
