"""Cost 재저작 전후의 능력치·원기록·로스터 보존과 전체 가격 분포를 검증한다."""
import argparse
import collections
import json
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def audit(before, after):
    """가격 이외 Runtime 선수 필드와 연도별 콘텐츠 변경은 차단한다."""
    old_manifest, new_manifest = read(before / 'manifest.json'), read(after / 'manifest.json')
    if old_manifest['years'] and [row['year'] for row in old_manifest['years']] != [row['year'] for row in new_manifest['years']]:
        raise ValueError('연도 구성이 달라졌습니다.')
    if read(before / 'player_persons.json') != read(after / 'player_persons.json'):
        raise ValueError('선수 신원이 달라졌습니다.')
    counts = {'before': collections.Counter(), 'after': collections.Counter()}
    changes = []
    for entry in old_manifest['years']:
        old, new = read(before / entry['path']), read(after / entry['path'])
        old_seasons, new_seasons = old.pop('playerSeasons'), new.pop('playerSeasons')
        if old != new:
            raise ValueError(f'가격 범위 밖 연도 콘텐츠 변경: {entry["year"]}')
        current = {row['playerSeasonId']: row for row in new_seasons}
        if {row['playerSeasonId'] for row in old_seasons} != set(current):
            raise ValueError('선수 구성이 달라졌습니다.')
        for row in old_seasons:
            fresh = current[row['playerSeasonId']]
            before_cost, after_cost = row.pop('cost'), fresh.pop('cost')
            if row != fresh:
                raise ValueError(f'가격 범위 밖 선수 변경: {row["playerSeasonId"]}')
            counts['before'][before_cost] += 1
            counts['after'][after_cost] += 1
            if before_cost != after_cost:
                changes.append(dict(playerSeasonId=row['playerSeasonId'], year=entry['year'],
                                    before=before_cost, after=after_cost))
    return dict(beforeHash=old_manifest['sourceManifest']['contentHash'],
                afterHash=new_manifest['sourceManifest']['contentHash'],
                distributions=counts, changedCards=len(changes), changes=changes)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('before', type=Path)
    parser.add_argument('after', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    report = audit(args.before, args.after)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key != 'changes'}, ensure_ascii=False))


if __name__ == '__main__':
    main()
