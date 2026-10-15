"""Seed exact identity, provider and completeness failures in native persistence selection."""
import copy
import unittest

from persistence_selection import require_persistence_selection
from test_selection import catalogue


class PersistenceSelectionTests(unittest.TestCase):
    def setUp(self):
        self.inventory = {
            "PostgreSql": {"pg": ("postgres case", "PostgreSql", False, "IdentityPlatform.Persistence.Tests.Features")},
            "SqlServer": {"sql": ("sql case", "SqlServer", False, "IdentityPlatform.Persistence.Tests.Features")},
        }
        self.all = self.inventory["PostgreSql"] | self.inventory["SqlServer"]

    def test_exact_both_provider_inventory_is_accepted(self):
        for provider in self.inventory:
            require_persistence_selection(self.all, self.inventory[provider], provider, self.inventory)

    def test_omitted_extra_and_substituted_native_cases_fail(self):
        for observed in ({}, {"pg": self.all["pg"]}, self.all | {"extra": self.all["pg"]}, self.all | {"pg": self.all["sql"]}):
            with self.subTest(observed=observed), self.assertRaises(ValueError):
                require_persistence_selection(observed, self.inventory["PostgreSql"], "PostgreSql", self.inventory)

    def test_wrong_provider_or_incomplete_selection_fails(self):
        for selected in ({}, self.inventory["SqlServer"], self.all):
            with self.subTest(selected=selected), self.assertRaises(ValueError):
                require_persistence_selection(self.all, selected, "PostgreSql", self.inventory)

    def test_duplicated_inventory_identity_fails(self):
        inventory = copy.deepcopy(self.inventory)
        inventory["SqlServer"] = inventory["PostgreSql"]
        with self.assertRaises(ValueError):
            require_persistence_selection(self.all, inventory["PostgreSql"], "PostgreSql", inventory)

    def test_browser_persistence_case_is_rejected(self):
        import json
        raw = {"schemaVersion": 1, "tests": [{"uid": "pg", "displayName": "case", "traits": [
            {"key": "Provider", "value": "PostgreSql"}, {"key": "Category", "value": "Browser"}],
            "type": {"namespace": "IdentityPlatform.Persistence.Tests.Features.Browser"}}]}
        with self.assertRaises(ValueError):
            catalogue(json.dumps(raw), project="Persistence")


if __name__ == "__main__":
    unittest.main()
