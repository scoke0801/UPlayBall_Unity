"""EX 성적 1위와 최종 Cost의 불일치를 카드 수정 없이 추적한다."""
import argparse
import collections
import json
from pathlib import Path

from bake_special_cards import ROOT, verify_inputs


def audit(evaluation, root=ROOT):
    verify_inputs(evaluation, root)
    editor = {}
    for entry in evaluation['inputFiles']:
        if not entry['path'].startswith('Assets/Editor Default Resources/') or '/Years/' not in entry['path']:
            continue
        year = json.loads((root / entry['path']).read_text(encoding='utf-8-sig'))
        editor.update({row['playerSeasonId']: row for row in year['playerSeasons']})
    rows = []
    for winner in evaluation['ex']:
        if winner.get('cost') == 10:
            continue
        source = editor[winner['editorPlayerSeasonId']]
        trace = source['costDerivationTrace']
        qualified = [row for row in evaluation['seasons'] if row['qualified'] and row['year'] == winner['year']
                     and row['role'] == winner['role'] and row['cost'] == 10]
        qualified.sort(key=lambda row: (-row['sourcePerformance'], -row['reliability'], -row['sample'], row['playerSeasonId']))
        alternate = qualified[0] if qualified else None
        rows.append(dict(year=winner['year'], role=winner['role'], playerSeasonId=winner['playerSeasonId'],
                         sourceNames=winner['sourceNames'], cost=winner['cost'],
                         sourcePerformance=winner['sourcePerformance'], issuedStrength=winner['issuedStrength'],
                         costMethod=trace['costMethod'], costTrace=trace,
                         referenceOverride=source.get('annualReferenceOverride'),
                         qualifiedCostTenCount=len(qualified),
                         bestCostTenSeasonId=alternate['playerSeasonId'] if alternate else None,
                         bestCostTenPerformance=alternate['sourcePerformance'] if alternate else None,
                         bestCostTenNames=alternate['sourceNames'] if alternate else [],
                         performanceLossIfSubstituted=winner['sourcePerformance'] - alternate['sourcePerformance'] if alternate else None))
    rows.sort(key=lambda row: (row['year'], row['role']))
    return dict(inputHash=evaluation['inputHash'], affectedCount=len(rows),
                costCounts=dict(collections.Counter(row['cost'] for row in rows)),
                costMethods=dict(collections.Counter(row['costMethod'] for row in rows)),
                withoutQualifiedCostTen=sum(row['qualifiedCostTenCount'] == 0 for row in rows),
                canonicalMutated=False, rows=rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evaluation', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = audit(json.loads(args.evaluation.read_text(encoding='utf-8-sig')))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key != 'rows'}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
