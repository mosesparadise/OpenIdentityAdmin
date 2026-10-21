#!/usr/bin/env python3
"""Select every required browser cell after the internal B2 Firefox deferral."""
import json
import os

# A-007 restoration: every full run requires all three engines; no phase skip remains.
ENGINES = ('chromium', 'firefox', 'webkit')
CONTRACT_PARTITIONS = ('core', *ENGINES)
PROVIDERS = ('PostgreSql', 'SqlServer')
BROWSER_NOTICE = ('Chromium, Firefox and WebKit required on both providers for full verification. '
                  'Execution does not by itself close historical findings or activation/release gates.')


def select():
    """Require all three engines and every provider/story on all full verification runs."""
    return {'include': [{'provider': provider, 'browser': browser, 'partition': partition, 'browser_story': story,
                         'label': ('administrator-' if partition == 'administrator' else '') + provider + '-' + browser +
                                  ('-' + story.lower() if story else '')}
                        for browser in ENGINES for provider in PROVIDERS
                        for partition in ('standard', 'administrator')
                        for story in (('Accounts', 'Security') if partition == 'administrator' else ('',))]}


def main():
    """Publish consistent browser/contract obligations and an explicit three-engine verification notice."""
    result = json.dumps(select(), separators=(',', ':'))
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        output.write('matrix=' + result + '\n')
        output.write('contracts=' + json.dumps({'partition': CONTRACT_PARTITIONS}) + '\n')
    if summary := os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(summary, 'a') as output:
            output.write(BROWSER_NOTICE + '\n')
    print(BROWSER_NOTICE)
    print(result)


if __name__ == '__main__':
    main()
