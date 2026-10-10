#!/usr/bin/env python3
"""Validate project reviewer/skill structure; does not establish model effectiveness."""
from pathlib import Path
import re
import tomllib

root = Path(__file__).resolve().parents[1]
skills = sorted((root / ".agents/skills").glob("identity-*/SKILL.md"))
agents = sorted((root / ".codex/agents").glob("identity_*_reviewer.toml"))
assert len(skills) == len(agents) == 8, "Expected eight skills and eight reviewers"
for path in skills:
    text = path.read_text()
    assert text.startswith("---\n"), f"Missing front matter: {path}"
    assert re.search(r"^name: identity-", text, re.M), f"Missing skill name: {path}"
    assert "## Required output" in text and "## Stop and handoff" in text, path
for path in agents:
    data = tomllib.loads(path.read_text())
    assert data.get("name") == path.stem, path
    assert data.get("description") and data.get("developer_instructions"), path
    assert "model" not in data, f"Model must inherit: {path}"
    assert data.get("sandbox_mode") == "read-only", f"Reviewers must request read-only: {path}"
print("PASS: eight skill files and eight reviewer configurations; runtime permissions/effectiveness remain separately assessed.")
