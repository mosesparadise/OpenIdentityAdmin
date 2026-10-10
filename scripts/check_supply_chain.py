#!/usr/bin/env python3
"""Fail closed on incomplete advisory evidence or unreviewed license changes."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def require_no_vulnerabilities(report, expected_projects):
    if report.get("version") != 1 or not report.get("sources"):
        raise ValueError("Missing supported advisory report schema/sources")
    if report.get("problems"):
        raise ValueError("Advisory lookup reported problems")
    projects = report.get("projects", [])
    observed = {str(Path(p["path"]).resolve()) for p in projects}
    if len(projects) != len(observed) or observed != expected_projects:
        raise ValueError("Advisory report project inventory is incomplete or duplicated")
    for project in projects:
        if project.get("problems"):
            raise ValueError("Project advisory lookup reported problems")
        for framework in project.get("frameworks", []):
            for group in ("topLevelPackages", "transitivePackages"):
                for package in framework.get(group, []):
                    if package.get("vulnerabilities"):
                        raise ValueError(f"Known vulnerability: {package['id']}/{package['resolvedVersion']}")
    return len(projects)


def inventory_licenses(root, cache, policy):
    packages = {}
    for lock in sorted(root.glob("**/packages.lock.json")):
        if any(part in ("bin", "obj", "artifacts") for part in lock.parts):
            continue
        for dependencies in json.loads(lock.read_text())["dependencies"].values():
            for name, item in dependencies.items():
                if item["type"].lower() == "project":
                    continue
                key = f"{name}/{item['resolved']}"
                record = packages.setdefault(key, {"name": name, "version": item["resolved"],
                                                    "contentHash": item["contentHash"], "projects": []})
                if record["contentHash"] != item["contentHash"]:
                    raise ValueError(f"Inconsistent locked package hash: {key}")
                record["projects"].append(str(lock.parent.relative_to(root)))
    if not packages:
        raise ValueError("No locked dependencies found")
    for key, record in sorted(packages.items()):
        directory = cache / record["name"].lower() / record["version"].lower()
        metadata = ET.parse(directory / (record["name"].lower() + ".nuspec")).getroot()
        license_node = metadata.find(".//{*}license")
        license_url = metadata.find(".//{*}licenseUrl")
        record["licenseUrl"] = license_url.text if license_url is not None else None
        record["licenseDeclaration"] = license_node.text if license_node is not None else None
        if license_node is not None and license_node.get("type") == "expression" and license_node.text in policy["allowedExpressions"]:
            record["disposition"] = "Permissive expression; preserve applicable notices on redistribution."
        else:
            exception = policy["reviewedExceptions"].get(key)
            if not exception:
                raise ValueError(f"Unreviewed license declaration: {key}")
            digest = hashlib.sha256((directory / exception["file"]).read_bytes()).hexdigest()
            if digest != exception["sha256"]:
                raise ValueError(f"Reviewed license evidence changed: {key}")
            record.update(exception)
        record["projects"] = sorted(set(record["projects"]))
    return list(packages.values())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["vulnerabilities", "licenses"])
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    if args.mode == "vulnerabilities":
        expected = {str(p.resolve()) for folder in ("src", "tests") for p in (ROOT / folder).glob("**/*.csproj")}
        count = require_no_vulnerabilities(json.loads(args.report.read_text()), expected)
        print(f"Advisory evidence: {count} projects, no known vulnerable packages reported")
    else:
        cache = Path(os.environ.get("NUGET_PACKAGES", str(Path.home() / ".nuget/packages")))
        inventory = inventory_licenses(ROOT, cache, json.loads((ROOT / "eng/license-policy.json").read_text()))
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps({"scope": "Locked NuGet graph; not a full product SBOM or legal clearance", "packages": inventory}, indent=2) + "\n")
        print(f"License inventory: {len(inventory)} package/version pairs; reviewed exceptions retained")


if __name__ == "__main__":
    main()
