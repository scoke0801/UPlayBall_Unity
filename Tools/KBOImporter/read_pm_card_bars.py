"""프야매 카드의 1점=1픽셀 막대를 읽고 별도 숫자 전사 표본으로 검증한다."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image


def read_card(path):
    """서로 다른 세 주사선이 같은 길이인 기본 막대만 판독한다."""
    with Image.open(path) as image:
        if image.size != (228, 317):
            raise ValueError(f'지원하지 않는 카드 크기: {path}')
        image = image.convert('RGB')
        ratings = []
        for row in range(6):
            lengths = []
            for y in (223 + row * 12, 224 + row * 12, 225 + row * 12):
                length = 0
                for x in range(51, 172):
                    if min(image.getpixel((x, y))) < 180:
                        break
                    length += 1
                lengths.append(length)
            if len(set(lengths)) != 1 or not 1 <= lengths[0] <= 100:
                raise ValueError(f'막대 판독 불확실: {path.name}/{row}: {lengths}')
            ratings.append(lengths[0])
        return ratings


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report-dir', type=Path, required=True)
    parser.add_argument('--image-dir', type=Path, required=True)
    args = parser.parse_args()
    observations = json.loads((args.report_dir / 'article_observations.json').read_text(encoding='utf-8'))
    transcriptions = json.loads((args.report_dir / 'pitch_card_readings.json').read_text(encoding='utf-8'))
    mismatches = []
    for row in transcriptions:
        path = args.image_dir / f"{row['cardId']}f.jpg"
        values = read_card(path)
        row['imageSha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        if values != row['attributes']:
            mismatches.append({'cardId': row['cardId'], 'visual': row['attributes'], 'bar': values})
    if mismatches:
        print(json.dumps(mismatches, ensure_ascii=False, indent=2))
        raise ValueError('숫자 전사값과 막대 판독이 다릅니다. 원본을 다시 확인해야 합니다.')
    (args.report_dir / 'pitch_card_readings.json').write_text(json.dumps(transcriptions, ensure_ascii=False, indent=2), encoding='utf-8')
    readings, rejected = [], []
    for row in observations['86864']['rows']:
        if row['variant'] != 'Normal':
            continue
        path = args.image_dir / f"{row['cardId']}f.jpg"
        try:
            values = read_card(path)
        except ValueError as error:
            rejected.append({'cardId': row['cardId'], 'reason': str(error)})
            continue
        readings.append({**{k: row[k] for k in ('year', 'name', 'cardId', 'variant')}, 'attributes': values,
                         'articleCost': row['cost'], 'articleDate': row['sourceDate'],
                         'sourceUrl': f"https://img.inven.co.kr/image/site_image/bm/dataninfo/playercard/{row['cardId']}f.jpg",
                         'imageSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                         'readingMethod': 'ThreeScanlineBarLength',
                         'snapshotPolicy': 'LiveImageLinkedBy2010Article_NotArchivedBytes'})
    output = {'validatedVisualCards': len(transcriptions), 'readings': readings, 'rejected': rejected}
    (args.report_dir / 'hitter_card_readings.json').write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'validatedVisualCards': len(transcriptions), 'hitterCards': len(readings), 'rejected': rejected}, ensure_ascii=False))


if __name__ == '__main__':
    main()
