"""Build the immutable old implementation used by provider compatibility tests; never touch a database."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASELINE = "38eb41e06d40b6addbe57e3af839f912645e6dea"
PROJECT = ROOT / "eng/IdentityPlatform.ProviderCompatibility.Baseline/IdentityPlatform.ProviderCompatibility.Baseline.csproj"


def prepare(restore_only=False):
    """Verify the fixed commit, isolate its checkout, and build the producer against that checkout only."""
    checkout = ROOT / "work/provider-compatibility-baseline"
    if not checkout.exists():
        subprocess.run(["git", "fetch", "https://github.com/mosesparadise/OpenIdentityAdmin.git", BASELINE], cwd=ROOT, check=True)
        subprocess.run(["git", "worktree", "add", "--detach", str(checkout), BASELINE], cwd=ROOT, check=True)
    observed = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=checkout, text=True).strip()
    dirty = subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=no"], cwd=checkout, text=True)
    if observed != BASELINE or dirty:
        raise ValueError("Frozen compatibility source does not match the required clean checkpoint")
    setting = f"-p:ProviderCompatibilityBaselineRoot={checkout}"
    command = ["dotnet", "restore", str(PROJECT), setting, "--nologo"]
    if not restore_only:
        command.append("--locked-mode")
    subprocess.run(command, cwd=ROOT, check=True)
    if restore_only:
        return
    subprocess.run(["dotnet", "build", str(PROJECT), setting, "--no-restore", "--nologo", "-m:1", "-nr:false"], cwd=ROOT, check=True)
    executable = PROJECT.parent / "bin/baseline/net10.0/IdentityPlatform.ProviderCompatibility.Baseline.dll"
    dependencies = json.loads(executable.with_suffix(".deps.json").read_text())
    if any(name.startswith("IdentityPlatform.Database.") for name in dependencies["libraries"]):
        raise ValueError("Compatibility producer unexpectedly references the new provider implementation")
    evidence = ROOT / "artifacts/provider-compatibility"
    evidence.mkdir(parents=True, exist_ok=True)
    dirty = subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=no"], cwd=checkout, text=True)
    if dirty:
        raise ValueError("Frozen compatibility source changed during the build")
    files = sorted(path for path in executable.parent.rglob("*") if path.is_file())
    if len(files) > 500 or sum(path.stat().st_size for path in files) > 512 * 1024 * 1024:
        raise ValueError("Compatibility executable manifest exceeds its reviewed bounds")
    receipt = {"baseline": BASELINE, "files": {path.relative_to(executable.parent).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in files}}
    (evidence / "baseline-build.json").write_text(json.dumps(receipt, indent=2) + "\n")
    if os.environ.get("GITHUB_ENV"):
        with open(os.environ["GITHUB_ENV"], "a", encoding="utf-8") as output:
            output.write(f"PROVIDER_BASELINE_PRODUCER={executable}\n")
            output.write(f"PROVIDER_BASELINE_MANIFEST={evidence / 'baseline-build.json'}\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--restore-only", action="store_true")
    prepare(parser.parse_args().restore_only)
