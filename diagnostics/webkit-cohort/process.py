"""Journal and clean the process groups owned by this one diagnostic invocation."""
import json
import os
from pathlib import Path
import signal
import subprocess
import time
from common import read, write


def start_ticks(pid):
    # comm can contain spaces and parentheses; fields after its final ')' start at state.
    return int(Path(f'/proc/{pid}/stat').read_text().rsplit(')', 1)[1].split()[19])


def group_alive(pid):
    try: os.killpg(pid, 0); return True
    except ProcessLookupError: return False


def terminate(pid):
    for sig in (signal.SIGTERM, signal.SIGKILL):
        try: os.killpg(pid, sig)
        except ProcessLookupError: pass


def run(command, env, repo, journal, timeout):
    process, info = None, None
    try:
        process = subprocess.Popen(command, cwd=repo, env=env, start_new_session=True)
        info = {'pid': process.pid, 'start_ticks': start_ticks(process.pid), 'complete': False}
        write(journal, info)
        return process.wait(timeout=timeout)
    finally:
        if process is not None:
            for sig in (signal.SIGTERM, signal.SIGKILL):
                try: os.killpg(process.pid, sig)
                except ProcessLookupError: pass
                try: process.wait(timeout=10)
                except subprocess.TimeoutExpired: pass
            process.wait(timeout=10)
            end = time.monotonic() + 10
            while group_alive(process.pid) and time.monotonic() < end: time.sleep(0.05)
            if info is not None:
                info['complete'] = not group_alive(process.pid)
                write(journal, info)


def reconcile(journal):
    if not journal.exists(): return False
    info = json.loads(read(journal))
    if (type(info) is not dict or set(info) != {'pid', 'start_ticks', 'complete'}
            or type(info['pid']) is not int or info['pid'] <= 1
            or type(info['start_ticks']) is not int or info['start_ticks'] <= 0
            or type(info['complete']) is not bool):
        return False
    pid = info['pid']
    if not group_alive(pid): return True
    # If a cancelled invocation still has its exact leader, terminate only that owned group.
    # Missing/reused identity cannot justify targeting a group; mark cleanup incomplete instead.
    try: matches = start_ticks(pid) == info['start_ticks']
    except (FileNotFoundError, ProcessLookupError): matches = False
    if not matches: return False
    terminate(pid)
    end = time.monotonic() + 10
    while group_alive(pid) and time.monotonic() < end: time.sleep(0.05)
    return not group_alive(pid)
