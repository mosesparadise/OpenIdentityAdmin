#!/usr/bin/env python3
from persistence_selection import load_persistence_inventory, require_persistence_selection
"""Run the required matrix cells without silently skipping an unavailable provider."""
import argparse
import json
import os
from pathlib import Path
from verify_contrast_experiment import execute, install_cancellation_handler
from verify_history_experiment import MODE_VARIABLE as HISTORY_MODE_VARIABLE
from test_evidence import require_complete_trx
from administrator_inventory import load_inventory, require_native_inventory, PROVIDERS
from test_selection import catalogue, require_partition, require_execution_names, require_lifecycle_execution, select_projects, lifecycle_invocations, require_lifecycle_story, require_administrator_story, require_administrator_browser, administrator_browser_invocations

from apphost_selection import apphost_invocations, apphost_catalogue, apphost_filter, require_apphost_selections
from issuer_selection import issuer_catalogue, issuer_invocations, issuer_filter, require_issuer_inventory, require_issuer_selections
from browser_deferrals import CERTIFICATE_METHOD, select_required, receipt as browser_deferral_receipt
from browser_session_selection import administrator_execution_invocations, browser_session_invocations, browser_session_filter, require_browser_session_selections

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("profile", choices=["unit", "PostgreSql", "SqlServer"])
parser.add_argument("--browser", choices=["chromium", "firefox", "webkit"])
parser.add_argument("--partition", choices=["all", "standard", "foundation", "issuer", "lifecycle", "administrator"], default="all")
parser.add_argument("--story", choices=["Restart", "Distribution", "KeyRotation.Activation", "KeyRotation.Retirement", "Bootstrap", "Factors", "Ceremonies", "Accounts", "Recovery", "Security", "Governance.Policy", "Governance.Commands", "Governance.Failures", "ProtocolAdmission", "BrowserSessions"])
parser.add_argument("--browser-story", choices=["Accounts", "Security"])
parser.add_argument("--issuer-group", choices=["Flows", "Boundaries"])
parser.add_argument("--browser-session-group", choices=["Foundations", "Product"])
args = parser.parse_args()
if HISTORY_MODE_VARIABLE in os.environ:
    parser.error("Ordinary acceptance cannot run with a history diagnostic mode")
if args.issuer_group and (args.partition != "issuer" or args.browser or args.story or args.profile == "unit"):
    parser.error("Issuer group requires an ordinary issuer provider partition")
if args.browser_session_group and (args.partition != "administrator" or args.story != "BrowserSessions" or args.browser or args.profile == "unit"):
    parser.error("Browser-session group requires its ordinary administrator provider story")
if args.browser_story and (not args.browser or args.partition != "administrator"):
    parser.error("Browser story requires an administrator browser invocation")
install_cancellation_handler()
if args.story and (args.partition not in ("lifecycle", "administrator") or
                   (args.story in ("Bootstrap", "Factors", "Ceremonies", "Accounts", "Recovery", "Security", "Governance.Policy", "Governance.Commands", "Governance.Failures", "ProtocolAdmission", "BrowserSessions")) != (args.partition == "administrator")):
    parser.error("Story selection requires its matching lifecycle or administrator partition")
if args.browser and args.story:
    parser.error("HTTP story and browser selection cannot be combined")
if args.browser and args.profile == "unit":
    parser.error("Browser verification requires a real provider")
matrix = json.loads((ROOT / "eng" / "test-matrix.json").read_text())
try:
    projects = select_projects(matrix, args.profile, args.browser, args.partition)
except ValueError as error:
    parser.error(str(error))
invocations = []
for project, count in projects.items():
    if project == "AppHost" and args.profile != "unit":
        invocations.extend((project, expected, group, None, None) for group, expected in apphost_invocations(matrix, args.profile))
    elif project == "Issuer" and not args.browser:
        invocations.extend((project, expected, group, None, None) for group, expected in issuer_invocations(matrix, args.profile, args.issuer_group))
    elif project == "Issuer.Lifecycle":
        invocations.extend((project, expected, story, None, None) for story, expected in lifecycle_invocations(matrix, args.profile, args.story))
    elif project == "Administrator" and not args.browser:
        invocations.extend((project, count, story, None, group) for story, group, count in
                           administrator_execution_invocations(matrix, args.profile, args.story, args.browser_session_group))
    elif project == "Administrator":
        invocations.extend((project, expected, None, browser_story, None) for browser_story, expected in administrator_browser_invocations(matrix, args.browser_story))
    else:
        invocations.append((project, count, None, None, None))
