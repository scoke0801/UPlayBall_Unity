"""원문에서 판독한 주전 명단과 위치를 구분해 Archive 배치를 비교한다."""
from __future__ import annotations
import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path
from report_pm_calibration import load_seasons
from study_pm_calibration import read


def accepted_roles(value):
    """기사의 포괄 OF와 구체 수비 위치를 혼동하지 않는다."""
    roles = set()
    for part in value.split('|'):
        roles.update(('LF','CF','RF') if part == 'OF' else (part,))
    return {'StartingHitter:'+p for p in roles}


def compare_lineups(before, after, reference):
    """이름·연도·팀이 유일하게 연결되는 관측만 평가한다."""
    identity = defaultdict(set)
    for sid, season in before.items():
        for name in season['sourceReferenceNames']:
            identity[(season['originYear'],season['originFranchiseId'],name)].add(sid)
    rows = []
    for team in reference['teams']:
        expected = [(n,'Hitter',accepted_roles(p),p) for n,p in team['hitters'].items()]
        expected += [(n,'Starter',{'StartingPitcher:'+str(i) for i in range(1,6)},'Starter') for n in team['starters']]
        expected += [(n,'Bullpen',{'Bullpen'+str(i) for i in range(1,5)},'Bullpen') for n in team['bullpen']]
        expected += [(team['setup'],'Setup',{'Setup'},'Setup'), (team['closer'],'Closer',{'Closer'},'Closer')]
        for name,kind,roles,display in expected:
            ids = identity[(team['year'],team['team'],name)]
            sid = next(iter(ids)) if len(ids)==1 and name not in team.get('excluded',{}) else None
            row = {'year':team['year'],'team':team['team'],'split':team['split'],'name':name,'kind':kind,
                   'expected':display,'sourceUrl':reference['sourceUrl'],'image':team['image'],'seasonId':sid or '',
                   'status':'Matched' if sid else 'Unresolved_NoForcedAlias'}
            for phase, archive in (('before',before),('after',after)):
                actual = archive[sid]['rosterRole'] if sid else ''
                row[phase+'Role'] = actual
                row[phase+'PlacementError'] = int(actual not in roles) if sid else ''
                row[phase+'MembershipError'] = int(not actual.startswith('StartingHitter:')) if sid and kind=='Hitter' else ''
            rows.append(row)
    summary = []
    for kind in ('All','Hitter','Starter','Bullpen','Setup','Closer'):
        for split in ('All','Review','Validation'):
            part=[r for r in rows if r['status']=='Matched' and (kind=='All' or r['kind']==kind) and (split=='All' or r['split']==split)]
            summary.append({'kind':kind,'split':split,'count':len(part),
                            **{p+'PlacementErrors':sum(r[p+'PlacementError'] for r in part) for p in ('before','after')}})
    return rows, summary


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before',type=Path,required=True)
    parser.add_argument('--after',type=Path,required=True)
    parser.add_argument('--reference',type=Path,default=Path('docs/reports/pm_final_review_20260906/lineup_reference.json'))
    parser.add_argument('--output-dir',type=Path,required=True)
    args=parser.parse_args()
    rows,summary=compare_lineups(load_seasons(args.before),load_seasons(args.after),read(args.reference))
    args.output_dir.mkdir(parents=True,exist_ok=True)
    with (args.output_dir/'lineup_comparison.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(rows[0])); writer.writeheader(); writer.writerows(rows)
    (args.output_dir/'lineup_summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps([r for r in summary if r['split']=='All'],ensure_ascii=False))
    print('MISMATCHES',[(r['year'],r['team'],r['name'],r['expected'],r['afterRole']) for r in rows if r['afterPlacementError']==1])


if __name__=='__main__':
    main()
