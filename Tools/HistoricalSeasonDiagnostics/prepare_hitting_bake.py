"""현재 포지션·발급 가격을 보존하며 타격 산식 후보를 실제 로스터 선정까지 베이크한다."""
import argparse
import copy
import hashlib
import json
import sys
import statistics
from pathlib import Path

IMPORTER = Path(__file__).resolve().parents[1] / 'KBOImporter'
sys.path.insert(0, str(IMPORTER))
import synthetic_bake as bake


def load_versioned_baseline(path, expected_hash):
    """이미 검증한 해시의 기준선만 당시 산식 버전으로 읽고 구조·기록·아카이브 해시는 모두 검증한다."""
    manifest = json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'))['sourceManifest']
    if manifest['contentHash'] != expected_hash:
        raise ValueError('검증된 기준선 해시가 아닙니다.')
    old_ability = bake.ABILITY_FORMULA_VERSION
    old_derivation = bake.DERIVATION_BALANCE_VERSION
    try:
        bake.ABILITY_FORMULA_VERSION = manifest['abilityFormulaVersion']
        bake.DERIVATION_BALANCE_VERSION = manifest['derivationBalanceVersion']
        return bake.load_and_validate_editor_asset_archive(path)
    finally:
        bake.ABILITY_FORMULA_VERSION = old_ability
        bake.DERIVATION_BALANCE_VERSION = old_derivation


def retain_issued_values(previous, candidate, editor):
    """타격 산식 검증이 포지션 개정·가격 재평가로 범위를 넓히지 않도록 검사한다."""
    old_years = {y['year']: y for y in previous['years']}
    if set(old_years) != {y['year'] for y in candidate['years']}:
        raise ValueError('후보의 연도 구성이 기준선과 다릅니다.')
    restored = 0
    for year in candidate['years']:
        before = old_years[year['year']]
        old = {p['playerSeasonId']: p for p in before['playerSeasons']}
        if set(old) != {p['playerSeasonId'] for p in year['playerSeasons']}:
            raise ValueError('후보의 선수 구성이 기준선과 다릅니다.')
        for key in ('originalSeasonRecords', 'originalAwardRecords', 'normalCards'):
            if year[key] != before[key]:
                raise ValueError('원기록·수상·카드 변경: '+key)
        teams = {t['teamSeasonKey']: t for t in before.get('teamSeasons', [])}
        new_teams = {t['teamSeasonKey']: t for t in year.get('teamSeasons', [])}
        if teams.keys() != new_teams.keys():
            raise ValueError('구단 구성 변경')
        for key, team in new_teams.items():
            changed = {field for field in set(team) | set(teams[key])
                       if team.get(field) != teams[key].get(field)}
            if changed - {'core25CardIds', 'referenceStrength', 'rosterSelectionTrace', 'validationWarnings'}:
                raise ValueError('로스터 선정 이외 구단 정의 변경')
            current = {p['playerSeasonId']: p for p in year['playerSeasons']}
            # Editor 원본은 투수가 부족하면 뒤 슬롯에도 타자를 담는다. Runtime은 14타자·11투수 계약이다.
            old_pitchers = [card for card in teams[key]['core25CardIds']
                            if old[card.rsplit(':', 1)[0]]['playerType'] == 'Pitcher'] if editor else teams[key]['core25CardIds'][14:]
            new_pitchers = [card for card in team['core25CardIds']
                            if current[card.rsplit(':', 1)[0]]['playerType'] == 'Pitcher'] if editor else team['core25CardIds'][14:]
            if new_pitchers != old_pitchers:
                raise ValueError('타격 산식 후보가 선발·불펜 구성을 바꿨습니다.')
            if 'referenceStrength' in team:
                # 기용 후보가 바뀌면 표시용 로스터 평균도 바뀐다. 임의의 팀 전력 보너스는 허용하지 않는다.
                core = [current[card.rsplit(':', 1)[0]] for card in team['core25CardIds']]
                expected = round(statistics.mean(statistics.mean(p['baseAttributes'][:6]
                    if p['playerType'] == 'Hitter' else p['baseAttributes'][6:]) for p in core), 4)
                if team['referenceStrength'] != expected:
                    raise ValueError('구단 전력이 실제 로스터 능력치 평균과 다릅니다.')
        for player in year['playerSeasons']:
            original = old[player['playerSeasonId']]
            for field in ('position', 'secondaryPositions', 'isPositionEvidenceMissing',
                          'pitcherRole', 'playerPersonId', 'originTeamSeasonKey', 'playerType'):
                if player.get(field) != original.get(field):
                    raise ValueError('타격 산식 범위 밖 변경: '+field)
            allowed = (0, 1, 5) if player['playerType'] == 'Hitter' else ()
            for field in ('baseAttributes', 'trainingCeiling'):
                if any(value != original[field][i] for i, value in enumerate(player[field]) if i not in allowed):
                    raise ValueError('타격 3속성 외 능력치 변경: '+field)
            restored += original['cost'] != player['cost']
            for field in ('cost', 'costDerivationTrace', 'costMetricEvidence') if editor else ('cost',):
                if field in original:
                    player[field] = copy.deepcopy(original[field])
    bake.refresh_content_hash(candidate)
    return restored


