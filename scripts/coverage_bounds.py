"""Conservative coverage bounds from original, identity-bound Cobertura reports."""
from dataclasses import dataclass, field
from fractions import Fraction
from pathlib import Path
import re
import xml.etree.ElementTree as ET


def require(value, message):
    if not value:
        raise ValueError(message)


def integer(value, positive=False):
    require(isinstance(value, str) and re.fullmatch(r'[0-9]+', value), 'Invalid coverage integer')
    result = int(value)
    require(not positive or result > 0, 'Expected positive coverage integer')
    return result


def name(value):
    require(isinstance(value, str) and value.strip(), 'Missing coverage identity')
    return value


def percentage(value, covered=None, total=None):
    require(isinstance(value, str) and re.fullmatch(r'[0-9]+(?:\.[0-9]+)?%', value), 'Invalid coverage percentage')
    number = Fraction(value[:-1])
    require(0 <= number <= 100, 'Coverage percentage outside range')
    if total:
        # Native switch percentages use binary floating serialization; counts/floors stay exact.
        digits = len(value[:-1].split('.')[1]) if '.' in value else 0
        require(abs(number - Fraction(100 * covered, total)) <= max(Fraction(1, 2 * 10 ** digits), Fraction(1, 10 ** 12)),
                'Coverage percentage contradicts counts')
    return number


@dataclass
class Point:
    branch: bool
    covered: int = 0
    total: int | None = None
    conditions: dict = field(default_factory=dict)

    def add(self, other):
        require(self.branch == other.branch, 'Branch flag changed for the same method point')
        if other.total is None:
            return
        if self.total is None:
            self.total, self.covered = other.total, other.covered
            self.conditions = dict(other.conditions)
            return
        require(self.total == other.total and self.conditions.keys() == other.conditions.keys(),
                'Branch population or condition identity changed')
        self.covered = max(self.covered, other.covered)
        for key, value in other.conditions.items():
            self.conditions[key] = max(self.conditions[key], value)

    def bounds(self):
        if not self.branch:
            return 0, 0
        require(self.total is not None, 'Branch point has no usable declaration in any input')
        covered = self.covered
        if all(kind == 'jump' for _, kind in self.conditions):
            covered = max(covered, sum(int(value / 50) for value in self.conditions.values()))
        require(covered <= self.total, 'Branch bound exceeds its population')
        return covered, self.total


def point(line, hit):
    flag = line.get('branch')
    require(flag in ('True', 'False', 'true', 'false'), 'Unknown or absent branch flag')
    result = Point(flag.lower() == 'true')
    declaration = line.get('condition-coverage')
    conditions = line.findall('./conditions/condition')
    if declaration is None:
        require(not conditions, 'Conditions lack branch counts')
        return result
    require(result.branch, 'Branch declaration contradicts branch flag')
    match = re.fullmatch(r'([0-9]+(?:\.[0-9]+)?%) \(([0-9]+)/([0-9]+)\)', declaration)
    require(match, 'Malformed branch declaration')
    result.covered, result.total = integer(match[2]), integer(match[3], positive=True)
    require(result.covered <= result.total and (hit or result.covered == 0), 'Invalid branch counts/hits')
    percentage(match[1], result.covered, result.total)
    identifiers = set()
    for condition in conditions:
        identity, kind = integer(condition.get('number')), condition.get('type')
        require(identity not in identifiers and kind in ('jump', 'switch'), 'Duplicate or unknown condition')
        identifiers.add(identity)
        value = percentage(condition.get('coverage'))
        require(kind != 'jump' or value in (0, 50, 100), 'Jump does not describe zero, one or two outcomes')
        result.conditions[identity, kind] = value
    require(result.total == 0 or conditions, 'Nonempty branch population lacks conditions')
    jumps = [value for (_, kind), value in result.conditions.items() if kind == 'jump']
    if len(jumps) == len(conditions):
        require(result.total == 2 * len(jumps) and result.covered == sum(int(value / 50) for value in jumps),
                'Jump condition counts contradict declaration')
    else:
        switches = [value for (_, kind), value in result.conditions.items() if kind == 'switch']
        require(len(switches) == 1, 'Multiple switch populations are ambiguous')
        known = sum(int(value / 50) for value in jumps)
        require(result.total > 2 * len(jumps) and known <= result.covered <= known + result.total - 2 * len(jumps),
                'Mixed condition counts contradict declaration')
        switch = next(condition for condition in conditions if condition.get('type') == 'switch')
        percentage(switch.get('coverage'), result.covered - known, result.total - 2 * len(jumps))
    return result


