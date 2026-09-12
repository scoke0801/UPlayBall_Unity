using System.Text;
using Baseball.Presentation.Career;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Popup_OwnerSeasonReview
    {
        private Sequence _bracketSequence;
        private readonly CanvasGroup[] _seriesGroups = new CanvasGroup[3];
        private readonly Vector2[] _seriesPositions = new Vector2[3];
        private Button _skipBracket;
        private string _revealedBracketKey;

        public bool IsBracketRevealing => _bracketSequence != null && _bracketSequence.IsActive();

        private void BuildBracketMotion()
        {
            for (int index = 0; index < _seriesCards.Length; index++)
            {
                _seriesGroups[index] = _seriesCards[index].gameObject.AddComponent<CanvasGroup>();
                _seriesPositions[index] = _seriesCards[index].anchoredPosition;
            }
            _skipBracket = OwnerWorkspaceUiFactory.CreateButton(_modal, "SkipBracket", "대진 바로 보기", SkipBracketReveal);
            SetRect(_skipBracket.GetComponent<RectTransform>(), new Vector2(32f, 122f), new Vector2(206f, 162f));
            OwnerUiButtonSkin.Apply(_skipBracket, OwnerButtonRole.Secondary);
            _skipBracket.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = _close, selectOnRight = _primary, selectOnUp = _tabs[1], selectOnDown = _primary };
            _skipBracket.gameObject.SetActive(false);
        }

        private void PlayBracketReveal()
        {
            if (_page != 1 || _snapshot == null || _skipBracket == null ||
                CareerPresentationSettings.Mode == CareerPresentationMode.ResultOnly) return;
            var key = new StringBuilder().Append(_snapshot.SeasonNumber).Append('|').Append(_snapshot.PlayerTeamSeasonKey);
            foreach (var series in _snapshot.Series)
                key.Append('|').Append(series.SeriesId).Append(':').Append(series.HigherSeedWins).Append(':').Append(series.LowerSeedWins);
            string signature = key.ToString();
            if (signature == _revealedBracketKey) return;
            _revealedBracketKey = signature;
            OwnerPostseasonPresentationData data = OwnerPostseasonPresentationData.Load();
            float factor = CareerPresentationSettings.Mode == CareerPresentationMode.Simplified ? 0.5f : 1f;
            _bracketSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this).Pause();
            int ordinal = 0;
            for (int index = 0; index < _seriesCards.Length; index++)
            {
                if (!_seriesCards[index].gameObject.activeSelf) continue;
                CanvasGroup group = _seriesGroups[index];
                RectTransform card = _seriesCards[index];
                Vector2 destination = _seriesPositions[index];
                group.alpha = 0f;
                card.anchoredPosition = destination + Vector2.right * data.bracketSlide;
                float start = ordinal++ * data.bracketStagger * factor;
                _bracketSequence.Insert(start, DOTween.To(() => group.alpha, value => group.alpha = value,
                    1f, data.bracketFade * factor).SetEase(Ease.OutQuad));
                _bracketSequence.Insert(start, DOTween.To(() => card.anchoredPosition, value => card.anchoredPosition = value,
                    destination, data.bracketFade * factor).SetEase(Ease.OutCubic));
            }
            _skipBracket.gameObject.SetActive(true);
            Navigation navigation = _tabs[1].navigation;
            navigation.selectOnDown = _skipBracket;
            _tabs[1].navigation = navigation;
            _bracketSequence.OnComplete(SkipBracketReveal).Play();
        }

        /// <summary>대진의 위치와 투명도를 즉시 확정한다.</summary>
        public void SkipBracketReveal()
        {
            _bracketSequence?.Kill();
            _bracketSequence = null;
            for (int index = 0; index < _seriesGroups.Length; index++)
            {
                if (_seriesGroups[index] == null) continue;
                _seriesGroups[index].alpha = 1f;
                _seriesCards[index].anchoredPosition = _seriesPositions[index];
            }
            if (_skipBracket == null) return;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _skipBracket.gameObject)
                _primary.Select();
            _skipBracket.gameObject.SetActive(false);
            if (_snapshot != null) ConfigureNavigation();
        }

        private void OnDisable() => SkipBracketReveal();
        private void OnDestroy() => _bracketSequence?.Kill();
    }
}
