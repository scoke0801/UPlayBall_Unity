"""정규화된 KBO Source에서 개발용 실제 표시 Identity 카탈로그를 만든다.

이 산출물은 Editor와 Development Build의 표시 전용이다. Runtime Stable ID와
World Identity 원본은 바꾸지 않으며 Production Build에서는 코드 경계에서 비활성화된다.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from source_backed_runtime_bake import (
    runtime_franchise_id,
    runtime_player_person_id,
    runtime_team_season_key,
)


TEAM_IDENTITIES = {
    "kbo-selector:HT": ("KIA 타이거즈", ["KIA", "타이거즈"], "KiaTigers"),
    "kbo-selector:KT": ("KT 위즈", ["KT", "위즈"], "KtWiz"),
    "kbo-selector:LG": ("LG 트윈스", ["LG", "트윈스"], "LgTwins"),
    "kbo-selector:NC": ("NC 다이노스", ["NC", "다이노스"], "NcDinos"),
    "kbo-selector:SK": ("SSG 랜더스", ["SSG", "랜더스"], "SsgLanders"),
    "kbo-selector:OB": ("두산 베어스", ["두산", "베어스"], "DoosanBears"),
    "kbo-selector:LT": ("롯데 자이언츠", ["롯데", "자이언츠"], "LotteGiants"),
    "kbo-selector:SS": ("삼성 라이온즈", ["삼성", "라이온즈"], "SamsungLions"),
    "kbo-selector:WO": ("키움 히어로즈", ["키움", "히어로즈"], "KiwoomHeroes"),
    "kbo-selector:HH": ("한화 이글스", ["한화", "이글스"], "HanwhaEagles"),
    "kbo-selector:HD": ("현대 유니콘스", ["현대", "유니콘스"], "HyundaiUnicorns"),
    "kbo-selector:SB": ("쌍방울 레이더스", ["쌍방울", "레이더스"], "SsangbangwoolRaiders"),
}


# Source는 구단 매각 전후에도 같은 Franchise 계보를 유지한다. 실제 표시에서는
# Franchise의 최신 이름이 아니라 해당 TeamSeason 당시의 브랜드를 사용해야 한다.
TEAM_SEASON_IDENTITIES = {
    "삼미": ("삼미 슈퍼스타즈", "SammiSuperstars"),
    "청보": ("청보 핀토스", "ChungboPintos"),
    "태평양": ("태평양 돌핀스", "PacificDolphins"),
    "현대": ("현대 유니콘스", "HyundaiUnicorns"),
    "해태": ("해태 타이거즈", "HaitaiTigers1982"),
    "KIA": ("KIA 타이거즈", "KiaTigers"),
    "MBC": ("MBC 청룡", "MbcChungyong"),
    "LG": ("LG 트윈스", "LgTwins"),
    "OB": ("OB 베어스", "ObBears"),
    "두산": ("두산 베어스", "DoosanBears"),
    "빙그레": ("빙그레 이글스", "BinggraeEagles"),
    "한화": ("한화 이글스", "HanwhaEagles"),
    "쌍방울": ("쌍방울 레이더스", "SsangbangwoolRaiders"),
    "SK": ("SK 와이번스", "SkWyverns"),
    "SSG": ("SSG 랜더스", "SsgLanders"),
    "우리": ("우리 히어로즈", "HeroesWordmark"),
    "히어로즈": ("히어로즈", "HeroesWordmark"),
    "넥센": ("넥센 히어로즈", "NexenHeroes"),
    "키움": ("키움 히어로즈", "KiwoomHeroes"),
    "삼성": ("삼성 라이온즈", "SamsungLions"),
    "롯데": ("롯데 자이언츠", "LotteGiants"),
    "NC": ("NC 다이노스", "NcDinos"),
    "KT": ("KT 위즈", "KtWiz"),
}
def build_catalog(normalized_dir: Path, research_supplement_path: Path | None = None) -> dict[str, Any]:
    """1982~2025 정규화 파일에서 선수 실명과 연도별 구단 Identity를 내보낸다."""

    player_names_by_source_id: dict[str, str] = {}
    observed_franchise_ids: set[str] = set()
    team_seasons: list[dict[str, str]] = []
    season_files = sorted(
        (path for path in normalized_dir.glob("[0-9][0-9][0-9][0-9].json")),
        key=lambda path: int(path.stem),
    )
    if not season_files:
        raise ValueError(f"정규화된 KBO 시즌 파일이 없습니다: {normalized_dir}")

    for season_path in season_files:
        payload = json.loads(season_path.read_text(encoding="utf-8"))
        for player in payload.get("players", []):
            source_player_id = str(player.get("sourcePlayerId", "")).strip()
            player_name = str(player.get("playerName", "")).strip()
            if source_player_id and player_name:
                player_names_by_source_id[source_player_id] = player_name
        for team in payload.get("teams", []):
            source_franchise_id = str(team.get("sourceFranchiseId", "")).strip()
            source_team_name = str(team.get("sourceTeamName", "")).strip()
            if source_franchise_id:
                observed_franchise_ids.add(source_franchise_id)
            if not source_franchise_id or source_team_name not in TEAM_SEASON_IDENTITIES:
                raise ValueError(
                    f"개발용 실제 TeamSeason Identity가 없습니다: "
                    f"year={payload.get('year')}, franchise={source_franchise_id}, "
                    f"team={source_team_name}"
                )
            year = int(payload["year"])
            display_name, emblem_file = TEAM_SEASON_IDENTITIES[source_team_name]
            team_seasons.append(
                {
                    "teamSeasonKey": runtime_team_season_key(
                        source_franchise_id, year
                    ),
                    "name": display_name,
                    "emblemResource": (
                        f"DevelopmentKboIdentities/Emblems/{emblem_file}"
                    ),
                }
            )

    if research_supplement_path is not None:
        from research_roster_supplement import load_supplement
        for card in load_supplement(research_supplement_path)["cards"]:
            player_names_by_source_id.setdefault(card["sourcePersonKey"], card["name"])

    players = [
        {"id": runtime_player_person_id(source_id), "name": player_names_by_source_id[source_id]}
        for source_id in sorted(player_names_by_source_id)
    ]
    teams = []
    for source_franchise_id in sorted(TEAM_IDENTITIES):
        name, aliases, emblem_file = TEAM_IDENTITIES[source_franchise_id]
        teams.append(
            {
                "id": runtime_franchise_id(source_franchise_id),
                "name": name,
                "aliases": aliases,
                "emblemResource": f"DevelopmentKboIdentities/Emblems/{emblem_file}",
            }
        )

    return {
        "version": 2,
        "players": players,
        "teams": teams,
        "teamSeasons": sorted(team_seasons, key=lambda item: item["teamSeasonKey"]),
        "unmappedHistoricalFranchiseIds": sorted(observed_franchise_ids - TEAM_IDENTITIES.keys()),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--research-supplement", type=Path)
    parser.add_argument(
        "--normalized-dir",
        type=Path,
        default=Path(__file__).parent / ".cache" / "KBOImport" / "Normalized",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(__file__).parents[2]
        / "Assets"
        / "10.Datas"
        / "Resources"
        / "DevelopmentKboIdentities"
        / "DevelopmentRealIdentityCatalog.json",
    )
    args = parser.parse_args()
    catalog = build_catalog(args.normalized_dir, args.research_supplement)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(catalog, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(
        f"실제 Identity 카탈로그 생성 완료: 선수 {len(catalog['players'])}명, "
        f"구단 {len(catalog['teams'])}개, "
        f"TeamSeason {len(catalog['teamSeasons'])}개 -> {args.output}"
    )


if __name__ == "__main__":
    main()
