using Baseball.Core.Growth;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>확정된 합성 블록을 순서대로 공개하고 위에서 아래로 등급 빛을 흘린다.</summary>
    public sealed class UISkillFusionReveal : MaskableGraphic
    {
        [SerializeField] private float slotDelay = .18f;
        [SerializeField] private float revealDuration = .95f;
        [SerializeField] private float sweepHeight = 24f;
        private RectTransform _block;
        private CanvasGroup _blockGroup;
        private CanvasGroup _nameGroup;
        private CanvasGroup _effectGroup;
        private Color _gradeColor;
        private float _startedAt;
        private float _progress = 1f;
        private int _slot;

        /// <summary>저장된 결과의 공개 시각을 공유해 화면을 다시 그려도 연출을 되감지 않는다.</summary>
        public void Bind(RectTransform block, Text nameLabel, Text effectLabel,
            SkillBlockRarity rarity, float startedAt, int slot)
        {
            raycastTarget = false;
            _block = block;
            _blockGroup = block.gameObject.AddComponent<CanvasGroup>();
            _nameGroup = nameLabel.gameObject.AddComponent<CanvasGroup>();
            _effectGroup = effectLabel.gameObject.AddComponent<CanvasGroup>();
            _gradeColor = SkillBlockVisual.GetRarityColor(rarity);
            _slot = slot;
            StartReveal(startedAt);
        }

        /// <summary>화면 생성이 끝난 시각부터 슬롯별 공개를 시작한다.</summary>
        public void StartReveal(float startedAt)
        {
            _startedAt = startedAt + _slot * slotDelay;
            ApplyFrame();
        }

        private void Update()
        {
            if (_block != null && _progress < 1f) ApplyFrame();
        }

        private void ApplyFrame()
        {
            // 게임 속도와 무관하게 재생하고, 슬롯 재생성 시에도 같은 시각을 소비한다.
            _progress = Mathf.Clamp01((Time.realtimeSinceStartup - _startedAt) / Mathf.Max(.1f, revealDuration));
            float reveal = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_progress / .32f));
            _blockGroup.alpha = reveal;
            float scale = _progress < .32f ? Mathf.Lerp(.82f, 1.06f, reveal)
                : Mathf.Lerp(1.06f, 1f, Mathf.SmoothStep(0, 1, (_progress - .32f) / .4f));
            _block.localScale = Vector3.one * scale;
            _nameGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_progress - .3f) / .25f));
            _effectGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_progress - .48f) / .25f));
            SetVerticesDirty();
        }

        /// <summary>타일 영역 안에서 가장자리가 부드러운 빛 띠와 잔광을 그린다.</summary>
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_progress <= 0f || _progress >= 1f) return;
            Rect bounds = rectTransform.rect;
            float strength = Mathf.Sin(_progress * Mathf.PI);
            float center = Mathf.Lerp(bounds.yMax + sweepHeight, bounds.yMin - sweepHeight, _progress);
            const int rows = 32;
            const int columns = 12;
            for (int row = 0; row <= rows; row++)
            {
                float y = Mathf.Lerp(bounds.yMin, bounds.yMax, row / (float)rows);
                float distance = Mathf.Abs(y - center) / Mathf.Max(1f, sweepHeight);
                float band = Mathf.Clamp01(1f - distance);
                for (int column = 0; column <= columns; column++)
                {
                    float fraction = column / (float)columns;
                    float edge = Mathf.Sin(fraction * Mathf.PI);
                    Color tint = Color.Lerp(_gradeColor, Color.white, band * band * .8f);
                    tint.a = strength * edge * edge * (.045f + band * band * .65f);
                    mesh.AddVert(new Vector3(Mathf.Lerp(bounds.xMin, bounds.xMax, fraction), y), tint, Vector2.zero);
                    if (row == rows || column == columns) continue;
                    int index = row * (columns + 1) + column;
                    mesh.AddTriangle(index, index + columns + 1, index + 1);
                    mesh.AddTriangle(index + 1, index + columns + 1, index + columns + 2);
                }
            }
        }
    }
}
