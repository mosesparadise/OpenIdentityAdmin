#!/usr/bin/env python3
"""Run the required matrix cells without silently skipping an unavailable provider."""
import argparse
import json
import os
from pathlib import Path
from verify_contrast_experiment import execute, install_cancellation_handler
from test_evidence import require_complete_trx
from test_selection import catalogue, require_partition, require_execution_names, require_lifecycle_execution, select_projects, lifecycle_invocations, require_lifecycle_story, require_administrator_story, administrator_invocations

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("profile", choices=["unit", "PostgreSql", "SqlServer"])
parser.add_argument("--browser", choices=["chromium", "firefox", "webkit"])
parser.add_argument("--partition", choices=["all", "standard", "lifecycle", "administrator"], default="all")
parser.add_argument("--story", choices=["Restart", "Distribution", "KeyRotation.Activation", "KeyRotation.Retirement", "Bootstrap", "Factors"])
args = parser.parse_args()
install_cancellation_handler()
if args.story and (args.partition not in ("lifecycle", "administrator") or
                   (args.story in ("Bootstrap", "Factors")) != (args.partition == "administrator")):
    parser.error("Story selection requires its matching lifecycle or administrator partition")
if args.browser and args.profile == "unit":
    parser.error("Browser verification requires a real provider")
matrix = json.loads((ROOT / "eng" / "test-matrix.json").read_text())
try:
    projects = select_projects(matrix, args.profile, args.browser, args.partition)
except ValueError as error:
    parser.error(str(error))
invocations = []
for project, count in projects.items():
    if project == "Issuer.Lifecycle":
        invocations.extend((project, expected, story) for story, expected in lifecycle_invocations(matrix, args.profile, args.story))
    elif project == "Administrator":
        invocations.extend((project, expected, story) for story, expected in administrator_invocations(matrix, args.profile, args.story))
    else:
        invocations.append((project, count, None))
for project, expected_count, story in invocations:
    label = args.profile + ("-" + args.browser if args.browser else "-lifecycle-" + story.lower().replace(".", "-") if story else "")
    if project == "Administrator":
        label = "administrator-" + args.profile + "-" + story.lower()
    results = ROOT / "artifacts" / "tests" / label
    results.mkdir(parents=True, exist_ok=True)
    name = f"IdentityPlatform.{project}.Tests"
    command = ["dotnet", "test", "--project", str(ROOT / "tests" / matrix["projects"][project]["group"] / name / f"{name}.csproj"),
               "--no-build", "--no-restore", "--no-progress", "--report-trx", "--report-trx-filename", f"{project}.trx",
               "--results-directory", str(results), "--pre-enumerate-theories", "on"]
    selected = None
    if project in ("Issuer", "Issuer.Lifecycle", "Administrator"):
        # Invoke the native executable: the SDK's discovery transport does not forward JSON output.
        discovery = ["dotnet", "run", "--project", command[3], "--no-build", "--no-restore", "--",
                     "--pre-enumerate-theories", "on", "--list-tests", "json"]
        def discover(expression=None):
            invocation = discovery + (["--filter", expression] if expression else [])
            result = execute(invocation, os.environ.copy(), timeout=60, capture=True)
            return catalogue(result, lifecycle=project != "Issuer")
        all_tests = discover()
        if project == "Administrator":
            expression = f"Provider={args.profile}&FullyQualifiedName~IdentityPlatform.Administrator.Tests.Features.{story}."
            selected = discover(expression)
            inventory = json.loads((ROOT / "eng/administrator-test-matrix.json").read_text())
            require_administrator_story(all_tests, selected, args.profile, story, matrix, inventory)
            command += ["--filter", expression]
        elif project == "Issuer.Lifecycle":
            expression = f"Provider={args.profile}&FullyQualifiedName~IdentityPlatform.Issuer.Lifecycle.Tests.Features.{story}."
            selected = discover(expression)
            require_lifecycle_story(all_tests, selected, args.profile, story, matrix)
            command += ["--filter", expression]
        else:
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
    if project in ("AppHost", "Issuer", "Issuer.Lifecycle", "Administrator"):
        environment["DOTNET_ENVIRONMENT"] = "Development"
    report = results / f"{project}.trx"
    report.unlink(missing_ok=True)
    execute(command, environment, timeout=180 if args.profile == "unit" else 660)
    count = require_complete_trx(report, expected_count=expected_count)
    if selected is not None:
        require_execution_names(report, selected)
        if project in ("Issuer.Lifecycle", "Administrator"):
            require_lifecycle_execution(report, selected)
    print(f"Verified {project}/{label}: {count} required tests passed; none skipped", flush=True)
