"""검증된 연구 보충 Archive를 백업하고 Editor Runtime·게임 Runtime에 동기화한다."""

import argparse
import json
import shutil
from collections import Counter
from pathlib import Path

import synthetic_bake as bake
from research_roster_supplement import ROOT, DEFAULT_PATH, SOURCE_KIND, load_supplement
from source_backed_runtime_bake import runtime_player_person_id


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def verify_preservation(before, after):
    """원본 선수와 기록, 363개 구단의 Core25가 바뀌지 않았는지 확인한다."""
    for old, new in zip(before["years"], after["years"]):
        by_id = {s["playerSeasonId"]: s for s in new["playerSeasons"]}
        if old["year"] != new["year"] or any(s != by_id.get(s["playerSeasonId"]) for s in old["playerSeasons"]):
            raise ValueError("기존 선수 시즌의 값이 변경되었습니다.")
        if old["originalSeasonRecords"] != new["originalSeasonRecords"] or old["originalAwardRecords"] != new["originalAwardRecords"]:
            raise ValueError("원본 기록·수상이 변경되었습니다.")
        before_core = [(t["teamSeasonKey"], t["core25CardIds"], t["referenceStrength"]) for t in old["teamSeasons"]]
        after_core = [(t["teamSeasonKey"], t["core25CardIds"], t["referenceStrength"]) for t in new["teamSeasons"]]
        if before_core != after_core:
            raise ValueError("기존 Core25 또는 기본 전력이 변경되었습니다.")
    if len(before["years"]) != len(after["years"]):
        raise ValueError("연도 수가 바뀌었습니다.")


def summarize_simulation(simulation, baseline):
    team_rows = [t for row in simulation["rows"] for t in row["teams"]]
    statistics = [s for row in simulation["rows"] for s in row["statistics"]
        if not s["IsFirstHalf"] and not s["IsPostseason"] and not s["IsAllStarGame"]]
    total = lambda key: sum(row[key] for row in team_rows)
    team_games = total("Games")
    if simulation["rows"][0]["teams"] != baseline["rows"][0]["teams"]:
        raise ValueError("동일 시드의 변경 전후 구단 성적이 다릅니다.")
    return dict(games=simulation["games"], regularSeasonGames=team_games // 2,
        determinismChecks=simulation["determinismChecks"], sameSeedTeamStatisticsUnchanged=True,
        battingAverage=total("Hits") / total("AtBats"), earnedRunAverage=27 * total("EarnedRuns") / total("PitchingOuts"),
        runsPerTeamGame=total("RunsScored") / team_games,
        homeRunsPerGame=sum(s["HomeRuns"] for s in statistics) / (team_games / 2),
        walkStrikeoutRatio=sum(s["Walks"] for s in statistics) / sum(s["Strikeouts"] for s in statistics))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage", type=Path, required=True)
    parser.add_argument("--simulation", type=Path, required=True)
    parser.add_argument("--baseline-simulation", type=Path, required=True)
    parser.add_argument("--publish", action="store_true")
    args = parser.parse_args()
    production = ROOT / "Assets/10.Datas/HistoricalSimulation/1982-2025"
    editor = ROOT / "Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Runtime"
    backup = ROOT / ".tmp/research-roster/Before"
    report_dir = ROOT / "docs/reports/historical-roster-coverage"
    before = bake.load_and_validate_editor_asset_archive(production)
    after = bake.load_and_validate_editor_asset_archive(args.stage)
    supplement = load_supplement(DEFAULT_PATH)
    if after["manifest"].get("researchRosterSupplementHash") != supplement["contentSha256"]:
        raise ValueError("검증 Archive가 최신 연구 보충 정본과 다릅니다.")
    verify_preservation(before, after)
    simulation, baseline = read(args.simulation), read(args.baseline_simulation)
    if simulation["contentHash"] != after["manifest"]["contentHash"] or baseline["contentHash"] != before["manifest"]["contentHash"]:
        raise ValueError("시뮬레이션 입력 해시가 게시 전후 Archive와 다릅니다.")
    if simulation["games"] < 10000 or simulation["determinismChecks"] < 1:
        raise ValueError("1만 경기·결정론 검증이 부족합니다.")
    added = [s for y in after["years"] for s in y["playerSeasons"] if s.get("sourceDataKind") == SOURCE_KIND]
    report = dict(published=args.publish, beforeContentHash=before["manifest"]["contentHash"],
        afterContentHash=after["manifest"]["contentHash"], supplementalSeasons=len(added),
        affectedTeamSeasons=len({s["originTeamSeasonKey"] for s in added}),
        beforeSeasons=sum(len(y["playerSeasons"]) for y in before["years"]),
        afterSeasons=sum(len(y["playerSeasons"]) for y in after["years"]),
        sourceRecordsPreserved=True, core25Preserved=True, existingAttributesPreserved=True,
        costCounts=dict(sorted(Counter(s["cost"] for s in added).items())),
        deferredCount=len(supplement["deferred"]), deferredReasons=dict(Counter(s["reason"] for s in supplement["deferred"])),
        simulation=summarize_simulation(simulation, baseline))
    paths = [Path("manifest.json"), Path("player_persons.json")] + [Path(f"Years/{y['year']}.json") for y in after["years"]]
    if args.publish:
        if backup.exists():
            raise ValueError("보충 전 백업이 이미 있습니다. 기존 백업을 덮어쓰지 않습니다.")
        for path in paths:
            target = backup / path
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(production / path, target)
        for destination in (production, editor):
            for path in paths:
                bake.write_bytes_atomically(destination / path, (args.stage / path).read_bytes())
            if bake.load_and_validate_editor_asset_archive(destination) != after:
                raise ValueError(f"게시 후 검증이 다릅니다: {destination}")
        old_report = editor / "validation_report.json"
        if old_report.exists():
            validation = read(old_report)
            validation["researchRosterSupplement"] = report
            validation["runtimeArchive"] = bake.build_archive_validation_snapshot(read(editor / "manifest.json"))
            bake.write_bytes_atomically(old_report, bake.canonical_json_bytes(validation))
        identities_path = ROOT / "Assets/10.Datas/Resources/DevelopmentKboIdentities/DevelopmentRealIdentityCatalog.json"
        identities = read(identities_path)
        known = {p["id"] for p in identities["players"]}
        shutil.copy2(identities_path, backup / "development_identities.json")
        for card in supplement["cards"]:
            identity = runtime_player_person_id(card["sourcePersonKey"])
            if identity not in known:
                identities["players"].append(dict(id=identity, name=card["name"]))
                known.add(identity)
        identities["players"].sort(key=lambda p: p["id"])
        bake.write_bytes_atomically(identities_path, bake.canonical_json_bytes(identities))
    report_dir.mkdir(parents=True, exist_ok=True)
    (report_dir / "supplement-result.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
