"""변경 전/후 실제 Bake를 비교하며 탐색 참조와 경기 검증의 범위를 분리한다."""
from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from pathlib import Path

from study_pm_calibration import ROOT, REPORT, metrics, read


def load_seasons(root):
    """명시된 Archive의 Source 선수만 Stable ID로 읽는다."""
    result = {}
    for path in sorted((root / "Years").glob("*.json")):
        for season in read(path)["playerSeasons"]:
            result[season["playerSeasonId"]] = season
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", type=Path, required=True)
    parser.add_argument("--after", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, default=REPORT)
    args = parser.parse_args()
    report_dir = args.output_dir
    before, after = load_seasons(args.before), load_seasons(args.after)
    if set(before) != set(after):
        raise ValueError("Source 선수 ID 집합이 변경되었습니다.")
    with (ROOT / "Tools/PMReference/reports/COST_PLAYER_BASELINE.csv").open(encoding="utf-8-sig") as stream:
        editor_id = {row["PlayerSeasonId"]: row["EditorPlayerSeasonId"] for row in csv.DictReader(stream)}
    with (ROOT / "Tools/PMReference/reports/COST_REFERENCE_VALIDATION.csv").open(encoding="utf-8-sig") as stream:
        references = [row for row in csv.DictReader(stream) if row["DataVersion"] == "OriginalObserved2011"
                      and editor_id.get(row["PlayerSeasonId"]) in after]
    comparisons = [{"referenceCardId": row["ReferenceCardId"], "year": row["Year"],
                    "name": row["PlayerName"], "team": row["SourceTeam"], "variant": row["CardType"],
                    "snapshot": row["DataVersion"], "sourceUrl": row["SourceUrl"],
                    "hasCostConflict": row["HasCostConflict"], "yearSplit": row["YearSplit"],
                    "playerType": after[editor_id[row["PlayerSeasonId"]]]["playerType"],
                    "reference": int(row["ReferenceCost"]), "before": before[editor_id[row["PlayerSeasonId"]]]["cost"],
                    "after": after[editor_id[row["PlayerSeasonId"]]]["cost"]} for row in references]
    groups = []
    for split in ("All", "Train", "Validation", "Holdout"):
        part = [row for row in comparisons if split == "All" or row["yearSplit"] == split]
        groups.append({"split": split, "before": metrics([(r["reference"], r["before"]) for r in part]),
                       "after": metrics([(r["reference"], r["after"]) for r in part])})
    workbook = read(ROOT / "docs/reports/pm_reference_review_20260906/workbook_extracted.json")
    identity = {(s["originYear"], s["originFranchiseId"], name): s["playerSeasonId"] for s in after.values() for name in s["sourceReferenceNames"]}
    article_rows = next(s["Rows"] for s in workbook["Sheets"] if s["Name"] == "Cost_근거")[1:]
    external = []
    article_comparisons = []
    for row in article_rows:
        c = row["Cells"]
        season_id = identity.get((int(c["A"]), c.get("B", ""), c["C"]))
        if season_id:
            external.append({"date": c["G"], "reference": int(c["E"]), "before": before[season_id]["cost"], "after": after[season_id]["cost"]})
        article_comparisons.append({"workbookRow": row["Row"], "year": c["A"], "team": c.get("B", ""),
                                    "name": c["C"], "variant": c["F"], "snapshot": c["G"],
                                    "sourceUrl": c["K"], "reference": int(c["E"]),
                                    "before": before[season_id]["cost"] if season_id else "",
                                    "after": after[season_id]["cost"] if season_id else "",
                                    "joinStatus": "ExactYearTeamName" if season_id else "Unresolved_NoForcedAlias"})
    external_metrics = [{"date": date, **{phase: metrics([(r["reference"], r[phase]) for r in external if r["date"] == date])
                                         for phase in ("before", "after")}} for date in sorted({r["date"] for r in external})]
    images = next(s["Rows"] for s in workbook["Sheets"] if s["Name"] == "정확스탯_카드이미지")[1:]
    ability = []
    image_comparisons = []
    attributes = (("Contact", "G", 0), ("Power", "H", 1), ("Speed", "I", 2),
                  ("Defense", "K", 4), ("BatterMental", "L", 5), ("Stamina", "M", 6),
                  ("Velocity", "N", 7), ("Stuff", "O", 8), ("Breaking", "P", 9),
                  ("Control", "Q", 10), ("PitcherMental", "R", 11))
    for attribute, column, index in attributes:
        part = []
        for row in images:
            c = row["Cells"]
            season_id = identity.get((int(c["A"]), c["B"], c["C"]))
            if column not in c:
                continue
            comparison = {"workbookRow": row["Row"], "year": int(c["A"]), "team": c["B"], "name": c["C"],
                          "variant": c["E"], "sourceUrl": c.get("V", ""), "attribute": attribute,
                          "reference": int(c[column]),
                          "before": before[season_id]["baseAttributes"][index] if season_id else "",
                          "after": after[season_id]["baseAttributes"][index] if season_id else "",
                          "metricStatus": "NormalExploratory" if c["E"] == "Normal" and season_id else
                                          "ExcludedVariant" if season_id else "UnresolvedIdentity"}
            image_comparisons.append(comparison)
            if comparison["metricStatus"] == "NormalExploratory":
                part.append(comparison)
        ability.append({"attribute": attribute, "before": metrics([(r["reference"], r["before"]) for r in part]),
                        "after": metrics([(r["reference"], r["after"]) for r in part]), "rows": part})
    output = {"referencePolicy": "OriginalObserved2011_ExploratoryNotFinalNormalAcceptance", "costMetrics": groups,
              "externalArticleMetrics": external_metrics, "abilityMetrics": ability,
              "sourceCount": len(after), "costChanged": sum(before[k]["cost"] != after[k]["cost"] for k in after),
              "hitterCostChanged": sum(before[k]["cost"] != after[k]["cost"] for k in after if after[k]["playerType"] == "Hitter"),
              "distributionBefore": dict(sorted(Counter(s["cost"] for s in before.values()).items())),
              "distributionAfter": dict(sorted(Counter(s["cost"] for s in after.values()).items())),
              "beforeManifest": read(args.before / "manifest.json")["sourceManifest"],
              "afterManifest": read(args.after / "manifest.json")["sourceManifest"]}
    report_dir.mkdir(parents=True, exist_ok=True)
    (report_dir / "bake_comparison.json").write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding="utf-8")
    with (report_dir / "reference_cost_comparison.csv").open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(comparisons[0]))
        writer.writeheader()
        writer.writerows(comparisons)
    for filename, rows in (("workbook_cost_comparison.csv", article_comparisons),
                           ("workbook_ability_comparison.csv", image_comparisons)):
        with (report_dir / filename).open("w", encoding="utf-8-sig", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
            writer.writeheader()
            writer.writerows(rows)
    print(json.dumps({k: output[k] for k in ("costMetrics", "externalArticleMetrics", "sourceCount", "costChanged", "hitterCostChanged", "distributionBefore", "distributionAfter")}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
