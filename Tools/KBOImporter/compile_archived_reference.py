"""보존된 일반 카드 행을 같은 연도·구단·선수 타입의 정본 보정으로 연결한다."""

import argparse
import csv
import hashlib
import json
from pathlib import Path

from calibrate_annual_reference import ARCHIVE_NAMES, archive_kind


def compile_cards(cards, seasons):
    """모호한 연결과 특수 카드를 제외하고 관측된 능력치만 반환한다."""
    accepted, rejected = [], []
    for card in cards:
        if card['CardType'] != '일반' or card['CardTypeCss'] != 'playerCard1':
            rejected.append(dict(cardId=card['CardId'], name=card['Name'], reason='SpecialOrUnknownEdition'))
            continue
        kind = archive_kind(card['Position'])
        candidates = [s for s in seasons if s['originYear'] == card['SeasonYear']
            and s['originFranchiseId'] == card['Team'] and s['playerType'] == kind
            and card['Name'] in s['sourceReferenceNames']]
        if len(candidates) != 1:
            rejected.append(dict(cardId=card['CardId'], name=card['Name'], reason='UnresolvedIdentity', candidates=len(candidates)))
            continue
        values = {target: card['Stats'][source] for source, target in ARCHIVE_NAMES[kind].items()
            if source in card['Stats']}
        values['Cost'] = card['Cost']
        accepted.append(dict(playerSeasonId=candidates[0]['playerSeasonId'], playerType=kind,
            originYear=card['SeasonYear'], values=values,
            sources={target: f"{card.get('SourceKind', 'ArchivedWebCard')}:{card['SourceUrl']}:CardId={card['CardId']}" for target in values}))
        if card.get('Supersedes'):
            accepted[-1]['supersedes'] = card['Supersedes']
    if len({c['playerSeasonId'] for c in accepted}) != len(accepted):
        raise ValueError('같은 시즌의 서로 다른 카드 버전을 먼저 검토해야 합니다.')
    return sorted(accepted, key=lambda c: c['playerSeasonId']), rejected


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cards', type=Path, required=True)
    parser.add_argument('--article-cards', type=Path)
    parser.add_argument('--editor-year', type=Path, required=True)
    parser.add_argument('--team', required=True)
    parser.add_argument('--research-output', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
    cards = [c for c in read(args.cards) if c['Team'] == args.team]
    if args.article_cards:
        cards.extend(c for c in read(args.article_cards) if c['Team'] == args.team)
    accepted, rejected = compile_cards(cards, read(args.editor_year)['playerSeasons'])
    args.research_output.mkdir(parents=True, exist_ok=True)
    (args.research_output / 'archive-cards.json').write_text(json.dumps(cards, ensure_ascii=False, indent=2), encoding='utf-8')
    stat_fields = sorted({s for c in cards for s in c['Stats']})
    fields = sorted({k for c in cards for k in c if k not in ('Stats', 'Supersedes')}) + stat_fields
    with (args.research_output / 'archive-cards.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        for card in cards:
            writer.writerow({**{k: v for k, v in card.items() if k not in ('Stats', 'Supersedes')}, **card['Stats']})
    payload = dict(version='annual-general-reference-v1', maximumCardYear=2013,
        cards=accepted, researchCards=cards, rejected=rejected)
    data = (json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n').encode('utf-8')
    args.output.write_bytes(data)
    print(json.dumps(dict(cardCount=len(accepted), rejected=rejected,
        contentSha256=hashlib.sha256(data).hexdigest()), ensure_ascii=False))


if __name__ == '__main__':
    main()
