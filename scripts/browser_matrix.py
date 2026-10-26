#!/usr/bin/env python3
"""Select every phase-required browser cell; A-009 defers Firefox during internal roadmap development."""
import json
import os

# A-009: retain Firefox as deferred evidence until browser-support/release acceptance.
ENGINES = ('chromium', 'webkit')
CONTRACT_PARTITIONS = ('core', *ENGINES)
PROVIDERS = ('PostgreSql', 'SqlServer')
PHASE_NOTICE = ('A-009 internal roadmap development: Chromium and WebKit required; '
                'Firefox deferred, not passed. Known browser issues are tracked; restore before release acceptance.')


def select():
    """Require both active engines and every provider/story on all full verification runs."""
    return {'include': [{'provider': provider, 'browser': browser, 'partition': partition, 'browser_story': story,
                         'label': ('administrator-' if partition == 'administrator' else '') + provider + '-' + browser +
                                  ('-' + story.lower() if story else '')}
                        for browser in ENGINES for provider in PROVIDERS
                        for partition in ('standard', 'administrator')
                        for story in (('Accounts', 'Security', 'BrowserSessions', 'BrowserLogin', 'BrowserProtection') if partition == 'administrator' else ('',))]}


def main():
    """Publish consistent browser/contract obligations and a visible Firefox deferral notice."""
    result = json.dumps(select(), separators=(',', ':'))
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        output.write('matrix=' + result + '\n')
        output.write('contracts=' + json.dumps({'partition': CONTRACT_PARTITIONS}) + '\n')
    if summary := os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(summary, 'a') as output:
            output.write(PHASE_NOTICE + '\n')
    print(PHASE_NOTICE)
    print(result)


if __name__ == '__main__':
    main()
