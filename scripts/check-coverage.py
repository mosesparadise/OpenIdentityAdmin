#!/usr/bin/env python3
"""Reject absent/under-covered first-party modules in merged Cobertura evidence."""
import argparse
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("report", type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
policy = json.loads((root / "eng/coverage-policy.json").read_text())
report = ET.parse(args.report).getroot()
packages = {node.attrib["name"].removesuffix(".dll"): node for node in report.findall(".//package")}
failed = False
for name in policy["requiredModules"]:
    package = packages.get(name)
    if package is None:
        print(f"FAIL {name}: no execution evidence (including child processes)")
        failed = True
        continue
    lines, branches = float(package.attrib["line-rate"]), float(package.attrib["branch-rate"])
    passed = lines >= policy["minimumLineCoverage"] and branches >= policy["minimumBranchCoverage"]
    print(f"{'PASS' if passed else 'FAIL'} {name}: lines {lines:.1%}; branches {branches:.1%}")
    failed |= not passed
sys.exit(1 if failed else 0)
