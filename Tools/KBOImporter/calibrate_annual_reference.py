"""캐시 기록에서 산출한 기준값을 연도 카드로 검증하고 공통 회귀 설정을 만든다."""
from __future__ import annotations

import argparse
import copy
import csv
import hashlib
import json
from collections import Counter, defaultdict
from pathlib import Path

import synthetic_bake as bake
from record_calibration import evaluate_model, has_observed_sample, read_feature, resolve_model_cost, validate_models
from study_pm_calibration import metrics

ROOT = Path(__file__).resolve().parents[2]
NAMES = {'Hitter': {'교타': 'Contact', '장타': 'Power', '주력': 'Speed', '어깨': 'Arm', '수비': 'Defense', '정신력': 'BatterMental'},
         'Pitcher': {'체력': 'Stamina', '구속': 'Velocity', '구위': 'Stuff', '변화구': 'Breaking', '제구력': 'Control', '정신력': 'PitcherMental'}}
ARCHIVE_NAMES = {
    'Hitter': {'교타력': 'Contact', '장타력': 'Power', '주력': 'Speed', '수비력': 'Defense', '정신력': 'BatterMental'},
    'Pitcher': {'체력': 'Stamina', '구속': 'Velocity', '구위': 'Stuff', '변화구': 'Breaking', '제구력': 'Control', '정신력': 'PitcherMental'},
}


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def normalize_team(team):
    return {'히어': '히어로즈', '우리': '히어로즈'}.get(team, team)


def is_annual_reference(year, edition, text, policy):
    """알려진 일반 연도 카드만 사용하고 월별·초과 연도는 거부한다."""
    return (1982 <= int(year) <= policy['maximumCardYear'] and edition in ('Normal', '일반', '일반/기사 문맥')
            and not any(token in str(text).lower() for token in ('월', 'monthly', 'month', '라이브', 'live card')))


def split_person(person, policy):
    bucket = int(hashlib.sha256((policy['splitSalt'] + '|' + person).encode()).hexdigest()[:8], 16) % 10
    return 'Train' if bucket < 6 else 'Validation' if bucket < 8 else 'Holdout'


def match_record_candidates(candidates, seasons, players, year, team, kind, record_counts):
    """동명이인·개명 연결은 가격이 아닌 같은 구단 시즌의 다중 기록으로 확인한다."""
    stats_key='hitterStats' if kind=='Hitter' else 'pitcherStats'
    record_ids={bake.pitch_source_identity.editor_source_season_id(str(p['sourcePlayerId']),year)
                for p in players if all((p.get(stats_key) or {}).get(k)==v for k,v in record_counts.items())}
    if not candidates and len(record_counts)>=5 and sum(v>0 for v in record_counts.values())>=3:
        candidates=[s for s in seasons if s['originYear']==year and s['playerType']==kind
                    and normalize_team(s['originFranchiseId'])==normalize_team(team)]
    return [s for s in candidates if s['playerSeasonId'] in record_ids]


def archive_kind(position):
    """웹 카드의 포지션을 선수 타입으로 변환한다."""
    return 'Pitcher' if position in ('선발','중계','셋업','마무리') else 'Hitter'


def filter_position_candidates(candidates, kind, position):
    """동명이인은 카드에 표시된 포지션과 역할로만 추가 구분한다."""
    if kind=='Hitter':
        expected={'포수':{'C'},'1루수':{'1B'},'2루수':{'2B'},'3루수':{'3B'},'유격수':{'SS'},
                  '외야수':{'LF','CF','RF','DH'}}.get(position,set())
        return [s for s in candidates if s.get('position') in expected]
    expected={'선발':{'Starter'},'중계':{'LongRelief','MiddleRelief'},'셋업':{'Setup'},'마무리':{'Closer'}}.get(position,set())
    return [s for s in candidates if s.get('pitcherRole') in expected]


