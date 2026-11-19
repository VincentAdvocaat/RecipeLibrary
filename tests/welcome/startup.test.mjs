import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createStartup, normalizeReturnUrl } from '../../src/Web/RecipeLibrary.Web/wwwroot/startup.js';

function scenario(sequence, { startFailures = [], tokenFailures = [] } = {}) {
    let time = 0;
    let index = 0;
    let active = 0;
    const states = [], destinations = [], requests = [], timeouts = [];
    const controller = createStartup({
        backend: 'https://app.example', returnUrl: '/recipes/42?tab=ingredients',
        render: state => states.push(state), navigate: url => destinations.push(url),
        now: () => time, sleep: async ms => { time += ms; },
        timeoutSignal: ms => { timeouts.push(ms); return undefined; },
        fetcher: async (url, options) => {
            assert.equal(active++, 0, 'no overlapping requests');
            requests.push({ url: url.href, options });
            try {
                const failures = url.pathname === '/api/system/start-token' ? tokenFailures : options.method === 'POST' ? startFailures : [];
                const failure = failures.shift();
                if (failure instanceof Error) throw failure;
                if (typeof failure === 'number') return { ok: false, status: failure };
                if (failure) return failure;
                if (url.pathname === '/api/system/start-token') return { ok: true, json: async () => ({ token: 'test' }) };
                if (options.method === 'POST') return { ok: true };
                const next = sequence[Math.min(index++, sequence.length - 1)];
                if (next instanceof Error) throw next;
                if (next === 'http') return { ok: false };
                if (next === 'json') return { ok: true, json: async () => { throw new SyntaxError(); } };
                return { ok: true, json: async () => ({ state: next }) };
            } finally { active--; }
        }
    });
    return { controller, states, destinations, requests, timeouts };
}

test('explicit start posts once, waits until Ready and preserves destination', async () => {
    const s = scenario(['Starting', 'Ready']);
    await s.controller.start();
    assert.deepEqual(s.states, ['Starting']);
    assert.deepEqual(s.destinations, ['https://app.example/recipes/42?tab=ingredients']);
    assert.equal(s.requests.length, 5);
    assert.equal(s.requests[0].options.credentials, 'same-origin');
    assert.equal(s.requests[0].options.cache, 'no-store');
    assert.equal(s.requests[0].url, 'https://app.example/api/system/start-token'
    );
    assert.equal(s.requests[1].options.method, 'POST'
    );
    assert.equal(s.requests[2].url, 'https://app.example/api/system/readiness');
    assert.equal(s.timeouts[0], 10000);
});

test('network timeout, non-JSON and HTTP errors recover', async () => {
    const s = scenario([new DOMException('Timeout', 'TimeoutError'), 'json', 'http', 'Ready']);
    await s.controller.start();
    assert.equal(s.destinations.length, 1);
    assert.equal(s.requests.length, 9);
});

test('permanent failure stops, retry starts a new round', async () => {
    const s = scenario(['Failed', 'Ready']);
    await s.controller.start();
    assert.deepEqual(s.states, ['Starting', 'Failed']);
    assert.equal(s.destinations.length, 0);
    await s.controller.start();
    assert.equal(s.destinations.length, 1);
});

test('five-minute deadline stops retries and permits retry', async () => {
    const s = scenario(['Starting']);
    await s.controller.start();
    assert.deepEqual(s.states, ['Starting', 'Timeout']);
    assert.equal(s.requests.length, 121);
    await s.controller.start();
    assert.equal(s.requests.length, 242);
});

test('double click cannot start overlapping loops; page disposal stops work', async () => {
    const s = scenario(['Starting']);
    const first = s.controller.start();
    await s.controller.start();
    s.controller.dispose();
    await first;
    assert.equal(s.requests.length, 1);
    assert.equal(s.destinations.length, 0);
});

test('local destinations reject authority escapes and startup loops', () => {
    for (const value of ['//evil.example', 'https://evil.example', '/%2fevil.example', '/\\evil.example',
        '/%5cevil.example', '/starting?returnUrl=/starting', '/failed/', '/a/../starting', '/%00']) {
        assert.equal(normalizeReturnUrl(value), '/recipes', value);
    }
    assert.equal(normalizeReturnUrl('/recipes/42?tab=ingredients'), '/recipes/42?tab=ingredients');
});

for (const failure of [503, new DOMException('Timeout', 'TimeoutError')]) {
    test(`initial start failure ${failure} retries before reading readiness`, async () => {
        const s = scenario(['Ready'], { startFailures: [failure] });
        await s.controller.start();
        assert.deepEqual(s.states, ['Starting']);
        assert.equal(s.destinations.length, 1);
        assert.deepEqual(s.requests.map(r => new URL(r.url).pathname), [
            '/api/system/start-token', '/api/system/start', '/api/system/start', '/api/system/readiness'
        ]);
    });
}

test('later start timeout recovers within the same startup round', async () => {
    const s = scenario(['Starting', 'Ready'], { startFailures: [null, new DOMException('Timeout', 'TimeoutError')] });
    await s.controller.start();
    assert.deepEqual(s.states, ['Starting']);
    assert.equal(s.destinations.length, 1);
    assert.equal(s.requests.filter(r => r.options.method === 'POST').length, 3);
});

for (const status of [429, 502, 503, 504]) {
    test(`start token ${status} retries securely before posting`, async () => {
        const s = scenario(['Ready'], { tokenFailures: [status] });
        await s.controller.start();
        assert.equal(s.destinations.length, 1);
        const post = s.requests.find(r => r.options.method === 'POST');
        assert.equal(post.options.headers.RequestVerificationToken, 'test');
        assert.equal(s.requests.filter(r => new URL(r.url).pathname.endsWith('start-token')).length, 2);
    });
}

for (const status of [400, 403]) {
    test(`start ${status} is a definitive failure without readiness calls`, async () => {
        const s = scenario(['Ready'], { startFailures: [status] });
        await s.controller.start();
        assert.deepEqual(s.states, ['Starting', 'Failed']);
        assert.equal(s.destinations.length, 0);
        assert.equal(s.requests.length, 2);
    });
}

test('persistent start errors stop at the shared five-minute deadline', async () => {
    const s = scenario(['Ready'], { startFailures: Array(100).fill(503) });
    await s.controller.start();
    assert.deepEqual(s.states, ['Starting', 'Timeout']);
    assert.equal(s.requests.filter(r => r.options.method === 'POST').length, 60);
    assert.equal(s.requests.some(r => r.url.endsWith('readiness')), false);
});
