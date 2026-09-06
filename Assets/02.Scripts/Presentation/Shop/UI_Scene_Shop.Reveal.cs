using System;
using System.Collections;
using Baseball.Core.Shop;
using Baseball.Core.Players;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.Owner;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Shop
{
    public sealed partial class UI_Scene_Shop
    {
        private Button _revealSkipButton;
        private Button _revealModeButton;
        private Coroutine _revealCoroutine;
        private ShopRevealPlan _revealPlan;
        private ShopRevealPresentationMode _revealMode = ShopRevealPresentationMode.HighlightsOnly;
        private bool _isRevealPlaying;
        private Image _revealPanel;
        private RectTransform _revealCardGrid;
        private PlayerMiniCardModel[] _revealPlayerCards;
        private RectTransform[] _revealSlots = Array.Empty<RectTransform>();
        private CanvasGroup[] _revealFaces = Array.Empty<CanvasGroup>();
        private CanvasGroup[] _revealBacks = Array.Empty<CanvasGroup>();
        [SerializeField, Min(1)] private int _revealColumns = 5;
        [SerializeField, Min(0f)] private float _revealCardGap = 28f;
        [SerializeField, Min(.01f)] private float _revealFlipDuration = .36f;

        private bool IsPlayerReveal => _revealPlan?.Theme == ShopRevealTheme.ScoutingReport;

        private void PrepareRevealCards()
        {
            if (_revealCardGrid != null)
            {
                _revealCardGrid.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_revealCardGrid.gameObject);
                else DestroyImmediate(_revealCardGrid.gameObject);
            }
            _revealSlots = Array.Empty<RectTransform>();
            _revealFaces = Array.Empty<CanvasGroup>();
            _revealBacks = Array.Empty<CanvasGroup>();
            _revealPanel.color = Color.clear;
            _revealBody.gameObject.SetActive(false);
            _revealArtwork.gameObject.SetActive(false);

            _revealCardGrid = OwnerRuntimeUiFactory.CreateRect("RevealCardGrid", _revealPanel.rectTransform);
            OwnerRuntimeUiFactory.SetAnchors(_revealCardGrid,
                new Vector2(0f, .12f), new Vector2(1f, .89f), Vector2.zero, Vector2.zero);
            int count = _revealPlan.Items.Length;
            _revealSlots = new RectTransform[count];
            _revealFaces = new CanvasGroup[count];
            _revealBacks = new CanvasGroup[count];
            for (int index = 0; index < count; index++) CreateRevealCard(index);
            LayoutRevealCards();
        }

        private void CreateRevealCard(int index)
        {
            ShopGrantedItem item = _revealPlan.Items[index].Item;
            RectTransform slot = OwnerRuntimeUiFactory.CreateRect("RevealSlot_" + index, _revealCardGrid);
            slot.sizeDelta = new Vector2(PlayerMiniCardView.PreferredWidth, PlayerMiniCardView.PreferredHeight);
            _revealSlots[index] = slot;
            Color accent = GetRevealAccent(item.HighestIntensity);
            Image back = OwnerRuntimeUiFactory.CreateImage("CardBack", slot, new Color32(10, 21, 37, 255));
            OwnerRuntimeUiFactory.Stretch(back.rectTransform);
            var outline = back.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(89, 116, 146, 255);
            outline.effectDistance = new Vector2(1f, -1f);
            Text backLabel = OwnerRuntimeUiFactory.CreateText("BackLabel", back.rectTransform,
                GetRevealCardBackLabel() + "\n\n" + (index + 1).ToString("00"), 19, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color32(174, 196, 219, 255));
            OwnerRuntimeUiFactory.Stretch(backLabel.rectTransform);
            _revealBacks[index] = back.gameObject.AddComponent<CanvasGroup>();

            _revealFaces[index] = IsPlayerReveal
                ? CreatePlayerRevealFace(slot, item, accent, index)
                : CreateItemRevealFace(slot, item, accent);
            _revealFaces[index].alpha = 0f;
            _revealFaces[index].blocksRaycasts = false;
        }

        private CanvasGroup CreatePlayerRevealFace(
            RectTransform slot,
            ShopGrantedItem item,
            Color accent,
            int index)
        {
            PlayerMiniCardModel model = _revealPlayerCards != null && index < _revealPlayerCards.Length
                ? _revealPlayerCards[index] : null;
            if (model != null && !string.Equals(model.PlayerId, item.ItemId, StringComparison.Ordinal)) model = null;
            // 표시 스냅샷이 없는 호출에서도 지급 결과를 보존하며 선수 능력이나 포지션을 만들어내지 않는다.
            model ??= new PlayerMiniCardModel(item.ItemId, item.DisplayName, string.Empty,
                string.Empty, string.Empty, item.GradeLabel, item.IsNew ? "신규 영입" : "중복 획득",
                isInteractable: false);
            var styledModel = new PlayerMiniCardModel(model.PlayerId, model.DisplayName,
                model.PositionLabel, model.YearLabel, model.CostLabel, model.EditionLabel,
                item.IsNew ? "신규 영입" : "중복 획득", model.PortraitAssetKey,
                "#" + ColorUtility.ToHtmlStringRGB(accent),
                item.HighestIntensity >= ShopRevealIntensity.Rare
                    ? PlayerMiniCardVisualState.Highlighted : PlayerMiniCardVisualState.Normal,
                isInteractable: false, stats: model.Stats, frameEdition: model.FrameEdition, cost: model.Cost);
            PlayerMiniCardView face = PlayerMiniCardView.CreateRuntime(slot, "PlayerCard");
            OwnerRuntimeUiFactory.Stretch((RectTransform)face.transform);
            Enum.TryParse(model.PortraitAssetKey, out PlayerPosition position);
            face.Bind(styledModel, PlayerPortraitSprites.GetDefault(position));
            return face.GetComponent<CanvasGroup>();
        }

        private CanvasGroup CreateItemRevealFace(RectTransform slot, ShopGrantedItem item, Color accent)
        {
            string faceName = _revealPlan.Theme == ShopRevealTheme.DevelopmentAnalysis
                ? "SkillBlockCard"
                : "TacticCard";
            Image face = OwnerRuntimeUiFactory.CreateImage(faceName, slot, new Color32(20, 31, 46, 255));
            OwnerRuntimeUiFactory.Stretch(face.rectTransform);
            var outline = face.gameObject.AddComponent<Outline>();
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(3f, -3f);

            Image categoryBand = OwnerRuntimeUiFactory.CreateImage(
                "CategoryBand", face.rectTransform, new Color32(8, 18, 32, 248));
            OwnerRuntimeUiFactory.SetAnchors(categoryBand.rectTransform,
                new Vector2(.04f, .86f), new Vector2(.96f, .98f), Vector2.zero, Vector2.zero);
            Text category = OwnerRuntimeUiFactory.CreateText(
                "Category", face.rectTransform, GetRevealCardBackLabel(), 12, FontStyle.Bold,
                TextAnchor.MiddleCenter, accent);
            OwnerRuntimeUiFactory.SetAnchors(category.rectTransform,
                new Vector2(.08f, .86f), new Vector2(.92f, .98f), Vector2.zero, Vector2.zero);

            string artworkKey = string.IsNullOrEmpty(item.ArtworkKey)
                ? _revealPlan.Theme == ShopRevealTheme.TacticalLab
                    ? TacticCardArtwork.CommonKey
                    : ShopArtwork.SkillPackKey
                : item.ArtworkKey;
            Image artworkFrame = OwnerRuntimeUiFactory.CreateImage(
                "ArtworkFrame", face.rectTransform, new Color32(12, 23, 37, 255));
            OwnerRuntimeUiFactory.SetAnchors(artworkFrame.rectTransform,
                new Vector2(.08f, .37f), new Vector2(.92f, .84f), Vector2.zero, Vector2.zero);
            artworkFrame.gameObject.AddComponent<RectMask2D>();
            RawImage artwork = ShopArtwork.Create(artworkFrame.rectTransform, "CardArtwork", artworkKey, Color.white);
            OwnerRuntimeUiFactory.Stretch(artwork.rectTransform,
                new Vector2(3f, 3f), new Vector2(-3f, -3f));
            var artworkAspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
            artworkAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            if (artwork.texture != null)
                artworkAspect.aspectRatio = (float)artwork.texture.width / artwork.texture.height;

            Image informationBand = OwnerRuntimeUiFactory.CreateImage(
                "InformationBand", face.rectTransform, new Color32(8, 18, 32, 248));
            OwnerRuntimeUiFactory.SetAnchors(informationBand.rectTransform,
                new Vector2(.04f, .02f), new Vector2(.96f, .35f), Vector2.zero, Vector2.zero);
            Text grade = OwnerRuntimeUiFactory.CreateText(
                "Grade", face.rectTransform,
                string.IsNullOrEmpty(item.GradeLabel) ? "일반" : item.GradeLabel,
                14, FontStyle.Bold, TextAnchor.MiddleCenter, accent);
            OwnerRuntimeUiFactory.SetAnchors(grade.rectTransform,
                new Vector2(.08f, .27f), new Vector2(.92f, .35f), Vector2.zero, Vector2.zero);
            Text title = OwnerRuntimeUiFactory.CreateText(
                "Title", face.rectTransform, item.DisplayName, 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, Color.white);
            OwnerRuntimeUiFactory.SetAnchors(title.rectTransform,
                new Vector2(.08f, .11f), new Vector2(.92f, .27f), Vector2.zero, Vector2.zero);
            Text status = OwnerRuntimeUiFactory.CreateText(
                "Status", face.rectTransform, DescribeRevealItemStatus(item), 11, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color32(214, 226, 239, 255));
            OwnerRuntimeUiFactory.SetAnchors(status.rectTransform,
                new Vector2(.08f, .03f), new Vector2(.92f, .11f), Vector2.zero, Vector2.zero);
            return face.gameObject.AddComponent<CanvasGroup>();
        }

        private void LayoutRevealCards()
        {
            if (_revealCardGrid == null || _revealSlots.Length == 0) return;
            int columns = Mathf.Min(Mathf.Max(1, _revealColumns), _revealSlots.Length);
            int rows = Mathf.CeilToInt((float)_revealSlots.Length / columns);
            float width = _revealCardGrid.rect.width / columns;
            float height = _revealCardGrid.rect.height / rows;
            float scale = Mathf.Max(.01f, Mathf.Min(
                (width - _revealCardGap) / PlayerMiniCardView.PreferredWidth,
                (height - _revealCardGap) / PlayerMiniCardView.PreferredHeight));
            // 한 장 뽑기는 중앙에서, 묶음은 왼쪽 위부터 같은 목표 위치를 끝까지 유지한다.
            scale = Mathf.Min(scale, 1.7f);
            for (int index = 0; index < _revealSlots.Length; index++)
            {
                int row = index / columns;
                int rowCount = Mathf.Min(columns, _revealSlots.Length - row * columns);
                var anchor = new Vector2(.5f + (index % columns - (rowCount - 1) * .5f) / columns,
                    1f - (row + .5f) / rows);
                RectTransform slot = _revealSlots[index];
                slot.anchorMin = slot.anchorMax = anchor;
                slot.anchoredPosition = Vector2.zero;
                slot.localScale = Vector3.one * scale;
            }
        }

        private IEnumerator PlayCardRevealSequence()
        {
            yield return WaitForReveal(.3f);
            for (int index = 0; index < _revealFaces.Length; index++)
            {
                ShopRevealItemPlan plan = _revealPlan.Items[index];
                _revealTitle.text = GetRevealProgressLabel() + " · " +
                    (index + 1) + " / " + _revealFaces.Length;
                if (plan.UsesFullSequence)
                {
                    // Cost와 Edition의 강조는 각각 카드의 앞면 정보로 읽으며 서류 화면으로 전환하지 않는다.
                    var outline = _revealBacks[index].GetComponent<Outline>();
                    outline.effectColor = GetRevealAccent(plan.Item.HighestIntensity);
                    outline.effectDistance = new Vector2(3f, -3f);
                    if (plan.Item.HighestIntensity == ShopRevealIntensity.Exceptional)
                        RequestRevealAudio(ShopRevealAudioCue.ExceptionalPause);
                    yield return WaitForReveal(GetAnticipationDuration(plan.Item.HighestIntensity));
                }
                float elapsed = 0f;
                bool hasPlayedAudio = false;
                while (elapsed < _revealFlipDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float ratio = Mathf.Clamp01(elapsed / _revealFlipDuration);
                    bool isFront = ratio >= .5f;
                    _revealBacks[index].alpha = isFront ? 0f : 1f;
                    _revealFaces[index].alpha = isFront ? 1f : 0f;
                    float width = Mathf.Max(.015f, Mathf.Abs(Mathf.Cos(ratio * Mathf.PI)));
                    _revealBacks[index].transform.localScale = new Vector3(width, 1f, 1f);
                    _revealFaces[index].transform.localScale = new Vector3(width, 1f, 1f);
                    if (isFront && !hasPlayedAudio)
                    {
                        RequestRevealAudio(ShopRevealAudioCue.CardReveal);
                        hasPlayedAudio = true;
                    }
                    yield return null;
                }
                SetCardRevealed(index);
                yield return WaitForReveal(plan.UsesFullSequence ? .45f : .1f);
            }
        }

        private void SetCardRevealed(int index)
        {
            _revealFaces[index].alpha = 1f;
            _revealFaces[index].transform.localScale = Vector3.one;
            _revealBacks[index].alpha = 0f;
            _revealBacks[index].transform.localScale = Vector3.one;
        }

        private static Color GetRevealAccent(ShopRevealIntensity intensity)
        {
            return intensity switch
            {
                ShopRevealIntensity.Exceptional => new Color32(242, 191, 80, 255),
                ShopRevealIntensity.Rare => new Color32(182, 145, 242, 255),
                ShopRevealIntensity.Notable => new Color32(89, 176, 234, 255),
                _ => new Color32(142, 163, 190, 255)
            };
        }

        private void BuildRevealPlaybackControls()
        {
            _revealModeButton = OwnerWorkspaceUiFactory.CreateButton(
                _revealRoot, "RevealMode", DescribeRevealMode(), CycleRevealMode);
            OwnerRuntimeUiFactory.SetAnchors(
                _revealModeButton.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -48f),
                new Vector2(178f, -12f));
            _revealModeButton.GetComponent<LayoutElement>().ignoreLayout = true;

            _revealSkipButton = OwnerWorkspaceUiFactory.CreateButton(
                _revealRoot, "Skip", "건너뛰기", SkipReveal);
            OwnerRuntimeUiFactory.SetAnchors(
                _revealSkipButton.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-118f, -48f),
                new Vector2(-18f, -12f));
            _revealSkipButton.GetComponent<LayoutElement>().ignoreLayout = true;
            foreach (Button button in new[] { _revealModeButton, _revealSkipButton })
            {
                button.GetComponent<Image>().color = new Color32(29, 48, 72, 255);
                button.transform.Find("Label").GetComponent<Text>().color = Color.white;
            }
        }

        /// <summary>확정된 결과를 상품 종류별 연출 계획으로 재생한다.</summary>
        private void StartRevealPlayback(ShopPurchaseResult result, ShopProductDetailsSnapshot details)
        {
            if (details == null) return;
            if (_revealCoroutine != null) StopCoroutine(_revealCoroutine);
            _activeDetails = details;
            _lastPurchasedProductId = details.ProductId;
            _revealPlan = ShopRevealPlanBuilder.Build(result, details, _revealMode);
            PrepareRevealCards();
            _isRevealPlaying = true;
            HideDecisionOverlays();
            SetRevealButtonsVisible(false);
            _revealSkipButton.gameObject.SetActive(_revealMode != ShopRevealPresentationMode.Minimal);
            _revealModeButton.gameObject.SetActive(true);
            _revealModeButton.interactable = false;
            _revealTitle.text = GetRevealOpeningTitle();
            _revealBody.fontSize = 16;
            _revealBody.alignment = TextAnchor.MiddleCenter;
            _revealRoot.gameObject.SetActive(true);
            _revealRoot.SetAsLastSibling();

            if (_revealMode == ShopRevealPresentationMode.Minimal)
            {
                CompleteReveal();
                return;
            }
            _revealCoroutine = StartCoroutine(PlayRevealSequence());
        }

        private IEnumerator PlayRevealSequence()
        {
            yield return PlayCardRevealSequence();
            _revealCoroutine = null;
            CompleteReveal();
        }

        private static IEnumerator WaitForReveal(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void CompleteReveal()
        {
            Coroutine runningCoroutine = _revealCoroutine;
            _revealCoroutine = null;
            if (runningCoroutine != null)
            {
                StopCoroutine(runningCoroutine);
            }
            _isRevealPlaying = false;
            _revealSkipButton.gameObject.SetActive(false);
            _revealModeButton.interactable = true;
            for (int index = 0; index < _revealFaces.Length; index++) SetCardRevealed(index);
            _revealTitle.text = GetRevealCompletionTitle();
            RefreshRevealActions();
            SetRevealButtonsVisible(true);
            RequestRevealAudio(ShopRevealAudioCue.Summary);
        }

        private void SkipReveal()
        {
            if (!_isRevealPlaying) return;
            CompleteReveal();
        }

        private void CycleRevealMode()
        {
            if (_isRevealPlaying) return;
            _revealMode = _revealMode switch
            {
                ShopRevealPresentationMode.Full => ShopRevealPresentationMode.HighlightsOnly,
                ShopRevealPresentationMode.HighlightsOnly => ShopRevealPresentationMode.Minimal,
                _ => ShopRevealPresentationMode.Full
            };
            _revealModeButton.transform.Find("Label").GetComponent<Text>().text = DescribeRevealMode();
        }

        private string DescribeRevealMode()
        {
            return _revealMode switch
            {
                ShopRevealPresentationMode.Full => "연출: 전체",
                ShopRevealPresentationMode.HighlightsOnly => "연출: 희귀만",
                ShopRevealPresentationMode.Minimal => "연출: 최소",
                _ => "연출"
            };
        }

        private void SetRevealButtonsVisible(bool visible)
        {
            _revealRepeatButton.gameObject.SetActive(visible);
            _revealInventoryButton.gameObject.SetActive(visible);
            _revealCloseButton.gameObject.SetActive(visible);
        }

        private void StopRevealPlayback()
        {
            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
            }
            _isRevealPlaying = false;
        }

        private static float GetAnticipationDuration(ShopRevealIntensity intensity)
        {
            switch (intensity)
            {
                case ShopRevealIntensity.Standard: return .18f;
                case ShopRevealIntensity.Notable: return .32f;
                case ShopRevealIntensity.Rare: return .62f;
                case ShopRevealIntensity.Exceptional: return .92f;
                default: return .18f;
            }
        }

        private string GetRevealCardBackLabel()
        {
            switch (_revealPlan.Theme)
            {
                case ShopRevealTheme.ScoutingReport: return "선수 카드";
                case ShopRevealTheme.DevelopmentAnalysis: return "스킬 블록";
                case ShopRevealTheme.TacticalLab: return "작전 카드";
                default: return "획득 카드";
            }
        }

        private string GetRevealOpeningTitle()
        {
            switch (_revealPlan.Theme)
            {
                case ShopRevealTheme.ScoutingReport: return "새로운 선수를 만날 시간";
                case ShopRevealTheme.DevelopmentAnalysis: return "새로운 스킬 블록을 확인합니다";
                case ShopRevealTheme.TacticalLab: return "새로운 작전 카드를 확인합니다";
                default: return "획득 결과를 확인합니다";
            }
        }

        private string GetRevealProgressLabel()
        {
            switch (_revealPlan.Theme)
            {
                case ShopRevealTheme.ScoutingReport: return "선수 공개";
                case ShopRevealTheme.DevelopmentAnalysis: return "스킬 블록 공개";
                case ShopRevealTheme.TacticalLab: return "작전 카드 공개";
                default: return "결과 공개";
            }
        }

        private string GetRevealCompletionTitle()
        {
            int count = _revealPlan.Items.Length;
            switch (_revealPlan.Theme)
            {
                case ShopRevealTheme.ScoutingReport: return "선수 영입 완료 · " + count + "명";
                case ShopRevealTheme.DevelopmentAnalysis: return "스킬 블록 획득 완료 · " + count + "개";
                case ShopRevealTheme.TacticalLab: return "작전 카드 획득 완료 · " + count + "장";
                default: return "획득 완료 · " + count + "개";
            }
        }

        private string DescribeRevealItemStatus(ShopGrantedItem item)
        {
            if (_revealPlan.Theme == ShopRevealTheme.DevelopmentAnalysis) return "획득 완료";
            return item.IsNew ? "신규 획득" : "중복 획득";
        }

        private void RequestRevealAudio(ShopRevealAudioCue cue)
        {
            RevealAudioRequested?.Invoke(cue);
        }

    }
}
