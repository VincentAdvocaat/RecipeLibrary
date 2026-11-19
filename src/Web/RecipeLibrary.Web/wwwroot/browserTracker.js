// Optional measurements have no visitor identifier. The functional cookie only stores an opt-out.
const root = document.querySelector('[data-startup]');
const backend = root?.dataset.backend || location.origin;
let lastPage = '';
export function recordVisit(event, page = location.pathname) {
    if (navigator.doNotTrack === '1' || window.doNotTrack === '1' || navigator.globalPrivacyControl === true ||
        document.cookie.split(';').some(cookie => cookie.trim() === 'RecipeLibrary.Analytics=off')) return;
    if (location.pathname === '/analytics.html' || location.pathname.startsWith('/analytics/')) return;
    let referrer = '';
    try {
        const source = new URL(document.referrer);
        if (['http:', 'https:'].includes(source.protocol) && !source.username && !source.password) referrer = source.origin;
    } catch { }
    const payload = JSON.stringify({ event, page, referrer });
    fetch(new URL('/api/analytics/visit', backend), {
        method: 'POST', mode: 'cors', credentials: 'omit', referrerPolicy: 'no-referrer',
        headers: { 'Content-Type': 'application/json' }, body: payload,
        // A scaled-to-zero backend can take longer to accept its first request.
        // This background request never delays the page or retries a recorded event.
        keepalive: true, signal: AbortSignal.timeout(60000)
    }).catch(() => {});
}
function pageView() {
    if (lastPage === location.pathname) return;
    lastPage = location.pathname;
    recordVisit(root ? 'landing' : 'browser');
}
pageView();
window.addEventListener('pageshow', pageView);
window.addEventListener('popstate', pageView);
// Blazor enhanced navigation keeps the document alive.
window.addEventListener('load', () => globalThis.Blazor?.addEventListener?.('enhancedload', pageView));
root?.querySelector('[data-continue]')?.addEventListener('click', () => recordVisit('open-app'));
root?.querySelector('[data-retry]')?.addEventListener('click', () => recordVisit('open-app'));
