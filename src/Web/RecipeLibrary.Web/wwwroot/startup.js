export function normalizeReturnUrl(value) {
    if (!value || !value.startsWith('/') || value.startsWith('//') || /[\\\x00-\x1f\x7f]/.test(value)) return '/recipes';
    try {
        const decoded = decodeURIComponent(value);
        if (decoded.startsWith('//') || /[\\\x00-\x1f\x7f]/.test(decoded)) return '/recipes';
        const parsed = new URL(value, 'https://local.invalid');
        if (parsed.origin !== 'https://local.invalid' || /^\/(starting|failed)\/*$/i.test(decodeURIComponent(parsed.pathname))) return '/recipes';
        return parsed.pathname + parsed.search + parsed.hash;
    } catch { return '/recipes'; }
}

// Dependencies are injectable so timing and network failures can be tested without waiting minutes.
export function createStartup({ backend, returnUrl, render, navigate,
    fetcher = globalThis.fetch, now = () => performance.now(),
    sleep = ms => new Promise(resolve => setTimeout(resolve, ms)),
    timeoutSignal = ms => AbortSignal.timeout(ms) }) {
    let running = false;
    let disposed = false;
    return {
        dispose() { disposed = true; },
        async start() {
            if (running || disposed) return;
            running = true;
            const deadline = now() + 300000;
            render('Starting');
            try {
                const requestUntilAvailable = async (path, options, read = response => response) => {
                    while (!disposed && now() < deadline) {
                        try {
                            const response = await fetcher(new URL(path, backend), {
                                ...options,
                                signal: timeoutSignal(Math.max(1, Math.min(10000, deadline - now())))
                            });
                            if (disposed) return;
                            if (!response.ok) {
                                const error = new Error('Start unavailable');
                                error.permanent = ![429, 502, 503, 504].includes(response.status);
                                throw error;
                            }
                            const result = await read(response);
                            if (!disposed && now() < deadline) return result;
                        } catch (error) {
                            if (error.permanent) throw error;
                        }
                        if (!disposed && now() < deadline) await sleep(Math.min(5000, deadline - now()));
                    }
                    throw new Error('Startup deadline');
                };
                const sameOrigin = typeof location === 'undefined' || new URL(backend).origin === location.origin;
                const headers = { 'X-Application-Start': 'user' };
                if (sameOrigin) {
                    headers.RequestVerificationToken = await requestUntilAvailable('/api/system/start-token',
                        { cache: 'no-store', credentials: 'same-origin' }, async response => {
                            const { token } = await response.json();
                            if (typeof token !== 'string' || !token) throw new Error('Invalid start token');
                            return token;
                        });
                }
                if (disposed) return;
                const sendStart = () => requestUntilAvailable('/api/system/start', {
                    method: 'POST', headers, credentials: sameOrigin ? 'same-origin' : 'omit'
                });
                await sendStart();
                while (!disposed && now() < deadline) {
                    try {
                        const response = await fetcher(new URL('/api/system/readiness', backend), {
                            cache: 'no-store', credentials: 'omit',
                            signal: timeoutSignal(Math.max(1, Math.min(10000, deadline - now())))
                        });
                        if (!response.ok) throw new Error('Backend unavailable');
                        const body = await response.json();
                        if (disposed) return;
                        if (body.state === 'Ready' && now() < deadline) {
                            navigate(new URL(normalizeReturnUrl(returnUrl), backend).href);
                            return;
                        }
                        if (body.state === 'Failed') { render('Failed'); return; }
                    } catch {
                        // Ingress errors, SQL resumption and offline browsers can all recover.
                    }
                    if (!disposed && now() < deadline) {
                        await sleep(Math.min(5000, deadline - now()));
                        // This loop exists only after a deliberate click. Retry a resume probe,
                        // while ordinary readiness polling remains passive.
                        if (!disposed && now() < deadline) await sendStart();
                    }
                }
                if (!disposed) render('Timeout');
            } catch { if (!disposed) render(now() >= deadline ? 'Timeout' : 'Failed'); } finally { running = false; }
        }
    };
}

const translations = {
    nl: {
        Idle: 'Klik op Naar de applicatie om de receptenbibliotheek te starten.',
        heading: 'Welkom bij RecipeLibrary', message: 'Je recepten op één plek. Start de receptenbibliotheek wanneer je verder wilt.',
        Starting: 'De applicatie wordt gestart. Je gaat automatisch verder zodra alles klaar is.',
        Failed: 'De receptenbibliotheek kon niet worden gestart. Probeer het later opnieuw.',
        Timeout: 'Het opstarten duurt langer dan verwacht. Probeer het opnieuw. Bij onderhoud of een bereikt gebruikslimiet kan de beheerder nodig zijn.',
        retry: 'Opnieuw proberen', continue: 'Naar de applicatie'
    },
    en: {
        Idle: 'Click Open the application to start your recipe library.',
        heading: 'Welcome to RecipeLibrary', message: 'Your recipes in one place. Start your recipe library when you want to continue.',
        Starting: 'The application is starting. You will continue automatically when everything is ready.',
        Failed: 'The recipe library could not start. Please try again later.',
        Timeout: 'Startup is taking longer than expected. Please try again. Maintenance or a usage limit may require help from the administrator.',
        retry: 'Try again', continue: 'Open the application'
    }
};

if (typeof document !== 'undefined') {
    const root = document.querySelector('[data-startup]');
    if (root) {
        const backend = new URL(root.dataset.backend || location.origin);
        const returnUrl = normalizeReturnUrl(new URLSearchParams(location.search).get('returnUrl'));
        let language = root.dataset.language === 'en' ? 'en' : 'nl';
        let state = root.dataset.initialFailed === 'true' ? 'Failed' : 'Idle';
        const render = next => {
            state = next;
            const text = translations[language];
            document.documentElement.lang = language;
            root.querySelector('[data-heading]').textContent = text.heading;
            root.querySelector('[data-message]').textContent = text.message;
            root.querySelector('[data-status]').textContent = text[state];
            root.querySelector('[data-retry]').textContent = text.retry;
            root.querySelector('[data-retry]').hidden = state === 'Starting' || state === 'Idle';
            root.querySelector('[data-continue]').textContent = text.continue;
            root.setAttribute('aria-busy', String(state === 'Starting'));
        };
        root.querySelector('[data-continue]').href = new URL(returnUrl, backend).href;
        root.querySelector('[data-continue]').addEventListener('click', event => { event.preventDefault(); controller.start(); });
        const controller = createStartup({ backend, returnUrl, render, navigate: url => location.replace(url) });
        root.querySelector('[data-retry]').addEventListener('click', () => controller.start());
        root.querySelectorAll('[data-select-language]').forEach(button => button.addEventListener('click', () => {
            language = button.dataset.selectLanguage;
            render(state);
        }));
        window.addEventListener('pagehide', () => controller.dispose(), { once: true });
        render(state);
        // The database starts only after a button or link click.
    }
}
