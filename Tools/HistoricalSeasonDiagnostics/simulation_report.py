"""대량 결과의 선수별 기록은 읽는 즉시 정규시즌 합계로 줄여 분석 메모리를 제한한다."""
import hashlib
import json


def summarize_row(value):
    """경기 체크섬·팀 기록·순번 기록을 보존하고 중간·PS·올스타 선수 통계의 중복을 제외한다."""
    if 'statistics' not in value or 'seed' not in value or 'year' not in value:
        return value
    fields = ('HomeRuns', 'Walks', 'Strikeouts', 'FieldingErrors')
    totals = dict.fromkeys(fields, 0)
    for player in value['statistics']:
        if player['IsFirstHalf'] or player['IsPostseason'] or player['IsAllStarGame']:
            continue
        for field in fields:
            totals[field] += player[field]
    if 'regularTotals' in value and value['regularTotals'] != totals:
        raise ValueError('선수 통계와 정규시즌 합계가 다릅니다.')
    value['regularTotals'] = totals
    del value['statistics']
    return value


def read(path):
    """원시 보고서는 보존하고 분석용 메모리에서만 선수별 상세를 접는다."""
    with open(path, encoding='utf-8-sig') as stream:
        return json.load(stream, object_hook=summarize_row)


def file_hash(path):
    """입력 파일 전체를 별도 메모리에 복제하지 않고 SHA-256을 구한다."""
    with open(path, 'rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()
