#!/usr/bin/env python3
"""Run the repository's SDK analysis/style gates; optionally prove they reject defects."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def run(*arguments: str, cwd: Path = ROOT) -> None:
    subprocess.run(arguments, cwd=cwd, check=True, timeout=300)


def prove_enforcement() -> None:
    # Inherit the real Directory.Build.props/.editorconfig, without touching product source.
    artifacts = ROOT / "artifacts"
    artifacts.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="analysis-probe-", dir=artifacts) as directory:
        probe = Path(directory)
        project = probe / "AnalysisProbe.csproj"
        project.write_text('<Project Sdk="Microsoft.NET.Sdk" />\n', encoding="utf-8")
        source = probe / "Probe.cs"
        good = (
            "using System.Security.Cryptography;\n\n"
            "namespace AnalysisProbe;\n\n"
            "internal static class Probe\n{\n"
            "    internal static byte[] Create() => RandomNumberGenerator.GetBytes(32);\n"
            "}\n"
        )
        source.write_text(good, encoding="utf-8")
        run("dotnet", "restore", str(project), "--nologo", cwd=probe)
        build = ["dotnet", "build", str(project), "--no-restore", "--nologo",
                 "--disable-build-servers", "-m:1"]
        run(*build, cwd=probe)
        controls = {
            "CA5350": good.replace("RandomNumberGenerator.GetBytes(32)", "SHA1.HashData(new byte[32])"),
            "IDE0055": good.replace("class Probe\n{", "class Probe{"),
            "IDE1006": good.replace("class Probe", "class probe"),
        }
        for diagnostic, mutation in controls.items():
            source.write_text(mutation, encoding="utf-8")
            result = subprocess.run(build, cwd=probe, text=True, capture_output=True, timeout=120)
            if result.returncode == 0 or f"error {diagnostic}:" not in result.stdout + result.stderr:
                raise RuntimeError(f"Static-analysis control did not reject {diagnostic}")
            print(f"PASS: seeded {diagnostic} rejected by the real build", flush=True)
        source.write_text(good, encoding="utf-8")
        run(*build, cwd=probe)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--self-test", action="store_true", help="also prove clean/unsafe/misformatted controls")
    args = parser.parse_args()
    if shutil.which("dotnet") is None:
        raise RuntimeError("A stable .NET 10 SDK accepted by global.json must be available on PATH")
    run("dotnet", "build", "IdentityPlatform.slnx", "--no-restore", "--nologo",
        "--disable-build-servers", "-m:1")
    run("dotnet", "format", "whitespace", "IdentityPlatform.slnx", "--no-restore", "--verify-no-changes")
    run("dotnet", "format", "style", "IdentityPlatform.slnx", "--no-restore", "--verify-no-changes",
        "--severity", "info", "--diagnostics", "IDE0005")
    run("python3", "scripts/test_code_quality.py")
    run("python3", "scripts/check_code_quality.py")
    if args.self_test:
        prove_enforcement()


if __name__ == "__main__":
    main()