def load_labels(seasons, policy):
    """DB를 우선하고 중복·충돌·미확정 연결은 학습과 분리한다."""
    exact, without_team = defaultdict(list), defaultdict(list)
    for s in seasons.values():
        for name in s['sourceReferenceNames']:
            exact[(s['originYear'], normalize_team(s['originFranchiseId']), name, s['playerType'])].append(s)
            without_team[(s['originYear'], name, s['playerType'])].append(s)
    labels, rejected, seen = [], [], {}

    def add(year, team, name, kind, edition, values, origin, text='', record_counts=None,
            reference_position=None):
        if not is_annual_reference(year, edition, text, policy):
            rejected.append(dict(origin=origin, name=name, year=year, reason='OutOfScope')); return
        kinds = (kind,) if kind else ('Hitter', 'Pitcher')
        candidates = [s for k in kinds for s in (exact[(year, normalize_team(team), name, k)] if team else without_team[(year, name, k)])]
        candidates = list({s['playerSeasonId']:s for s in candidates}.values())
        join_method='YearTeamNameType'
        if len(candidates)>1 and kind and reference_position:
            positioned=filter_position_candidates(candidates,kind,reference_position)
            if len(positioned)==1:
                candidates=positioned;join_method='YearTeamNameTypePosition'
        if len(candidates)!=1 and record_counts:
            join_method='NameAndRecordCounts' if candidates else 'TeamAndRecordCounts'
            source=read(ROOT/f'Tools/KBOImporter/.cache/KBOImport/Normalized/{year}.json')
            candidates=match_record_candidates(candidates,seasons.values(),source['players'],year,team,kind,record_counts)
        if len(candidates) != 1:
            rejected.append(dict(origin=origin, name=name, year=year, reason='Identity', candidates=len(candidates))); return
        s = candidates[0]
        for target, expected in values.items():
            identity = (s['playerSeasonId'], target)
            if identity in seen:
                rejected.append(dict(origin=origin, name=name, year=year, target=target,
                                     reason='Duplicate' if seen[identity] == expected else 'VersionConflict', expected=expected)); continue
            seen[identity] = expected
            labels.append(dict(id=s['playerSeasonId'], person=s['playerPersonId'], year=year,
                               kind=s['playerType'], target=target, expected=expected, origin=origin,
                               joinMethod=join_method,referenceName=name,
                               split=split_person(s['playerPersonId'], policy)))

    for table, kind in (('ta','Hitter'), ('too','Pitcher')):
        with (ROOT/f'Research/PyaMaeCardDb/{table}.csv').open(encoding='utf-8-sig', newline='') as stream:
            database=list(csv.DictReader(stream))
            for r in database:
                if r['카드종류']=='올스타':continue
                values = {NAMES[kind][k]:int(r[k]) for k in NAMES[kind] if r[k].strip()}
                values['Cost'] = int(r['코스트'])
                add(2000+int(r['년도']), r['팀'], r['이름'], kind, r['카드종류'], values,
                    f'Database:{table}:{r["ID"]}',
                    record_counts={k:int(r[v]) for k,v in (
                        (('games','시합수'),('atBats','타수'),('hits','안타'),('homeRuns','홈런'),('strikeouts','삼진'),('walks','4구')) if kind=='Hitter' else
                        (('games','시합수'),('wins','승리'),('losses','패전'),('saves','세이브'),('strikeouts','탈삼진')))
                        if r.get(v,'').strip()})
        # 일반/올스타 짝으로 동일 Cost를 먼저 검증한다. 일반 카드가 없는 스타도 가격 학습에서 누락하지 않는다.
        normal=defaultdict(list)
        for r in database:
            if r['카드종류']=='일반':normal[(r['년도'],r['팀'],r['이름'])].append(r)
        edition_pairs=[(normal[(r['년도'],r['팀'],r['이름'])][0],r) for r in database
                       if r['카드종류']=='올스타' and len(normal[(r['년도'],r['팀'],r['이름'])])==1]
        if len(edition_pairs)<policy['minimumEditionPairCount'] or any(a['코스트']!=b['코스트'] for a,b in edition_pairs):
            raise ValueError('올스타의 일반 카드 Cost 동등성이 검증되지 않았습니다.')
        for r in database:
            if r['카드종류']!='올스타':continue
            add(2000+int(r['년도']),r['팀'],r['이름'],kind,'Normal',{'Cost':int(r['코스트'])},
                f'DatabaseAllStarCostEquivalent:{table}:{r["ID"]}')
    for relative in ('Research/PyaMaeCardDb/1989-1999/archive-cards.csv',
                     'Research/PyaMaeCardDb/2010-2016/archive-cards.csv'):
        with (ROOT/relative).open(encoding='utf-8-sig',newline='') as stream:
            for r in csv.DictReader(stream):
                year=int(r['SeasonYear'])
                if r['CardType']!='일반' or r['CardTypeCss']!='playerCard1':
                    continue
                if 'SourceYearLabel' in r and r['SourceYearLabel']!=f"{year%100:02d}'":
                    continue
                kind=archive_kind(r['Position'])
                values={target:int(r[source]) for source,target in ARCHIVE_NAMES[kind].items() if r.get(source,'').strip()}
                values['Cost']=int(r['Cost'])
                add(year,r['Team'],r['Name'],kind,'일반',values,f'ArchivedWebCard:{relative}:{r["CardId"]}',
                    text=r.get('SourceYearLabel',''),reference_position=r['Position'])
    folder = ROOT/'docs/reports/pm_threshold_review_20260906'
    for file, kind in (('hitter_card_readings.json','Hitter'), ('pitch_card_readings.json','Pitcher')):
        data = read(folder/file)
        for r in data['readings'] if isinstance(data, dict) else data:
            # 타자 막대의 네 번째 열은 번트이며 Arm이 아니다.
            columns = ('Contact','Power','Speed','Bunt','Defense','BatterMental') if kind=='Hitter' else bake.ABILITY_NAMES[6:]
            values={k:int(v) for k,v in zip(columns,r['attributes']) if k!='Bunt'}
            values['Cost']=int(r.get('articleCost',r.get('cost')))
            add(r['year'],r.get('team',''),r['name'],kind,r.get('variant',''),values,
                f'ArticleImage:{r["cardId"]}',text=r.get('note',''))
    workbook=read(ROOT/'docs/reports/pm_reference_review_20260906/workbook_extracted.json')
    for r in next(s['Rows'] for s in workbook['Sheets'] if s['Name']=='Cost_근거')[1:]:
        c=r['Cells']
        add(int(c['A']),c.get('B',''),c['C'],None,c.get('F',''),{'Cost':int(c['E'])},
            f'ArticleCost:{c["K"]}',text=c.get('J',''))
    # 사용자가 이번 검증 목표로 지정한 기준표는 웹 검증 자료와 출처를 구분한다.
    for path in policy.get('userReferenceFixtures',[]):
        fixture=read(ROOT/path)
        for card in fixture['Cards']:
            kind='Hitter' if card['Slot'].startswith(('StartingHitter','Bench')) else 'Pitcher'
            add(fixture['SourceYear'],fixture['SourceTeam'],card['PlayerName'],kind,'Normal',
                {'Cost':card['Cost']},f'UserCostReference:{fixture["FixtureId"]}',text='사용자가 지정한 연도 카드 Cost 기준')
    return labels, rejected


