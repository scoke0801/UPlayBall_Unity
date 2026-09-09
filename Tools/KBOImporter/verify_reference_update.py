"""일반 카드 보정의 변경 범위·대량 경기 결과를 검증하고 Archive를 안전하게 게시한다."""

import argparse
import json
import shutil
from collections import Counter
from pathlib import Path

import synthetic_bake as bake
from source_backed_runtime_bake import runtime_franchise_id

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def verify_scope(before, after, year, franchise):
    """대상 구단 이외의 선수·시즌과 기존 기록이 동일한지 검증한다."""
    changed = []
    if before['playerPersons'] != after['playerPersons']:
        raise ValueError('선수 신원 데이터가 변경되었습니다.')
    if before.get('worldIdentityNamePool') != after.get('worldIdentityNamePool'):
        raise ValueError('이름 후보군이 변경되었습니다.')
    if [y['year'] for y in before['years']] != [y['year'] for y in after['years']]:
        raise ValueError('연도 구성이 변경되었습니다.')
    for old, new in zip(before['years'], after['years']):
        if old['year'] != year:
            if old != new:
                raise ValueError(f"대상 밖 연도가 변경되었습니다: {old['year']}")
            continue
        for field in ('originalSeasonRecords', 'originalAwardRecords', 'normalCards'):
            if old[field] != new[field]:
                raise ValueError(f'원본 기록 또는 카드 식별자가 바뀌었습니다: {field}')
        current = {s['playerSeasonId']: s for s in new['playerSeasons']}
        if set(current) != {s['playerSeasonId'] for s in old['playerSeasons']}:
            raise ValueError('선수 구성이 바뀌었습니다.')
        for season in old['playerSeasons']:
            fresh = current[season['playerSeasonId']]
            if season == fresh:
                continue
            if season['originFranchiseId'] != franchise:
                raise ValueError(f"대상 밖 구단 선수가 바뀌었습니다: {season['playerSeasonId']}")
            fields = [k for k in sorted(set(season) | set(fresh)) if season.get(k) != fresh.get(k)]
            changed.append(dict(playerSeasonId=season['playerSeasonId'], changedFields=fields,
                beforeCost=season['cost'], afterCost=fresh['cost'],
                beforeAttributes=season['baseAttributes'], afterAttributes=fresh['baseAttributes'],
                beforeRole=season['rosterRole'], afterRole=fresh['rosterRole']))
        teams = {t['teamSeasonKey']: t for t in new['teamSeasons']}
        for team in old['teamSeasons']:
            if team['franchiseId'] != franchise and team != teams[team['teamSeasonKey']]:
                raise ValueError('대상 밖 구단 Core25가 바뀌었습니다.')
    return changed


def summarize(simulation):
    """정규 시즌 성적과 전체 리그 지표를 합산한다."""
    teams = [t for r in simulation['rows'] for t in r['teams']]
    stats = [s for r in simulation['rows'] for s in r['statistics']
        if not s['IsFirstHalf'] and not s['IsPostseason'] and not s['IsAllStarGame']]
    total = lambda key: sum(t[key] for t in teams)
    games = total('Games') / 2
    standings = []
    for key in sorted({t['TeamSeasonKey'] for t in teams}):
        rows = [t for t in teams if t['TeamSeasonKey'] == key]
        amount = lambda field: sum(t[field] for t in rows)
        standings.append(dict(teamSeasonKey=key, wins=amount('Wins'), losses=amount('Losses'),
            winningPercentage=amount('Wins')/(amount('Wins')+amount('Losses')),
            battingAverage=amount('Hits')/amount('AtBats'),
            earnedRunAverage=27*amount('EarnedRuns')/amount('PitchingOuts'),
            runsPerGame=amount('RunsScored')/amount('Games')))
    return dict(games=simulation['games'], regularSeasonGames=int(games),
        determinismChecks=simulation['determinismChecks'],
        battingAverage=total('Hits')/total('AtBats'),
        earnedRunAverage=27*total('EarnedRuns')/total('PitchingOuts'),
        runsPerTeamGame=total('RunsScored')/(games*2),
        homeRunsPerGame=sum(s['HomeRuns'] for s in stats)/games,
        walkStrikeoutRatio=sum(s['Walks'] for s in stats)/sum(s['Strikeouts'] for s in stats),
        standings=sorted(standings, key=lambda t: (-t['winningPercentage'], t['teamSeasonKey'])))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage', type=Path, required=True)
    parser.add_argument('--year', type=int, required=True)
    parser.add_argument('--source-franchise', required=True)
    parser.add_argument('--before-simulation', type=Path)
    parser.add_argument('--after-simulation', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--backup', type=Path)
    parser.add_argument('--publish', action='store_true')
    args = parser.parse_args()
    production = ROOT / 'Assets/10.Datas/HistoricalSimulation/1982-2025'
    editor = ROOT / 'Assets/Editor Default Resources/HistoricalSimulation/1982-2025'
    before = bake.load_and_validate_editor_asset_archive(production)
    after = bake.load_and_validate_editor_asset_archive(args.stage / 'Runtime')
    franchise = runtime_franchise_id(args.source_franchise)
    changed = verify_scope(before, after, args.year, franchise)
    report = dict(published=False, beforeContentHash=before['manifest']['contentHash'],
        afterContentHash=after['manifest']['contentHash'], changedPlayers=changed,
        changedFieldCounts=dict(Counter(k for row in changed for k in row['changedFields'])),
        otherYearsUnchanged=True, otherTeamsUnchanged=True, originalRecordsPreserved=True)
    for label, path, content in (('beforeSimulation', args.before_simulation, before),
                                 ('afterSimulation', args.after_simulation, after)):
        if path:
            simulation = read(path)
            if simulation['contentHash'] != content['manifest']['contentHash']:
                raise ValueError('시뮬레이션과 Archive 입력 해시가 다릅니다.')
            if simulation['games'] < 10000 or simulation['determinismChecks'] < 1:
                raise ValueError('1만 경기·결정론 검증이 부족합니다.')
            report[label] = summarize(simulation)
    if args.publish:
        if not args.backup or args.backup.exists() or not args.before_simulation or not args.after_simulation:
            raise ValueError('게시에는 새 백업 경로와 변경 전후 대량 경기 검증이 필요합니다.')
        # 관련 없는 에디터 자산과 .meta를 보존하고 생성 Archive 파일만 교체한다.
        for label, source, target in (('Runtime', args.stage/'Runtime', production),
                                      ('EditorRuntime', args.stage/'Runtime', editor/'Runtime'),
                                      ('EditorSource', args.stage, editor)):
            paths = ['manifest.json', 'player_persons.json'] + [f'Years/{y["year"]}.json' for y in after['years']]
            for relative in paths:
                destination = target / relative
                payload = (source / relative).read_bytes()
                if destination.is_file() and destination.read_bytes() == payload:
                    continue
                if destination.is_file():
                    snapshot = args.backup / label / relative
                    snapshot.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(destination, snapshot)
                bake.write_bytes_atomically(destination, payload)
            if (source/'validation_report.json').is_file():
                destination = target/'validation_report.json'
                if destination.exists():
                    (args.backup/label).mkdir(parents=True, exist_ok=True)
                    shutil.copy2(destination, args.backup/label/'validation_report.json')
                bake.write_bytes_atomically(destination, (source/'validation_report.json').read_bytes())
        loaded = bake.load_and_validate_editor_asset_archive(production)
        if loaded != after:
            raise ValueError('게시 후 로드가 임시 Archive와 다릅니다.')
        report['published'] = True
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k not in ('changedPlayers', 'beforeSimulation', 'afterSimulation')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
