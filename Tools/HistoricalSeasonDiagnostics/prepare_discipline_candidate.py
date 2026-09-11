"""팀 성적을 읽지 않고 선수 BB/PA만 바꾼 독립 Mental 후보 Archive를 만든다."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'KBOImporter'))
import synthetic_bake as bake


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def season_id(source_id, year):
    digest = hashlib.sha256(f'source-backed-identity-v1\0{source_id}\0{year}'.encode()).hexdigest()
    return 'SEASON_' + digest[:20]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('runtime', type=Path)
    parser.add_argument('normalized', type=Path)
    parser.add_argument('policy', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('기존 후보를 덮어쓰지 않습니다.')
    policy = read(args.policy)
    manifest = read(args.runtime / 'manifest.json')
    persons = read(args.runtime / 'player_persons.json')
    content = dict(schemaVersion=manifest['contentSchemaVersion'], manifest=manifest['sourceManifest'],
        playerPersons=persons['items'], worldIdentityNamePool=persons['worldIdentityNamePool'],
        years=[read(args.runtime / entry['path']) for entry in manifest['years']])
    before_hash = content['manifest']['contentHash']
    changes = []
    for year in content['years']:
        players = read(args.normalized / f"{year['year']}.json")['players']
        evidence = {season_id(p['sourcePlayerId'], year['year']): p['hitterStats'] for p in players
                    if (p.get('hitterStats') or {}).get('walks') is not None
                    and (p['hitterStats'].get('plateAppearances') or 0) > 0}
        league_rate = sum(p['walks'] for p in evidence.values()) / sum(p['plateAppearances'] for p in evidence.values())
        for player in year['playerSeasons']:
            if player['playerType'] != 'Hitter' or player['playerSeasonId'] not in evidence:
                continue
            stats = evidence[player['playerSeasonId']]
            pa = stats['plateAppearances']
            # 작은 표본은 리그 평균으로 수축하여 몇 타석의 볼넷으로 능력치가 폭등하지 않게 한다.
            adjusted_rate = (stats['walks'] + league_rate * policy['priorPlateAppearances']) / (pa + policy['priorPlateAppearances'])
            old = player['baseAttributes'][5]
            new = bake.clamp_rating(old + policy['mentalPerWalkRate'] * (adjusted_rate - league_rate))
            if new == old:
                continue
            gap = player['trainingCeiling'][5] - old
            player['baseAttributes'][5] = new
            player['trainingCeiling'][5] = min(100, new + gap)
            changes.append(dict(playerSeasonId=player['playerSeasonId'], year=year['year'],
                before=old, after=new, plateAppearances=pa, walks=stats['walks'], leagueRate=league_rate))
    bake.refresh_content_hash(content)
    bake.write_editor_asset_archive(content, args.output)
    report = dict(experimentalOnly=True, beforeContentHash=before_hash,
        afterContentHash=content['manifest']['contentHash'], policy=policy,
        policySha256=hashlib.sha256(args.policy.read_bytes()).hexdigest(), changes=changes)
    (args.output / 'discipline-candidate.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(changedPlayers=len(changes), contentHash=content['manifest']['contentHash'])))


if __name__ == '__main__':
    main()
