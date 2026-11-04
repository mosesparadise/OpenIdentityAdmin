"""Retain scoped browser-session entry points over shared exact administrator grouping."""
from administrator_group_selection import administrator_execution_invocations, group_invocations, group_filter, require_group_selections


def browser_session_invocations(matrix, provider, group=None):
    """Require the unchanged three browser-session groups and both-provider counts."""
    return group_invocations(matrix, provider, 'BrowserSessions', group)


def browser_session_filter(matrix, provider, group):
    """Build the reviewed positive browser-session class filter."""
    return group_filter(matrix, provider, 'BrowserSessions', group)


def require_browser_session_selections(all_tests, selections, provider, matrix, inventory):
    """Reconcile actual browser-session tuples and their complete disjoint union."""
    require_group_selections(all_tests, selections, provider, 'BrowserSessions', matrix, inventory)
