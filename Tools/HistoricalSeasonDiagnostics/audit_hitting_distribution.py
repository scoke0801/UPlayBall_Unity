"""후보 타격 능력치의 코스트별 분포를 비교한다. 코스트는 산출 입력이 아니라 검증 축이다."""
import argparse
import json
import statistics
from pathlib import Path


def summarize(root):
    """각 선수 시즌을 한 번씩 집계해 타격 세 속성 평균의 평균·90백분위를 반환한다."""
    read = lambda path: json.loads(path.read_text(encoding='utf-8-sig'))
    manifest = read(root/'manifest.json')
    groups = {}
    for entry in manifest['years']:
        for player in read(root/entry['path'])['playerSeasons']:
            if player['playerType'] != 'Hitter':
                continue
            values = player['baseAttributes']
            groups.setdefault(player['cost'], []).append(sum(values[i] for i in (0, 1, 5))/3)
    return dict(contentHash=manifest['sourceManifest']['contentHash'], groups={
        str(cost): dict(count=len(values), mean=statistics.mean(values),
                       p90=sorted(values)[int(.9*(len(values)-1))])
        for cost, values in sorted(groups.items())})


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('before', type=Path)
    parser.add_argument('after', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    result = dict(before=summarize(args.before), after=summarize(args.after))
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))
