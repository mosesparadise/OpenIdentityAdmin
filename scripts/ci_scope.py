#!/usr/bin/env python3
"""Choose proportional CI and reject incomplete final evidence; uncertainty means full CI."""
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import sys

DOC_ROOTS = ('docs/', 'specs/')
POLICY_ROOTS = ('docs/engineering/', 'docs/approved-v0.2/', 'docs/amendments/')
POLICY_FILES = frozenset(('docs/approval.md',))
ROOT_DOCUMENTS = frozenset(('README.md', 'CONTRIBUTING.md', 'CHANGELOG.md'))
ALWAYS_JOBS = frozenset(('scope', 'assurance', 'source-secrets'))
FULL_JOBS = frozenset(('browser-plan', 'static-analysis', 'supply-chain', 'verify',
                       'browser', 'contrast-experiment', 'mutations', 'administrator-mutations', 'governance-mutations', 'administrator-protocol-mutations', 'browser-session-mutations', 'contracts', 'admin-ui', 'coverage'))
ORDINARY_MODES = frozenset(('100644', '000000'))
SUCCESS = 'success'
SKIPPED = 'skipped'


def is_prose(path):
    """Accept only explicit prose paths; reject special paths and executable documentation assets."""
    parts = PurePosixPath(path).parts
    if not parts or any(part in ('.', '..') or part.startswith('.') for part in parts):
        return False
    if any(character in path for character in ('\n', '\r', '\\', '\0')):
        return False
    if path.startswith(POLICY_ROOTS) or path in POLICY_FILES:
        return False
    return path in ROOT_DOCUMENTS or (path.startswith(DOC_ROOTS) and path.endswith('.md'))


def select(event_name, paths):
    """Require full checks for manual/nightly, empty, unavailable, mixed or non-prose evidence."""
    return 'docs' if event_name in ('pull_request', 'push') and paths and all(map(is_prose, paths)) else 'full'


def changed_paths(before, after):
    """Read both sides of renames as delete/add paths without newline or argument ambiguity."""
    for revision in (before, after):
        if len(revision) != 40 or any(character not in '0123456789abcdef' for character in revision):
            raise ValueError('Expected a complete Git commit identity')
    output = subprocess.check_output(['git', 'diff', '--raw', '--no-abbrev', '--no-renames', '-z', before, after, '--'],
                                     text=True, stderr=subprocess.DEVNULL, timeout=30)
    fields = output.split('\0')
    if fields[-1] != '' or len(fields) % 2 != 1:
        raise ValueError('Malformed Git change evidence')
    paths = []
    for index in range(0, len(fields) - 1, 2):
        header, path = fields[index:index + 2]
        metadata = header.removeprefix(':').split(' ')
        if not header.startswith(':') or len(metadata) != 5 or metadata[-1] not in ('A', 'M', 'D', 'T'):
            raise ValueError('Unsupported Git change evidence')
        if metadata[0] not in ORDINARY_MODES or metadata[1] not in ORDINARY_MODES:
            return None
        paths.append(path)
    return paths


def event_paths(event_name, event):
    """Resolve exact event commit identities; unavailable Git evidence cannot select a light run."""
    try:
        if event_name == 'pull_request':
            return changed_paths(event['pull_request']['base']['sha'], event['pull_request']['head']['sha'])
        if event_name == 'push':
            return changed_paths(event['before'], event['after'])
    except (KeyError, TypeError, ValueError, OSError, subprocess.SubprocessError):
        pass
    return None


def require_acceptance(scope, needs):
    """Require the exact job inventory and reject failure, cancellation or unexplained skips."""
    if scope not in ('docs', 'full') or set(needs) != ALWAYS_JOBS | FULL_JOBS:
        raise ValueError('Missing or unsupported CI scope/job inventory')
    for job, evidence in needs.items():
        expected = SKIPPED if scope == 'docs' and job in FULL_JOBS else SUCCESS
        if evidence.get('result') != expected:
            raise ValueError(f'Required CI result mismatch: {job}')


def main():
    """Emit selection to GitHub output or validate an always-running final acceptance job."""
    if sys.argv[1:] == ['accept']:
        require_acceptance(os.environ['CI_SCOPE'], json.loads(os.environ['CI_NEEDS']))
        print('PASS: every required job satisfied the selected CI scope')
        return
    event_name = os.environ.get('GITHUB_EVENT_NAME', '')
    try:
        event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    except (KeyError, OSError, ValueError):
        event = {}
    paths = event_paths(event_name, event)
    scope = select(event_name, paths)
    with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as output:
        output.write(f'scope={scope}\n')
    print(json.dumps({'scope': scope, 'paths': paths, 'event': event_name}))


if __name__ == '__main__':
    main()