def prepare(args):
    """새 경로에만 기록하고 결과 판정에 사용한 정본·정책 해시를 함께 보존한다."""
    if args.output.exists():
        raise ValueError('기존 후보를 덮어쓰지 않습니다.')
    editor = bake.load_and_validate_editor_asset_archive(args.editor)
    runtime = bake.load_and_validate_editor_asset_archive(args.runtime)
    return prepare_from_validated_baseline(args, editor, runtime)


def prepare_from_validated_baseline(args, editor, runtime):
    """이전 버전으로 검증한 기준선을 받아 새 산식 버전의 아카이브를 생성한다."""
    if args.output.exists():
        raise ValueError('기존 후보를 덮어쓰지 않습니다.')
    baseline_hash = runtime['manifest']['contentHash']
    years = [y['year'] for y in runtime['years']]
    policy_bytes = args.policy.read_bytes()
    policy = json.loads(policy_bytes.decode('utf-8-sig'))
    previous_policy = bake.DERIVATION_BALANCE.get('hitterRecordCalibration')
    try:
        bake.DERIVATION_BALANCE['hitterRecordCalibration'] = policy
        fresh_runtime, _ = bake.bake_with_report(args.normalized, years, 20260901, args.supplement)
        fresh_runtime = bake.create_runtime_safe_content(fresh_runtime)
        fresh_editor = bake.build_editor_original_content(args.normalized, years)
    finally:
        if previous_policy is None:
            bake.DERIVATION_BALANCE.pop('hitterRecordCalibration', None)
        else:
            bake.DERIVATION_BALANCE['hitterRecordCalibration'] = previous_policy
    counts = dict(Editor=retain_issued_values(editor, fresh_editor, True),
                  Runtime=retain_issued_values(runtime, fresh_runtime, False))
    current = json.loads((args.runtime/'manifest.json').read_text(encoding='utf-8-sig'))
    if current['sourceManifest']['contentHash'] != baseline_hash:
        raise ValueError('베이크 중 공식 Runtime 변경: 새 기준선으로 재검증해야 합니다.')
    current_editor = json.loads((args.editor/'manifest.json').read_text(encoding='utf-8-sig'))
    if current_editor['sourceManifest']['contentHash'] != editor['manifest']['contentHash']:
        raise ValueError('베이크 중 공식 Editor 변경: 덮어쓰지 않고 다시 검증해야 합니다.')
    for content, path in ((fresh_editor, args.output), (fresh_runtime, args.output/'Runtime')):
        bake.write_editor_asset_archive(content, path)
        if bake.load_and_validate_editor_asset_archive(path) != content:
            raise ValueError('후보 Archive 재읽기 불일치')
    report = dict(baselineHash=baseline_hash, baselineEditorHash=editor['manifest']['contentHash'],
                  contentHash=fresh_runtime['manifest']['contentHash'],
                  policy=policy, policySha256=hashlib.sha256(policy_bytes).hexdigest(), restoredCosts=counts)
    (args.output/'experiment.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--editor', type=Path, required=True)
    parser.add_argument('--runtime', type=Path, required=True)
    parser.add_argument('--normalized', type=Path, required=True)
    parser.add_argument('--policy', type=Path, required=True)
    parser.add_argument('--supplement', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    prepare(parser.parse_args())
