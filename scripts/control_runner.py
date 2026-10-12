"""Require an exact nonzero successful unittest inventory without skipped or expected-failure controls."""
import unittest


def is_complete(result, expected):
    """Accept only the specified inventory and successful execution of every control."""
    return (expected > 0 and result.testsRun == expected and result.wasSuccessful()
            and not result.skipped and not result.expectedFailures)


def run(expected):
    """Execute the calling main module and return failure for incomplete or exceptional evidence."""
    result = unittest.main(exit=False).result
    raise SystemExit(0 if is_complete(result, expected) else 1)
