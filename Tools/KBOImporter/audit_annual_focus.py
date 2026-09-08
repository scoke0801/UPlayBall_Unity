"""지정한 연도·구단의 카드별 과소평가를 합계와 분리해 검증한다."""
import argparse
import csv
import json
from pathlib import Path

from study_pm_calibration import metrics


def audit_focus(seasons, labels, team, years, fixture=None):
    """식별 가능한 관측만 비교하며 미연결·판본 불명은 별도 보고한다."""
    selected={s['playerSeasonId']:s for s in seasons if s['originFranchiseId']==team and s['originYear'] in years}
    rows=[]
    for label in labels:
        if label['target']!='Cost' or label['id'] not in selected:
            continue
        s=selected[label['id']]
        rows.append(dict(id=label['id'],year=s['originYear'],name='/'.join(s['sourceReferenceNames']),
            expected=int(label['expected']),actual=s['cost'],origin=label['origin'],
            status='UserFixtureVersionUnknown' if label['origin'].startswith('UserCostReference:') else 'VerifiedReference'))
    unresolved=[]
    if fixture and fixture['SourceTeam']==team and fixture['SourceYear'] in years:
        for card in fixture['Cards']:
            matches=[s for s in selected.values() if s['originYear']==fixture['SourceYear'] and card['PlayerName'] in s['sourceReferenceNames']]
            if len(matches)!=1:
                unresolved.append(dict(name=card['PlayerName'],year=fixture['SourceYear'],candidates=len(matches)))
                continue
            s=matches[0]
            if any(r['id']==s['playerSeasonId'] and r['expected']==card['Cost'] and r['status']=='UserFixtureVersionUnknown' for r in rows):
                continue
            rows.append(dict(id=s['playerSeasonId'],year=s['originYear'],name=card['PlayerName'],
                expected=card['Cost'],actual=s['cost'],origin=fixture['FixtureId'],status='UserFixtureVersionUnknown'))
    for r in rows:
        r['delta']=r['actual']-r['expected']
    groups=[]
    for year in sorted(years):
        for status in sorted({r['status'] for r in rows if r['year']==year}):
            part=[r for r in rows if r['year']==year and r['status']==status]
            groups.append(dict(year=year,status=status,**metrics([(r['expected'],r['actual']) for r in part]),
                underCount=sum(r['delta']<0 for r in part),overCount=sum(r['delta']>0 for r in part)))
    missing_years=sorted(years-{r['year'] for r in rows})
    compared_ids={r['id'] for r in rows}
    coverage=[dict(year=year,sourceCount=sum(s['originYear']==year for s in selected.values()),
        comparedCount=len({r['id'] for r in rows if r['year']==year}),
        withoutReference=['/'.join(s['sourceReferenceNames']) for s in selected.values()
                          if s['originYear']==year and s['playerSeasonId'] not in compared_ids]) for year in sorted(years)]
    return dict(team=team,years=sorted(years),groups=groups,rows=rows,unresolved=unresolved,missingYears=missing_years,coverage=coverage,
                passed=bool(rows) and not unresolved and not missing_years and all(r['delta']==0 for r in rows))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--candidate',type=Path,required=True)
    parser.add_argument('--comparison',type=Path,required=True)
    parser.add_argument('--team',required=True)
    parser.add_argument('--years',required=True)
    parser.add_argument('--fixture',type=Path)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--require-exact-cost',action='store_true')
    args=parser.parse_args()
    years={int(y) for y in args.years.split(',')}
    seasons=[s for y in sorted(years) for s in json.loads((args.candidate/f'Years/{y}.json').read_text(encoding='utf-8'))['playerSeasons']]
    with args.comparison.open(encoding='utf-8-sig',newline='') as stream:
        labels=list(csv.DictReader(stream))
    fixture=json.loads(args.fixture.read_text(encoding='utf-8-sig')) if args.fixture else None
    report=audit_focus(seasons,labels,args.team,years,fixture)
    args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for group in report['groups']:
        print(group,flush=True)
    if args.require_exact_cost and not report['passed']:
        raise SystemExit('카드별 과소·과대평가 또는 미연결 관측이 남아 있습니다.')


if __name__=='__main__':
    main()
