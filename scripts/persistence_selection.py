"""Require reviewed portable persistence identities and exact run-local UID selection."""
from administrator_inventory import load_inventory as load_persistence_inventory

PROVIDERS = ("PostgreSql", "SqlServer")


def require_persistence_selection(all_tests, selected, provider, inventory):
    """Bind portable reviewed names to both providers, then retain exact discovered UIDs within this run."""
    if provider not in PROVIDERS:
        raise ValueError("Unknown persistence provider")
    expected_names = set(inventory["PostgreSql"]) | set(inventory["SqlServer"])
    if len(expected_names) != len(inventory["PostgreSql"]) + len(inventory["SqlServer"]):
        raise ValueError("Duplicated persistence identity")
    names = [case[0] for case in all_tests.values()]
    if len(names) != len(set(names)) or set(names) != expected_names:
        raise ValueError("Persistence discovery differs from reviewed native identities")
    for name in PROVIDERS:
        if {case[0] for case in all_tests.values() if case[1] == name} != set(inventory[name]):
            raise ValueError("Persistence discovery differs from reviewed providers")
    if selected != {uid: case for uid, case in all_tests.items() if case[1] == provider}:
        raise ValueError("Persistence selection differs from discovered provider identities")
