"""검증된 포지션 복원만 백업 후 게시하고 동시 콘텐츠 변경은 거부한다."""
import argparse
import json
import shutil
import sys
from pathlib import Path

import synthetic_bake as bake

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'Tools/HistoricalSeasonDiagnostics'))
from audit_candidate import audit


def publish(stage, expected_hash, backup):
    production = ROOT/'Assets/10.Datas/HistoricalSimulation/1982-2025'
    editor = ROOT/'Assets/Editor Default Resources/HistoricalSimulation/1982-2025'
    result = audit(production, stage/'Runtime')
    if result['beforeHash'] != expected_hash:
        raise ValueError('진행 중 Runtime 변경 감지: 새 기준선 검증이 필요합니다.')
    permitted = {'secondaryPositions', 'position', 'isPositionEvidenceMissing', 'rosterRole'}
    if set(result['changedFields']) - permitted:
        raise ValueError('포지션 복원 범위 밖의 선수 변경이 있습니다.')
    runtime = bake.load_and_validate_editor_asset_archive(stage/'Runtime')
    source = bake.load_and_validate_editor_asset_archive(stage)
    if backup.exists():
        raise ValueError('기존 백업 경로를 덮어쓰지 않습니다.')
    backup.mkdir(parents=True)
    shutil.copytree(production, backup/'Runtime')
    shutil.copytree(editor, backup/'Editor')
    # 검증 이후 다른 도구가 게시했다면 덮어쓰지 않는다.
    current = json.loads((production/'manifest.json').read_text(encoding='utf-8-sig'))
    if current['sourceManifest']['contentHash'] != expected_hash:
        raise ValueError('게시 직전 Runtime 변경 감지')
    for content, target in ((source, editor), (runtime, editor/'Runtime'), (runtime, production)):
        bake.write_editor_asset_archive(content, target)
        if bake.load_and_validate_editor_asset_archive(target) != content:
            raise ValueError(f'게시 후 검증 실패: {target}')
    result['published'] = True
    result['backup'] = str(backup)
    (backup/'publication.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k != 'changes'}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', type=Path)
    parser.add_argument('expected_hash')
    parser.add_argument('backup', type=Path)
    args = parser.parse_args()
    publish(args.stage.resolve(), args.expected_hash, args.backup.resolve())
