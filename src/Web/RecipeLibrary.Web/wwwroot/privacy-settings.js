const cookieName = 'RecipeLibrary.Analytics';
const browserObjects = () => navigator.doNotTrack === '1' || window.doNotTrack === '1' || navigator.globalPrivacyControl === true;
const disabled = () => document.cookie.split(';').some(part => part.trim() === `${cookieName}=off`);
const status = document.querySelector('#privacy-choice-status');
function render() {
    status.textContent = browserObjects()
        ? 'Bezoekersmetingen staan uit door uw browservoorkeur (DNT/GPC). / Analytics is disabled by your browser preference.'
        : disabled()
            ? 'Bezoekersmetingen staan uit voor dit webadres. / Analytics is disabled for this website address.'
            : 'Alleen de hieronder beschreven beperkte bezoekersmetingen zijn actief. / Only the limited analytics described below is active.';
}
for (const button of document.querySelectorAll('[data-analytics-choice]')) {
    button.disabled = false;
    button.addEventListener('click', () => {
        try {
            const off = button.dataset.analyticsChoice === 'off';
            document.cookie = `${cookieName}=${off ? 'off' : ''}; Path=/; Max-Age=${off ? 31536000 : 0}; SameSite=Lax${location.protocol === 'https:' ? '; Secure' : ''}`;
            render();
            if (off && !disabled()) status.textContent = 'Uw browser kon de voorkeur niet bewaren. Gebruik DNT/GPC of neem contact op. / Your browser could not save the preference; use DNT/GPC or contact us.';
        } catch {
            status.textContent = 'Uw browser blokkeert het bewaren van deze voorkeur. / Your browser blocks saving this preference.';
        }
    });
}
render();