@dataclass
class Evidence:
    modules: set = field(default_factory=set)
    lines: dict = field(default_factory=dict)
    points: dict = field(default_factory=dict)

    def add(self, other):
        self.modules.update(other.modules)
        for key, hit in other.lines.items():
            self.lines[key] = self.lines.get(key, False) or hit
        for key, value in other.points.items():
            if key not in self.points:
                self.points[key] = Point(value.branch)
            self.points[key].add(value)


def class_lines(node, prefix):
    result = {}
    for line in node.findall('./lines/line'):
        key = (*prefix, integer(line.get('number'), positive=True))
        require(key not in result, 'Duplicate source line')
        result[key] = integer(line.get('hits')) > 0
    require(result, 'Class/method has no source lines')
    return result


def validate_class_branches(node, method_points):
    """Class summaries must agree with disjoint method owners, without reusing class IDs."""
    for line in node.findall('./lines/line'):
        number = integer(line.get('number'), positive=True)
        summary = point(line, integer(line.get('hits')) > 0)
        owners = method_points[number]
        require(summary.branch == any(owner.branch for owner in owners), 'Class and method branch flags differ')
        branches = [owner for owner in owners if owner.branch]
        if not branches:
            continue
        if summary.total is None:
            require(all(owner.total is None for owner in branches), 'Class branch declaration is missing')
        else:
            require(all(owner.total is not None for owner in branches), 'Method branch declaration is missing')
            require(summary.total == sum(owner.total for owner in branches) and
                    summary.covered == sum(owner.covered for owner in branches), 'Class and method branch counts differ')


def read_report(path):
    root = ET.parse(path).getroot()
    require(root.tag == 'coverage', 'Unexpected coverage document')
    result = Evidence()
    for package in root.findall('./packages/package'):
        module = name(package.get('name')).removesuffix('.dll')
        require(module not in result.modules, 'Duplicate coverage module')
        result.modules.add(module)
        classes = set()
        for node in package.findall('./classes/class'):
            prefix = (module, name(node.get('name')), name(node.get('filename')))
            require(prefix not in classes, 'Duplicate coverage class')
            classes.add(prefix)
            lines = class_lines(node, prefix)
            methods, method_hits, method_points = set(), {}, {}
            for method in node.findall('./methods/method'):
                identity = (name(method.get('name')), name(method.get('signature')))
                require(identity not in methods, 'Duplicate method identity')
                methods.add(identity)
                observed = class_lines(method, prefix)
                for key, hit in observed.items():
                    method_hits[key] = method_hits.get(key, False) or hit
                for line in method.findall('./lines/line'):
                    number = integer(line.get('number'), positive=True)
                    key = (*prefix, *identity, number)
                    result.points[key] = point(line, observed[(*prefix, number)])
                    method_points.setdefault(number, []).append(result.points[key])
            require(method_hits == lines, 'Class line map differs from its method union')
            validate_class_branches(node, method_points)
            result.lines.update(lines)
    require(result.modules and result.lines, 'Empty coverage evidence')
    return result


def merged_line_map(path):
    """Merged branch fields are deliberately not used: the upstream merge loses them."""
    root = ET.parse(path).getroot()
    require(root.tag == 'coverage', 'Unexpected merged coverage document')
    result, modules = {}, set()
    for package in root.findall('./packages/package'):
        module = name(package.get('name')).removesuffix('.dll')
        require(module not in modules, 'Duplicate merged module')
        modules.add(module)
        classes = set()
        for node in package.findall('./classes/class'):
            prefix = (module, name(node.get('name')), name(node.get('filename')))
            require(prefix not in classes, 'Duplicate merged class')
            classes.add(prefix)
            result.update(class_lines(node, prefix))
    return modules, result


def statistics(evidence, policy):
    required = policy['requiredModules']
    require(len(required) == len(set(required)), 'Duplicate required module')
    rows = []
    for module in required:
        require(module in evidence.modules, 'Missing required coverage module: ' + module)
        hits = [hit for key, hit in evidence.lines.items() if key[0] == module]
        require(hits, 'Required module has no source lines: ' + module)
        counts = [value.bounds() for key, value in evidence.points.items() if key[0] == module]
        covered, total = sum(c for c, _ in counts), sum(t for _, t in counts)
        line_rate, branch_rate = Fraction(sum(hits), len(hits)), Fraction(covered, total) if total else Fraction(1)
        require(line_rate >= Fraction(str(policy['minimumLineCoverage'])), 'Raw line floor failed: ' + module)
        require(branch_rate >= Fraction(str(policy['minimumBranchCoverage'])), 'Conservative branch floor failed: ' + module)
        rows.append({'module': module, 'linesCovered': sum(hits), 'linesTotal': len(hits),
                     'branchesCoveredLowerBound': covered, 'branchesTotal': total,
                     'lineRate': float(line_rate), 'branchRateLowerBound': float(branch_rate),
                     'zeroBranchPopulation': total == 0})
    return rows
