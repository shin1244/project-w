"""Focused checks for relocated training data; no Godot or PyTorch required."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


spec = importlib.util.spec_from_file_location(
    "watch_snapshot", Path(__file__).resolve().parents[1] / "tools/export-watch-snapshot.py")
snapshot = importlib.util.module_from_spec(spec)
spec.loader.exec_module(snapshot)


class WatchSnapshotPaths(unittest.TestCase):
    def test_completed_receipt_follows_moved_checkpoint(self):
        with tempfile.TemporaryDirectory() as temporary:
            runs = Path(temporary) / "custom-runs"
            checkpoint = runs / "league5/mixed/hero/league/phase-0072.pt"
            checkpoint.parent.mkdir(parents=True)
            checkpoint.write_bytes(b"preserved checkpoint")
            policy = runs / "league5/exports/mixed/hero-r0072.json"
            policy.parent.mkdir(parents=True)
            policy.write_text("{}", encoding="utf-8")
            receipt = runs / "league5/completed/mixed-hero-r0072.json"
            receipt.parent.mkdir(parents=True)
            receipt.write_text(json.dumps({"checkpoint":
                r"C:\old-pc\project-w-server\rl\runs\league5\mixed\hero\league\phase-0072.pt"}),
                encoding="utf-8")
            resolved, metadata = snapshot.resolve_source(runs, "league5", "mixed", "hero", "completed")
            self.assertEqual(resolved, checkpoint)
            self.assertIn("project-w-server", metadata["checkpoint"])

    def test_completed_policy_fallback_without_checkpoint(self):
        with tempfile.TemporaryDirectory() as temporary:
            runs = Path(temporary)
            policy = runs / "league5/exports/mixed/commander-r0073.json"
            policy.parent.mkdir(parents=True)
            policy.write_text("{}", encoding="utf-8")
            resolved, metadata = snapshot.resolve_source(runs, "league5", "mixed", "commander", "completed")
            self.assertEqual(resolved, policy)
            self.assertEqual(metadata, {})

    def test_latest_selects_new_runs_root(self):
        with tempfile.TemporaryDirectory() as temporary:
            runs = Path(temporary)
            checkpoint = runs / "league5/mixed/hero/latest.pt"
            checkpoint.parent.mkdir(parents=True)
            checkpoint.touch()
            resolved, _ = snapshot.resolve_source(runs, "league5", "mixed", "hero", "latest")
            self.assertEqual(resolved, checkpoint)


if __name__ == "__main__":
    unittest.main()
