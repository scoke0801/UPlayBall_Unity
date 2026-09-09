"""후보 Archive가 원기록·신원을 보존하는지 검사하고 선수 필드 변경 범위를 출력한다."""
import argparse
import json
from collections import Counter
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def audit(before, after):
    old_manifest, new_manifest = read(before/'manifest.json'), read(after/'manifest.json')
    old_people, new_people = read(before/'player_persons.json'), read(after/'player_persons.json')
    if old_people['worldIdentityNamePool'] != new_people['worldIdentityNamePool']:
        raise ValueError('이름 후보군 변경')
    persons = {p['playerPersonId']:p for p in new_people['items']}
    if set(persons) != {p['playerPersonId'] for p in old_people['items']}:
        raise ValueError('선수 신원 구성 변경')
    person_positions = []
    for person in old_people['items']:
        fresh = persons[person['playerPersonId']]
        changed = {k for k in set(person)|set(fresh) if person.get(k) != fresh.get(k)}
        if changed - {'primaryPosition'}:
            raise ValueError(f'선수 신원 변경: {changed}')
        if changed:
            person_positions.append(person['playerPersonId'])
    fields = Counter()
    changes = []
    rosters = []
    qualified = 0
    record_positions = []
    award_positions = []
    for entry in old_manifest['years']:
        old, new = read(before/entry['path']), read(after/entry['path'])
        for key in ('normalCards',):
            if old[key] != new[key]:
                raise ValueError(f"원기록/카드 변경: {entry['year']} {key}")
        award_key = lambda r: (r['seasonYear'], r['awardType'], r['playerSeasonId'])
        awards = {award_key(r): r for r in new['originalAwardRecords']}
        if set(awards) != {award_key(r) for r in old['originalAwardRecords']}:
            raise ValueError('수상자 변경')
        for award in old['originalAwardRecords']:
            fresh = awards[award_key(award)]
            changed = {k for k in set(award)|set(fresh) if award.get(k) != fresh.get(k)}
            if changed - {'position'} or (changed and award['awardType'] == 'GoldenGlove'):
                raise ValueError('수상 부문 변경')
            if changed:
                award_positions.append(award_key(award))
        records = {r['playerSeasonId']: r for r in new['originalSeasonRecords']}
        if set(records) != {r['playerSeasonId'] for r in old['originalSeasonRecords']}:
            raise ValueError('원기록 구성 변경')
        for record in old['originalSeasonRecords']:
            fresh = records[record['playerSeasonId']]
            changed = {k for k in set(record)|set(fresh) if record.get(k) != fresh.get(k)}
            if changed - {'position'}:
                raise ValueError(f'원기록 통계 변경: {changed}')
            if changed:
                record_positions.append(record['playerSeasonId'])
        current = {s['playerSeasonId']: s for s in new['playerSeasons']}
        if set(current) != {s['playerSeasonId'] for s in old['playerSeasons']}:
            raise ValueError('선수 구성 변경')
        for row in old['playerSeasons']:
            fresh = current[row['playerSeasonId']]
            changed = [k for k in set(row)|set(fresh) if row.get(k) != fresh.get(k)]
            if changed:
                fields.update(changed)
                changes.append(dict(id=row['playerSeasonId'], team=row['originTeamSeasonKey'], fields=sorted(changed)))
            qualified += bool(fresh.get('secondaryPositions'))
        new_teams = {t['teamSeasonKey']:t for t in new['teamSeasons']}
        for team in old['teamSeasons']:
            if team['core25CardIds'] != new_teams[team['teamSeasonKey']]['core25CardIds']:
                rosters.append(team['teamSeasonKey'])
    return dict(beforeHash=old_manifest['sourceManifest']['contentHash'],
        afterHash=new_manifest['sourceManifest']['contentHash'], identityPreserved=True,
        originalStatisticsPreserved=True, recordPositionChanges=record_positions,
        awardRecipientsPreserved=True, awardPositionMetadataChanges=award_positions,
        qualifiedPlayerSeasons=qualified, personPositionChanges=person_positions,
        changedFields=dict(fields), changedRosters=rosters, changes=changes)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('before', type=Path)
    parser.add_argument('after', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    result = audit(args.before, args.after)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k != 'changes'}, ensure_ascii=False))
