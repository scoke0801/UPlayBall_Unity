"""일반 카드 전사 표본에서 공통 기록 평가 후보를 비교한다. Production을 쓰지 않는다."""
from __future__ import annotations

import copy
import argparse
import json
from pathlib import Path

import synthetic_bake as bake
from study_pm_calibration import ROOT, REPORT, read


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-balance", type=Path, required=True)
    args = parser.parse_args()
    bake.DERIVATION_BALANCE.clear()
    bake.DERIVATION_BALANCE.update(read(args.baseline_balance))
    bake.validate_derivation_balance(bake.DERIVATION_BALANCE)
    workbook = read(ROOT / "docs/reports/pm_reference_review_20260906/workbook_extracted.json")
    rows = next(s["Rows"] for s in workbook["Sheets"] if s["Name"] == "정확스탯_카드이미지")[1:]
    source = {}
    for year in (2008, 2009, 2010):
        data = read(ROOT / f"Tools/KBOImporter/.cache/KBOImport/Normalized/{year}.json")
        games, _ = bake.source_season_games(data)
        for player_type in ("Hitter", "Pitcher"):
            players = [p for p in data["players"] if bake.source_player_type(p) == player_type]
            vectors, traces, groups = bake.build_adjusted_feature_pool(players, year, player_type,
                bake.derive_pitcher_role_availability(data["players"]), season_games=games)
            for player in players:
                source[(year, bake.source_primary_team_name(player), player["playerName"])] = (
                    player_type, vectors[player["sourcePlayerId"]], traces[player["sourcePlayerId"]])
    base = copy.deepcopy(bake.DERIVATION_BALANCE)
    definitions = {
        "BatterMental": [{"BattingAverage": 1.0}, {"OnBasePercentage": .7, "BattingAverage": .3}, {"WalkRate": .5, "BattingAverage": .5}],
        "Breaking": [{"NegativeEarnedRunAverage": 1.0}, {"StrikeoutsPerNine": .7, "NegativeHomeRunsPerNine": .3}, {"StrikeoutsPerNine": .5, "NegativeEarnedRunAverage": .5}],
        "Stuff": [{"NegativeEarnedRunAverage": 1.0}, {"NegativeWhip": .6, "StrikeoutsPerNine": .4}],
        "Speed": [{"StolenBases": 1.0}, {"StolenBaseAttemptRate": .7, "StolenBaseSuccessRate": .3}],
    }
    selected = []
    for attribute, profiles in definitions.items():
        player_type = "Hitter" if attribute in ("BatterMental", "Speed") else "Pitcher"
        index = bake.ABILITY_INDEX[attribute]
        column = {"BatterMental": "L", "Speed": "I", "Breaking": "P", "Stuff": "O"}[attribute]
        labels = [row["Cells"] for row in rows if row["Cells"]["E"] == "Normal" and column in row["Cells"]
                  and (int(row["Cells"]["A"]), row["Cells"]["B"], row["Cells"]["C"]) in source]
        def score(profile, offset, year_filter):
            errors = []
            for c in labels:
                year = int(c["A"])
                if not year_filter(year):
                    continue
                kind, vector, trace = source[(year, c["B"], c["C"])]
                metrics = dict(zip(bake.HITTER_METRIC_NAMES if kind == "Hitter" else bake.PITCHER_METRIC_NAMES, vector))
                available = {m: w for m, w in profile.items() if trace[m]["isAvailable"]}
                total = sum(available.values())
                value = 55 + offset + base["ratingProfiles"][kind][attribute]["scale"] * sum(metrics[m] * w / total for m, w in available.items()) if total else 55
                errors.append(abs(bake.clamp_rating(value) - int(c[column])))
            return sum(errors) / len(errors) if errors else None
        candidates = []
        for i, profile in enumerate(profiles):
            # 각 능력에 많은 계수를 맞추지 않고 기존 폭과 작은 공통 중심 이동만 비교한다.
            for offset in (0, 4, 8, 12):
                mae = score(profile, offset, lambda year: year < 2010)
                candidates.append((mae + .05 * offset + (.1 if i else 0), i, offset))
        _, i, offset = min(candidates)
        result = {"attribute": attribute, "profile": profiles[i], "centerOffset": offset,
                  "trainBefore": score(profiles[0], 0, lambda y: y < 2010),
                  "trainAfter": score(profiles[i], offset, lambda y: y < 2010),
                  "validationBefore": score(profiles[0], 0, lambda y: y == 2010),
                  "validationAfter": score(profiles[i], offset, lambda y: y == 2010), "count": len(labels)}
        selected.append(result)
    REPORT.mkdir(parents=True, exist_ok=True)
    (REPORT / "ability_study.json").write_text(json.dumps(selected, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(selected, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
