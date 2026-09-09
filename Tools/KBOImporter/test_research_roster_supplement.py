"""연구 보충의 실제 원본 보존·미확보 기록·결정론 계약을 검증한다."""

import copy
import unittest
from pathlib import Path

import synthetic_bake as bake
import research_roster_supplement as supplement
from source_backed_runtime_bake import runtime_franchise_id, runtime_player_person_id, runtime_player_season_id


class ResearchRosterSupplementTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.path = supplement.DEFAULT_PATH
        cls.runtime = supplement.ROOT / "Assets/10.Datas/HistoricalSimulation/1982-2025"
        if not cls.path.is_file():
            raise unittest.SkipTest("연구 보충 정본이 없습니다.")
        cls.data = supplement.load_supplement(cls.path)
        # 과거 로컬 백업은 평가식 버전이 달라질 수 있다. 보충 대상 연도를 현재 식으로 재현한다.
        normalized = Path(__file__).parent / ".cache/KBOImport/Normalized"
        years = sorted({card["year"] for card in cls.data["cards"]})
        cls.before, _ = bake.bake_with_report(normalized, years, 20260901)
        cls.after = copy.deepcopy(cls.before)
        cls.report = supplement.apply_supplement(cls.after, cls.data, bake)

    def test_all_existing_seasons_records_and_core25_are_preserved(self):
        for old, new in zip(self.before["years"], self.after["years"]):
            seasons = {s["playerSeasonId"]: s for s in new["playerSeasons"]}
            self.assertTrue(all(row == seasons[row["playerSeasonId"]] for row in old["playerSeasons"]))
            self.assertEqual(old["originalSeasonRecords"], new["originalSeasonRecords"])
            self.assertEqual(old["originalAwardRecords"], new["originalAwardRecords"])
            self.assertEqual([(t["teamSeasonKey"], t["core25CardIds"], t["referenceStrength"]) for t in old["teamSeasons"]],
                             [(t["teamSeasonKey"], t["core25CardIds"], t["referenceStrength"]) for t in new["teamSeasons"]])

    def test_1994_lg_has_58_players_and_existing_person_identity(self):
        year = next(y for y in self.after["years"] if y["year"] == 1994)
        team = next(t for t in year["teamSeasons"] if t["franchiseId"] == runtime_franchise_id("kbo-selector:LG"))
        self.assertEqual(58, len(team["allNormalCardIds"]))
        season = next(s for s in year["playerSeasons"] if s["playerSeasonId"] == runtime_player_season_id("91006", 1994))
        self.assertEqual(runtime_player_person_id("91006"), season["playerPersonId"])
        self.assertEqual(25, len(team["core25CardIds"]))

    def test_research_attributes_cost_and_record_absence(self):
        seasons = {s["playerSeasonId"]: s for y in self.after["years"] for s in y["playerSeasons"]}
        records = {r["playerSeasonId"] for y in self.after["years"] for r in y["originalSeasonRecords"]}
        for card in self.data["cards"]:
            identity = runtime_player_season_id(card["sourcePersonKey"], card["year"])
            season = seasons[identity]
            self.assertEqual(card["cost"], season["cost"])
            self.assertEqual(card["baseAttributes"], season["baseAttributes"])
            self.assertEqual("Unavailable", season["sourceRecordAvailability"])
            self.assertNotIn(identity, records)

    def test_missing_record_status_and_fake_zero_record_are_rejected(self):
        content = copy.deepcopy(self.after)
        year = next(y for y in content["years"] if any(s.get("sourceDataKind") == supplement.SOURCE_KIND for s in y["playerSeasons"]))
        season = next(s for s in year["playerSeasons"] if s.get("sourceDataKind") == supplement.SOURCE_KIND)
        season["sourceRecordAvailability"] = "Available"
        with self.assertRaisesRegex(ValueError, "미확보"):
            bake.validate_bake(content)
        season["sourceRecordAvailability"] = "Unavailable"
        year["originalSeasonRecords"].append(dict(playerSeasonId=season["playerSeasonId"], games=0))
        with self.assertRaisesRegex(ValueError, "1:1"):
            bake.validate_bake(content)

    def test_input_order_does_not_change_output(self):
        content = copy.deepcopy(self.before)
        reversed_data = copy.deepcopy(self.data)
        reversed_data["cards"].reverse()
        supplement.apply_supplement(content, reversed_data, bake)
        self.assertEqual(self.after, content)

    def test_duplicate_application_fails(self):
        with self.assertRaisesRegex(ValueError, "한 번"):
            supplement.apply_supplement(copy.deepcopy(self.after), self.data, bake)

    def test_runtime_removes_source_labels_and_has_enough_unique_names(self):
        content = bake.create_runtime_safe_content(self.after)
        forbidden = {c["name"] for c in self.data["cards"]}
        names = content["worldIdentityNamePool"]["domesticPlayerNames"]
        self.assertGreaterEqual(len(names), len(content["playerPersons"]))
        self.assertEqual(len(names), len(set(names)))
        self.assertFalse(forbidden.intersection(names))
        for year in content["years"]:
            for season in year["playerSeasons"]:
                self.assertNotIn("sourcePersonKey", season)
                self.assertNotIn("sourceRow", season)
                self.assertNotIn("name", season)

    def test_identity_does_not_join_far_away_namesake(self):
        policy = self.data["policy"]
        card = dict(reference="fixture", year=1994, team="LG", status="MissingSourceSeason",
            identityCandidates=[dict(sourcePlayerId="old", years=[1993, 1997], teams=["LG"]),
                                dict(sourcePlayerId="new", years=[2021], teams=["SSG"])])
        identity, _ = supplement.resolve_identity(card, policy)
        self.assertEqual("old", identity)
        card["identityCandidates"].append(dict(sourcePlayerId="other", years=[1993], teams=["LG"]))
        self.assertIsNone(supplement.resolve_identity(card, policy)[0])

    def test_normal_bake_can_recreate_research_supplement(self):
        normalized = Path(__file__).parent / ".cache/KBOImport/Normalized"
        content, report = bake.bake_with_report(normalized, [1994], 20260901, self.path)
        year = content["years"][0]
        team = next(t for t in year["teamSeasons"] if t["franchiseId"] == runtime_franchise_id("kbo-selector:LG"))
        self.assertEqual(58, len(team["allNormalCardIds"]))
        self.assertEqual(sum(c["year"] == 1994 for c in self.data["cards"]), report["researchRosterSupplement"]["addedCount"])
        bake.validate_bake(bake.create_runtime_safe_content(content))


if __name__ == "__main__":
    unittest.main()
