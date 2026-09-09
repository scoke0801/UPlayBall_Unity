"""구단·연도별 한정 보직 발급과 동률 결정론을 검증한다."""

import copy
import unittest

from synthetic_bake import limit_team_season_pitcher_roles, validate_pitcher_role_limits


class PitcherRoleLimitTests(unittest.TestCase):
    @staticmethod
    def pitcher(identity, role, saves=0, holds=0, year=2025, team="team"):
        return {
            "playerSeasonId": identity, "playerType": "Pitcher",
            "originYear": year, "originTeamSeasonKey": team, "pitcherRole": role,
            "positionRoleDerivationTrace": {
                "pitcherRoleEvidence": {"saves": saves, "holds": holds},
            },
        }

    def test_limits_each_role_independently_by_records(self):
        rows = [self.pitcher("c" + str(i), "Closer", saves=i) for i in range(4)]
        rows += [self.pitcher("s" + str(i), "Setup", holds=i) for i in range(6)]
        limit_team_season_pitcher_roles(rows)
        self.assertEqual([r["playerSeasonId"] for r in rows if r["pitcherRole"] == "Closer"], ["c2", "c3"])
        self.assertEqual([r["playerSeasonId"] for r in rows if r["pitcherRole"] == "Setup"], ["s4", "s5"])
        self.assertEqual(sum(r["pitcherRole"] == "MiddleRelief" for r in rows), 6)

    def test_ties_are_stable_and_repeated_application_is_identical(self):
        rows = [self.pitcher(identity, "Setup", holds=7) for identity in ("c", "b", "a")]
        reverse = copy.deepcopy(rows[::-1])
        limit_team_season_pitcher_roles(rows)
        limit_team_season_pitcher_roles(reverse)
        self.assertEqual(rows, reverse[::-1])
        before = copy.deepcopy(rows)
        limit_team_season_pitcher_roles(rows)
        self.assertEqual(rows, before)
        self.assertEqual(rows[0]["pitcherRole"], "MiddleRelief")

    def test_teams_years_and_short_candidate_pools_are_independent(self):
        rows = [self.pitcher(str(i), "Closer", year=year, team=team)
                for i, (year, team) in enumerate(((2025, "a"), (2025, "b"), (2024, "a")))]
        rows.append(self.pitcher("starter", "Starter"))
        limit_team_season_pitcher_roles(rows)
        self.assertEqual([r["pitcherRole"] for r in rows], ["Closer"] * 3 + ["Starter"])

    def test_2025_lg_candidates_keep_two_closers_and_top_two_setup_pitchers(self):
        rows = [self.pitcher("유영찬", "Closer", saves=21), self.pitcher("장현식", "Closer", saves=10)]
        rows += [self.pitcher(name, "Setup", holds=holds) for name, holds in (
            ("김진성", 33), ("박명근", 10), ("김영우", 7), ("이정용", 7), ("이지강", 4), ("김강률", 4))]
        limit_team_season_pitcher_roles(rows)
        self.assertEqual([r["playerSeasonId"] for r in rows if r["pitcherRole"] == "Setup"], ["김진성", "박명근"])

    def test_archive_validation_rejects_excess_reserve_pitchers(self):
        rows = [self.pitcher(str(i), "Setup") for i in range(3)]
        content = {"years": [{"playerSeasons": rows}]}
        with self.assertRaisesRegex(ValueError, "상한 초과"):
            validate_pitcher_role_limits(content)
        limit_team_season_pitcher_roles(rows)
        validate_pitcher_role_limits(content)


if __name__ == "__main__":
    unittest.main()