def evidence_for(season):
    return {c['metric']:c for t in season['abilityDerivationTrace'] for c in t['components']}


def fit_model(rows, definitions, ridge, policy, boundary_method='WeightedAbsoluteError'):
    """학습 자료만으로 표준화하고 정규화된 공통 계수를 구한다."""
    import numpy as np
    features=[]; columns=[]
    for definition in definitions:
        f={'source':definition} if isinstance(definition,str) else dict(definition)
        values=np.array([read_feature(f,r['evidence'],r['before'],r['value']) for r in rows],dtype=float)
        valid=values[np.isfinite(values)]
        if len(valid)<policy['minimumTrainingRows']: continue
        minimum,maximum=np.quantile(valid,policy['clipQuantiles'])
        bounded=np.clip(values,minimum,maximum)
        center=float(np.nanmean(bounded)); scale=float(np.nanstd(bounded))
        if scale<1e-8: continue
        f.update(mean=center,scale=scale,minimum=float(minimum),maximum=float(maximum))
        features.append(f);columns.append((np.where(np.isfinite(bounded),bounded,center)-center)/scale)
    x=np.array(columns).T; y=np.array([r['expected'] for r in rows],dtype=float)
    frequencies=Counter(r['expected'] for r in rows)
    sample_weights=np.array([frequencies[r['expected']]**(-policy['costFrequencyWeightExponent']) if r['target']=='Cost' else 1.0 for r in rows])
    sample_weights/=sample_weights.mean()
    intercept=float(np.average(y,weights=sample_weights)); centered=y-intercept
    # 희귀한 상위 Cost가 다수 중간 카드에 묻히지 않도록 학습 빈도만으로 가중한다.
    x_center=np.average(x,axis=0,weights=sample_weights)
    x=x-x_center
    weights=np.linalg.solve(x.T@(x*sample_weights[:,None])+ridge*np.eye(x.shape[1]),x.T@(centered*sample_weights))
    # 성과 지표는 모두 클수록 우수한 방향으로 정규화되어 있다. 공선성 때문에 부호가 뒤집히지 않게 한다.
    positive=[i for i,f in enumerate(features) if f['source'] in policy['nonnegativeFeatureSources'] or
              f['source'].split('.')[-1] in policy['nonnegativeFeatureFields']]
    for _ in range(200):
        previous=weights.copy()
        for i in range(len(weights)):
            residual=centered-x@weights+x[:,i]*weights[i]
            w=float(x[:,i]@(residual*sample_weights)/(x[:,i]@(x[:,i]*sample_weights)+ridge))
            weights[i]=max(0,w) if i in positive else w
        if np.max(np.abs(weights-previous))<1e-9:break
    for i,(f,w) in enumerate(zip(features,weights)):
        f['coefficient']=float(w)
        if i in positive:f['minimumCoefficient']=0.0
    model=dict(intercept=float(intercept-x_center@weights),features=features,ridge=ridge,trainingCount=len(rows))
    if rows[0]['target']=='Cost':
        model['costBoundaryMethod']=boundary_method
        if boundary_method=='TrainingFrequency':
            scores=sorted(evaluate_model(model,r['evidence'],r['before'],r['value'])[0] for r in rows)
            model['costBoundaries']=[float(np.quantile(scores,sum(r['expected']<=grade for r in rows)/len(rows))) for grade in range(1,10)]
        else:
            model['costBoundaries']=fit_cost_boundaries(rows,model,policy)
    return model


