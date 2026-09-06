"""Cost·주전·저표본 기용의 실제 Bake 전후를 같은 관측 행으로 비교한다."""
import argparse,json,csv
from pathlib import Path
from collections import Counter
from study_pm_cost_shape import reference_labels,summarize
from study_pm_calibration import read,metrics
from report_pm_calibration import load_seasons
from report_pm_lineups import compare_lineups
def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before',type=Path,required=True)
    parser.add_argument('--after',type=Path,required=True)
    parser.add_argument('--output-dir',type=Path,required=True)
    parser.add_argument('--reference',type=Path,default=Path('docs/reports/pm_final_review_20260906/lineup_reference.json'))
    args=parser.parse_args()
    report=args.output_dir
    report.mkdir(parents=True,exist_ok=True)
    before=load_seasons(args.before); after=load_seasons(args.after)
    assert before.keys()==after.keys()
    labels=reference_labels(before,args.before)
    cost=[]
    for r in labels:
     s=before[r['id']]; t=after[r['id']]
     cost.append(dict(r,year=s['originYear'],team=s['originFranchiseId'],name=s['sourceReferenceNames'][0],playerType=s['playerType'],before=s['cost'],after=t['cost']))
    with (report/'cost_comparison.csv').open('w',encoding='utf-8-sig',newline='') as f:
     w=csv.DictWriter(f,fieldnames=list(cost[0]));w.writeheader();w.writerows(cost)
    cost_summary={kind:{phase:summarize([r for r in labels if kind=='All' or before[r['id']]['playerType']==kind],{sid:s['cost'] for sid,s in archive.items()}) for phase,archive in [('before',before),('after',after)]} for kind in ['All','Hitter','Pitcher']}
    rows,lineups=compare_lineups(before,after,read(args.reference))
    for r in rows:
     r['hasSourcePositionEvidence']=bool(r['seasonId'] and before[r['seasonId']]['positionRoleDerivationTrace'].get('positionCandidates')) if r['kind']=='Hitter' else ''
    with (report/'lineup_comparison.csv').open('w',encoding='utf-8-sig',newline='') as f:
     w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
    def low_sample(archive,maximum):
     return [s for s in archive.values() if s['playerType']=='Hitter' and s['rosterRole'].startswith('StartingHitter:') and s['costEligibilitySample']<=maximum]
    overview={'sourceCount':len(after),'costChanged':sum(before[k]['cost']!=after[k]['cost'] for k in before),
     'pitcherCostChanged':sum(before[k]['cost']!=after[k]['cost'] for k in before if before[k]['playerType']=='Pitcher'),
     'abilityChanged':sum(before[k]['baseAttributes']!=after[k]['baseAttributes'] for k in before),
     'roleChanged':sum(before[k]['rosterRole']!=after[k]['rosterRole'] for k in before),
     'costDistribution':{p:dict(sorted(Counter(s['cost'] for s in archive.values()).items())) for p,archive in [('before',before),('after',after)]},
     'zeroPlateAppearanceStarters':{p:len(low_sample(a,0)) for p,a in [('before',before),('after',after)]},
     'under10PlateAppearanceStarters':{p:len(low_sample(a,9)) for p,a in [('before',before),('after',after)]},
     'hitterMembershipErrors':{p:sum(r[p+'MembershipError'] for r in rows if r['status']=='Matched' and r['kind']=='Hitter') for p in ['before','after']},
     'hitterPlacementWithPositionEvidence':{p:{'count':sum(r['status']=='Matched' and r['kind']=='Hitter' and r['hasSourcePositionEvidence'] for r in rows),'errors':sum(r[p+'PlacementError'] for r in rows if r['status']=='Matched' and r['kind']=='Hitter' and r['hasSourcePositionEvidence'])} for p in ['before','after']},
     'lineups':lineups,'cost':cost_summary,'unresolvedLineups':[r for r in rows if r['status']!='Matched'],
     'remainingLowSampleStarters':[{'name':s['sourceReferenceNames'][0],'year':s['originYear'],'team':s['originFranchiseId'],'role':s['rosterRole'],'pa':s['costEligibilitySample']} for s in low_sample(after,9)],
     'beforeManifest':read(args.before/'manifest.json')['sourceManifest'],
     'afterManifest':read(args.after/'manifest.json')['sourceManifest']}
    (report/'comparison_summary.json').write_text(json.dumps(overview,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in overview.items() if k not in ['beforeManifest','afterManifest','cost','unresolvedLineups']},ensure_ascii=False))
    print('COST',json.dumps({c:{p:v[c]['All'] for p,v in cost_summary['All'].items()} for c in cost_summary['All']['before']},ensure_ascii=False))


if __name__ == "__main__":
    main()
