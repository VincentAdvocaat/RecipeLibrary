const $ = selector => document.querySelector(selector);
const names = { request: 'Serververzoek', landing: 'Welkomstpagina', browser: 'Browserpagina', 'open-app': 'Applicatie openen', 'database-start-requested': 'Start aangevraagd', 'database-start-success': 'Database gereed', 'database-start-failure': 'Start mislukt', 'database-probe': 'Databasecontrole', 'database-probe-failure': 'Controle mislukt', 'database-connection-open': 'SQL-verbinding geopend', 'database-connection-failure': 'SQL-verbinding mislukt' };
const count = value => new Intl.NumberFormat('nl-NL').format(Number(value) || 0);
const date = value => value ? new Date(value).toLocaleString('nl-NL') : '—';
let report;
let key = sessionStorage.getItem('recipelibrary-analytics-key') || '';
function text(tag, value, className) {
    const node = document.createElement(tag);
    node.textContent = value ?? '—';
    if (className) node.className = className;
    return node;
}
function table(selector, rows) {
    const body = $(selector);
    body.replaceChildren();
    if (!rows.length) { const row = document.createElement('tr'); const cell = text('td', 'Nog geen meldingen in deze periode.'); cell.colSpan = body.closest('table').querySelectorAll('thead th').length; row.append(cell); body.append(row); return; }
    for (const values of rows) { const row = document.createElement('tr'); values.forEach(value => row.append(text('td', value))); body.append(row); }
}
function top(selector, values, fallback) {
    const target = $(selector);
    target.replaceChildren();
    const sorted = Object.entries(values).sort((a, b) => b[1] - a[1]).slice(0, 12);
    if (!sorted.length) { target.append(text('p', 'Nog geen gegevens.', 'hint')); return; }
    for (const [label, value] of sorted) { const row = text('div', '', 'row'); row.append(text('span', label || fallback), text('b', count(value))); target.append(row); }
}
function render(data) {
    report = data;
    const hours = data.hours || [];
    const totals = {};
    const breakdown = { paths: {}, referrers: {}, bots: {} };
    const events = [];
    for (const hour of hours) {
        for (const [kind, value] of Object.entries(hour.counts || {})) totals[kind] = (totals[kind] || 0) + value;
        for (const dimension of Object.keys(breakdown)) for (const [label, value] of Object.entries(hour[dimension] || {})) breakdown[dimension][label] = (breakdown[dimension][label] || 0) + value;
        events.push(...(hour.events || []));
    }
    events.sort((a, b) => Date.parse(b.at) - Date.parse(a.at));
    $('#stats').replaceChildren();
    for (const [label, number, hint] of [
        ['Serververzoeken', totals.request, 'Exclusief statische bestanden en healthchecks'],
        ['Browsermeldingen', (totals.landing || 0) + (totals.browser || 0), 'JavaScript actief; geen unieke personen'],
        ['Startverzoeken', totals['database-start-requested'], 'Bewuste startacties en hun aanleiding'],
        ['Mislukte starts', totals['database-start-failure'], 'Bekijk de gebeurtenissen hieronder']
    ]) { const card = text('article', '', 'stat'); card.append(text('div', label), text('strong', count(number)), text('span', hint)); $('#stats').append(card); }
    const bars = $('#chart'); bars.replaceChildren();
    const sortedHours = [...hours].sort((a, b) => a.hour.localeCompare(b.hour));
    const max = Math.max(1, ...sortedHours.map(hour => hour.counts?.request || 0));
    for (const hour of sortedHours) { const bar = text('div', '', 'bar'); const total = hour.counts?.request || 0; bar.style.height = `${Math.max(1, total / max * 100)}%`; bar.title = `${date(hour.hour)} · ${count(total)} verzoeken`; bars.append(bar); }
    if (!hours.length) bars.append(text('p', 'Nog geen verkeer gemeten.', 'hint'));
    top('#referrers', breakdown.referrers, 'Direct / onbekend');
    top('#paths', breakdown.paths, '/');
    top('#bots', breakdown.bots, 'Geen herkenbare bot');
    table('#wake-events', events.filter(event => event.kind?.startsWith('database-')).slice(0, 200).map(event => [date(event.at), names[event.kind] || event.kind, event.reason, event.path, event.requestId]));
    table('#events', events.filter(event => !event.kind?.startsWith('database-')).slice(0, 200).map(event => [date(event.at), names[event.kind] || event.kind, [event.method, event.path].filter(Boolean).join(" "), event.status || '—', event.referrer || 'Direct / onbekend', [event.browser, event.bot].filter(Boolean).join(' / '), event.requestId]));
    $('#updated').textContent = `Bijgewerkt ${date(data.generatedAt)}. Tijden in je lokale tijdzone.`;
    const persistence = data.persistence || {};
    const available = persistence.configured && persistence.available;
    $('#storage').classList.toggle('warning', !available);
    $('#storage').textContent = available
        ? `Meetgegevens worden bewaard. Laatste opslag ${date(persistence.lastSuccess)}. ${count(persistence.buffered)} meldingen wachten op verwerking.`
        : `Opslagstatus: ${persistence.configured ? 'tijdelijk niet bereikbaar' : 'lokale meetmodus'}. Gegevens kunnen nog op verwerking wachten. ${count(persistence.buffered)} gebufferd, ${count(persistence.dropped)} overgeslagen.`;
    const sampled = hours.reduce((sum, hour) => sum + (hour.droppedDetails || 0), 0);
    $('#limits').textContent = `Bewaartermijn ${data.limits?.retentionDays || 31} dagen. Maximaal ${data.limits?.detailsPerHour || 200} detailmeldingen per uur. ${count(sampled)} details buiten de steekproef. ${count(persistence.dropped)} meldingen overgeslagen door begrenzing. Alleen verkeer sinds de installatie wordt getoond.`;
    $('#access').hidden = true;
    $('#dashboard').hidden = false;
}
async function load() {
    $('#error').hidden = true;
    $('#refresh').disabled = true;
    try {
        const response = await fetch(`/api/analytics/report?days=${$('#days').value}`, { headers: { 'X-Analytics-Key': key }, credentials: 'omit', cache: 'no-store', signal: AbortSignal.timeout(15000) });
        if (response.status === 401 || response.status === 403) { lock(); throw new Error('Deze beheersleutel is ongeldig.'); }
        if (!response.ok) throw new Error('Het dashboard kan de meetgegevens tijdelijk niet ophalen. Probeer het opnieuw.');
        const data = await response.json();
        sessionStorage.setItem('recipelibrary-analytics-key', key);
        render(data);
    } catch (error) { $('#error').textContent = error.message || 'Ophalen mislukt.'; $('#error').hidden = false; }
    finally { $('#refresh').disabled = false; }
}
function lock() { key = ''; report = undefined; sessionStorage.removeItem('recipelibrary-analytics-key'); $('#key').value = ''; $('#access').hidden = false; $('#dashboard').hidden = true; }
$('#unlock').addEventListener('submit', event => { event.preventDefault(); key = $('#key').value.trim(); load(); });
$('#refresh').addEventListener('click', load);
$('#days').addEventListener('change', load);
$('#lock').addEventListener('click', lock);
$('#download').addEventListener('click', () => {
    if (!report) return;
    const url = URL.createObjectURL(new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' }));
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = `recipelibrary-verkeer-${new Date().toISOString().slice(0, 10)}.json`; anchor.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
});
if (key) load();
