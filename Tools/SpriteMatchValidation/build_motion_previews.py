"""Unity에서 캡처한 연속 프레임으로 검수용 재생 GIF를 만든다."""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'output/sprite-sheet-ingame/python-deps'))
from PIL import Image


def main():
    report = ROOT / 'output/sprite-sheet-validation'
    for hand, suffix in [('Right', 'R'), ('Left', 'L')]:
        paths = sorted((report / ('motion-' + hand)).glob('*.png'))
        if len(paths) != 36:
            raise ValueError('Unity 투구 캡처 36프레임이 필요합니다: ' + hand)
        clip_path = ROOT / 'output/sprite-sheet-ingame/Processed' / ('Pitcher.Pitch.' + suffix)
        clip = json.loads((clip_path / ('Pitcher.Pitch.' + suffix + '.json')).read_text(encoding='utf-8'))
        duration = sum(frame['durationMs'] for frame in clip['frames'])
        frames = []
        for path in paths:
            with Image.open(path) as image:
                frames.append(image.convert('RGB'))
        frames[0].save(report / ('motion-' + hand + '.gif'), save_all=True, append_images=frames[1:],
                       duration=round(duration / len(frames) / 10) * 10, loop=0, disposal=2)
        print(hand, len(frames), 'frames')
    for folder in sorted(report.glob('replay-*')):
        if not folder.is_dir():
            continue
        paths = sorted(folder.glob('*.png'))
        if not paths:
            continue
        frames = []
        for path in paths:
            with Image.open(path) as image:
                preview = image.convert('RGB')
                preview.thumbnail((960, 540))
                frames.append(preview)
        frames[0].save(report / (folder.name + '.gif'), save_all=True, append_images=frames[1:],
                       duration=40, loop=0, disposal=2)
        print(folder.name, len(frames), 'frames, 24 fps approximation')


if __name__ == '__main__':
    main()
