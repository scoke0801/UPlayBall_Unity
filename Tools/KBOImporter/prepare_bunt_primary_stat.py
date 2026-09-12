"""기존 정본을 보존한 채 44년 번트 독립화 후보를 새 경로에 생성한다."""
import argparse
import json
from collections import Counter
from pathlib import Path

import synthetic_bake as bake
import bunt_primary_stat
from source_backed_runtime_bake import editor_source_season_id, runtime_player_season_id

ROOT = Path(__file__).resolve().parents[2]


def prepare(output):
    """가격·원기록·포지션·신원을 유지하고 번트 슬롯과 출처만 재발급한다."""
    if output.exists():
        raise ValueError('후보는 기존 파일을 덮어쓰지 않는 새 경로여야 합니다.')
    editor = bake.load_editor_asset_archive(ROOT / 'Assets/Editor Default Resources/HistoricalSimulation/1982-2025')
    runtime = bake.load_editor_asset_archive(ROOT / 'Assets/10.Datas/HistoricalSimulation/1982-2025')
    references = bake.load_annual_reference_overrides()
    observations = bunt_primary_stat.load_observations()
    runtime_rows = {s['playerSeasonId']: s for y in runtime['years'] for s in y['playerSeasons']}
    counts = Counter()
    for year in editor['years']:
        source = json.loads((ROOT / 'Tools/KBOImporter/.cache/KBOImport/Normalized' / f'{year["year"]}.json').read_text(encoding='utf-8-sig'))
        identities = {editor_source_season_id(p['sourcePlayerId'], year['year']):
            runtime_player_season_id(p['sourcePlayerId'], year['year']) for p in source['players']}
        for season in year['playerSeasons']:
            if season['playerType'] == 'Hitter':
                # 가격 재평가를 함께 실행하지 않으므로 기존 가격의 송구 입력은 Editor 근거에만 보존한다.
                season['costDerivationTrace'].setdefault('legacyThrowingRating', season['baseAttributes'][3])
        bunt_primary_stat.apply(year['playerSeasons'], references, observations)
        for season in year['playerSeasons']:
            target = runtime_rows[identities[season['playerSeasonId']]]
            if season['cost'] != target['cost']:
                raise ValueError('Editor/Runtime 가격이 다릅니다.')
            if season['playerType'] == 'Hitter':
                if len(target.get('trainingCeiling', [])) >= 4:
                    headroom = max(0, target['trainingCeiling'][3] - target['baseAttributes'][3])
                    target['trainingCeiling'][3] = min(100, season['baseAttributes'][3] + headroom)
                target['baseAttributes'][3] = season['baseAttributes'][3]
                counts[season['buntDerivation']['status']] += 1
    for content, destination in ((editor, output), (runtime, output / 'Runtime')):
        content['schemaVersion'] = bake.CONTENT_SCHEMA_VERSION
        content['manifest']['abilityFormulaVersion'] = bake.ABILITY_FORMULA_VERSION
        content['manifest']['derivationBalanceVersion'] = bake.DERIVATION_BALANCE_VERSION
        bake.refresh_content_hash(content)
        bake.write_editor_asset_archive(content, destination)
        if bake.load_editor_asset_archive(destination) != content:
            raise ValueError('후보 재로드 결과가 다릅니다.')
    report = dict(years=len(editor['years']), counts=dict(counts),
        contentHash=runtime['manifest']['contentHash'], policy=bunt_primary_stat.POLICY['version'])
    (output / 'bunt-audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    prepare(parser.parse_args().output)
