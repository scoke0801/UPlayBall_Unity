"""통계 채택·타격 변경 범위·아카이브 무결성을 확인한 후보를 백업 후 로컬 정본에 게시한다."""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path

from audit_candidate import audit
from prepare_hitting_bake import bake, retain_issued_values, load_versioned_baseline

ROOT = Path(__file__).resolve().parents[2]


def validate_scope(before, after, editor, before_hash):
    """발급값을 고치지 않아도 범위 검사가 통과하고 아카이브가 그대로인지 확인한다."""
    previous = load_versioned_baseline(before, before_hash)
    candidate = bake.load_and_validate_editor_asset_archive(after)
    expected = candidate['manifest']['contentHash']
    if previous['playerPersons'] != candidate['playerPersons']:
        raise ValueError('타격 산식 범위 밖 선수 신원 변경')
    if retain_issued_values(previous, candidate, editor) or candidate['manifest']['contentHash'] != expected:
        raise ValueError('발급 가치가 보존되지 않은 후보입니다.')
    return candidate


def publish(stage, selection_path, backup):
    """검증 실패·동시 게시·백업 충돌 시 정본 쓰기를 시작하지 않는다."""
    production = ROOT/'Assets/10.Datas/HistoricalSimulation/1982-2025'
    editor = ROOT/'Assets/Editor Default Resources/HistoricalSimulation/1982-2025'
    source_before = (editor/'manifest.json').read_bytes()
    runtime_before = (production/'manifest.json').read_bytes()
    selection = json.loads(selection_path.read_text(encoding='utf-8-sig'))
    result = audit(production, stage/'Runtime')
    if not selection['statisticalSelectionPassed']:
        raise ValueError('통계 채택 조건을 통과하지 않은 후보입니다.')
    if (selection['baselineHash'], selection['contentHash']) != (result['beforeHash'], result['afterHash']):
        raise ValueError('통계 판정의 기준선·후보가 현재 아카이브와 다릅니다.')
    experiment = json.loads((stage/'experiment.json').read_text(encoding='utf-8-sig'))
    if experiment.get('baselineEditorHash') != json.loads(source_before)['sourceManifest']['contentHash']:
        raise ValueError('후보를 준비한 뒤 Editor 정본이 달라졌거나 기준 해시가 없습니다.')
    policy = bake.DERIVATION_BALANCE.get('hitterRecordCalibration') or {}
    if any(policy.get(key) != experiment['policy'].get(key) for key in ('version', 'metrics')):
        raise ValueError('검증한 타격 산식과 공식 설정이 다릅니다.')
    for marker, filename in (('mini-game', 'MiniGameBalance.json'), ('rating-curve', 'MatchRatingCurve.json')):
        expected = re.search(r':'+marker+r'-([0-9a-f]{64})(?=:|$)', selection['candidateBalanceHash'])
        actual = hashlib.sha256((ROOT/'Assets/10.Datas/Resources/NewGame'/filename).read_bytes()).hexdigest()
        if expected is None or expected.group(1) != actual:
            raise ValueError('검증한 경기 계수와 공식 설정이 다릅니다: '+filename)
    if set(result['changedFields']) - {'baseAttributes', 'trainingCeiling', 'rosterRole'}:
        raise ValueError('타격 능력·로스터 선정 범위 밖 변경')
    runtime = validate_scope(production, stage/'Runtime', False, selection['baselineHash'])
    source = validate_scope(editor, stage, True, experiment['baselineEditorHash'])
    special_bytes = (stage/'Runtime/BakedSpecialCards.json').read_bytes()
    special = json.loads(special_bytes)
    if special['baseContentHash'] != result['afterHash']:
        raise ValueError('특수 카드의 기준 콘텐츠가 다릅니다.')
    special_hash = special.pop('contentHash')
    if special_hash != hashlib.sha256(json.dumps(special, sort_keys=True, separators=(',', ':')).encode()).hexdigest():
        raise ValueError('특수 카드 해시가 다릅니다.')
    catalog = production.parent/'HistoricalRuntimeContentCatalog.asset'
    catalog_before = catalog.read_bytes()
    catalog_after, count = re.subn(r'(_specialCardsSha256: )[A-Fa-f0-9]+',
        lambda match: match.group(1)+hashlib.sha256(special_bytes).hexdigest().upper(),
        catalog_before.decode('utf-8'))
    if count != 1 or backup.exists():
        raise ValueError('카탈로그 필드 또는 새 백업 경로를 확인하세요.')
    backup.mkdir(parents=True)
    shutil.copytree(production, backup/'Runtime')
    shutil.copytree(editor, backup/'Editor')
    (backup/'HistoricalRuntimeContentCatalog.asset').write_bytes(catalog_before)
    if ((editor/'manifest.json').read_bytes() != source_before or
            (production/'manifest.json').read_bytes() != runtime_before or catalog.read_bytes() != catalog_before):
        raise ValueError('게시 직전 다른 작업의 아카이브 변경을 감지했습니다.')
    for content, target in ((source, editor), (runtime, editor/'Runtime'), (runtime, production)):
        bake.write_editor_asset_archive(content, target)
        if bake.load_and_validate_editor_asset_archive(target) != content:
            raise ValueError('게시 후 아카이브 재읽기 불일치: '+str(target))
    for target in (editor/'Runtime', production):
        bake.write_bytes_atomically(target/'BakedSpecialCards.json', special_bytes)
    bake.write_bytes_atomically(catalog, catalog_after.encode('utf-8'))
    result.update(published=True, selection=selection, backup=str(backup))
    (backup/'publication.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'changes'}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', type=Path)
    parser.add_argument('selection', type=Path)
    parser.add_argument('backup', type=Path)
    args = parser.parse_args()
    publish(args.stage.resolve(), args.selection.resolve(), args.backup.resolve())
