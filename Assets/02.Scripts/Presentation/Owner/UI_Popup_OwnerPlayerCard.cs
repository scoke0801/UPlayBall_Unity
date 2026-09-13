using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Presentation.UI;
using Baseball.Presentation.SharedUI;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Baseball.Presentation.Owner
{
    /// <summary>선수 카드 앞면·뒷면과 실제 시즌 능력치를 표시하고 보유 카드 잠금을 관리한다.</summary>
    public sealed partial class UI_Popup_OwnerPlayerCard : UIPopupBase
    {
        private static UI_Popup_OwnerPlayerCard _current;
        private Transform _source;
        private RectTransform _cardRoot;
        private RectTransform _drawerRoot;
        private RectTransform _front;
        private RectTransform _back;
        private OwnerCollectionCardSnapshot[] _cards;
        private Func<OwnerCollectionCardSnapshot, OwnerCollectionCardSnapshot> _detailResolver;
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
            int selectedIndex,
            Func<OwnerCollectionCardSnapshot, OwnerCollectionCardSnapshot> detailResolver = null)
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
            view._detailResolver = detailResolver;
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
            UIOwnerFrontOfficeSkin.ApplyButton(view._closeButton.GetComponent<Button>(), OwnerButtonRole.Secondary);
            view._front = Surface(panel, "Front", Ink, 0, 0, 1, 1);
            view._back = Surface(panel, "Back", Ink, 0, 0, 1, 1);
            view._previousButton = CreateNavigationButton(root, "PreviousCard", "<", .5f, 0, .5f, 0, view.ShowPrevious);
            view._nextButton = CreateNavigationButton(root, "NextCard", ">", .5f, 0, .5f, 0, view.ShowNext);
            view.BuildGrowthHistory(root);
            view.ResizeCard();
            view.RenderCard();
            view.Show();
        }

        private void RenderCard()
        {
            OwnerRuntimeUiFactory.ClearChildren(_front);
            OwnerRuntimeUiFactory.ClearChildren(_back);
            OwnerCollectionCardSnapshot card = _cards[_cardIndex];
            if (_detailResolver != null) card = _detailResolver(card);
            bool pitcher = card.Position == PlayerPosition.StartingPitcher || card.Position == PlayerPosition.ReliefPitcher;
            BuildFrontCard(_front, card);
            BindLockControl(card);
            BuildReferenceBack(_back, card, pitcher);
            BindGrowthHistory(card);
            _front.gameObject.SetActive(!_isBack);
            _back.gameObject.SetActive(_isBack);
            _previousButton.interactable = _cardIndex > 0;
            _nextButton.interactable = _cardIndex < _cards.Length - 1;
        }

        /// <summary>현재 보유 상태를 다시 읽어 카드 보호 버튼과 변경 불가 사유를 표시한다.</summary>
        private void BindLockControl(OwnerCollectionCardSnapshot card)
        {
            Transform iconTransform = _front.Find("LockStatus");
            OwnerModeManager manager = OwnerModeManager.Instance;
            if (!card.IsOwnedCard || iconTransform == null || manager?.Runtime == null ||
                !manager.Runtime.TryGetOwnedCard(card.CardId, out var owned)) return;
            Image icon = iconTransform.GetComponent<Image>();
            icon.sprite = Resources.Load<Sprite>("UI/PlayerCardStatus/" + (owned.IsLocked ? "Locked" : "Unlocked"));
            bool isPermanent = manager.Runtime.WorldCardCatalog.TryGetCard(card.CardId, out var definition) && definition.IsUniqueOwnedCard;
            bool isReserved = manager.Runtime.IsCardReserved(card.CardId);
            Text caption = Label(_front, "LockAction", isPermanent ? "영구 잠금" : isReserved ? "영입 예약 중" :
                owned.IsLocked ? "잠금 해제" : "카드 잠금", .055f, .866f, .225f, .893f, 12, Gold);
            Text feedback = Label(_front, "LockFeedback", "", .07f, .369f, .93f, .405f, 13, Gold);
            icon.raycastTarget = true;
            Button button = icon.gameObject.AddComponent<Button>();
            button.targetGraphic = icon;
            button.interactable = !isPermanent && !isReserved;
            button.onClick.AddListener(() =>
            {
                if (_isFlipping) return;
                try
                {
                    if (!manager.Runtime.TryGetOwnedCard(card.CardId, out var current))
                        throw new InvalidOperationException("더 이상 보유하지 않은 카드입니다.");
                    manager.SetPlayerCardLocked(card.CardId, !current.IsLocked);
                }
                catch (InvalidOperationException error) { feedback.text = error.Message; return; }
                catch (Exception) { feedback.text = "잠금 상태를 저장하지 못했습니다. 다시 시도하세요."; return; }
                if (!manager.Runtime.TryGetOwnedCard(card.CardId, out var updated)) return;
                icon.sprite = Resources.Load<Sprite>("UI/PlayerCardStatus/" + (updated.IsLocked ? "Locked" : "Unlocked"));
                caption.text = updated.IsLocked ? "잠금 해제" : "카드 잠금";
                feedback.text = updated.IsLocked ? "카드를 잠갔습니다. 합성·특수 영입 재료에서 제외됩니다." : "카드 잠금을 해제했습니다.";
            });
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
                Label(teamPlate, "Team", card.TeamDisplayName, .04f, .02f, .96f, .98f, 14,
                    card.Edition == PlayerCardEdition.Legend
                        ? OwnerPlayerCardFrames.GetNameColor(card.Edition) : Color.white);
            Image enhancementIcon = null;
            if (card.EnhancementLevel > 0)
            {
                RectTransform enhancement = StatusIcon(parent, "EnhancementBadge", "Enhancement_" + card.EnhancementLevel, .855f, .864f, .965f, .942f);
                enhancementIcon = enhancement.GetComponent<Image>();
            }
            if (card.IsOwnedCard || card.IsLocked)
                StatusIcon(parent, "LockStatus", card.IsLocked ? "Locked" : "Unlocked", .10f, .895f, .18f, .947f);
            Rect name = OwnerPlayerCardFrames.GetNameRect(card.Edition, false);
            Color nameColor = OwnerPlayerCardFrames.GetNameColor(card.Edition);
            RectTransform positionPlate = ContentRect(parent, "PositionPlate", .055f, name.yMin, .22f, name.yMax);
            Label(positionPlate, "Position",
                OwnerCollectionPresentationBuilder.FormatPlayerRole(card.Position, card.PitcherRole, card.IsPositionEvidenceMissing),
                .02f, 0, .98f, 1, 17, nameColor);
            Label(parent, "Name", card.DisplayName, name.xMin, name.yMin, name.xMax, name.yMax, 28, nameColor);
            Label(parent, "Year", (card.OriginYear % 100).ToString("00") + "′", .79f, name.yMin, .91f, name.yMax, 20, nameColor);
            if (card.IsOwnedCard)
            {
                RectTransform conditionPanel = ContentRect(parent, "ConditionPanel", .045f, .548f, .195f, .647f);
                Label(conditionPanel, "Title", "컨디션", .05f, .68f, .95f, .98f, 11, Gold);
                PlayerCardConditionSprites.Bind(conditionPanel, card.ConditionLevel,
                    new Vector2(.02f, .27f), new Vector2(.46f, .70f));
                Label(conditionPanel, "Value", card.Condition?.ToString() ?? "—",
                    card.ConditionLevel.HasValue ? .47f : .05f, .26f, .98f, .70f, 22, Color.white);
                Label(conditionPanel, "State", card.ConditionLabel, .02f, .02f, .98f, .28f, 10, Gold);
            }
            // 기존 카드 프레임 위에 직접 표시해 사각 배경이 테두리를 덮지 않게 한다.
            CreateAbilityLegend(parent);
            PlayerAbility[] abilities = pitcher ? new[] { PlayerAbility.Stamina, PlayerAbility.Velocity, PlayerAbility.Stuff,
                PlayerAbility.Breaking, PlayerAbility.Control, PlayerAbility.PitcherMental } :
                new[] { PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed, PlayerAbility.Bunt, PlayerAbility.Defense, PlayerAbility.BatterMental };
            for (int i = 0; i < abilities.Length; i++)
            {
                float y = .297f - i * .036f;
                Surface(parent, "RowRule" + i, new Color(0.48f, 0.64f, 0.79f, .16f),
                    .045f, y - .001f, .95f, y);
                bool isBunt = !pitcher && i == 3;
                int? value = isBunt ? card.GetBuntAbility() : card.GetEffectiveAbility(abilities[i]);
                Label(parent, "Ability" + i, PlayerAbilityCatalog.GetDisplayName(abilities[i]), .035f, y, .20f, y + .034f, 14, Color.white);
                Gradient(parent, "Track" + i, new Color32(93, 97, 107, 255), new Color32(44, 47, 55, 255),
                    .205f, y + .010f, .77f, y + .023f);
                OwnerAbilityBreakdownSnapshot? breakdown = isBunt
                    ? card.GetBuntAbilityBreakdown() : card.GetAbilityBreakdown(abilities[i]);
                if (breakdown.HasValue)
                    BuildAbilitySegments(parent, i, y, breakdown.Value, card.AbilityGraphMaximum);
                else if (value.HasValue)
                    Gradient(parent, "BaseFill" + i, Color.white, new Color32(194, 205, 225, 255),
                        .205f, y + .010f, .205f + .565f * CareerUiTheme.GetCardStatGaugeRatio(value.Value), y + .023f);
                int baseStat = breakdown?.BaseCard ?? value ?? 0;
                Text statText = Label(parent, "Value" + i, value?.ToString() ?? "—", .78f, y, .865f, y + .034f,
                    15, CareerUiTheme.GetCardBaseStatColor(baseStat));
                statText.gameObject.AddComponent<CareerUiPreserveTextColor>();
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
            PlayerCardGrowthBadgesView.Bind(parent, card.GrowthBadges, isDetail: true, enhancement: enhancementIcon);
        }

        /// <summary>생성한 투명 상태 아이콘을 카드 원화 위에 원본 비율로 표시한다.</summary>
        private static RectTransform StatusIcon(Transform parent, string name, string resource,
            float x0, float y0, float x1, float y1)
        {
            RectTransform rect = Surface(parent, name, Color.white, x0, y0, x1, y1);
            // 잠금 버튼도 원화를 소유한다. 공용 버튼 스킨이 Sprite를 단색 면으로 교체하지 않게 한다.
            rect.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            Image icon = rect.GetComponent<Image>();
            icon.sprite = Resources.Load<Sprite>("UI/PlayerCardStatus/" + resource);
            icon.preserveAspect = true;
            return rect;
        }

        private static void CreateAbilityLegend(Transform parent)
        {
            CreateLegendItem(parent, "BaseLegend", "기본", new Color32(218, 224, 235, 255), .05f, .14f);
            CreateLegendItem(parent, "TrainingLegend", "훈련", TrainingColor, .17f, .26f);
            CreateLegendItem(parent, "SkillLegend", "블록", SkillBlockColor, .29f, .38f);
            CreateLegendItem(parent, "TeamColorLegend", "팀컬러", TeamColorColor, .41f, .53f);
            CreateLegendItem(parent, "StudyLegend", "유학", StudyColor, .56f, .65f);
            CreateLegendItem(parent, "EnhancementLegend", "강화", EnhancementColor, .68f, .77f);
            CreateLegendItem(parent, "GrowthLedgerLegend", "추가", new Color32(169,137,211,255), .80f, .92f);
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
            int operations = breakdown.Mentoring + breakdown.Correction + breakdown.Support + breakdown.Slogan + breakdown.Staff;
            AddAbilitySegment(parent, "BaseFill" + index, y, Math.Max(1, breakdown.BaseCard + Math.Min(0, operations)),
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
            AddAbilitySegment(parent, "DevelopmentFill" + index, y, Math.Max(0, operations),
                new Color32(153, 128, 220, 255), new Color32(153, 128, 220, 255), maximum, ref cursor);
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
            float x0 = .205f + .565f * CareerUiTheme.GetCardStatGaugeRatio(cursor);
            float x1 = .205f + .565f * CareerUiTheme.GetCardStatGaugeRatio(next);
            if (x1 > x0)
                Gradient(parent, name, start, end, x0, y + .010f, x1, y + .023f);
            cursor = next;
        }

        private static void BuildCardBorder(RectTransform parent, PlayerCardEdition edition)
        {
            Image frame = Surface(parent, "MainFrame", Color.white, 0, 0, 1, 1).GetComponent<Image>();
            frame.sprite = OwnerPlayerCardFrames.Get(edition, false);
            frame.preserveAspect = false;
        }

        /// <summary>기존 카드 프레임 안에 배경 없는 원본 구단명 영역을 배치한다.</summary>
        internal static RectTransform BuildTeamPlate(RectTransform parent)
        {
            return ContentRect(parent, "TeamPlate", .28f, .944f, .73f, .985f);
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
            UIOwnerFrontOfficeSkin.ApplyButton(button, OwnerButtonRole.Utility);
            return button;
        }

        private static RectTransform ContentRect(Transform parent, string name,
            float x0, float y0, float x1, float y1)
        {
            RectTransform rect = OwnerRuntimeUiFactory.CreateRect(name, parent);
            OwnerRuntimeUiFactory.SetAnchors(rect, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
            return rect;
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
            const float gap = 24f;
            const float edge = 88f;
            float closedHeight = Mathf.Min(bounds.height * .82f, (bounds.width - edge * 2) / aspect);
            float panelWidth = Mathf.Min(760f, bounds.width * .53f);
            float openHeight = Mathf.Min(closedHeight, (bounds.width - panelWidth - gap - edge * 2) / aspect);
            openHeight = Mathf.Max(120f, openHeight);
            _historyProgress = Mathf.MoveTowards(_historyProgress, _isHistoryOpen ? 1f : 0f, Time.unscaledDeltaTime / .28f);
            float t = Mathf.SmoothStep(0, 1, _historyProgress);
            float height = Mathf.Lerp(closedHeight, openHeight, t);
            float width = height * aspect;
            float cardX = -(panelWidth + gap) * .5f * t;
            _cardRoot.sizeDelta = new Vector2(width, height);
            _cardRoot.anchoredPosition = new Vector2(cardX, 0);
            if (_growthHistoryRoot != null)
            {
                float panelHeight = Mathf.Min(bounds.height * .82f, 640f);
                PositionCardControl(_growthHistoryRoot,
                    new Vector2(cardX + width * .5f + gap + panelWidth * .5f + 24 * (1 - t), 0),
                    new Vector2(panelWidth, panelHeight));
                _historyGroup.alpha = t;
                _historyGroup.blocksRaycasts = _isHistoryOpen;
                _historyGroup.interactable = _isHistoryOpen;
                if (!_isHistoryOpen && _historyProgress == 0) _growthHistoryRoot.gameObject.SetActive(false);
            }
            if (_closeButton == null || _previousButton == null || _nextButton == null) return;
            PositionCardControl(_previousButton.GetComponent<RectTransform>(),
                new Vector2(cardX - width * .5f - 40, 0), new Vector2(56, 48));
            PositionCardControl(_nextButton.GetComponent<RectTransform>(),
                new Vector2(cardX + width * .5f + 40, Mathf.Lerp(0, -height * .5f - 24, t)), new Vector2(56, 48));
            PositionCardControl(_closeButton,
                new Vector2(bounds.width * .5f - 64, bounds.height * .5f - 36), new Vector2(96, 44));
            if (_growthHistoryButton != null)
                PositionCardControl(_growthHistoryButton.GetComponent<RectTransform>(),
                    new Vector2(cardX, -height * .5f - 28), new Vector2(192, 44));
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
