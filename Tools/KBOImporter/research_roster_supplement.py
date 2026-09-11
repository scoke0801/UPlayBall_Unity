"""KBO 기록이 없는 연구 일반 카드를 출처가 있는 예비 선수풀로 추가한다."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
from collections import Counter, defaultdict
from pathlib import Path

from audit_roster_coverage import ROOT, normalize_team, read_json
from source_backed_runtime_bake import runtime_franchise_id, runtime_player_person_id, runtime_player_season_id

SOURCE_KIND = "ResearchCardSupplement"
DEFAULT_PATH = Path(__file__).with_name("research_roster_supplement.json")
POSITIONS = {"포수": "C", "1루수": "1B", "2루수": "2B", "3루수": "3B", "유격수": "SS",
    "외야수": "LF", "외야": "LF", "1루": "1B", "2루": "2B", "3루": "3B", "유격": "SS",
    "지명타자": "DH", "선발": "P", "중계": "P", "셋업": "P", "마무리": "P"}
ROLES = {"선발": "Starter", "중계": "MiddleRelief", "셋업": "Setup", "마무리": "Closer"}


def digest(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def resolve_identity(card, policy):
    """가까운 시즌의 인물 후보가 유일할 때만 기존 KBO Person을 재사용한다."""
    for person in policy["reviewedResearchPersons"]:
        if card["reference"] in person["references"]:
            return person["sourcePersonKey"], dict(method="ReviewedResearchPerson", evidence=person)
    if card["status"] != "MissingSourceSeason":
        return None, dict(reason=card["status"])
    candidates = [p for p in card["identityCandidates"] if
        min(abs(card["year"] - year) for year in p["years"]) <= policy["maximumIdentityYearDistance"]]
    same_team = [p for p in candidates if card["team"] in p["teams"]]
    if same_team:
        candidates = same_team
    if len(candidates) != 1:
        return None, dict(reason="IdentityNeedsReview", candidateCount=len(candidates))
    person = candidates[0]
    if card["year"] in person["years"]:
        return None, dict(reason="ExistingPersonSeason")
    return person["sourcePlayerId"], dict(method="UniqueNameAndNearbyCareer", candidate=person,
        sameTeamObserved=bool(same_team), maximumYearDistance=policy["maximumIdentityYearDistance"])


def card_attributes(card, policy):
    """미관측 Arm과 반대 타입 능력값은 설정값을 쓰고 관측 필드와 구분한다."""
    row = card["sourceValues"]
    database = card["reference"].startswith("Database:")
    if card["kind"] == "Hitter":
        fields = ("교타", "장타", "주력", "어깨", "수비", "정신력") if database else ("교타력", "장타력", "주력", None, "수비력", "정신력")
        offset = 0
    else:
        fields = ("체력", "구속", "구위", "변화구", "제구력", "정신력")
        offset = 6
    values = [policy["unobservedAttributeBaseline"]] * 12
    observed = []
    for index, key in enumerate(fields):
        if key and row.get(key, "").strip():
            values[offset + index] = int(row[key])
            observed.append(offset + index)
    required = 6 if database or offset == 6 else 5
    if len(observed) != required or any(not 1 <= value <= 100 for value in values):
        raise ValueError(f"연구 카드 능력치가 불완전합니다: {card['reference']}")
    return values, observed


def compile_supplement(audit, normalized_dir, policy):
    """추가 가능한 카드와 신원·타입·소속 재검토 항목을 별도 산출한다."""
    teams = {}
    normalized_hashes = {}
    for path in sorted(normalized_dir.glob("[0-9][0-9][0-9][0-9].json")):
        document = read_json(path)
        normalized_hashes[path.stem] = hashlib.sha256(path.read_bytes()).hexdigest()
        for team in document["teams"]:
            teams[document["year"], normalize_team(team["sourceTeamName"])] = team
    accepted, deferred = [], []
    for card in audit["researchCards"]:
        if card["status"] == "Matched":
            continue
        source_id, identity = resolve_identity(card, policy)
        if source_id is None:
            deferred.append(dict(reference=card["reference"], year=card["year"], team=card["team"],
                name=card["name"], **identity))
            continue
        team = teams.get((card["year"], card["team"]))
        if team is None:
            raise ValueError(f"연구 카드의 구단·연도가 원본에 없습니다: {card['reference']}")
        row = card["sourceValues"]
        position_label = card.get("position") or row.get("수비위치") or row.get("보직", "")
        position = POSITIONS.get(position_label)
        if card["kind"] == "Pitcher":
            position = "P"
        if position is None:
            deferred.append(dict(reference=card["reference"], year=card["year"], team=card["team"], name=card["name"], reason="PositionNeedsReview", position=position_label))
            continue
        values, observed = card_attributes(card, policy)
        accepted.append(dict(sourcePersonKey=source_id, year=card["year"], name=card["name"],
            sourceTeamName=team["sourceTeamName"], sourceFranchiseKey=team["sourceFranchiseId"],
            playerType=card["kind"], position=policy["outfieldFallbackPosition"] if position_label in ("외야수", "외야") else position,
            pitcherRole=ROLES.get(position_label, "MiddleRelief") if card["kind"] == "Pitcher" else "",
            cost=card["cost"], baseAttributes=values, observedAttributeIndices=observed,
            sourceRecordAvailability="Unavailable", identityEvidence=identity, reference=card["reference"],
            sourceFile=card["sourceFile"], sourceFileSha256=hashlib.sha256((ROOT / card["sourceFile"]).read_bytes()).hexdigest(),
            sourceRow=copy.deepcopy(row)))
    # 같은 인물·연도에 구단별 연구 카드가 두 개 있으면 대표 시즌을 임의 선택하지 않는다.
    groups = defaultdict(list)
    for card in accepted:
        groups[card["sourcePersonKey"], card["year"]].append(card)
    cards = []
    for key in sorted(groups):
        rows = groups[key]
        if len(rows) != 1:
            for card in rows:
                deferred.append(dict(reference=card["reference"], year=card["year"], team=card["sourceTeamName"], name=card["name"], reason="DuplicatePersonSeason"))
        else:
            cards.append(rows[0])
    payload = dict(version=policy["version"], policy=policy, normalizedFileHashes=normalized_hashes,
        cards=cards, deferred=sorted(deferred, key=lambda r: (r["year"], r["team"], r["name"], r["reference"])))
    payload["contentSha256"] = digest(payload)
    return payload


def load_supplement(path):
    data = read_json(path)
    expected = data.pop("contentSha256")
    if digest(data) != expected:
        raise ValueError("Research 보충 정본의 해시가 다릅니다.")
    data["contentSha256"] = expected
    return data


def apply_supplement(content, supplement, derivation):
    """기존 Core25·능력치·원기록을 보존하고 연구 카드만 전체 구단풀에 더한다."""
    import pitch_arsenal_generation as pitch
    from source_backed_final_bake import _assign_training_ceiling, _materialize_persons
    from source_backed_runtime_bake import build_world_identity_name_pool

    if "researchRosterSupplementHash" in content["manifest"]:
        raise ValueError("Research 보충은 한 번만 적용할 수 있습니다.")
    years = {row["year"]: row for row in content["years"]}
    persons = {p["playerPersonId"]: p for p in content["playerPersons"]}
    forbidden_names = {c["name"] for c in supplement["cards"]}
    original_names = [name for name in content["worldIdentityNamePool"]["domesticPlayerNames"] if name not in forbidden_names]
    person_seasons = {(s["playerPersonId"], s["originYear"]) for y in years.values() for s in y["playerSeasons"]}
    pitch_balance = pitch.load_balance()
    added = []
    for card in sorted(supplement["cards"], key=lambda c: runtime_player_season_id(c["sourcePersonKey"], c["year"])):
        year = card["year"]
        if year not in years:
            continue
        year_content = years[year]
        person_id = runtime_player_person_id(card["sourcePersonKey"])
        season_id = runtime_player_season_id(card["sourcePersonKey"], year)
        if (person_id, year) in person_seasons:
            raise ValueError(f"연구 보충이 기존 PersonSeason을 중복합니다: {season_id}")
        person_seasons.add((person_id, year))
        franchise = runtime_franchise_id(card["sourceFranchiseKey"])
        team = next(t for t in year_content["teamSeasons"] if t["franchiseId"] == franchise)
        role = card["pitcherRole"]
        # 원기록 없는 보충 카드는 기존 통계 기반 한정 보직을 밀어내지 않는다.
        if role in ("Closer", "Setup"):
            maximum = int(derivation.PITCHER_ROLE_CLASSIFIER_CONFIG["maximumClosersPerTeamSeason" if role == "Closer" else "maximumSetupPitchersPerTeamSeason"])
            count = sum(s["originFranchiseId"] == franchise and s["pitcherRole"] == role for s in year_content["playerSeasons"])
            if count >= maximum:
                role = "MiddleRelief"
        # 출처는 sourceDataKind에 보존하고, 기존 예비 순번은 바꾸지 않는다.
        reserve_prefix = "ReservePitcher:" if card["playerType"] == "Pitcher" else "ReserveHitter:"
        reserve_number = 1 + max((int(s["rosterRole"][len(reserve_prefix):])
            for s in year_content["playerSeasons"]
            if s["originFranchiseId"] == franchise and s["rosterRole"].startswith(reserve_prefix)), default=0)
        season = dict(playerSeasonId=season_id, playerPersonId=person_id, originYear=year,
            originFranchiseId=franchise, originTeamSeasonKey=team["teamSeasonKey"],
            playerType=card["playerType"], position=card["position"], pitcherRole=role,
            pitcherRoleConfidence="Low", registrationType="Domestic", cost=card["cost"],
            baseAttributes=list(card["baseAttributes"]), dataProvenance="SourceBacked", sourceDataKind=SOURCE_KIND,
            sourceRecordAvailability="Unavailable", observedAttributeIndices=card["observedAttributeIndices"],
            rosterRole=f"{reserve_prefix}{reserve_number}")
        _assign_training_ceiling(season, derivation)
        pitch.attach(season, pitch_balance)
        pitch.validate(season, pitch_balance)
        year_content["playerSeasons"].append(season)
        card_id = f"{season_id}:Normal"
        year_content["normalCards"].append(dict(cardId=card_id, playerSeasonId=season_id, edition="Normal", editionStatModifiers=[0] * 12))
        team["allNormalCardIds"].append(card_id)
        if person_id not in persons:
            person = dict(playerPersonId=person_id, primaryPosition=card["position"], careerStartYear=year, careerEndYear=year)
            persons[person_id] = _materialize_persons([person], {}, derivation)[0]
        else:
            persons[person_id]["careerStartYear"] = min(year, persons[person_id]["careerStartYear"])
            persons[person_id]["careerEndYear"] = max(year, persons[person_id]["careerEndYear"])
        added.append(dict(playerSeasonId=season_id, playerPersonId=person_id, year=year,
            name=card["name"], team=card["sourceTeamName"], reference=card["reference"]))
    content["playerPersons"] = sorted(persons.values(), key=lambda p: p["playerPersonId"])
    for year in years.values():
        year["playerSeasons"].sort(key=lambda s: s["playerSeasonId"])
        year["normalCards"].sort(key=lambda c: c["cardId"])
        for team in year["teamSeasons"]:
            team["allNormalCardIds"].sort()
    if len(original_names) < len(persons):
        pool = build_world_identity_name_pool(domestic_player_count=len(persons), foreign_player_count=0,
            franchise_count=len({t["franchiseId"] for y in years.values() for t in y["teamSeasons"]}),
            forbidden_player_names=[c["name"] for c in supplement["cards"]] + original_names,
            forbidden_franchise_names=[])
        original_names.extend(pool["domesticPlayerNames"][:len(persons) - len(original_names)])
    content["worldIdentityNamePool"]["domesticPlayerNames"] = original_names
    all_seasons = [s for y in years.values() for s in y["playerSeasons"]]
    manifest = content["manifest"]
    manifest["researchRosterSupplementVersion"] = supplement["version"]
    manifest["researchRosterSupplementHash"] = supplement["contentSha256"]
    manifest["researchRosterSupplementCount"] = len(added)
    manifest["sourceBackedPlayerSeasonCount"] = sum(s["dataProvenance"] == "SourceBacked" for s in all_seasons)
    manifest["sourceBackedPlayerPersonCount"] = len({s["playerPersonId"] for s in all_seasons if s["dataProvenance"] == "SourceBacked"})
    derivation.validate_bake(content)
    derivation.validate_pitcher_role_limits(content)
    derivation.refresh_content_hash(content)
    return dict(addedCount=len(added), added=added, deferredCount=len(supplement["deferred"]),
        deferredReasons=dict(Counter(c["reason"] for c in supplement["deferred"])))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--audit", type=Path)
    parser.add_argument("--output", type=Path, default=DEFAULT_PATH)
    parser.add_argument("--runtime-source", type=Path)
    parser.add_argument("--runtime-output", type=Path)
    args = parser.parse_args()
    if args.audit:
        policy = read_json(Path(__file__).with_name("research_roster_policy.json"))
        data = compile_supplement(read_json(args.audit), Path(__file__).parent / ".cache/KBOImport/Normalized", policy)
        args.output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    else:
        data = load_supplement(args.output)
    if args.runtime_source:
        import synthetic_bake as bake
        if not args.runtime_output or args.runtime_source.resolve() == args.runtime_output.resolve():
            raise ValueError("검증용 출력은 원본 Archive와 다른 경로여야 합니다.")
        content = bake.load_and_validate_editor_asset_archive(args.runtime_source)
        report = apply_supplement(content, data, bake)
        content = bake.create_runtime_safe_content(content)
        bake.write_editor_asset_archive(content, args.runtime_output)
        if bake.load_and_validate_editor_asset_archive(args.runtime_output) != content:
            raise ValueError("연구 보충 Archive의 저장 왕복이 일치하지 않습니다.")
        (args.runtime_output / "research_supplement_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(accepted=len(data["cards"]), deferred=len(data["deferred"]),
        reasons=dict(Counter(c["reason"] for c in data["deferred"])),
        lg1994=[c["name"] for c in data["cards"] if c["year"] == 1994 and c["sourceTeamName"] == "LG"]), ensure_ascii=False))


if __name__ == "__main__":
    main()
