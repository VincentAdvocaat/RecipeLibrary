export function normalizeReturnUrl(value) {
    if (!value || !value.startsWith('/') || value.startsWith('//') || /[\\\x00-\x1f\x7f]/.test(value)) return '/';
    try {
        const decoded = decodeURIComponent(value);
        if (decoded.startsWith('//') || /[\\\x00-\x1f\x7f]/.test(decoded)) return '/';
        const parsed = new URL(value, 'https://local.invalid');
        if (parsed.origin !== 'https://local.invalid' || /^\/(starting|failed)\/*$/i.test(decodeURIComponent(parsed.pathname))) return '/';
        return parsed.pathname + parsed.search + parsed.hash;
    } catch { return '/'; }
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
                    if (!disposed && now() < deadline) await sleep(Math.min(5000, deadline - now()));
                }
                if (!disposed) render('Timeout');
            } finally { running = false; }
        }
    };
}

const translations = {
    nl: {
        heading: 'Welkom bij RecipeLibrary', message: 'Je recepten op één plek. We zetten de receptenbibliotheek voor je klaar.',
        Starting: 'De applicatie wordt gestart. Je gaat automatisch verder zodra alles klaar is.',
        Failed: 'De receptenbibliotheek kon niet worden gestart. Probeer het later opnieuw.',
        Timeout: 'Het opstarten duurt langer dan verwacht. Probeer het opnieuw. Bij onderhoud of een bereikt gebruikslimiet kan de beheerder nodig zijn.',
        retry: 'Opnieuw proberen', continue: 'Naar de applicatie'
    },
    en: {
        heading: 'Welcome to RecipeLibrary', message: 'Your recipes in one place. We are getting your recipe library ready.',
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
        let state = root.dataset.initialFailed === 'true' ? 'Failed' : 'Starting';
        const render = next => {
            state = next;
            const text = translations[language];
            document.documentElement.lang = language;
            root.querySelector('[data-heading]').textContent = text.heading;
            root.querySelector('[data-message]').textContent = text.message;
            root.querySelector('[data-status]').textContent = text[state];
            root.querySelector('[data-retry]').textContent = text.retry;
            root.querySelector('[data-retry]').hidden = state === 'Starting';
            root.querySelector('[data-continue]').textContent = text.continue;
            root.setAttribute('aria-busy', String(state === 'Starting'));
        };
        root.querySelector('[data-continue]').href = new URL(returnUrl, backend).href;
        const controller = createStartup({ backend, returnUrl, render, navigate: url => location.replace(url) });
        root.querySelector('[data-retry]').addEventListener('click', () => controller.start());
        root.querySelectorAll('[data-select-language]').forEach(button => button.addEventListener('click', () => {
            language = button.dataset.selectLanguage;
            render(state);
        }));
        window.addEventListener('pagehide', () => controller.dispose(), { once: true });
        render(state);
        if (state === 'Starting') controller.start();
    }
}
