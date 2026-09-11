using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Presentation.UI;
using Baseball.Presentation.SharedUI;
using Baseball.Core.Historical;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Baseball.Presentation.Owner
{
    /// <summary>보유 선수의 카드 앞면·뒷면과 실제 시즌 기본 능력치를 읽기 전용으로 표시한다.</summary>
    public sealed partial class UI_Popup_OwnerPlayerCard : UIPopupBase
    {
        private static UI_Popup_OwnerPlayerCard _current;
        private Transform _source;
        private RectTransform _cardRoot;
        private RectTransform _drawerRoot;
        private RectTransform _front;
        private RectTransform _back;
        private OwnerCollectionCardSnapshot[] _cards;
        private int _cardIndex;
        private RectTransform _closeButton;
        private Button _previousButton;
        private Button _nextButton;
        private bool _isFlipping;
        private bool _isBack;
        private static readonly Color Ink = new Color32(8, 10, 16, 255);
        private static readonly Color Gold = new Color32(218, 223, 232, 255);
        private static readonly Color TrainingColor = new Color32(58, 178, 219, 255);
        private static readonly Color SkillBlockColor = new Color32(239, 166, 66, 255);
        private static readonly Color TeamColorColor = new Color32(75, 196, 135, 255);
        private static readonly Color StudyColor = new Color32(230, 73, 167, 255);
        private static readonly Color EnhancementColor = new Color32(231, 92, 83, 255);

        public override bool BlocksLowerInput => true;

        /// <summary>한 번에 하나의 상세 팝업을 최상단 Canvas에 연다.</summary>
        public static void Show(Transform source, OwnerCollectionCardSnapshot card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            Show(source, new[] { card }, 0);
        }

        /// <summary>주어진 타순 또는 투수 역할 순서를 유지하며 좌우로 비교 가능한 상세 팝업을 연다.</summary>
        public static void Show(
            Transform source,
            IReadOnlyList<OwnerCollectionCardSnapshot> cards,
            int selectedIndex)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (cards == null || cards.Count == 0) throw new ArgumentException("표시할 선수 카드가 필요합니다.", nameof(cards));
            if (selectedIndex < 0 || selectedIndex >= cards.Count) throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            if (_current != null) _current.Close();
            Canvas canvas = source.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerPlayerCard), canvas.transform);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerPlayerCard>();
            view._source = source;
            view._cards = new OwnerCollectionCardSnapshot[cards.Count];
            for (int index = 0; index < cards.Count; index++)
                view._cards[index] = cards[index] ?? throw new ArgumentException("null 선수 카드가 있습니다.", nameof(cards));
            view._cardIndex = selectedIndex;
            _current = view;
            var layer = root.gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true; layer.sortingOrder = 200;
            root.gameObject.AddComponent<GraphicRaycaster>();
            view._drawerRoot = Surface(root, "DetailDrawer", new Color32(13, 16, 22, 220), 0, 0, 1, 1);
            view._drawerRoot.GetComponent<Image>().raycastTarget = true;
            RectTransform panel = Surface(root, "CardDetail", Ink, 0.5f, 0.5f, 0.5f, 0.5f);
            view._cardRoot = panel;
            panel.GetComponent<Image>().raycastTarget = true;
            Button flip = panel.gameObject.AddComponent<Button>();
            flip.transition = Selectable.Transition.None;
            flip.onClick.AddListener(view.Flip);
            view._closeButton = Surface(root, "Close", Ink, .5f, .5f, .5f, .5f);
            view._closeButton.GetComponent<Image>().raycastTarget = true;
            view._closeButton.gameObject.AddComponent<Button>().onClick.AddListener(view.Close);
            Label(view._closeButton, "Label", "닫기 ×", 0, 0, 1, 1, 14, Color.white);
            view._front = Surface(panel, "Front", Ink, 0, 0, 1, 1);
            view._back = Surface(panel, "Back", Ink, 0, 0, 1, 1);
            view._previousButton = CreateNavigationButton(root, "PreviousCard", "<", .5f, 0, .5f, 0, view.ShowPrevious);
            view._nextButton = CreateNavigationButton(root, "NextCard", ">", .5f, 0, .5f, 0, view.ShowNext);
            view.ResizeCard();
            view.RenderCard();
            view.Show();
        }

        private void RenderCard()
        {
            OwnerRuntimeUiFactory.ClearChildren(_front);
            OwnerRuntimeUiFactory.ClearChildren(_back);
            OwnerCollectionCardSnapshot card = _cards[_cardIndex];
            bool pitcher = card.Position == PlayerPosition.StartingPitcher || card.Position == PlayerPosition.ReliefPitcher;
            BuildFrontCard(_front, card);
            BuildReferenceBack(_back, card, pitcher);
            _front.gameObject.SetActive(!_isBack);
            _back.gameObject.SetActive(_isBack);
            _previousButton.interactable = _cardIndex > 0;
            _nextButton.interactable = _cardIndex < _cards.Length - 1;
        }

        private void ShowPrevious()
        {
            if (_cardIndex <= 0 || _isFlipping) return;
            _cardIndex--;
            RenderCard();
        }

        private void ShowNext()
        {
            if (_cardIndex >= _cards.Length - 1 || _isFlipping) return;
            _cardIndex++;
            RenderCard();
        }

        /// <summary>카드 상세와 전력보강 선택 영역이 공유하는 구단주 선수 카드 앞면을 그린다.</summary>
        internal static void BuildFrontCard(RectTransform parent, OwnerCollectionCardSnapshot card)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (card == null) throw new ArgumentNullException(nameof(card));
            bool pitcher = card.Position == PlayerPosition.StartingPitcher ||
                card.Position == PlayerPosition.ReliefPitcher;
            BuildCardBorder(parent, card.Edition);
            RectTransform photoWindow = OwnerRuntimeUiFactory.CreateRect("PortraitWindow", parent);
            float portraitBottom = OwnerPlayerCardFrames.GetPortraitBottom(card.Edition, false);
            OwnerRuntimeUiFactory.SetAnchors(photoWindow, new Vector2(.025f, portraitBottom), new Vector2(.975f, OwnerPlayerCardFrames.GetPortraitTop(card.Edition)), Vector2.zero, Vector2.zero);
            photoWindow.gameObject.AddComponent<UICardPortraitMask>().raycastTarget = false;
            photoWindow.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            // 정면 상반신 초상의 모자와 어깨가 사진 창 안에 들어오도록 원본 비율을 유지한다.
            Image portrait = Surface(photoWindow, "Silhouette", Color.white, 0f, 0f, 1f, 1f).GetComponent<Image>();
            portrait.sprite = PlayerPortraitSprites.GetAssigned(card.PlayerSeasonId)
                ?? PlayerPortraitSprites.GetAssigned(card.CardId)
                ?? PlayerPortraitSprites.GetForPlayer(card.PlayerPersonId, card.Position);
            portrait.preserveAspect = true;
            OwnerPlayerCardFrames.SetDecoration(parent, parent.Find("MainFrame").GetComponent<Image>().sprite,
                card.Edition, false, 1f, photoWindow.GetSiblingIndex() + 1);
            RectTransform teamPlate = BuildTeamPlate(parent);
            if (!string.IsNullOrWhiteSpace(card.TeamDisplayName))
                Label(teamPlate, "Team", card.TeamDisplayName, .04f, .02f, .96f, .98f, 14, Color.white);
            RectTransform editionPlate = Gradient(parent, "EditionPlate", new Color32(58, 60, 63, 255),
                new Color32(29, 30, 32, 255), .72f, .895f, .95f, .933f);
            Label(editionPlate, "Edition", OwnerCollectionPresentationBuilder.FormatEdition(card.Edition),
                .03f, 0, .97f, 1, 12, Gold);
            if (card.EnhancementLevel > 0)
                Label(parent, "Enhancement", "+" + card.EnhancementLevel, .79f, .94f, .95f, .98f, 19, Gold);
            if (card.IsLocked) Label(parent, "Locked", "잠금", .04f, .85f, .23f, .90f, 12, Gold);
            RectTransform positionPlate = Gradient(parent, "PositionPlate", new Color32(58, 60, 63, 255),
                new Color32(29, 30, 32, 255), .04f, .91f, .24f, .94f);
            Label(positionPlate, "Position",
                OwnerCollectionPresentationBuilder.FormatPlayerRole(card.Position, card.PitcherRole, card.IsPositionEvidenceMissing),
                .02f, 0, .98f, 1, 12, Color.white);
            Rect name = OwnerPlayerCardFrames.GetNameRect(card.Edition, false);
            Color nameColor = OwnerPlayerCardFrames.GetNameColor(card.Edition);
            Label(parent, "Name", card.DisplayName, name.xMin, name.yMin, name.xMax, name.yMax, 28, nameColor);
            Label(parent, "Year", (card.OriginYear % 100).ToString("00") + "′", .79f, name.yMin, .91f, name.yMax, 20, nameColor);
            if (card.IsOwnedCard)
            {
                RectTransform conditionPanel = Gradient(parent, "ConditionPanel", new Color32(37, 25, 30, 235),
                    new Color32(15, 13, 17, 245), .045f, .548f, .195f, .647f);
                Label(conditionPanel, "Title", "컨디션", .05f, .68f, .95f, .98f, 11, Gold);
                Label(conditionPanel, "Value", card.Condition?.ToString() ?? "—", .05f, .26f, .95f, .70f, 25, Color.white);
                Label(conditionPanel, "State", card.ConditionLabel, .02f, .02f, .98f, .28f, 10, Gold);
            }
            // 프레임과 독립된 표면에 실제 능력치만 그린다.
            RectTransform statsPanel = Surface(
                parent, "StatsPanel", new Color32(10, 10, 12, 255), .016f, .108f, .984f, .356f);
            statsPanel.gameObject.AddComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.DataImage);
            CreateAbilityLegend(parent);
            string[] labels = pitcher ? new[] { "체력", "구속", "구위", "변화구", "제구력", "정신력" } :
                new[] { "교타력", "장타력", "주력", "송구력", "수비력", "정신력" };
            PlayerAbility[] abilities = pitcher ? new[] { PlayerAbility.Stamina, PlayerAbility.Velocity, PlayerAbility.Stuff,
                PlayerAbility.Breaking, PlayerAbility.Control, PlayerAbility.PitcherMental } :
                new[] { PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed, PlayerAbility.Arm, PlayerAbility.Defense, PlayerAbility.BatterMental };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = .297f - i * .036f;
                Surface(parent, "RowRule" + i, new Color(0.48f, 0.64f, 0.79f, .16f),
                    .045f, y - .001f, .95f, y);
                int? value = card.GetEffectiveAbility(abilities[i]);
                Label(parent, "Ability" + i, labels[i], .035f, y, .20f, y + .034f, 14, Color.white);
                Gradient(parent, "Track" + i, new Color32(93, 97, 107, 255), new Color32(44, 47, 55, 255),
                    .205f, y + .010f, .77f, y + .023f);
                OwnerAbilityBreakdownSnapshot? breakdown = card.GetAbilityBreakdown(abilities[i]);
                if (breakdown.HasValue)
                    BuildAbilitySegments(parent, i, y, breakdown.Value, card.AbilityGraphMaximum);
                else if (value.HasValue)
                    Gradient(parent, "BaseFill" + i, Color.white, new Color32(194, 205, 225, 255),
                        .205f, y + .010f, .205f + .565f * Mathf.Clamp01(value.Value / (float)card.AbilityGraphMaximum), y + .023f);
                Label(parent, "Value" + i, value?.ToString() ?? "—", .78f, y, .865f, y + .034f, 15, Color.white);
                int appliedGrowth = breakdown.HasValue && value.HasValue
                    ? Mathf.Max(0, value.Value - Mathf.Min(card.AbilityGraphMaximum, breakdown.Value.BaseCard))
                    : 0;
                string growth = appliedGrowth > 0
                    ? "+" + appliedGrowth
                    : string.Empty;
                Label(parent, "GrowthValue" + i, growth, .855f, y, .96f, y + .034f, 13, StudyColor);
            }
            const float costBottom = .014f;
            const float costTop = .060f;
            Label(parent, "CostLabel", "COST", .04f, costBottom, .19f, costTop, 13, Gold);
            RectTransform stars = OwnerRuntimeUiFactory.CreateRect("CostStars", parent);
            OwnerRuntimeUiFactory.SetAnchors(stars, new Vector2(.20f, costBottom), new Vector2(.81f, costTop), Vector2.zero, Vector2.zero);
            OwnerPlayerCardFrames.SetCostStars(stars, card.Edition, card.Cost);
            Label(parent, "Cost", card.Cost.ToString(), .83f, costBottom, .96f, costTop, 23, Color.white);
        }

        private static void CreateAbilityLegend(Transform parent)
        {
            CreateLegendItem(parent, "BaseLegend", "기본", new Color32(218, 224, 235, 255), .05f, .145f);
            CreateLegendItem(parent, "TrainingLegend", "훈련", TrainingColor, .195f, .29f);
            CreateLegendItem(parent, "SkillLegend", "블록", SkillBlockColor, .34f, .435f);
            CreateLegendItem(parent, "TeamColorLegend", "팀컬러", TeamColorColor, .485f, .61f);
            CreateLegendItem(parent, "StudyLegend", "유학", StudyColor, .66f, .755f);
            CreateLegendItem(parent, "EnhancementLegend", "강화", EnhancementColor, .805f, .90f);
        }

        private static void CreateLegendItem(
            Transform parent,
            string name,
            string text,
            Color color,
            float x0,
            float x1)
        {
            Surface(parent, name + "Swatch", color, x0, .333f, x0 + .018f, .345f);
            Text label = Label(parent, name, text, x0 + .021f, .327f, x1, .351f, 10, new Color32(205, 210, 220, 255));
            label.alignment = TextAnchor.MiddleLeft;
        }

        private static void BuildAbilitySegments(
            Transform parent,
            int index,
            float y,
            OwnerAbilityBreakdownSnapshot breakdown,
            int maximum)
        {
            float cursor = 0f;
            AddAbilitySegment(parent, "BaseFill" + index, y, breakdown.BaseCard,
                Color.white, new Color32(194, 205, 225, 255), maximum, ref cursor);
            AddAbilitySegment(parent, "TrainingFill" + index, y, breakdown.Training,
                TrainingColor, TrainingColor, maximum, ref cursor);
            AddAbilitySegment(parent, "SkillBlockFill" + index, y, breakdown.SkillBlock,
                SkillBlockColor, SkillBlockColor, maximum, ref cursor);
            AddAbilitySegment(parent, "TeamColorFill" + index, y, breakdown.TeamColor,
                TeamColorColor, TeamColorColor, maximum, ref cursor);
            AddAbilitySegment(parent, "StudyFill" + index, y, breakdown.Study,
                StudyColor, StudyColor, maximum, ref cursor);
            AddAbilitySegment(parent, "EnhancementFill" + index, y, breakdown.Enhancement,
                EnhancementColor, EnhancementColor, maximum, ref cursor);
        }

        private static void AddAbilitySegment(
            Transform parent,
            string name,
            float y,
            int amount,
            Color start,
            Color end,
            int maximum,
            ref float cursor)
        {
            if (amount <= 0 || cursor >= maximum) return;
            float next = Mathf.Min(maximum, cursor + amount);
            float x0 = .205f + .565f * cursor / maximum;
            float x1 = .205f + .565f * next / maximum;
            Gradient(parent, name, start, end, x0, y + .010f, x1, y + .023f);
            cursor = next;
        }

        private static void BuildCardBorder(RectTransform parent, PlayerCardEdition edition)
        {
            Image frame = Surface(parent, "MainFrame", Color.white, 0, 0, 1, 1).GetComponent<Image>();
            frame.sprite = OwnerPlayerCardFrames.Get(edition, false);
            frame.preserveAspect = false;
        }

        /// <summary>등급별 배경에 의존하지 않는 상단 구단 정보 표면이다.</summary>
        internal static RectTransform BuildTeamPlate(RectTransform parent)
        {
            return Gradient(parent, "TeamPlate", new Color32(58, 60, 63, 255),
                new Color32(29, 30, 32, 255), .28f, .944f, .73f, .985f);
        }

        private static Button CreateNavigationButton(
            Transform parent,
            string name,
            string label,
            float x0,
            float y0,
            float x1,
            float y1,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Surface(parent, name, new Color(0.04f, 0.06f, 0.09f, 0.94f), x0, y0, x1, y1);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            Label(rect, "Label", label, 0, 0, 1, 1, 34, Color.white);
            return button;
        }

        private static RectTransform Gradient(Transform parent, string name, Color top, Color bottom,
            float x0, float y0, float x1, float y1)
        {
            RectTransform rect = OwnerRuntimeUiFactory.CreateRect(name, parent);
            OwnerRuntimeUiFactory.SetAnchors(rect, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
            rect.gameObject.AddComponent<PlayerCardSurface>().SetColors(top, bottom);
            return rect;
        }

        private static RectTransform Surface(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            RectTransform rect = OwnerRuntimeUiFactory.CreateRect(name, parent);
            OwnerRuntimeUiFactory.SetAnchors(rect, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            return rect;
        }

        private static Text Label(Transform parent, string name, string value, float x0, float y0, float x1, float y1, int size, Color color)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, FontStyle.Bold, TextAnchor.MiddleCenter, color);
            OwnerRuntimeUiFactory.SetAnchors(text.rectTransform, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 9; text.resizeTextMaxSize = size;
            text.raycastTarget = false;
            return text;
        }

        private void LateUpdate()
        {
            if (_source == null || !_source.gameObject.activeInHierarchy) { Close(); return; }
            ResizeCard();
        }

        private void ResizeCard()
        {
            Rect bounds = _drawerRoot.rect;
            const float aspect = 2f / 3f;
            float height = Mathf.Min(bounds.height * 0.82f, bounds.width * 0.90f / aspect);
            float width = height * aspect;
            _cardRoot.sizeDelta = new Vector2(width, height);
            _cardRoot.anchoredPosition = Vector2.zero;
            if (_closeButton == null || _previousButton == null || _nextButton == null) return;

            const float navigationButtonWidth = 64f;
            const float navigationHeight = 48f;
            const float navigationGap = 12f;
            PositionCardControl(
                _previousButton.GetComponent<RectTransform>(),
                new Vector2(-width * .5f - navigationGap - navigationButtonWidth * .5f, 0f),
                new Vector2(navigationButtonWidth, navigationHeight));
            PositionCardControl(
                _nextButton.GetComponent<RectTransform>(),
                new Vector2(width * .5f + navigationGap + navigationButtonWidth * .5f, 0f),
                new Vector2(navigationButtonWidth, navigationHeight));
            const float closeButtonWidth = 96f;
            const float closeButtonHeight = 44f;
            PositionCardControl(
                _closeButton,
                new Vector2(width * .5f + closeButtonWidth * .5f, height * .5f - closeButtonHeight * .5f),
                new Vector2(closeButtonWidth, closeButtonHeight));
        }

        private static void PositionCardControl(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void Flip()
        {
            if (!_isFlipping) StartCoroutine(AnimateFlip());
        }

        private IEnumerator AnimateFlip()
        {
            _isFlipping = true;
            const float halfDuration = 0.16f;
            for (int phase = 0; phase < 2; phase++)
            {
                float elapsed = 0;
                while (elapsed < halfDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / halfDuration));
                    _cardRoot.localScale = new Vector3(phase == 0 ? 1 - t : t, 1, 1);
                    yield return null;
                }
                if (phase == 0)
                {
                    _isBack = !_isBack;
                    _front.gameObject.SetActive(!_isBack);
                    _back.gameObject.SetActive(_isBack);
                }
            }
            _cardRoot.localScale = Vector3.one;
            _isFlipping = false;
        }
        /// <summary>카드 상세를 UI Popup 스택에서 제거하고 런타임 오브젝트를 폐기한다.</summary>
        public override void Close()
        {
            if (_current == this)
                _current = null;
            base.Close();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