for project, expected_count, story, browser_story, browser_session_group in invocations:
    label = args.profile + ("-" + args.browser if args.browser else "-lifecycle-" + story.lower().replace(".", "-") if story else "")
    if project == "AppHost" and args.profile != "unit":
        label = args.profile + "-apphost-" + story.lower()
    if project == "Issuer" and not args.browser:
        label = "issuer-" + args.profile + "-" + story.lower()
    if project == "Administrator":
        label = "administrator-" + args.profile + "-" + ((args.browser + "-" + browser_story.lower()) if args.browser else story.lower().replace(".", "-"))
        if browser_session_group:
            label += "-" + browser_session_group.lower()
    results = ROOT / "artifacts" / "tests" / label
    results.mkdir(parents=True, exist_ok=True)
    name = f"IdentityPlatform.{project}.Tests"
    command = ["dotnet", "test", "--project", str(ROOT / "tests" / matrix["projects"][project]["group"] / name / f"{name}.csproj"),
               "--no-build", "--no-restore", "--no-progress", "--report-trx", "--report-trx-filename", f"{project}.trx",
               "--results-directory", str(results), "--pre-enumerate-theories", "on"]
    selected = None
    if project in ("Issuer", "Issuer.Lifecycle", "Administrator", "Persistence") or (project == "AppHost" and args.profile != "unit"):
        # Invoke the native executable: the SDK's discovery transport does not forward JSON output.
        discovery = ["dotnet", "run", "--project", command[3], "--no-build", "--no-restore", "--",
                     "--pre-enumerate-theories", "on", "--list-tests", "json"]
        native_inventory = (load_inventory(ROOT / "eng/administrator-test-matrix.json") if project == "Administrator" else
                            load_persistence_inventory(ROOT / "eng/persistence-test-matrix.json") if project == "Persistence" else None)
        def discover(expression=None):
            invocation = discovery + (["--filter", expression] if expression else [])
            result = execute(invocation, os.environ.copy(), timeout=60, capture=True)
            if native_inventory is not None:
                require_native_inventory(result, native_inventory, complete=expression is None)
            if project == "AppHost":
                return apphost_catalogue(result)
            return issuer_catalogue(result) if project == "Issuer" else catalogue(result, project=project)
        all_tests = discover(f"Provider={args.profile}") if project == "AppHost" else discover()
        if project == "AppHost":
            groups = {group: discover(apphost_filter(args.profile, group))
                      for group, _ in apphost_invocations(matrix, args.profile)}
            require_apphost_selections(all_tests, groups, args.profile, matrix)
            selected = groups[story]
            command += ["--filter", apphost_filter(args.profile, story)]
        elif project == "Persistence":
            selected = discover(f"Provider={args.profile}")
            inventory = load_persistence_inventory(ROOT / "eng/persistence-test-matrix.json")
            require_persistence_selection(all_tests, selected, args.profile, inventory)
            command += ["--filter-trait", f"Provider={args.profile}"]
        elif project == "Administrator":
            inventory = {provider: native_inventory[provider] for provider in PROVIDERS}
            if args.browser:
                expression = f"Provider={args.profile}&Category=Browser&BrowserStory={browser_story}"
                selected = discover(expression)
                require_administrator_browser(all_tests, selected, args.profile, matrix, inventory, browser_story)
            elif browser_session_group:
                groups = {group: discover(browser_session_filter(matrix, args.profile, group))
                          for group, _ in browser_session_invocations(matrix, args.profile)}
                require_browser_session_selections(all_tests, groups, args.profile, matrix, native_inventory)
                selected = groups[browser_session_group]
                expression = browser_session_filter(matrix, args.profile, browser_session_group)
            else:
                expression = f"Provider={args.profile}&FullyQualifiedName~IdentityPlatform.Administrator.Tests.Features.{story}."
                selected = discover(expression)
                require_administrator_story(all_tests, selected, args.profile, story, matrix, inventory)
            command += ["--filter", expression]
        elif project == "Issuer.Lifecycle":
            expression = f"Provider={args.profile}&FullyQualifiedName~IdentityPlatform.Issuer.Lifecycle.Tests.Features.{story}."
            selected = discover(expression)
            require_lifecycle_story(all_tests, selected, args.profile, story, matrix)
            command += ["--filter", expression]
        else:
            require_issuer_inventory(all_tests, matrix)
            http_filter = f"Provider={args.profile}&Category!=Browser"
            browser_filter = f"Provider={args.profile}&Category=Browser"
            http, browser = discover(http_filter), discover(browser_filter)
            require_partition(all_tests, http, browser, args.profile, matrix["profiles"][args.profile]["Issuer"], matrix["browser"]["Issuer"])
            expression = browser_filter if args.browser else issuer_filter(matrix, args.profile, story)
            if args.browser:
                selected, deferred = select_required(browser, args.browser, args.profile)
                (results / 'browser-deferrals.json').write_text(json.dumps(
                    browser_deferral_receipt(deferred, args.browser, args.profile), indent=2) + '\n')
                if deferred:
                    expression += '&FullyQualifiedName!~' + CERTIFICATE_METHOD
                    if discover(expression) != selected:
                        raise ValueError('Native filter does not match the exact A-009 required identity set')
                    expected_count -= len(deferred)
                    notice = f'A-009: {args.profile}/{args.browser}: one exact certificate evidence case deferred, not passed; all other cases required.'
                    print(notice, flush=True)
                    if summary := os.environ.get('GITHUB_STEP_SUMMARY'):
                        with open(summary, 'a') as output:
                            output.write(notice + '\n')
            else:
                groups = {group: discover(issuer_filter(matrix, args.profile, group))
                          for group, _ in issuer_invocations(matrix, args.profile)}
                require_issuer_selections(all_tests, groups, args.profile, matrix)
                selected = groups[story]
            command += ["--filter", expression]
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
        if project in ("AppHost", "Issuer", "Issuer.Lifecycle", "Administrator", "Persistence"):
            require_lifecycle_execution(report, selected)
    print(f"Verified {project}/{label}: {count} required tests passed; none skipped", flush=True)
