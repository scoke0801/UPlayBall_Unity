"""구속 보완의 범위·대량 경기·입력 불변성을 확인한 후 로컬 콘텐츠를 교체한다."""
import argparse
import hashlib
import json
import shutil
from collections import Counter
from pathlib import Path

import synthetic_bake as bake
from verify_reference_update import summarize

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    """보존된 JSON 검증 자료를 읽는다."""
    return json.loads(path.read_text(encoding='utf-8-sig'))


def verify_scope(before, after):
    """신원·Cost·원기록과 구속 이외 능력치를 보존했는지 전체 대조한다."""
    if before['playerPersons'] != after['playerPersons'] or before['worldIdentityNamePool'] != after['worldIdentityNamePool']:
        raise ValueError('구속 변경 범위 밖의 선수 신원 또는 이름풀이 바뀌었습니다.')
    changes = Counter()
    for old, new in zip(before['years'], after['years'], strict=True):
        if old['year'] != new['year']:
            raise ValueError('구속 보완의 연도 범위가 다릅니다.')
        for field in ('normalCards', 'originalSeasonRecords', 'originalAwardRecords'):
            if old[field] != new[field]:
                raise ValueError('원본 카드 ID 또는 원기록이 변경되었습니다: ' + field)
        fresh = {s['playerSeasonId']:s for s in new['playerSeasons']}
        if set(fresh) != {s['playerSeasonId'] for s in old['playerSeasons']}:
            raise ValueError('기존 선수 시즌의 구성 또는 ID가 바뀌었습니다.')
        for s in old['playerSeasons']:
            r = fresh[s['playerSeasonId']]
            if s['cost'] != r['cost']:
                raise ValueError('구속 보완이 기존 Cost를 바꿨습니다.')
            changed = [i for i,(a,b) in enumerate(zip(s['baseAttributes'],r['baseAttributes'],strict=True)) if a!=b]
            if changed and (changed != [7] or s['playerType'] != 'Pitcher' or s.get('sourceDataKind')=='ResearchCardSupplement'):
                raise ValueError('구속 외 능력치 또는 확보한 Research 카드가 바뀌었습니다.')
            if changed:
                changes[s['dataProvenance']] += 1
    return dict(changes)


def main():
    """검증 전에는 게시하지 않으며 백업·해시 재검사를 함께 수행한다."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--publish', action='store_true')
    args = parser.parse_args()
    work = args.work.resolve()
    if not work.is_relative_to(ROOT):
        raise ValueError('검증 폴더는 저장소 안에 있어야 합니다.')
    candidate = bake.load_and_validate_editor_asset_archive(work/'Candidate/Runtime')
    bake.load_and_validate_editor_asset_archive(work/'Candidate')
    # 이전 평가 버전을 새 버전 검증기로 재계산하지 않는다. 파일 해시와 C# 로더를 통과한 기준본이다.
    manifest = read(work/'Before/manifest.json')
    persons = read(work/'Before/player_persons.json')
    before = dict(playerPersons=persons['items'], worldIdentityNamePool=persons['worldIdentityNamePool'],
                  years=[read(work/'Before'/entry['path']) for entry in manifest['years']])
    changes = verify_scope(before, candidate)
    for year in candidate['years']:
        source = read(work/f'Candidate/Years/{year["year"]}.json')
        for season in source['playerSeasons']:
            values = season.get('annualReferenceOverride', {}).get('values', {})
            if 'Velocity' in values and season['baseAttributes'][7] != values['Velocity']:
                raise ValueError('확보한 일반 카드 구속이 변경되었습니다.')
    simulations = {}
    for label, expected in (('before', manifest['sourceManifest']['contentHash']),
                            ('after', candidate['manifest']['contentHash'])):
        simulation = read(work/f'{label}-simulation.json')
        if simulation['contentHash'] != expected or simulation['games'] < 10000 or simulation['determinismChecks'] < 5:
            raise ValueError('전후 콘텐츠와 일치하는 1만 경기·5개 결정론 검증이 필요합니다.')
        simulations[label] = summarize(simulation)
    for field in ('battingAverage','earnedRunAverage','runsPerTeamGame','homeRunsPerGame','walkStrikeoutRatio'):
        if abs(simulations['after'][field]/simulations['before'][field]-1) > 0.05:
            raise ValueError('리그 평균 변화가 검수 범위 5%를 넘었습니다: ' + field)
    paths = [Path('manifest.json'), Path('player_persons.json')] + [Path(e['path']) for e in manifest['years']]
    production = ROOT/'Assets/10.Datas/HistoricalSimulation/1982-2025'
    editor = ROOT/'Assets/Editor Default Resources/HistoricalSimulation/1982-2025'
    for destination in (production, editor/'Runtime'):
        for path in paths:
            if (destination/path).read_bytes() != (work/'Before'/path).read_bytes():
                raise ValueError('검증 중 기존 콘텐츠가 변경되어 덮어쓰지 않습니다: ' + str(destination/path))
    report = dict(published=False, beforeContentHash=manifest['sourceManifest']['contentHash'],
                  afterContentHash=candidate['manifest']['contentHash'], changedPitchers=changes,
                  simulations=simulations, costPreserved=True, observedVelocityPreserved=True)
    if args.publish:
        backup = work/'PublicationBackup'
        if backup.exists():
            raise ValueError('기존 게시 백업을 덮어쓰지 않습니다.')
        operations = []
        for name, source, destination in (('EditorSource', work/'Candidate', editor),
                                           ('EditorRuntime', work/'Candidate/Runtime', editor/'Runtime'),
                                           ('Runtime', work/'Candidate/Runtime', production)):
            for path in paths:
                target = (destination/path).resolve()
                if not target.is_relative_to(ROOT):
                    raise ValueError('저장소 밖의 콘텐츠 경로입니다.')
                saved = backup/name/path
                saved.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(target, saved)
                operations.append((source/path, target, hashlib.sha256(target.read_bytes()).hexdigest()))
        for source, target, expected in operations:
            if hashlib.sha256(target.read_bytes()).hexdigest() != expected:
                raise ValueError('백업 이후 대상 파일이 변경되었습니다: ' + str(target))
        for source, target, _ in operations:
            bake.write_bytes_atomically(target, source.read_bytes())
        for destination in (production, editor/'Runtime', editor):
            bake.load_and_validate_editor_asset_archive(destination)
        # 검증 보고서도 해당 콘텐츠 해시의 새 산출물로 연결한다.
        validation = work/'Candidate/Runtime/validation_report.json'
        if validation.exists():
            for destination in (production, editor/'Runtime'):
                old = destination/'validation_report.json'
                if old.exists():
                    shutil.copy2(old, backup/(('Runtime' if destination==production else 'EditorRuntime')+'-validation_report.json'))
                bake.write_bytes_atomically(old, validation.read_bytes())
        report['published'] = True
    (work/'publication.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='simulations'}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
