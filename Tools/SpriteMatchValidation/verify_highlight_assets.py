"""구단주 삽입 컷의 생성 원본·경기 리소스 일치와 이미지 형식을 검사한다."""
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
try:
    from PIL import Image
except ImportError:
    sys.path.insert(0, str(ROOT / 'output/highlight-tools'))
    from PIL import Image


def main():
    resource_root = ROOT / 'Assets/10.Datas/Resources'
    config = json.loads((resource_root / 'UI/OwnerMatch/Highlights/HighlightPresentation.json').read_text(encoding='utf-8'))
    kinds = set()
    records = []
    for definition in config['images']:
        kind = definition['kind']
        if kind in kinds or kind not in range(1, 7):
            raise ValueError('중복 또는 잘못된 하이라이트 종류')
        kinds.add(kind)
        runtime = resource_root / (definition['resourcePath'] + '.png')
        source = ROOT / 'docs/design/sprite_sheet_ingame/highlight-insets-v1' / (runtime.stem + '-source.png')
        source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
        if source_hash != hashlib.sha256(runtime.read_bytes()).hexdigest():
            raise ValueError('생성 원본과 런타임 이미지 불일치: ' + runtime.name)
        with Image.open(runtime) as image:
            width, height = image.size
            if abs(width / height - 16 / 9) > 0.02:
                raise ValueError('16:9 삽입 컷 비율 불일치: ' + runtime.name)
            # 이번 산출물은 배경까지 포함한 삽입 장면이며 크로마키/투명 선수 시트가 아니다.
            alpha = image.convert('RGBA').getchannel('A').getextrema()
            if alpha != (255, 255):
                raise ValueError('완성 삽입 장면에 투명 영역 존재: ' + runtime.name)
        records.append(dict(kind=kind, file=str(runtime.relative_to(ROOT)), sourceSha256=source_hash,
                            width=width, height=height, isOpaque=True))
    if kinds != set(range(1, 7)):
        raise ValueError('하이라이트 여섯 종류가 모두 필요합니다.')
    report = ROOT / 'output/sprite-sheet-validation/highlight-image-qa.json'
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('6/6 원본 일치·16:9 비율·불투명 배경 검증 통과')


if __name__ == '__main__':
    main()
