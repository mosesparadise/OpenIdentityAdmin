#!/usr/bin/env python3
"""Run the required matrix cells without silently skipping an unavailable provider."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import signal
from test_evidence import require_complete_trx
from test_selection import catalogue, require_partition, require_execution_names

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("profile", choices=["unit", "PostgreSql", "SqlServer"])
parser.add_argument("--browser", choices=["chromium", "firefox", "webkit"])
args = parser.parse_args()
if args.browser and args.profile == "unit":
    parser.error("Browser verification requires a real provider")
matrix = json.loads((ROOT / "eng" / "test-matrix.json").read_text())
projects = matrix["browser"] if args.browser else matrix["profiles"][args.profile]
label = args.profile + ("-" + args.browser if args.browser else "")
results = ROOT / "artifacts" / "tests" / label
results.mkdir(parents=True, exist_ok=True)
for project in projects:
    name = f"IdentityPlatform.{project}.Tests"
    command = ["dotnet", "test", "--project", str(ROOT / "tests" / matrix["projects"][project]["group"] / name / f"{name}.csproj"),
               "--no-build", "--no-restore", "--no-progress", "--report-trx", "--report-trx-filename", f"{project}.trx",
               "--results-directory", str(results), "--pre-enumerate-theories", "on"]
    selected = None
    if project == "Issuer":
        # Invoke the native executable: the SDK's discovery transport does not forward JSON output.
        discovery = ["dotnet", "run", "--project", command[3], "--no-build", "--no-restore", "--",
                     "--pre-enumerate-theories", "on", "--list-tests", "json"]
        def discover(expression=None):
            invocation = discovery + (["--filter", expression] if expression else [])
            result = subprocess.run(invocation, cwd=ROOT, capture_output=True, text=True, timeout=60, check=True)
            return catalogue(result.stdout)
        all_tests = discover()
        http_filter = f"Provider={args.profile}&Category!=Browser"
        browser_filter = f"Provider={args.profile}&Category=Browser"
        http, browser = discover(http_filter), discover(browser_filter)
        require_partition(all_tests, http, browser, args.profile, matrix["profiles"][args.profile]["Issuer"], matrix["browser"]["Issuer"])
        selected = browser if args.browser else http
        command += ["--filter", browser_filter if args.browser else http_filter]
    elif args.profile != "unit":
        command += ["--filter-trait", f"Provider={args.profile}"]
    elif project == "AppHost":
        command += ["--filter-trait", "Category=Harness"]
    environment = os.environ.copy()
    environment["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1"
    environment["IDENTITY_TEST_ARTIFACTS"] = str(results)
    if args.browser:
        environment["IDENTITY_TEST_BROWSER"] = args.browser
    if project in ("AppHost", "Issuer"):
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
    if selected is not None:
        require_execution_names(report, selected)
    print(f"Verified {project}/{label}: {count} required tests passed; none skipped", flush=True)
