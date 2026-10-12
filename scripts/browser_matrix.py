#!/usr/bin/env python3
"""Select browser obligations conservatively; unavailable change evidence means all engines."""
import json
import os
from pathlib import Path
import subprocess

ENGINES = ('chromium', 'firefox', 'webkit')
PROVIDERS = ('PostgreSql', 'SqlServer')


def select(event, changed):
    broad = event in ('schedule', 'workflow_dispatch') or changed is None
    prefixes = ('.github/', 'eng/', 'scripts/', 'tests/', 'src/backend/IdentityPlatform.AuthServer/',
                'src/frontend/IdentityPlatform.DesignTokens/', 'src/aspire/', 'src/migrations/', 'src/persistence/')
    broad |= any(path.startswith(prefixes) or path.endswith(('packages.lock.json', '.props', '.targets', '.csproj'))
                 or path in ('global.json', 'NuGet.Config', 'dotnet-tools.json') for path in changed or ())
    return {'include': [{'provider': provider, 'browser': browser, 'partition': partition, 'browser_story': story,
                         'label': ('administrator-' if partition == 'administrator' else '') + provider + '-' + browser +
                                  ('-' + story.lower() if story else '')}
                        for browser in ENGINES if broad or browser == 'chromium'
                        for provider in PROVIDERS for partition in ('standard', 'administrator')
                        for story in (('Accounts', 'Security') if partition == 'administrator' else ('',))]}



def changed_paths(before, after):
    output = subprocess.check_output(['git', 'diff', '--name-only', '-z', before, after, '--'], text=True, stderr=subprocess.DEVNULL)
    return [path for path in output.split('\0') if path]


def main():
    event_name = os.environ.get('GITHUB_EVENT_NAME', '')
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    changed = None
    try:
        if event_name == 'pull_request':
            before, after = event['pull_request']['base']['sha'], event['pull_request']['head']['sha']
        elif event_name == 'push':
            before, after = event['before'], event['after']
        else:
            before = after = ''
        if before and after:
            changed = changed_paths(before, after)
    except (KeyError, subprocess.CalledProcessError):
        changed = None
    result = json.dumps(select(event_name, changed), separators=(',', ':'))
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        output.write('matrix=' + result + '\n')
    print(result)


if __name__ == '__main__':
    main()