def fit_cost_boundaries(rows, model, policy):
    """가중 절대 오차를 최소화하는 10개 순서형 가격 구간을 동적 계획법으로 학습한다."""
    import numpy as np
    frequencies=Counter(r['expected'] for r in rows)
    grouped=defaultdict(list)
    for row in rows:
        score,_=evaluate_model(model,row['evidence'],row['before'],row['value'])
        grouped[score].append(row)
    scores=sorted(grouped)
    losses=np.zeros((10,len(scores)))
    for j,score in enumerate(scores):
        for row in grouped[score]:
            weight=frequencies[row['expected']]**(-policy['costBoundaryFrequencyWeightExponent'])
            for grade in range(1,11):losses[grade-1,j]+=weight*abs(grade-row['expected'])
    prefix=np.column_stack((np.zeros(10),np.cumsum(losses,axis=1)))
    previous=prefix[0].copy(); links=[]
    for grade in range(1,10):
        best=float('inf');cut=0;current=np.zeros(len(scores)+1);choices=[]
        for j in range(len(scores)+1):
            candidate=previous[j]-prefix[grade,j]
            if candidate<best:best=candidate;cut=j
            current[j]=prefix[grade,j]+best;choices.append(cut)
        links.append(choices);previous=current
    j=len(scores);cuts=[]
    for choices in reversed(links):j=choices[j];cuts.append(j)
    cuts.reverse()
    return [scores[0]-1 if cut==0 else scores[-1]+1 if cut==len(scores) else
            (scores[cut-1]+scores[cut])/2 for cut in cuts]


def validation_loss(rows, model, policy):
    """Cost 경계의 희귀 표본도 선택 지표에 반영한다. 보류 자료는 읽지 않는다."""
    frequencies=Counter(r['expected'] for r in rows)
    weights=[frequencies[r['expected']]**(-policy['costFrequencyWeightExponent']) if r['target']=='Cost' else 1.0 for r in rows]
    loss=sum(w*abs(r['expected']-(prediction(r,model) if model else r['before'])) for r,w in zip(rows,weights))/sum(weights)
    upper=[r for r in rows if r['target']=='Cost' and r['expected']>=9]
    if upper:
        loss+=policy['upperCostValidationWeight']*sum(abs(r['expected']-(prediction(r,model) if model else r['before'])) for r in upper)/len(upper)
    return loss


