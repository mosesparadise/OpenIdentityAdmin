"""Independent faults around the one-expression private source instrumentation."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import overlay
from common import sha


class OverlayTests(unittest.TestCase):
    def text(self): return 'prefix\n' + overlay.METHOD + '\n{' + overlay.OLD + '}\n    [Theory]\n' + overlay.OLD

    def test_only_target_expression_changes(self):
        source = self.text(); updated = overlay.transform(source)
        self.assertEqual(updated.count(overlay.NEW), 1)
        self.assertEqual(updated.replace(overlay.NEW, overlay.OLD), source)
        self.assertTrue(updated.endswith(overlay.OLD))

    def test_missing_duplicated_and_stale_scope_rejected(self):
        for source in ('', self.text() + overlay.METHOD, self.text().replace(overlay.OLD, overlay.OLD * 2), self.text() + overlay.NEW):
            with self.assertRaises((AssertionError, ValueError)): overlay.transform(source)

    def test_private_copy_restores_original_without_source_edit(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); repo = root/'repo'; work = root/'work'
            (repo/overlay.TEST_PATH).parent.mkdir(parents=True); work.mkdir(); (work/'public').mkdir()
            (repo/overlay.TEST_PATH).write_text(self.text()); (repo/'other.cs').write_text('preserve')
            before = {p: sha(repo/p) for p in (overlay.TEST_PATH,'other.cs')}
            (work/'source-before.json').write_text(json.dumps(before))
            with patch.object(overlay, 'source_hashes', return_value=before):
                target = overlay.create(repo, work)
                self.assertTrue(overlay.unchanged(work)); self.assertTrue(overlay.restore(repo, work))
            self.assertFalse((target/overlay.HELPER_PATH).exists())
            self.assertEqual({p:sha(repo/p) for p in before}, before)
            self.assertEqual({p:sha(target/p) for p in before}, before)

    def test_instrumented_change_is_detected(self):
        with tempfile.TemporaryDirectory() as directory:
            work=Path(directory); (work/'instrumented').mkdir()
            (work/'instrumented/a.cs').write_text('expected')
            (work/'instrumented-hashes.json').write_text(json.dumps({'a.cs':sha(work/'instrumented/a.cs')}))
            (work/'instrumented/a.cs').write_text('changed')
            self.assertFalse(overlay.unchanged(work))

    def test_missing_helper_or_manifest_does_not_prevent_source_restoration(self):
        for missing in ('helper', 'manifest', 'malformed'):
            with self.subTest(missing=missing), tempfile.TemporaryDirectory() as directory:
                root=Path(directory); repo=root/'repo'; work=root/'work'
                (repo/overlay.TEST_PATH).parent.mkdir(parents=True); work.mkdir(); (work/'public').mkdir()
                (repo/overlay.TEST_PATH).write_text(self.text())
                before={overlay.TEST_PATH:sha(repo/overlay.TEST_PATH)}
                (work/'source-before.json').write_text(json.dumps(before))
                with patch.object(overlay,'source_hashes',return_value=before):
                    target=overlay.create(repo,work)
                    if missing=='malformed': (work/'instrumented-hashes.json').write_text('{')
                    else: (target/overlay.HELPER_PATH if missing=='helper' else work/'instrumented-hashes.json').unlink()
                    self.assertFalse(overlay.restore(repo,work))
                self.assertEqual((target/overlay.TEST_PATH).read_text(),self.text())
                self.assertFalse((target/overlay.HELPER_PATH).exists())

    def test_copy_failure_still_removes_helper(self):
        with tempfile.TemporaryDirectory() as directory:
            work=Path(directory); target=work/'instrumented'
            (target/overlay.HELPER_PATH).parent.mkdir(parents=True)
            (target/overlay.HELPER_PATH).write_text('helper')
            with patch.object(overlay.shutil,'copyfile',side_effect=OSError('copy failed')):
                self.assertFalse(overlay.restore(work,work))
            self.assertFalse((target/overlay.HELPER_PATH).exists())

    def test_unexpected_compilable_source_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            work=Path(directory); (work/'instrumented').mkdir()
            (work/'instrumented/a.cs').write_text('expected')
            (work/'instrumented-hashes.json').write_text(json.dumps({'a.cs':sha(work/'instrumented/a.cs')}))
            (work/'instrumented/extra.cs').write_text('unexpected')
            self.assertFalse(overlay.unchanged(work))


if __name__ == '__main__': unittest.main()
