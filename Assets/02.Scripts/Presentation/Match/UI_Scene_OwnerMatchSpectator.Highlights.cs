using Baseball.Simulation.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    public sealed partial class UI_Scene_OwnerMatchSpectator
    {
        private RectTransform _pitchContext;
        private RectTransform _highlightInset;
        private CanvasGroup _highlightOpacity;
        private Image _highlightImage;
        private Text _highlightCaption;
        private OwnerMatchHighlightConfig _highlightConfig;
        private Sprite[] _highlightSprites;
        private OwnerMatchHighlightKind _highlightKind;
        private float _highlightElapsed, _highlightDuration;

        private void OnDisable() => ClearHighlightInset();

        private RectTransform BuildPitchContext(RectTransform sidebar)
        {
            _pitchContext = new GameObject("PitchContext", typeof(RectTransform)).GetComponent<RectTransform>();
            _pitchContext.SetParent(sidebar, false);
            Place(_pitchContext, 0, 0, 504, 602);
            return _pitchContext;
        }

        private void BuildHighlightInset(RectTransform sidebar)
        {
            _highlightConfig = OwnerMatchHighlightConfig.Load();
            _highlightSprites = new Sprite[_highlightConfig.images.Length];
            for (int index = 0; index < _highlightSprites.Length; index++)
                _highlightSprites[index] = Resources.Load<Sprite>(_highlightConfig.images[index].resourcePath);
            // 투구 상세와 같은 영역을 교대로 사용한다. 점수·투타 이름·감독 설명·조작 영역은 계속 표시한다.
            _highlightInset = Panel("HighlightInset", sidebar, Paper, 16, 152, 472, 338);
            _highlightOpacity = _highlightInset.gameObject.AddComponent<CanvasGroup>();
            _highlightOpacity.blocksRaycasts = false;
            _highlightOpacity.interactable = false;
            Label("Heading", _highlightInset, _highlightConfig.heading, 15, 0, 0, 472, 28, Blue);
            var picture = new GameObject("Picture", typeof(RectTransform), typeof(Image));
            picture.transform.SetParent(_highlightInset, false);
            Place((RectTransform)picture.transform, 0, 34, 472, 265.5f);
            _highlightImage = picture.GetComponent<Image>();
            _highlightImage.preserveAspect = true;
            _highlightImage.raycastTarget = false;
            _highlightCaption = Label("Caption", _highlightInset, "", 17, 0, 306, 472, 30, Ink);
            ClearHighlightInset();
        }

        private bool TryShowHighlightInset(in MatchEvent revealed)
        {
            if (_session == null || _session.State.IsComplete || _session.State.ViewingMode == OwnerMatchViewingMode.ResultOnly)
                return false;
            OwnerMatchHighlightKind kind = OwnerMatchHighlightCue.Resolve(revealed);
            return TryPresentHighlightInset(kind, _session.State.Speed);
        }

        private bool TryPresentHighlightInset(OwnerMatchHighlightKind kind, OwnerMatchPlaybackSpeed speed)
        {
            if (kind == OwnerMatchHighlightKind.None) return false;
            for (int index = 0; index < _highlightConfig.images.Length; index++)
            {
                OwnerMatchHighlightImage definition = _highlightConfig.images[index];
                if (definition.kind != kind || _highlightSprites[index] == null) continue;
                _highlightImage.sprite = _highlightSprites[index];
                _highlightCaption.text = definition.caption;
                _highlightKind = kind;
                _highlightElapsed = 0;
                _highlightDuration = _highlightConfig.GetDuration(speed);
                _highlightOpacity.alpha = 0;
                _pitchContext.gameObject.SetActive(false);
                _highlightInset.gameObject.SetActive(true);
                return true;
            }
            return false;
        }

        private bool AdvanceHighlightInset(float deltaSeconds)
        {
            if (_highlightKind == OwnerMatchHighlightKind.None) return false;
            _highlightElapsed += Mathf.Max(0, deltaSeconds);
            if (_highlightElapsed >= _highlightDuration)
            {
                ClearHighlightInset();
                return false;
            }
            float fade = Mathf.Min(Mathf.Max(0.001f, _highlightConfig.fadeSeconds), _highlightDuration * 0.5f);
            _highlightOpacity.alpha = Mathf.Clamp01(Mathf.Min(_highlightElapsed, _highlightDuration - _highlightElapsed) / fade);
            return true;
        }

        private void ClearHighlightInset()
        {
            _highlightKind = OwnerMatchHighlightKind.None;
            _highlightElapsed = 0;
            if (_highlightInset != null) _highlightInset.gameObject.SetActive(false);
            if (_pitchContext != null) _pitchContext.gameObject.SetActive(true);
        }
    }
}
