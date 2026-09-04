from __future__ import annotations

import copy
import unittest

from sync_historical_validation_docs import SNAPSHOT_END, SNAPSHOT_START, build_snapshot, replace_snapshot


class HistoricalValidationDocumentSyncTests(unittest.TestCase):
    def setUp(self) -> None:
        archive = {
            "contentSchemaVersion": 4,
            "contentHash": "a" * 64,
            "assetArchiveHash": "b" * 64,
            "archivePayloadByteLength": 1000,
            "manifestByteLength": 200,
            "summary": {
                "playerPersonCount": 10,
                "playerSeasonCount": 20,
                "sourceBackedPlayerSeasonCount": 19,
                "replacementGeneratedPlayerSeasonCount": 1,
            },
        }
        self.report = {
            "sourceArchive": archive,
            "runtimeArchive": copy.deepcopy(archive),
            "years": [
                {
                    "originYear": 1982,
                    "sourceHitterCount": 102,
                    "sourcePitcherCount": 39,
                    "replacementHitterCount": 38,
                    "replacementPitcherCount": 71,
                    "replacementRatio": 0.436,
                    "replacementAverageCost": 2.1,
                    "replacementAverageRelevantAbility": 47.1,
                }
            ],
            "verification": {
                "validationDate": "2026-09-03",
                "pythonTests": {"passed": 1, "total": 1, "failed": 0, "skipped": 0},
                "csharpCompileStatus": "통과",
                "unityEditModeStatus": "미실행",
                "historicalWorldStatus": "미실행",
            },
        }

    def test_replace_snapshot_is_repeatable_without_duplicate_markers(self) -> None:
        snapshot = build_snapshot(self.report)
        first = replace_snapshot("# 문서\n", snapshot)
        second = replace_snapshot(first, snapshot)

        self.assertEqual(first, second)
        self.assertEqual(second.count(SNAPSHOT_START), 1)
        self.assertEqual(second.count(SNAPSHOT_END), 1)
        self.assertIn("43.6%", second)


if __name__ == "__main__":
    unittest.main()
