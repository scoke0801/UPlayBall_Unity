"""다른 게임에서 유입된 카드 관측을 출처 단위로 격리한다."""
import json
from pathlib import Path
from urllib.parse import parse_qs, urlsplit

POLICY = json.loads(Path(__file__).with_suffix('.json').read_text(encoding='utf-8'))


def rejection_reason(source):
    """선수명·가격과 무관하게 기사 식별자로 다른 게임 출처를 판별한다."""
    source = str(source or '')
    for scheme in ('https://', 'http://'):
        start = source.find(scheme)
        if start >= 0:
            source = source[start:]
            break
    parsed = urlsplit(source)
    host = (parsed.hostname or '').lower()
    query = parse_qs(parsed.query)
    for entry in POLICY['rejectedArticles']:
        domain = entry['domain']
        if (host == domain or host.endswith('.' + domain)) and entry['newsId'] in query.get('news', []):
            return entry['reason']
    return None


def card_rejection_reason(card):
    """활성 관측값의 출처만 검사하며 과거 supersedes 이력은 보존한다."""
    return next((reason for source in card.get('sources', {}).values()
                 if (reason := rejection_reason(source))), None)


def validate_training_sources(rows, cards):
    """옛 캐시를 직접 학습기에 넘겨도 격리 출처가 다시 학습되지 않게 거부한다."""
    if any(rejection_reason(row.get('origin')) for row in rows) or any(card_rejection_reason(card) for card in cards):
        raise ValueError('다른 게임 출처가 포함된 학습 캐시입니다. 출처 정책을 적용해 입력을 다시 수집해야 합니다.')
