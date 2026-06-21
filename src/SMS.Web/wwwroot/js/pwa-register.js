(() => {
    let deferredPrompt = null;

    window.addEventListener('beforeinstallprompt', (event) => {
        event.preventDefault();
        deferredPrompt = event;
        window.dispatchEvent(new CustomEvent('sms-pwa-install-available'));
    });

    window.smsPwaRegisterHandlers = (dotNetRef) => {
        const notify = () => dotNetRef.invokeMethodAsync('OnInstallAvailable');
        window.addEventListener('sms-pwa-install-available', notify);
        if (deferredPrompt) {
            notify();
        }
    };

    window.smsPwaIsDesktopChrome = () => {
        const ua = window.navigator.userAgent;
        const isMobile = /android|iphone|ipad|ipod/i.test(ua);
        const isChromium = /chrome|edg/i.test(ua) && !/opr|opera/i.test(ua);
        return !isMobile && isChromium;
    };

    window.smsPwaInstall = async () => {
        if (!deferredPrompt) {
            return false;
        }

        deferredPrompt.prompt();
        const choice = await deferredPrompt.userChoice;
        deferredPrompt = null;
        return choice.outcome === 'accepted';
    };

    window.smsPwaIsInstalled = () =>
        window.matchMedia('(display-mode: standalone)').matches ||
        window.navigator.standalone === true;

    window.smsPwaIsIos = () =>
        /iphone|ipad|ipod/i.test(window.navigator.userAgent);

    window.smsPwaIsAndroid = () =>
        /android/i.test(window.navigator.userAgent);

    window.smsPwaIsSecureContext = () => window.isSecureContext;

    function buildHttpsUrl(pathname, search = '') {
        const host = window.location.hostname;
        const port = window.location.port;

        // Local dev: dotnet run uses HTTP :5258 and HTTPS :7258
        if (port === '5258') {
            return `https://${host}:7258${pathname}${search}`;
        }

        // IIS / production: standard HTTPS on 443 (no port in URL)
        if (!port || port === '80') {
            return `https://${host}${pathname}${search}`;
        }

        // Custom HTTP port — try HTTPS on same port
        return `https://${host}:${port}${pathname}${search}`;
    }

    window.smsGateGetSecureUrl = () => {
        if (window.isSecureContext) {
            return window.location.href;
        }

        return buildHttpsUrl(window.location.pathname, window.location.search);
    };

    window.smsIsMobileDevice = () =>
        /android|iphone|ipad|ipod/i.test(window.navigator.userAgent);

    window.smsGetPhoneGateUrl = () => {
        const host = window.location.hostname;
        if (host === 'localhost' || host === '127.0.0.1') {
            return null;
        }

        return buildHttpsUrl('/attendance/gate');
    };

    if ('serviceWorker' in navigator) {
        window.addEventListener('load', () => {
            navigator.serviceWorker.register('/service-worker.js', { scope: '/' })
                .catch((error) => console.warn('Service worker registration failed:', error));
        });
    }

    (function redirectMobileBiometricToHttps() {
        const isMobile = /android|iphone|ipad|ipod/i.test(window.navigator.userAgent);
        if (!isMobile || window.isSecureContext) {
            return;
        }

        const path = window.location.pathname;
        if (!path.startsWith('/attendance/gate') && !path.startsWith('/attendance/local-test')) {
            return;
        }

        const httpsUrl = buildHttpsUrl(path, window.location.search);
        if (window.location.href !== httpsUrl) {
            window.location.replace(httpsUrl);
        }
    })();
})();