def prediction(row, model):
    if not row.get('canCalibrate',True):return row['before']
    value,_=evaluate_model(model,row['evidence'],row['before'],row['value'])
    if row['target']=='Cost':return resolve_model_cost(value,model,row['ceiling'])
    return bake.clamp_rating(value)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline',type=Path,required=True)
    parser.add_argument('--baseline-balance',type=Path,required=True)
    parser.add_argument('--rebuild-baseline',action='store_true',help='보존한 기준 설정으로 캐시에서 새 기준 Archive를 만든다. 기존 경로는 덮어쓰지 않는다.')
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--policy',type=Path,default=Path(__file__).with_name('reference_calibration_policy.json'))
    parser.add_argument('--export-cost-rows',type=Path)
    parser.add_argument('--export-reference-overrides',type=Path)
    args=parser.parse_args(); args.output.mkdir(parents=True,exist_ok=True)
    policy=read(args.policy)
    bake.DERIVATION_BALANCE.clear()
    bake.DERIVATION_BALANCE.update(read(args.baseline_balance))
    if bake.DERIVATION_BALANCE.get('referenceRecordModels'):
        raise ValueError('기준 설정은 회귀 보정 이전 설정이어야 합니다.')
    if args.rebuild_baseline:
        if args.baseline.exists():
            raise ValueError('기준 재생성은 존재하지 않는 새 폴더를 지정해야 합니다.')
        bake.ABILITY_FORMULA_VERSION=bake.DERIVATION_BALANCE['abilityFormulaVersion']
        bake.COST_FORMULA_VERSION=bake.DERIVATION_BALANCE['costFormulaVersion']
        bake.DERIVATION_BALANCE_VERSION=bake.DERIVATION_BALANCE['version']
        normalized=ROOT/'Tools/KBOImporter/.cache/KBOImport/Normalized'
        years=sorted(int(p.stem) for p in normalized.glob('*.json') if p.stem.isdigit())
        content=bake.build_editor_original_content(normalized,years)
        bake.write_editor_asset_archive(content,args.baseline)
        print('BASELINE_REBUILT',len(years),'years',flush=True)
    manifest=read(args.baseline/'manifest.json')['sourceManifest']
    for field in ('abilityFormulaVersion','costFormulaVersion'):
        if manifest[field]!=bake.DERIVATION_BALANCE[field]:
            raise ValueError('기준 Archive와 기준 산출 설정의 버전이 다릅니다.')
    seasons={s['playerSeasonId']:s for p in sorted((args.baseline/'Years').glob('*.json')) for s in read(p)['playerSeasons']}
    labels,rejected=load_labels(seasons,policy)
    evidence_by_id={}
    for year in sorted({r['year'] for r in labels}):
        source=read(ROOT/f'Tools/KBOImporter/.cache/KBOImport/Normalized/{year}.json')
        games,_=bake.source_season_games(source)
        availability=bake.derive_pitcher_role_availability(source['players'])
        for kind in ('Hitter','Pitcher'):
            players=[p for p in source['players'] if bake.source_player_type(p)==kind]
            _,traces,_=bake.build_adjusted_feature_pool(players,year,kind,availability,season_games=games)
            for player in players:
                sid=bake.pitch_source_identity.editor_source_season_id(str(player['sourcePlayerId']),year)
                evidence_by_id[sid]=traces[player['sourcePlayerId']]
    for row in labels:
        s=seasons[row['id']];row['evidence']=evidence_by_id[row['id']]
        row['before']=s['cost'] if row['target']=='Cost' else s['baseAttributes'][bake.ABILITY_INDEX[row['target']]]
        row['value']=s['costDerivationTrace']['componentScores'];row['ceiling']=s['costDerivationTrace']['eliteEligibility']['maximumCost']
        row['canCalibrate']=has_observed_sample(row['evidence']) and (row['target']=='Cost' or any(row['evidence'].get(metric,{}).get('isAvailable',False) for metric in bake.DERIVATION_BALANCE['ratingProfiles'][row['kind']][row['target']]['metrics']))
    if args.export_cost_rows:
        args.export_cost_rows.write_text(json.dumps([r for r in labels if r['target']=='Cost'],ensure_ascii=False),encoding='utf-8')
    if args.export_reference_overrides:
        cards={}
        for row in labels:
            card=cards.setdefault(row['id'],dict(playerSeasonId=row['id'],playerType=row['kind'],
                originYear=row['year'],values={},sources={}))
            card['values'][row['target']]=row['expected'];card['sources'][row['target']]=row['origin']
        payload=dict(version='annual-general-reference-v1',maximumCardYear=policy['maximumCardYear'],
            policyVersion=policy['version'],cards=sorted(cards.values(),key=lambda c:c['playerSeasonId']))
        args.export_reference_overrides.write_text(json.dumps(payload,ensure_ascii=False,sort_keys=True,
            separators=(',',':'))+'\n',encoding='utf-8')
    models={}; reports=[]
    for kind, targets in policy['features'].items():
        models[kind]={}
        for target,definitions in targets.items():
            part=[r for r in labels if r['kind']==kind and r['target']==target]
            train=[r for r in part if r['split']=='Train' and r['canCalibrate']]; validation=[r for r in part if r['split']=='Validation']
            if len(train)<policy['minimumTrainingRows'] or len(validation)<policy['minimumValidationRows']:continue
            candidates=[]
            for ridge in policy['ridgeCandidates']:
                for method in policy['costBoundaryMethods'] if target=='Cost' else ['WeightedAbsoluteError']:
                    model=fit_model(train,definitions,ridge,policy,method)
                    loss=validation_loss(validation,model,policy)
                    candidates.append((loss,-ridge,model))
            loss,_,model=min(candidates,key=lambda x:x[:2])
            baseline=validation_loss(validation,None,policy)
            selected=loss+policy['minimumValidationImprovement']<baseline
            if selected:models[kind][target]=model
            for split in ('All','Train','Validation','Holdout'):
                subset=[r for r in part if split=='All' or r['split']==split]
                scores={phase:metrics([(r['expected'],r['before'] if phase=='before' else prediction(r,model) if selected else r['before']) for r in subset]) for phase in ('before','after')}
                reports.append(dict(kind=kind,target=target,split=split,selected=selected,**scores))
                if split=='Holdout':print(kind,target,selected,round(scores['before']['mae'],3),round(scores['after']['mae'],3),flush=True)
    validate_models(models,{'Hitter':set(bake.HITTER_METRIC_NAMES),'Pitcher':set(bake.PITCHER_METRIC_NAMES)})
    config=copy.deepcopy(bake.DERIVATION_BALANCE)
    config.update(version='historical-derivation-balance-v23-candidate',abilityFormulaVersion='historical-ability-v10',costFormulaVersion='historical-season-value-v17-candidate',referenceRecordModels=models)
    config['ratingCalibrationNote']='2013년 이하 연도 카드 '+str(len({r['id'] for r in labels}))+'개를 캐시 기록과 연결. 일반 카드 및 가격 동등성을 검증한 올스타 Cost. 선수 단위 분리·단조 제약·희귀도 가중 회귀와 학습된 가격 경계. 월별/판본 충돌 제외. 후기 서비스 최종판의 복원은 아님.'
    config['referenceCalibration']={'policyVersion':policy['version'],'maximumCardYear':policy['maximumCardYear'],'splitSalt':policy['splitSalt'],
        'policySha256':hashlib.sha256(args.policy.read_bytes()).hexdigest(),'fitSource':'NormalizedCacheRecordFeatures',
        'note':'일반 연도 카드의 선수 단위 학습/검증 분리. DB 우선, 충돌/월별/2014 이후 제외. 구속 결측은 55 유지.'}
    (args.output/'derivation_balance.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    report={'policy':policy,'labelCount':len(labels),'cardCount':len({r['id'] for r in labels}),
            'joinMethods':dict(Counter(r['joinMethod'] for r in labels if r['target']=='Cost')),
            'baselineBalanceSha256':hashlib.sha256(args.baseline_balance.read_bytes()).hexdigest(),
            'baselineArchiveHashes':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((args.baseline/'Years').glob('*.json'))},
            'byYear':dict(sorted(Counter(r['year'] for r in labels if r['target']=='Cost').items())),
            'rejections':rejected,'scores':reports,'splitPeople':{split:len({r['person'] for r in labels if r['split']==split}) for split in ('Train','Validation','Holdout')}}
    source_paths=[ROOT/'Research/PyaMaeCardDb'/name for name in ('ta.csv','too.csv')]
    source_paths += [ROOT/'Research/PyaMaeCardDb'/folder/'archive-cards.csv' for folder in ('1989-1999','2010-2016')]
    source_paths += [ROOT/'docs/reports/pm_threshold_review_20260906'/name for name in ('hitter_card_readings.json','pitch_card_readings.json')]
    source_paths += [ROOT/'docs/reports/pm_reference_review_20260906/workbook_extracted.json']
    source_paths += [ROOT/path for path in policy.get('userReferenceFixtures',[])]
    source_paths += [ROOT/f'Tools/KBOImporter/.cache/KBOImport/Normalized/{year}.json' for year in sorted({r['year'] for r in labels})]
    report['sourceHashes']={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in source_paths}
    (args.output/'fit_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    fields=['id','person','year','kind','target','expected','origin','joinMethod','referenceName','split','before','after']
    with (args.output/'comparison.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=fields);writer.writeheader()
        for r in labels:
            writer.writerow({**{k:r[k] for k in fields if k!='after'},'after':prediction(r,models.get(r['kind'],{}).get(r['target']))})
    print('CARDS',report['cardCount'],'YEARS',report['byYear'],flush=True)


if __name__=='__main__':main()
