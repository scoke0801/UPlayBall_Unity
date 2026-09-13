from __future__ import annotations

import json
import random
import tempfile
import unittest
from pathlib import Path

import player_handedness
import research_player_handedness as research_tool
import source_backed_runtime_bake as source_plan


PROFILE_HTML = """
<li><strong>선수명: </strong><span id="cphContents_cphContents_cphContents_playerProfile_lblName">김광현</span></li>
<li><strong>생년월일: </strong><span id="cphContents_cphContents_cphContents_playerProfile_lblBirthday">1988년 07월 22일</span></li>
<li><strong>포지션: </strong><span id="cphContents_cphContents_cphContents_playerProfile_lblPosition">투수(좌투좌타)</span></li>
"""


class ProfileParsingTests(unittest.TestCase):
    def test_left_handed_pitcher_is_parsed(self) -> None:
        parsed = research_tool.parse_profile(PROFILE_HTML)
        self.assertEqual(parsed["profileName"], "김광현")
        self.assertEqual(parsed["throws"], "Left")
        self.assertEqual(parsed["bats"], "Left")
        self.assertEqual(parsed["birthDate"], "1988-07-22")

    def test_switch_hitter_bats_switch_and_throws_never_switch(self) -> None:
        html = PROFILE_HTML.replace("투수(좌투좌타)", "내야수(우투양타)")
        parsed = research_tool.parse_profile(html)
        self.assertEqual(parsed["throws"], "Right")
        self.assertEqual(parsed["bats"], "Switch")

    def test_missing_handedness_yields_none(self) -> None:
        html = PROFILE_HTML.replace("투수(좌투좌타)", "투수")
        parsed = research_tool.parse_profile(html)
        self.assertIsNone(parsed["throws"])
        self.assertIsNone(parsed["bats"])
        # 투타가 없어도 생년월일은 살린다.
        self.assertEqual(parsed["birthDate"], "1988-07-22")


class ResearchLookupTests(unittest.TestCase):
    def _write_research(self, directory: Path, players: list[dict]) -> Path:
        path = directory / player_handedness.RESEARCH_FILENAME
        path.write_text(
            json.dumps(
                {
                    "version": player_handedness.EXPECTED_RESEARCH_VERSION,
                    "retrievedAt": "2026-09-13",
                    "players": players,
                    "deferred": [],
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        return path

    def test_researched_person_uses_real_handedness_and_birth_year(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            path = self._write_research(
                Path(raw),
                [
                    {
                        "sourcePlayerId": "77829",
                        "displayName": "김광현",
                        "bats": "Left",
                        "throws": "Left",
                        "birthDate": "1988-07-22",
                    }
                ],
            )
            research = player_handedness.load_research(path)

        person_id = source_plan.runtime_player_person_id("77829")
        rng = random.Random(1)
        self.assertEqual(research.resolve_bats(person_id, rng), ("Left", True))
        self.assertEqual(research.resolve_throws(person_id, rng), ("Left", True))
        self.assertEqual(research.resolve_birth_year(person_id, 1975), (1988, True))

    def test_unresearched_person_falls_back_to_empirical_distribution(self) -> None:
        players = [
            {"sourcePlayerId": str(index), "bats": "Right", "throws": "Right", "birthDate": "1990-01-01"}
            for index in range(90)
        ] + [
            {"sourcePlayerId": str(100 + index), "bats": "Left", "throws": "Left", "birthDate": "1990-01-01"}
            for index in range(10)
        ]
        with tempfile.TemporaryDirectory() as raw:
            research = player_handedness.load_research(self._write_research(Path(raw), players))

        # 조사에 없는 인물은 폴백으로 가되, 분포는 조사된 실제 선수들을 따른다.
        unknown = "PERSON_does_not_exist"
        draws = [research.resolve_bats(unknown, random.Random(seed))[0] for seed in range(400)]
        self.assertTrue(all(not flag for flag in (research.resolve_bats(unknown, random.Random(0))[1],)))
        self.assertNotIn("Switch", draws)  # 조사 표본에 Switch가 없으면 추첨되지 않는다.
        self.assertGreater(draws.count("Right"), draws.count("Left"))

    def test_fallback_birth_year_is_kept_when_not_researched(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            research = player_handedness.load_research(self._write_research(Path(raw), []))
        self.assertEqual(research.resolve_birth_year("PERSON_x", 1971), (1971, False))

    def test_missing_research_file_is_tolerated(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            research = player_handedness.load_research(Path(raw) / "absent.json")
        self.assertEqual(len(research), 0)
        value, is_researched = research.resolve_throws("PERSON_x", random.Random(0))
        self.assertIn(value, player_handedness.THROWS_VALUES)
        self.assertFalse(is_researched)

    def test_version_mismatch_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            path = Path(raw) / player_handedness.RESEARCH_FILENAME
            path.write_text(json.dumps({"version": "player-handedness-research-v0", "players": []}), encoding="utf-8")
            with self.assertRaises(ValueError):
                player_handedness.load_research(path)


if __name__ == "__main__":
    unittest.main()
