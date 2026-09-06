"""원작 관측을 판본별로 비교하고 이름을 쓰지 않는 제한된 보정 후보를 고른다."""

from __future__ import annotations

import csv
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / "docs/reports/pm_calibration_20260906"


def read(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def metrics(pairs):
    """같은 관측 집합에서 가격 오차를 비교한다."""
    differences = [predicted - expected for expected, predicted in pairs]
    return {"count": len(differences), "mae": sum(abs(x) for x in differences) / len(differences),
            "exact": sum(x == 0 for x in differences) / len(differences),
            "within1": sum(abs(x) <= 1 for x in differences) / len(differences),
            "bias": sum(differences) / len(differences)} if differences else {"count": 0}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-editor", type=Path, required=True)
    parser.add_argument("--baseline-balance", type=Path, required=True)
    args = parser.parse_args()
    REPORT.mkdir(parents=True, exist_ok=True)
    balance = read(args.baseline_balance)
    seasons = {}
    by_identity = {}
    for year in range(1982, 2026):
        data = read(args.baseline_editor / f"Years/{year}.json")
        for season in data["playerSeasons"]:
            seasons[season["playerSeasonId"]] = season
            for name in season["sourceReferenceNames"]:
                by_identity[(year, season["originFranchiseId"], name)] = season
    with (ROOT / "Tools/PMReference/reports/COST_PLAYER_BASELINE.csv").open(encoding="utf-8-sig") as stream:
        editor_id = {row["PlayerSeasonId"]: row["EditorPlayerSeasonId"] for row in csv.DictReader(stream)}
    with (ROOT / "Tools/PMReference/reports/COST_REFERENCE_VALIDATION.csv").open(encoding="utf-8-sig") as stream:
        labels = [row for row in csv.DictReader(stream) if row["DataVersion"] == "OriginalObserved2011"
                  and row["PlayerSeasonId"] in editor_id and editor_id[row["PlayerSeasonId"]] in seasons]

    def predict(season, offset):
        trace = season["costDerivationTrace"]
        value = trace["continuousValue"] + offset
        cost = next(row["cost"] for row in balance["costValueModel"]["valueTierThresholds"] if value < row["upperExclusive"])
        return min(cost, trace["eliteEligibility"]["maximumCost"])

    offsets = {}
    scores = []
    for player_type in ("Hitter", "Pitcher"):
        subset = [row for row in labels if seasons[editor_id[row["PlayerSeasonId"]]]["playerType"] == player_type]
        train = [row for row in subset if row["YearSplit"] == "Train"]
        # 학습 연도에서만 탐색하고 0에서 멀어지는 보정에 작은 벌점을 부여한다.
        candidates = []
        for step in range(-8, 9):
            offset = step * 0.25
            result = metrics([(int(row["ReferenceCost"]), predict(seasons[editor_id[row["PlayerSeasonId"]]], offset)) for row in train])
            candidates.append((result["mae"] + 0.02 * abs(offset), abs(offset), offset))
        offset = min(candidates)[2]
        offsets[player_type] = offset
        for split in ("Train", "Validation", "Holdout"):
            part = [row for row in subset if row["YearSplit"] == split]
            scores.append({"type": player_type, "split": split, "offset": offset,
                           "before": metrics([(int(row["ReferenceCost"]), seasons[editor_id[row["PlayerSeasonId"]]]["cost"]) for row in part]),
                           "after": metrics([(int(row["ReferenceCost"]), predict(seasons[editor_id[row["PlayerSeasonId"]]], offset)) for row in part])})

    workbook = read(ROOT / "docs/reports/pm_reference_review_20260906/workbook_extracted.json")
    article_rows = next(sheet["Rows"] for sheet in workbook["Sheets"] if sheet["Name"] == "Cost_근거")[1:]
    external = []
    for row in article_rows:
        c = row["Cells"]
        season = by_identity.get((int(c["A"]), c.get("B", ""), c["C"]))
        if season:
            external.append((c, season))
    external_scores = []
    for date in sorted({c["G"] for c, season in external}):
        for player_type in ("Hitter", "Pitcher"):
            part = [(c, season) for c, season in external if c["G"] == date and season["playerType"] == player_type]
            external_scores.append({"date": date, "type": player_type,
                                    "before": metrics([(int(c["E"]), season["cost"]) for c, season in part]),
                                    "after": metrics([(int(c["E"]), predict(season, offsets[player_type])) for c, season in part])})
    image_rows = next(sheet["Rows"] for sheet in workbook["Sheets"] if sheet["Name"] == "정확스탯_카드이미지")[1:]
    image_comparison = []
    for row in image_rows:
        c = row["Cells"]
        season = by_identity.get((int(c["A"]), c["B"], c["C"]))
        if c["E"] != "Normal" or season is None:
            continue
        mapping = zip("GHIKL", (0, 1, 2, 4, 5)) if c["D"] == "타자" else zip("MNOPQR", range(6, 12))
        for column, index in mapping:
            image_comparison.append({"year": int(c["A"]), "name": c["C"], "index": index,
                                     "reference": int(c[column]), "current": season["baseAttributes"][index],
                                     "trace": season["abilityDerivationTrace"][index if index < 6 else index - 6]})
    output = {"labelPolicy": "OriginalObserved2011_ExploratorySubtypeUnknown", "costOffsets": offsets,
              "costScores": scores, "externalScores": external_scores, "normalImageDifferences": image_comparison,
              "workbookHash": workbook["SHA256"], "inputBalanceHash": hashlib.sha256(args.baseline_balance.read_bytes()).hexdigest()}
    (REPORT / "candidate_study.json").write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"costOffsets": offsets, "externalScores": external_scores}, ensure_ascii=False, indent=2))
    for index in sorted({row["index"] for row in image_comparison}):
        part = [row for row in image_comparison if row["index"] == index]
        print(index, metrics([(row["reference"], row["current"]) for row in part]),
              [(row["name"], row["reference"], row["current"]) for row in part])


if __name__ == "__main__":
    main()
