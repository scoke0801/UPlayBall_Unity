"""다른 게임 출처로 덮어쓴 가격을 기존 기록 모델과 공통 가격 규약으로 복원한다."""
import argparse
import copy
import json
from pathlib import Path

import synthetic_bake as bake
from reference_source_policy import card_rejection_reason, POLICY
from source_backed_runtime_bake import editor_source_season_id, runtime_player_season_id

ROOT = Path(__file__).resolve().parents[2]


def repair_seasons(seasons):
    """잘못된 출처를 이력으로 옮기고 해당 시즌에만 현재 공통 가격 하한을 적용한다."""
    affected = []
    for season in seasons:
        reference = season.get('annualReferenceOverride')
        reason = card_rejection_reason(reference or {})
        if not reason:
            continue
        if set(reference['values']) != {'Cost'}:
            raise ValueError('가격 외 관측도 오염된 시즌은 별도 복원이 필요합니다.')
        trace = season['costDerivationTrace']
        formula_cost = reference['formulaCost']
        if not isinstance(formula_cost, int) or not 1 <= formula_cost <= 10:
            raise ValueError('보존된 모델 가격이 없습니다.')
        trace['rejectedReference'] = dict(policyVersion=POLICY['version'], reason=reason,
            previousCost=season['cost'], reference=copy.deepcopy(reference))
        del season['annualReferenceOverride']
        trace.pop('eliteCostAdjustment', None)
        season['cost'] = trace['cost'] = formula_cost
        calibration = trace.get('referenceCalibration')
        trace['costMethod'] = (bake.RECORD_TREE_MODEL_TYPE if calibration and
            calibration.get('method') == bake.RECORD_TREE_MODEL_TYPE else
            'ReferenceRecordRidgeWithEliteGate' if calibration else 'SeasonValueOrdinalWithEliteGate')
        trace.setdefault('costEligibility', {})['affectsCost'] = True
        affected.append(season)
    # 연도 선두 하한의 후보군은 전체 시즌이다. 오염된 열 장만으로 선두를 다시 뽑지 않는다.
    floors = bake.elite_cost.build_cost_floors(seasons)
    for season in affected:
        trace = season['costDerivationTrace']
        floor, reason = floors.get(season['playerSeasonId'], (1, 'Unchanged'))
        if floor > season['cost']:
            trace['eliteCostAdjustment'] = dict(version=bake.elite_cost.POLICY['version'],
                previousCost=season['cost'], previousMethod=trace['costMethod'], floor=floor, reason=reason)
            season['cost'] = trace['cost'] = floor
            trace['costMethod'] = 'EliteSeasonFloor'
    return affected


def prepare(output):
    """원기록·능력치·로스터를 보존하는 별도 후보와 가격 복원 감사를 만든다."""
    if output.exists():
        raise ValueError('새 후보 경로가 필요합니다.')
    editor = bake.load_editor_asset_archive(ROOT/'Assets/Editor Default Resources/HistoricalSimulation/1982-2025')
    runtime = bake.load_editor_asset_archive(ROOT/'Assets/10.Datas/HistoricalSimulation/1982-2025')
    runtime_rows = {s['playerSeasonId']: s for y in runtime['years'] for s in y['playerSeasons']}
    report = []
    for year in editor['years']:
        affected = repair_seasons(year['playerSeasons'])
        if not affected:
            continue
        source = json.loads((ROOT/'Tools/KBOImporter/.cache/KBOImport/Normalized'/f'{year["year"]}.json').read_text(encoding='utf-8-sig'))
        identities = {editor_source_season_id(p['sourcePlayerId'],year['year']):
            runtime_player_season_id(p['sourcePlayerId'],year['year']) for p in source['players']}
        for season in affected:
            trace = season['costDerivationTrace']
            target = runtime_rows[identities[season['playerSeasonId']]]
            previous = trace['rejectedReference']['previousCost']
            if target['cost'] != previous:
                raise ValueError('Editor/Runtime 기존 가격이 다릅니다.')
            target['cost'] = season['cost']
            report.append(dict(year=year['year'],names=season.get('sourceReferenceNames'),
                before=previous,modelCost=trace['rejectedReference']['reference']['formulaCost'],
                after=season['cost'],method=trace['costMethod'],playerSeasonId=target['playerSeasonId']))
    for content, destination in ((editor,output),(runtime,output/'Runtime')):
        content['manifest']['costFormulaVersion'] = bake.COST_FORMULA_VERSION
        content['manifest']['derivationBalanceVersion'] = bake.DERIVATION_BALANCE_VERSION
        bake.refresh_content_hash(content)
        bake.write_editor_asset_archive(content,destination)
        if bake.load_and_validate_editor_asset_archive(destination) != content:
            raise ValueError('후보의 재로드 검증에 실패했습니다.')
    result = dict(quarantinedCards=len(report),changes=report,contentHash=runtime['manifest']['contentHash'])
    (output/'rejected-source-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    prepare(parser.parse_args().output)
