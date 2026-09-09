"""시트 입력 계약과 실제 크로마키 경계 보존을 검증한다."""
import copy
import json
import tempfile
import unittest
from pathlib import Path

import process_sheets as pipeline
from PIL import Image


class SpriteSheetPipelineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = json.loads((Path(__file__).parent / 'sprite_sheet_sources.json').read_text(encoding='utf-8-sig'))

    def setUp(self):
        self.sheet = copy.deepcopy(self.manifest['sheets'][0])

    def test_all_source_hashes_and_manifest_contracts(self):
        for sheet in self.manifest['sheets']:
            with self.subTest(clip=sheet['sheetId']):
                pipeline.validate_sheet(sheet)
                self.assertEqual(pipeline.digest(pipeline.ROOT / sheet['sourceFile']), sheet['sourceHash'])

    def test_non_divisible_grid_covers_every_pixel_once(self):
        edges = pipeline.boundaries(1774, 5)
        self.assertEqual(edges, [0, 355, 710, 1064, 1419, 1774])
        self.assertEqual(sum(b - a for a, b in zip(edges, edges[1:])), 1774)

    def test_rejects_missing_zero_and_non_finite_duration(self):
        for durations in ([], [0] * 12, [float('nan')] * 12, [float('inf')] * 12):
            self.sheet['durationsMs'] = durations
            with self.assertRaises(ValueError):
                pipeline.validate_sheet(self.sheet)

    def test_rejects_duplicate_and_unknown_events(self):
        marker = self.sheet['events'][0]
        self.sheet['events'].append(copy.deepcopy(marker))
        with self.assertRaises(ValueError):
            pipeline.validate_sheet(self.sheet)
        self.sheet['events'] = [{'name': 'Unknown', 'frameIndex': 1}]
        with self.assertRaises(ValueError):
            pipeline.validate_sheet(self.sheet)

    def test_rejects_throw_before_catch_without_transfer_marker(self):
        self.sheet['events'] = [{'name': 'ThrowRelease', 'frameIndex': 1}, {'name': 'GloveContact', 'frameIndex': 2}]
        with self.assertRaises(ValueError):
            pipeline.validate_sheet(self.sheet)

    def test_rejects_unreviewed_production(self):
        self.sheet.update(reviewStatus='Approved', handedness='NeedsReview')
        with self.assertRaises(ValueError):
            pipeline.validate_sheet(self.sheet)

    def test_authored_pivots_must_reference_valid_cells_and_coordinates(self):
        self.sheet['framePivots'] = {'0': {'x': 0.5, 'groundY': 0.85}}
        pipeline.validate_sheet(self.sheet)
        for value in ({'99': {'x': 0.5, 'groundY': 0.8}}, {'0': {'x': 0.5, 'groundY': float('nan')}},
                      {'0': {'x': 0.5, 'groundY': 1.2}}):
            self.sheet['framePivots'] = value
            with self.assertRaises(ValueError):
                pipeline.validate_sheet(self.sheet)

    def test_chroma_removal_preserves_uniform_and_thin_bat(self):
        # 실제 제거 도구를 거치는 합성 표본: 흰 유니폼·갈색 배트·파란 모자와 녹색 혼합 경계.
        with tempfile.TemporaryDirectory(prefix='sprite-golden-') as directory:
            source = Path(directory) / 'source.png'
            target = Path(directory) / 'rgba.png'
            image = Image.new('RGB', (32, 32), (3, 249, 5))
            for y in range(8, 24):
                for x in range(8, 24):
                    image.putpixel((x, y), (245, 245, 245))
            for y in range(8, 24):
                image.putpixel((25, y), (110, 65, 30))
                image.putpixel((7, y), (120, 220, 120))
            image.putpixel((16, 12), (20, 45, 180))
            image.save(source)
            key = pipeline.estimate_key(image, 0.03)
            result = pipeline.remove_background(source, target, key, self.manifest['chroma'])
            self.assertEqual(result.getpixel((0, 0))[3], 0)
            self.assertEqual(result.getpixel((16, 16))[3], 255)
            self.assertEqual(result.getpixel((16, 12))[3], 255)
            self.assertEqual(result.getpixel((25, 16))[3], 255)
            r, g, b, alpha = result.getpixel((7, 16))
            self.assertGreater(alpha, 0)
            self.assertLess(alpha, 255)
            self.assertLessEqual(g - max(r, b), self.manifest['chroma']['spillTolerance'])


if __name__ == '__main__':
    unittest.main()
