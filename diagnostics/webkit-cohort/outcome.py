"""Keep diagnostic completeness separate from native, collector and cleanup outcomes."""


def job_success(native_exit, child_exit, collector_exit, report, diagnostic, cleanup):
    """Require every independently observed success; never turn a captured failure green."""
    exits = (native_exit, child_exit, collector_exit)
    return (all(type(value) is int and value == 0 for value in exits)
            and report.get('report_valid') is True
            and report.get('outcome') == 'Passed'
            and diagnostic.get('diagnostic_complete') is True
            and diagnostic.get('acceptance') is False
            and cleanup.get('complete') is True)
