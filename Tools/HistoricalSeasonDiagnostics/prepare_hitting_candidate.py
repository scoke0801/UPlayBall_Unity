"""원본 카드 능력치를 보존하는 개인 타격 기록 후보를 별도 Archive에 만든다."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

IMPORTER = Path(__file__).resolve().parents[1] / 'KBOImporter'
sys.path.insert(0, str(IMPORTER))
import synthetic_bake as bake
from hitter_record_calibration import estimate_ratings, resolve_rating


def prepare(runtime, normalized, policy_path, destination):
    """실제 팀 승패·순위는 읽지 않으며 관측 카드 값이 있는 속성은 변경하지 않는다."""
    if destination.exists():
        raise ValueError('기존 후보를 덮어쓰지 않습니다.')
    policy = json.loads(policy_path.read_text(encoding='utf-8-sig'))
    if any(p['index'] not in (0, 1, 5) for p in policy['metrics']):
        raise ValueError('Contact·Power·Mental만 비교하는 후보입니다.')
    content = bake.load_and_validate_editor_asset_archive(runtime)
    before_hash = content['manifest']['contentHash']
    overrides = bake.load_annual_reference_overrides()
    changes = []
    for year in content['years']:
        source = json.loads((normalized / f"{year['year']}.json").read_text(encoding='utf-8-sig'))
        players = {}
        for player in source['players']:
            identity = f"source-backed-identity-v1\0{player['sourcePlayerId']}\0{year['year']}"
            key = 'SEASON_' + hashlib.sha256(identity.encode()).hexdigest()[:20]
            if key in players:
                raise ValueError('정규화 원본 선수 ID 중복')
            players[key] = player
        records = {key: p.get('hitterStats') or {} for key, p in players.items()}
        for profile in policy['metrics']:
            estimates = estimate_ratings(records, profile)
            index = profile['index']
            for season in year['playerSeasons']:
                key = season['playerSeasonId']
                if season['playerType'] != 'Hitter' or key not in estimates:
                    continue
                editor_id = 'SEASON_' + bake.stable_digest('editor-source-season-v1',
                    players[key]['sourcePlayerId'], year['year'])
                observed = overrides.get(editor_id, {}).get('values', {})
                if bake.ABILITY_NAMES[index] in observed:
                    if season['baseAttributes'][index] != observed[bake.ABILITY_NAMES[index]]:
                        raise ValueError('입력 Runtime의 관측 카드 능력치가 정본과 다릅니다.')
                    continue
                old = season['baseAttributes'][index]
                value = bake.clamp_rating(resolve_rating(estimates[key], old, profile))
                gap = season['trainingCeiling'][index] - old
                season['baseAttributes'][index] = value
                season['trainingCeiling'][index] = min(100, value + gap)
                if old != value:
                    changes.append(dict(id=key, index=index, before=old, after=value))
    bake.refresh_content_hash(content)
    bake.write_editor_asset_archive(content, destination)
    if bake.load_and_validate_editor_asset_archive(destination) != content:
        raise ValueError('후보 Archive 재읽기 불일치')
    report = dict(beforeHash=before_hash, afterHash=content['manifest']['contentHash'],
                  policy=policy, observedAttributesPreserved=True, changes=changes)
    (destination/'experiment.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(changes=len(changes), contentHash=report['afterHash'])))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('runtime', type=Path)
    parser.add_argument('normalized', type=Path)
    parser.add_argument('policy', type=Path)
    parser.add_argument('destination', type=Path)
    args = parser.parse_args()
    prepare(args.runtime, args.normalized, args.policy, args.destination)
