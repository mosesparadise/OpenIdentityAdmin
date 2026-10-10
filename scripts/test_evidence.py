"""Shared verification-report checks. A successful command alone is insufficient evidence."""
from pathlib import Path
import xml.etree.ElementTree as ET


def require_complete_trx(path: Path, *, expected_count: int | None = None) -> int:
    counters = ET.parse(path).getroot().find('.//{*}Counters')
    if counters is None:
        raise ValueError(f"Missing TRX counters: {path.name}")
    total = int(counters.attrib.get('total', '0'))
    passed = int(counters.attrib.get('passed', '0'))
    executed = int(counters.attrib.get('executed', '0'))
    if total <= 0 or passed != total or executed != total:
        raise ValueError(f"Required test evidence is incomplete: {path.name}; total={total}, executed={executed}, passed={passed}")
    if expected_count is not None and total != expected_count:
        raise ValueError(f"Required test selection changed: {path.name}; expected={expected_count}, actual={total}")
    return total
