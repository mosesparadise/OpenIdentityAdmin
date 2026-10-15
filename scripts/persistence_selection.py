"""Require the exact reviewed both-provider native persistence inventory and selected execution identities."""
import json

PROVIDERS = ("PostgreSql", "SqlServer")


def load_persistence_inventory(path):
    """Reject malformed or one-sided compatibility evidence instead of accepting an arbitrary count."""
    inventory = json.loads(path.read_text())
    if set(inventory) != set(PROVIDERS):
        raise ValueError("Persistence inventory requires both providers")
    for provider, cases in inventory.items():
        if not cases or any(len(value) != 4 or value[1] != provider or value[2] is not False for value in cases.values()):
            raise ValueError("Invalid persistence case metadata")
    return {provider: {uid: tuple(value) for uid, value in cases.items()} for provider, cases in inventory.items()}


def require_persistence_selection(all_tests, selected, provider, inventory):
    """Reject substitutions, omissions, extra cases and wrong-provider selections using reviewed native identities."""
    if provider not in PROVIDERS or set(inventory) != set(PROVIDERS):
        raise ValueError("Unknown persistence provider")
    expected = {}
    for cases in inventory.values():
        if set(expected) & set(cases):
            raise ValueError("Duplicated persistence identity")
        expected.update(cases)
    if all_tests != expected or selected != inventory[provider]:
        raise ValueError("Persistence discovery differs from reviewed native identities")
