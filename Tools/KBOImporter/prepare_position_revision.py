"""포지션 재저작 후보에서 기존 카드 가치 평가를 유지하는 범위 한정 Archive를 만든다."""
from __future__ import annotations

import argparse
import copy
from pathlib import Path

import synthetic_bake as bake


def retain_valuations(previous: dict, candidate: dict, *, editor: bool) -> int:
    """선수·능력치는 같아야 하며 가격 평가만 기존 발급 시점의 값으로 유지한다."""
    old_years = {row['year']: row for row in previous['years']}
    if set(old_years) != {row['year'] for row in candidate['years']}:
        raise ValueError('포지션 재저작에서 연도 구성이 달라졌습니다.')
    restored = 0
    for year in candidate['years']:
        before = {row['playerSeasonId']: row for row in old_years[year['year']]['playerSeasons']}
        if set(before) != {row['playerSeasonId'] for row in year['playerSeasons']}:
            raise ValueError('포지션 재저작에서 선수 구성이 달라졌습니다.')
        for row in year['playerSeasons']:
            old = before[row['playerSeasonId']]
            for field in ('baseAttributes', 'trainingCeiling', 'playerPersonId', 'originTeamSeasonKey', 'playerType'):
                if row.get(field) != old.get(field):
                    raise ValueError(f'포지션 범위 밖 변경: {row["playerSeasonId"]}:{field}')
            restored += row['cost'] != old['cost']
            row['cost'] = old['cost']
            if editor:
                # 특수 판본의 Peak 선정도 발급 시점의 가격 평가 가중치를 소비한다.
                # 포지션 자료 보강을 능력치·가격 재평가로 해석하지 않는다.
                for field in ('costDerivationTrace', 'costMetricEvidence'):
                    if field in old:
                        row[field] = copy.deepcopy(old[field])
                    else:
                        row.pop(field, None)
    bake.refresh_content_hash(candidate)
    return restored


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before-editor', type=Path, required=True)
    parser.add_argument('--before-runtime', type=Path, required=True)
    parser.add_argument('--candidate', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('기존 산출물을 덮어쓰지 않습니다. 새 출력 경로를 지정하세요.')
    for suffix, baseline, is_editor in (('', args.before_editor, True), ('Runtime', args.before_runtime, False)):
        old = bake.load_and_validate_editor_asset_archive(baseline)
        new = bake.load_and_validate_editor_asset_archive(args.candidate / suffix)
        restored = retain_valuations(old, new, editor=is_editor)
        bake.write_editor_asset_archive(new, args.output / suffix)
        if bake.load_and_validate_editor_asset_archive(args.output / suffix) != new:
            raise ValueError('포지션 재저작 Archive 재로드가 일치하지 않습니다.')
        print(f'{suffix or "Editor"}: 기존 가격 유지 {restored}건', flush=True)


if __name__ == '__main__':
    main()
