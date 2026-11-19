import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const code = readFileSync(new URL('../../src/Web/RecipeLibrary.Web/wwwroot/browserTracker.js', import.meta.url), 'utf8').replace('export function', 'function');
function browser({ navigator = {}, cookie = '', referrer = 'https://source.example/private?email=private@example.com', doNotTrack } = {}) {
    const requests = [];
    const document = { cookie, referrer, querySelector: () => null };
    const context = { navigator, document, location: { pathname: '/recipes', origin: 'https://app.example' },
        window: { doNotTrack, addEventListener() {} }, URL, AbortSignal: { timeout: () => undefined },
        fetch: (url, options) => { requests.push({ url, options }); return Promise.resolve(); } };
    runInNewContext(code, context);
    return { requests, context, document };
}

test('referrer is reduced to origin before the request leaves the browser', () => {
    const b = browser();
    assert.equal(b.requests.length, 1);
    const request = b.requests[0];
    assert.equal(JSON.parse(request.options.body).referrer, 'https://source.example');
    assert.equal(request.options.body.includes('private'), false);
    assert.equal(request.options.credentials, 'omit');
    assert.equal(request.options.referrerPolicy, 'no-referrer');
    assert.equal(b.document.cookie, '');
});
for (const options of [{ navigator: { doNotTrack: '1' } }, { doNotTrack: '1' },
    { navigator: { globalPrivacyControl: true } }, { cookie: 'other=value; RecipeLibrary.Analytics=off' }]) {
    test(`opt-out suppresses page and explicit beacons ${JSON.stringify(options)}`, () => {
        const b = browser(options);
        b.context.recordVisit('open-app');
        assert.equal(b.requests.length, 0);
    });
}
test('opting out after page load suppresses further beacons', () => {
    const b = browser();
    b.document.cookie = 'RecipeLibrary.Analytics=off';
    b.context.recordVisit('open-app');
    assert.equal(b.requests.length, 1);
});
for (const referrer of ['', 'javascript:alert(1)', 'https://user:password@source.example/private']) {
    test(`unsafe or missing referrer is omitted ${referrer}`, () => {
        assert.equal(JSON.parse(browser({ referrer }).requests[0].options.body).referrer, '');
    });
}
