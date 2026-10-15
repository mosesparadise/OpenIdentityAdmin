"""Restore the disposable driver, verify source/cache and account for owned resources."""
import json
from pathlib import Path
import shutil
import sys
from common import ORIGINAL, command, read, sha, source_hashes, write
from process import reconcile


def cleanup(repo, work):
    public = work / 'public'; public.mkdir(mode=0o700, parents=True, exist_ok=True)
    result = {'complete': False, 'source_unchanged': False, 'driver_restored': False,
              'shared_driver_unchanged': False, 'processes_clean': False, 'docker_clean': False,
              'acceptance': False}
    def attempt(action):
        try: return action()
        except BaseException: return False

    def processes():
        # The collector is the parent of the covered runner. Reconcile it before its native child.
        process_results = [attempt(lambda name=name: reconcile(work / 'private' / name))
                           for name in ('collector-process.json', 'native-process.json')]
        return all(process_results)

    def sources():
        before = json.loads(read(work / 'source-before.json'))
        return source_hashes(repo) == before

    def driver():
        original = Path(json.loads(read(work / 'private/driver.json'))['original']) / 'package/lib/coreBundle.js'
        result['shared_driver_unchanged'] = sha(original) == ORIGINAL
        target = work / 'driver/.playwright/package/lib/coreBundle.js'
        if result['shared_driver_unchanged']:
            shutil.copyfile(original, target)
            (target.parent / 'identity-webkit-observer.cjs').unlink(missing_ok=True)
            return sha(target) == ORIGINAL
        return False

    def docker():
        baseline = json.loads(read(work / 'private/docker-baseline.json'))
        assert baseline == {'containers': [], 'volumes': []}
        # A dedicated hosted runner was empty before this sole test. Only Testcontainers-labelled
        # resources are eligible; never prune a daemon or remove an unlabelled resource.
        for identifier in command(['docker', 'ps', '-aq']).splitlines():
            labels = json.loads(command(['docker', 'inspect', '--format', '{{json .Config.Labels}}', identifier])) or {}
            if labels.get('org.testcontainers') == 'true':
                command(['docker', 'rm', '-f', '-v', identifier])
        for identifier in command(['docker', 'volume', 'ls', '-q']).splitlines():
            labels = json.loads(command(['docker', 'volume', 'inspect', '--format', '{{json .Labels}}', identifier])) or {}
            if labels.get('org.testcontainers') == 'true': command(['docker', 'volume', 'rm', identifier])
        return not command(['docker', 'ps', '-aq']) and not command(['docker', 'volume', 'ls', '-q'])

    for name, action in [('processes_clean', processes), ('source_unchanged', sources),
                         ('driver_restored', driver), ('docker_clean', docker)]:
        result[name] = attempt(action)
    result['complete'] = all(result[key] for key in ('source_unchanged', 'driver_restored',
                             'shared_driver_unchanged', 'processes_clean', 'docker_clean'))
    if not result['complete']: result['failure_category'] = 'cleanup-or-restoration-incomplete'
    write(public / 'cleanup.json', result)
    return 0 if result['complete'] else 1


if __name__ == '__main__':
    raise SystemExit(cleanup(Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve()))
