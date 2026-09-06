"""인벤 원문 HTML의 카드 판본·등급 아이콘·상승 필요치를 손실 없이 추출한다."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections import Counter
from pathlib import Path

from bs4 import BeautifulSoup


def grade(cell):
    """텍스트 추출에서 빠지는 등급 아이콘의 파일 식별자를 읽는다."""
    return [match.group(1).upper() for image in cell.select('img')
            if (match := re.search(r'positionrank\d+_([a-z]+)\d+\.gif', image.get('src', '')))]


def player_label(text):
    """원문에 적힌 두 자리 시즌 연도와 이름을 분리한다."""
    match = re.fullmatch(r'(\d{2})\s+(.+)', text.strip())
    if not match:
        raise ValueError(f'선수 표제 해석 실패: {text}')
    year = int(match.group(1))
    return (1900 if year >= 82 else 2000) + year, match.group(2)


def parse_defense(body):
    """한 카드 행의 복수 포지션과 각 목표 등급을 그대로 보존한다."""
    rows = []
    variants = {'nor1': 'Normal', 'rare1': 'Rare', 'as1': 'AllStar', 'ex1': 'EX'}
    for tr in body.select('tr'):
        cells = tr.find_all('td', recursive=False)
        if len(cells) < 6 or not cells[0].get('class'):
            continue
        kind = cells[0]['class'][0]
        if kind not in variants:
            continue
        year, name = player_label(cells[1].get_text(' ', strip=True))
        event_text = str(cells[1])
        card_id = re.search(r'PlayerLayer\.show\((\d+)', event_text)
        targets = []
        for i in range(4, len(cells) - 1, 2):
            additions = [int(value) for value in re.findall(r'\+(\d+)', cells[i + 1].get_text(' ', strip=True))]
            if additions:
                targets.append({'positions': cells[i].get_text(' ', strip=True).replace(':', '').strip(),
                                'grades': grade(cells[i]), 'additional': additions})
        rows.append({'year': year, 'name': name, 'variant': variants[kind], 'cost': int(cells[0].get_text()),
                     'cardId': card_id.group(1) if card_id else None,
                     'basePositions': cells[2].get_text(' ', strip=True), 'baseGrades': grade(cells[2]),
                     'targets': targets, 'sourceDate': '2010-10-06', 'articleId': 86864})
    return rows


def parse_pitchers(body):
    """카드 이미지와 구종별 목표 등급을 선수 단위로 연결한다."""
    rows = []
    for tr in body.select('tr'):
        headers = tr.find_all('th', recursive=False)
        if len(headers) != 2 or '필요 변화구' not in headers[1].get_text():
            continue
        year, name = player_label(headers[0].get_text(' ', strip=True))
        data = tr.find_next_sibling('tr')
        images = [image['src'] for image in data.select('img') if '/playercard/' in image.get('src', '')]
        if len(images) != 2:
            raise ValueError(f'앞뒷면 이미지 수 불일치: {year} {name}')
        pitches = []
        for item in data.select('.subTable tr'):
            cells = item.find_all('td', recursive=False)
            if len(cells) == 2 and '+' in cells[1].get_text():
                pitches.append({'pitch': cells[0].get_text(' ', strip=True), 'targetGrades': grade(cells[0]),
                                'additional': int(cells[1].get_text().strip().lstrip('+'))})
        rows.append({'year': year, 'name': name, 'cardId': re.search(r'/(\d+)f\.jpg', images[0]).group(1),
                     'images': images, 'thresholds': pitches, 'sourceDate': '2010-10-20', 'articleId': 86866})
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input-dir', type=Path, required=True)
    args = parser.parse_args()
    output = {}
    for article, extract in ((86864, parse_defense), (86866, parse_pitchers)):
        path = args.input_dir / f'article_{article}_full.html'
        raw = path.read_text(encoding='utf-8-sig')
        soup = BeautifulSoup(raw.replace('&#111;n', 'on'), 'lxml')
        body = soup.select_one('#imageCollectDiv')
        if body is None:
            raise ValueError('기사 본문을 찾지 못했습니다.')
        rows = extract(body)
        if not rows:
            raise ValueError(f'기사 {article}에서 선수 행을 찾지 못했습니다.')
        (args.input_dir / f'article_{article}.html').write_text(str(body), encoding='utf-8')
        images = sorted(set(image.get('src') for image in body.select('img') if image.get('src')))
        (args.input_dir / f'article_{article}_images.json').write_text(json.dumps(images, ensure_ascii=False, indent=2), encoding='utf-8')
        output[str(article)] = {'url': f'https://www.inven.co.kr/webzine/news/?news={article}&site=bm',
                                'htmlSha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'rows': rows}
        print(article, 'rows', len(rows), 'variants', dict(Counter(r.get('variant', 'ImagePending') for r in rows)))
        for row in rows:
            if (row['year'],row['name']) in ((2009,'정근우'),(2008,'최정'),(2008,'박재홍')):
                print(json.dumps(row,ensure_ascii=False))
    (args.input_dir / 'article_observations.json').write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
