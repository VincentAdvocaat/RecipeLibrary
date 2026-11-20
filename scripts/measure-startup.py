"""Read-only live container timing. Never connects to SQL or requests app startup.

Requires Azure CLI login (management reads only) and curl. Run outside deployment
windows; browser visits/monitors can warm the target and invalidate a cold sample.
"""
import argparse
import json
import shutil
import statistics
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path

GROUP = 'rg-recipelibrary-test-sec'
APP = 'ca-rl-test-sec-gdqfvo'
SERVER = 'sql-recipelibrary-test-sec-gdqfvo7drqwie'
DATABASE = 'recipelibrary-test-sec'
URL = 'https://app-recipelibrary.vasoftware.nl/health/live'


def utc():
    return datetime.now(timezone.utc).isoformat()


def azure(*args):
    result = subprocess.run([shutil.which('az') or 'az', *args, '-o', 'json'],
                            capture_output=True, text=True, timeout=90)
    if result.returncode:
        raise RuntimeError('Azure management read failed; no HTTP probe sent')
    return json.loads(result.stdout)


def database():
    return azure('sql', 'db', 'show', '-g', GROUP, '-s', SERVER, '-n', DATABASE,
                 '--query', '{status:status,free:useFreeLimit,behavior:freeLimitExhaustionBehavior}')


def http_probe():
    # No redirect following or retries: do not hide a failed first request.
    curl = shutil.which('curl.exe') or shutil.which('curl') or 'curl'
    result = subprocess.run([curl, '--silent', '--show-error', '--max-time', '120',
                             '--output', 'NUL' if __import__('os').name == 'nt' else '/dev/null',
                             '--write-out', '%{json}', URL],
                            capture_output=True, text=True, timeout=125)
    data = json.loads(result.stdout) if result.stdout else {}
    # Retain timings only, not remote addresses, cookies, headers or credentials.
    return {'at': utc(), 'exitCode': result.returncode,
            **{k: data.get(k) for k in ('http_code', 'time_namelookup', 'time_connect',
                                       'time_appconnect', 'time_starttransfer', 'time_total')}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--samples', type=int, choices=range(1, 4), default=3)
    parser.add_argument('--idle-timeout', type=int, default=1200,
                        help='Maximum seconds to wait for zero replicas before each sample')
    args = parser.parse_args()
    report = {'startedAt': utc(), 'target': URL, 'sqlBefore': database(), 'samples': []}

    def save():
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2), encoding='utf-8')

    save()
    for number in range(1, args.samples + 1):
        deadline = time.monotonic() + args.idle_timeout
        while True:
            revisions = azure('containerapp', 'revision', 'list', '-g', GROUP, '-n', APP,
                              '--query', '[?properties.active].{name:name,replicas:properties.replicas}')
            if len(revisions) == 1 and revisions[0]['replicas'] == 0:
                observed = utc()
                break
            if time.monotonic() >= deadline:
                raise TimeoutError('Target did not scale to zero; no cold sample fabricated')
            print(json.dumps({'at': utc(), 'sample': number, 'waitingForZeroReplicas': True}), flush=True)
            time.sleep(30)
        print(json.dumps({'sample': number, 'zeroReplicasAt': observed}), flush=True)
        cold = http_probe()
        warm = http_probe()
        sample = {'number': number, 'zeroReplicasAt': observed, 'revision': revisions[0]['name'],
                  'cold': cold, 'warm': warm, 'sqlAfter': database()}
        report['samples'].append(sample)
        save()
        print(json.dumps(sample), flush=True)
        if any(p['exitCode'] != 0 or p['http_code'] != 200 for p in (cold, warm)):
            raise RuntimeError('Probe failed; result saved, not reported as successful startup')
    report['finishedAt'] = utc()
    report['summary'] = {state: {
        'medianSeconds': statistics.median(s[state]['time_total'] for s in report['samples']),
        'maxSeconds': max(s[state]['time_total'] for s in report['samples'])}
        for state in ('cold', 'warm')}
    save()
    print(json.dumps(report['summary']), flush=True)


if __name__ == '__main__':
    main()
