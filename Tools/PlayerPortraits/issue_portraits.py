"""역사 인물별 초상 정본과 시즌 별칭을 결정론적으로 발급하고 검증한다."""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / 'Assets/10.Datas/HistoricalSimulation/1982-2025'
TARGET = ROOT / 'Assets/Resources/UI/Portraits'
PRODUCTION = ROOT / 'output/imagegen/player-portraits/production-576-v1'


def build_assignments(scope):
    persons = sorted(p['playerPersonId'] for p in json.loads((CONTENT / 'player_persons.json').read_text())['items'])
    groups = collections.defaultdict(set)
    lineages = collections.defaultdict(set)
    aliases = {}
    for path in sorted((CONTENT / 'Years').glob('*.json')):
        for season in json.loads(path.read_text())['playerSeasons']:
            person = season['playerPersonId']
            aliases[season['playerSeasonId']] = person
            lineages[season['originFranchiseId']].add(person)
            group = season['originFranchiseId'] if scope == 'lineage' else season['originTeamSeasonKey']
            groups[group].add(person)
    if max(map(len, groups.values())) > 576:
        raise ValueError('선택한 중복 금지 범위의 인원이 576종을 초과합니다.')
    neighbors = {p: set() for p in persons}
    for members in groups.values():
        for person in members:
            neighbors[person].update(members - {person})
    # 많이 제약된 인물부터 배정하고, 허용된 얼굴 중 사용 인원이 가장 적은 것을 선택한다.
    forbidden = {p: set() for p in persons}
    remaining = set(persons)
    assigned = {}
    counts = [0] * 576
    while remaining:
        person = min(remaining, key=lambda p: (-len(forbidden[p]), -len(neighbors[p]), p))
        choices = [i for i in range(576) if i not in forbidden[person]]
        if not choices:
            raise ValueError(f'배정 실패: {person}')
        face = min(choices, key=lambda i: (counts[i], i))
        assigned[person] = face
        counts[face] += 1
        remaining.remove(person)
        for other in neighbors[person] & remaining:
            forbidden[other].add(face)
    assert set(assigned) == set(persons)
    assert all(len({assigned[p] for p in members}) == len(members) for members in groups.values())
    assert min(counts) > 0
    assert max(counts) - min(counts) <= 1, '전체 사용 인원 균등 분배 실패'
    payload = {'portraitCount': 576, 'persons': [
        {'id': p, 'appearance': assigned[p] + 1} for p in persons],
        'aliases': [{'id': s, 'person': aliases[s]} for s in sorted(aliases)]}
    report = {'persons': len(persons), 'seasons': len(aliases), 'portraits': 576,
              'minimumUsage': min(counts), 'maximumUsage': max(counts),
              'usageHistogram': dict(sorted(collections.Counter(counts).items())),
              'uniquenessScope': scope, 'verifiedGroups': len(groups),
              'maximumLineagePopulation': max(map(len, lineages.values())),
              'assignmentSha256': hashlib.sha256(json.dumps(payload, sort_keys=True).encode()).hexdigest()}
    return payload, report


def meta(path, template=None):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, path.relative_to(ROOT).as_posix()).hex
    if template:
        import re
        text = re.sub(r'guid: [0-9a-f]+', 'guid: ' + guid, template, count=1)
    else:
        text = f'fileFormatVersion: 2\nguid: {guid}\n'
    target.write_text(text, encoding='utf-8')


def publish(payload):
    sources = []
    for face in range(1, 577):
        revision = 'r06' if 17 <= face <= 176 else 'issued-v1'
        source = PRODUCTION / f'faces/transparent/{revision}/face-{face:04d}__{revision}.png'
        if not source.exists():
            raise FileNotFoundError(source)
        sources.append(source)
    folder = TARGET / 'Players'
    folder.mkdir(exist_ok=True)
    meta(folder)
    template = (TARGET / 'img_player_representative.png.meta').read_text()
    for face, source in enumerate(sources, 1):
        target = folder / f'face-{face:04d}.png'
        if target.exists() and target.read_bytes() != source.read_bytes():
            raise ValueError(f'기존 초상 파일과 충돌: {target}')
        if not target.exists():
            shutil.copyfile(source, target)
        meta(target, template)
    catalog = TARGET / 'player_portrait_assignments.json'
    catalog.write_text(json.dumps(payload, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    meta(catalog)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scope', choices=['lineage', 'team-season'], required=True)
    parser.add_argument('--publish', action='store_true')
    args = parser.parse_args()
    payload, report = build_assignments(args.scope)
    if args.publish:
        publish(payload)
        (ROOT / 'Tools/PlayerPortraits/issuance-report.json').write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False, indent=2))
