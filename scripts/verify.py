#!/usr/bin/env python3
"""Run the required matrix cells without silently skipping an unavailable provider."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import signal
from test_evidence import require_complete_trx

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("profile", choices=["unit", "PostgreSql", "SqlServer"])
args = parser.parse_args()
matrix = json.loads((ROOT / "eng" / "test-matrix.json").read_text())
projects = matrix["profiles"][args.profile]
results = ROOT / "artifacts" / "tests" / args.profile
results.mkdir(parents=True, exist_ok=True)
for project in projects:
    name = f"IdentityPlatform.{project}.Tests"
    command = ["dotnet", "test", "--project", str(ROOT / "tests" / matrix["projects"][project]["group"] / name / f"{name}.csproj"),
               "--no-build", "--no-restore", "--no-progress", "--report-trx", "--report-trx-filename", f"{project}.trx",
               "--results-directory", str(results), "--pre-enumerate-theories", "on"]
    if args.profile != "unit":
        command += ["--filter-trait", f"Provider={args.profile}"]
    elif project == "AppHost":
        command += ["--filter-trait", "Category=Harness"]
    environment = os.environ.copy()
    environment["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1"
    if project == "AppHost":
        environment["DOTNET_ENVIRONMENT"] = "Development"
    report = results / f"{project}.trx"
    report.unlink(missing_ok=True)
    with subprocess.Popen(command, cwd=ROOT, env=environment, start_new_session=True) as process:
        try:
            exit_code = process.wait(timeout=180 if args.profile == "unit" else 660)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGTERM)
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
            raise RuntimeError(f"{project}/{args.profile} exceeded its outer execution deadline") from None
        if exit_code:
            raise subprocess.CalledProcessError(exit_code, command)
    count = require_complete_trx(report, expected_count=projects[project])
    print(f"Verified {project}/{args.profile}: {count} required tests passed; none skipped", flush=True)
