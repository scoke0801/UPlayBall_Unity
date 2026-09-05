using System;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>선수 카드에서 한 번에 하나만 선택하는 대표 특수 카드 유형이다.</summary>
    public enum PlayerCardSpecialType
    {
        None,
        AllStar,
        Mvp,
        GoldenGlove
    }

    /// <summary>Neutral Frame·구단색·특수 카드·선수 데이터·등급 효과를 분리해 표시하는 재사용 선수 카드다.</summary>
    [DisallowMultipleComponent]
    public sealed class UIPlayerCard : MonoBehaviour
    {
        private const float SourceWidth = 1024f;
        private const float SourceHeight = 1536f;
        private const int AbilityCount = 6;

        private readonly Text[] _abilityLabels = new Text[AbilityCount];
        private readonly Text[] _abilityValues = new Text[AbilityCount];
        private readonly RectTransform[] _abilityFills = new RectTransform[AbilityCount];
        private readonly Text[] _awardMarks = new Text[3];

        private RectTransform _front;
        private RectTransform _back;
        private Image _frontTeamColorOverlay;
        private Image _backTeamColorOverlay;
        private Image _frontSpecialOverlay;
        private Image _backSpecialOverlay;
        private Image _commonTopMeta;
        private Image _photoBackground;
        private Image _portrait;
        private Image _frontEmblem;
        private Image _backEmblem;
        private Image _frontGradeEffect;
        private Image _backGradeEffect;
        private Image _topTeamEmblem;
        private readonly Image[] _awardSlots = new Image[3];
        private Text _playerName;
        private Text _season;
        private Text _position;
        private Text _role;
        private Text _overall;
        private Text _backTeamName;
        private Text _topTeamFallback;
        private Button _flipButton;

        public bool IsShowingBack { get; private set; }
        public PlayerCardSpecialType SpecialType { get; private set; }

        /// <summary>프리팹 없이도 같은 4레이어 카드 구조를 생성한다.</summary>
        public static UIPlayerCard CreateRuntime(
            Transform parent,
            Vector2 size,
            Vector2 position)
        {
            var cardObject = new GameObject(
                "Card",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(UIPlayerCard));
            var rect = cardObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image inputSurface = cardObject.GetComponent<Image>();
            inputSurface.color = Color.clear;
            inputSurface.raycastTarget = true;

            UIPlayerCard card = cardObject.GetComponent<UIPlayerCard>();
            card.BuildHierarchy();
            return card;
        }

        /// <summary>현재 선수 읽기 모델을 카드의 동적 데이터 레이어에 투영한다.</summary>
        public void Bind(PlayerProfileView view, string roleLabel)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            Color primary = ToColor(view.TeamColor);
            ApplyTeamColor(primary);
            _playerName.text = view.PlayerName;
            _season.text = view.SeasonYear.ToString();
            _position.text = GetPositionCode(view.Position);
            _role.text = roleLabel ?? string.Empty;
            _overall.text = view.Overall.ToString();
            _backTeamName.text = view.TeamName;

            _portrait.sprite = PlayerPortraitSprites.GetDefault(view.Position);
            _portrait.color = Color.white;
            _portrait.preserveAspect = true;

            Color secondary = GetReadableSecondary(primary);
            ApplyEmblem(_frontEmblem, view.TeamEmblemId, secondary);
            ApplyTopTeamEmblem(view.TeamEmblemId, view.TeamName);
            ApplyEmblem(_backEmblem, view.TeamEmblemId, secondary);
            ClearAwardMarks();
            BindAbilities(view.Abilities, primary);
            SetShowingBack(false);
        }

#if UNITY_EDITOR
        /// <summary>타이틀의 개발용 갤러리에서 게임 상태를 만들지 않고 카드 레이어만 검수한다.</summary>
        public void BindArtPreview(Color primary, PlayerPosition position)
        {
            ApplyTeamColor(primary);
            _portrait.sprite = PlayerPortraitSprites.GetDefault(position);
            _portrait.color = Color.white;
            _portrait.preserveAspect = true;
            _frontEmblem.sprite = null;
            _frontEmblem.color = Color.clear;
            _backEmblem.sprite = null;
            _backEmblem.color = Color.clear;
            if (!TeamEmblemSprites.TryApply(_topTeamEmblem, 0))
            {
                _topTeamEmblem.sprite = null;
                _topTeamEmblem.color = Color.clear;
                _topTeamFallback.text = "◆";
            }
            else
                _topTeamFallback.text = string.Empty;
            SetAwardMarks("★", "M", "G");
            _playerName.text = "디자인 샘플";
            _season.text = "20XX";
            _position.text = GetPositionCode(position);
            _role.text = "CARD PREVIEW";
            _overall.text = "--";
            _backTeamName.text = "TEAM COLOR PREVIEW";

            string[] labels = { "컨택", "장타", "주루", "송구", "수비", "정신력" };
            int[] values = { 72, 64, 68, 76, 82, 70 };
            for (int index = 0; index < AbilityCount; index++)
            {
                _abilityLabels[index].text = labels[index];
                _abilityValues[index].text = values[index].ToString();
                _abilityFills[index].gameObject.SetActive(true);
                _abilityFills[index].anchorMax = new Vector2(
                    (238f + 540f * values[index] / 100f) / SourceWidth,
                    _abilityFills[index].anchorMax.y);
                _abilityFills[index].GetComponent<Image>().color = primary;
            }

            SetShowingBack(false);
        }
#endif

        /// <summary>향후 카드 등급 시스템이 Team Color와 독립된 효과 Sprite를 주입한다.</summary>
        public void SetGradeEffect(Sprite sprite, Color color)
        {
            ApplyGradeEffect(_frontGradeEffect, sprite, color);
            ApplyGradeEffect(_backGradeEffect, sprite, color);
        }

        /// <summary>구단색과 등급 효과를 유지한 채 대표 특수 카드 Overlay만 교체한다.</summary>
        public void SetSpecialType(PlayerCardSpecialType specialType)
        {
            SpecialType = specialType;
            Sprite frontSprite = PlayerCardSprites.GetSpecialFront(specialType);
            Sprite backSprite = PlayerCardSprites.GetSpecialBack(specialType);
            ApplySpecialOverlay(_frontSpecialOverlay, frontSprite);
            ApplySpecialOverlay(_backSpecialOverlay, backSprite);
            _commonTopMeta.color = GetTopMetaColor(specialType);
        }

        /// <summary>우측 공통 Medal에 표시할 구단 엠블럼 Sprite를 주입한다.</summary>
        public void SetTopTeamEmblem(Sprite sprite)
        {
            ApplySlotSprite(_topTeamEmblem, sprite);
            _topTeamFallback.text = sprite == null ? "◆" : string.Empty;
        }

        /// <summary>대표 카드 외의 수상 이력은 최대 세 개의 작은 동적 아이콘으로 표시한다.</summary>
        public void SetAwardIcons(params Sprite[] sprites)
        {
            for (int index = 0; index < _awardSlots.Length; index++)
            {
                Sprite sprite = sprites != null && index < sprites.Length ? sprites[index] : null;
                ApplySlotSprite(_awardSlots[index], sprite);
                _awardMarks[index].text = string.Empty;
            }
        }

        /// <summary>동일 크기의 Front와 Back을 전환한다.</summary>
        public void SetShowingBack(bool isShowingBack)
        {
            IsShowingBack = isShowingBack;
            _front.gameObject.SetActive(!isShowingBack);
            _back.gameObject.SetActive(isShowingBack);
        }

        private void BuildHierarchy()
        {
            _front = CreateRect("Front", transform, stretch: true);
            _back = CreateRect("Back", transform, stretch: true);
            BuildFront();
            BuildBack();
            SetSpecialType(PlayerCardSpecialType.None);

            _flipButton = GetComponent<Button>();
            _flipButton.transition = Selectable.Transition.None;
            _flipButton.onClick.AddListener(ToggleSide);
            SetShowingBack(false);
        }

        private void BuildFront()
        {
            _photoBackground = CreateImage("TeamColorBackground", _front, null, Color.white,
                37f, 51f, 949f, 847f);
            _frontTeamColorOverlay = CreateFullImage(
                "TeamColorOverlay", _front, PlayerCardSprites.FrontTeamColorOverlay, Color.white);
            CreateFullImage("NeutralFrame", _front, PlayerCardSprites.FrontNeutral, Color.white);
            _frontSpecialOverlay = CreateFullImage("SpecialCardOverlay", _front, null, Color.clear);
            _frontSpecialOverlay.enabled = false;
            _portrait = CreateImage("Portrait", _front, null, Color.white,
                92f, 76f, 840f, 820f);
            _portrait.preserveAspect = true;
            _commonTopMeta = CreateFullImage(
                "CommonTopMeta", _front, PlayerCardSprites.TopMetaCommon, Color.white);

            for (int index = 0; index < _awardSlots.Length; index++)
            {
                _awardSlots[index] = CreateImage(
                    "AwardSlot_" + index, _front, null, Color.clear,
                    168f + index * 70f, 84f, 50f, 50f);
                _awardMarks[index] = CreateText(
                    "AwardMark_" + index, _front, 22, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Color32(232, 236, 238, 255),
                    168f + index * 70f, 84f, 50f, 50f);
            }

            _topTeamEmblem = CreateImage("TopTeamEmblem", _front, null, Color.clear,
                846f, 88f, 108f, 150f);
            _topTeamEmblem.preserveAspect = true;
            _topTeamFallback = CreateText(
                "TopTeamFallback", _front, 34, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color32(226, 232, 236, 245),
                846f, 88f, 108f, 150f);

            _frontEmblem = CreateImage("TeamEmblem", _front, null, Color.white,
                48f, 920f, 158f, 91f);
            _frontEmblem.preserveAspect = true;
            _playerName = CreateText("PlayerName", _front, 30, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color32(38, 41, 43, 255),
                212f, 920f, 570f, 92f);
            _season = CreateText("Season", _front, 19, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color32(56, 59, 61, 255),
                794f, 922f, 175f, 86f);
            _position = CreateText("Position", _front, 20, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color32(228, 232, 234, 255),
                58f, 82f, 92f, 52f);

            for (int index = 0; index < AbilityCount; index++)
            {
                float rowTop = 1038f + index * 64f;
                _abilityLabels[index] = CreateText(
                    "StatLabel_" + index,
                    _front,
                    18,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    new Color32(48, 50, 51, 255),
                    48f,
                    rowTop,
                    164f,
                    56f);
                _abilityFills[index] = CreateImage(
                    "StatFill_" + index,
                    _front,
                    null,
                    Color.white,
                    238f,
                    rowTop + 24f,
                    0f,
                    10f).rectTransform;
                _abilityValues[index] = CreateText(
                    "StatValue_" + index,
                    _front,
                    18,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    new Color32(48, 50, 51, 255),
                    812f,
                    rowTop,
                    164f,
                    56f);
            }

            _role = CreateText("Role", _front, 17, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color32(48, 50, 51, 255),
                104f, 1448f, 656f, 55f);
            _overall = CreateText("Overall", _front, 24, FontStyle.Bold,
                TextAnchor.MiddleRight, new Color32(35, 38, 40, 255),
                810f, 1444f, 150f, 62f);
            _frontGradeEffect = CreateFullImage("GradeEffect", _front, null, Color.clear);
            _frontGradeEffect.enabled = false;
        }

        private void BuildBack()
        {
            _backTeamColorOverlay = CreateFullImage(
                "TeamColorOverlay", _back, PlayerCardSprites.BackTeamColorOverlay, Color.white);
            CreateFullImage("NeutralFrame", _back, PlayerCardSprites.BackNeutral, Color.white);
            _backSpecialOverlay = CreateFullImage("SpecialCardOverlay", _back, null, Color.clear);
            _backSpecialOverlay.enabled = false;
            _backEmblem = CreateImage("TeamEmblem", _back, null, Color.white,
                318f, 470f, 388f, 388f);
            _backEmblem.preserveAspect = true;
            _backTeamName = CreateText("TeamName", _back, 28, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color32(50, 52, 53, 255),
                210f, 910f, 604f, 82f);
            _backGradeEffect = CreateFullImage("GradeEffect", _back, null, Color.clear);
            _backGradeEffect.enabled = false;
        }

        private void BindAbilities(PlayerProfileAbilityView[] abilities, Color primary)
        {
            for (int index = 0; index < AbilityCount; index++)
            {
                bool hasAbility = abilities != null && index < abilities.Length;
                PlayerProfileAbilityView ability = hasAbility ? abilities[index] : default;
                int value = hasAbility ? Mathf.Clamp(ability.StableValue, 0, 100) : 0;
                _abilityLabels[index].text = hasAbility ? GetAbilityLabel(ability.Ability) : string.Empty;
                _abilityValues[index].text = hasAbility ? value.ToString() : string.Empty;
                _abilityFills[index].gameObject.SetActive(hasAbility);
                _abilityFills[index].anchorMax = new Vector2(
                    (238f + 540f * value / 100f) / SourceWidth,
                    _abilityFills[index].anchorMax.y);
                _abilityFills[index].GetComponent<Image>().color = primary;
            }
        }

        private static void ApplyEmblem(Image image, int emblemId, Color fallbackColor)
        {
            if (TeamEmblemSprites.TryApply(image, emblemId))
                return;
            image.sprite = null;
            image.color = new Color(fallbackColor.r, fallbackColor.g, fallbackColor.b, 0.18f);
        }

        private void ApplyTopTeamEmblem(int emblemId, string teamName)
        {
            if (TeamEmblemSprites.TryApply(_topTeamEmblem, emblemId))
            {
                _topTeamFallback.text = string.Empty;
                return;
            }

            _topTeamEmblem.sprite = null;
            _topTeamEmblem.color = Color.clear;
            _topTeamFallback.text = string.IsNullOrWhiteSpace(teamName)
                ? "◆"
                : teamName.Substring(0, 1);
        }

        private static void ApplyGradeEffect(Image image, Sprite sprite, Color color)
        {
            image.sprite = sprite;
            image.color = color;
            image.enabled = sprite != null && color.a > 0f;
        }

        private static void ApplySpecialOverlay(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.enabled = sprite != null;
        }

        private static void ApplySlotSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.color = sprite == null ? Color.clear : Color.white;
        }

        private void SetAwardMarks(params string[] marks)
        {
            for (int index = 0; index < _awardMarks.Length; index++)
            {
                _awardSlots[index].sprite = null;
                _awardSlots[index].color = Color.clear;
                _awardMarks[index].text = marks != null && index < marks.Length
                    ? marks[index]
                    : string.Empty;
            }
        }

        private void ClearAwardMarks()
        {
            for (int index = 0; index < _awardMarks.Length; index++)
                _awardMarks[index].text = string.Empty;
        }

        private void ToggleSide()
        {
            SetShowingBack(!IsShowingBack);
        }

        private static RectTransform CreateRect(string name, Transform parent, bool stretch)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            return rect;
        }

        private static Image CreateFullImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = CreateRect(name, parent, stretch: true);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            float left,
            float top,
            float width,
            float height)
        {
            RectTransform rect = CreateRect(name, parent, stretch: false);
            ApplySourceRect(rect, left, top, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Color color,
            float left,
            float top,
            float width,
            float height)
        {
            RectTransform rect = CreateRect(name, parent, stretch: false);
            ApplySourceRect(rect, left, top, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void ApplySourceRect(
            RectTransform rect,
            float left,
            float top,
            float width,
            float height)
        {
            rect.anchorMin = new Vector2(left / SourceWidth, 1f - (top + height) / SourceHeight);
            rect.anchorMax = new Vector2((left + width) / SourceWidth, 1f - top / SourceHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Color ToColor(TeamColor color)
        {
            return new Color32(color.Red, color.Green, color.Blue, 255);
        }

        private void ApplyTeamColor(Color primary)
        {
            _frontTeamColorOverlay.color = primary;
            _backTeamColorOverlay.color = primary;
            _photoBackground.color = Color.Lerp(primary, Color.black, 0.38f);
        }

        private static Color GetReadableSecondary(Color primary)
        {
            float luminance = primary.r * 0.2126f + primary.g * 0.7152f + primary.b * 0.0722f;
            return luminance > 0.58f
                ? new Color32(43, 45, 46, 255)
                : new Color32(236, 230, 214, 255);
        }

        private static Color GetTopMetaColor(PlayerCardSpecialType specialType)
        {
            return specialType switch
            {
                PlayerCardSpecialType.AllStar => new Color32(220, 232, 240, 242),
                PlayerCardSpecialType.Mvp => new Color32(222, 188, 122, 242),
                PlayerCardSpecialType.GoldenGlove => new Color32(166, 106, 60, 242),
                _ => new Color32(164, 170, 174, 230)
            };
        }

        private static string GetPositionCode(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "C",
                PlayerPosition.FirstBase => "1B",
                PlayerPosition.SecondBase => "2B",
                PlayerPosition.ThirdBase => "3B",
                PlayerPosition.Shortstop => "SS",
                PlayerPosition.LeftField => "LF",
                PlayerPosition.CenterField => "CF",
                PlayerPosition.RightField => "RF",
                PlayerPosition.DesignatedHitter => "DH",
                PlayerPosition.StartingPitcher => "SP",
                PlayerPosition.ReliefPitcher => "RP",
                _ => "-"
            };
        }

        private static string GetAbilityLabel(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => "컨택",
                PlayerAbility.Power => "장타",
                PlayerAbility.Speed => "주루",
                PlayerAbility.Arm => "송구",
                PlayerAbility.Defense => "수비",
                PlayerAbility.BatterMental => "정신력",
                PlayerAbility.Stamina => "체력",
                PlayerAbility.Velocity => "구속",
                PlayerAbility.Stuff => "구위",
                PlayerAbility.Breaking => "변화구",
                PlayerAbility.Control => "제구",
                PlayerAbility.PitcherMental => "위기관리",
                _ => ability.ToString()
            };
        }
    }

    /// <summary>Resources의 공용 선수 카드 Sprite 레이어를 지연 로드한다.</summary>
    internal static class PlayerCardSprites
    {
        private const string Root = "UI/PlayerCards/";
        private static Sprite _frontNeutral;
        private static Sprite _frontTeamColorOverlay;
        private static Sprite _backNeutral;
        private static Sprite _backTeamColorOverlay;
        private static Sprite _topMetaCommon;
        private static Sprite _allStarFront;
        private static Sprite _allStarBack;
        private static Sprite _mvpFront;
        private static Sprite _mvpBack;
        private static Sprite _goldenGloveFront;
        private static Sprite _goldenGloveBack;

        public static Sprite FrontNeutral =>
            _frontNeutral ??= Resources.Load<Sprite>(Root + "PlayerCard_MainFrame_V2");

        public static Sprite FrontTeamColorOverlay =>
            _frontTeamColorOverlay ??= Resources.Load<Sprite>(Root + "PlayerCard_Front_TeamColorOverlay");

        public static Sprite BackNeutral =>
            _backNeutral ??= Resources.Load<Sprite>(Root + "PlayerCard_Back_Neutral");

        public static Sprite BackTeamColorOverlay =>
            _backTeamColorOverlay ??= Resources.Load<Sprite>(Root + "PlayerCard_Back_TeamColorOverlay");

        public static Sprite TopMetaCommon =>
            _topMetaCommon ??= Resources.Load<Sprite>(Root + "PlayerCard_TopMeta_Common");

        public static Sprite GetSpecialFront(PlayerCardSpecialType specialType)
        {
            return specialType switch
            {
                PlayerCardSpecialType.AllStar => _allStarFront ??= Load("PlayerCard_AllStar_FrontOverlay"),
                PlayerCardSpecialType.Mvp => _mvpFront ??= Load("PlayerCard_MVP_FrontOverlay"),
                PlayerCardSpecialType.GoldenGlove => _goldenGloveFront ??= Load("PlayerCard_GoldenGlove_FrontOverlay"),
                _ => null
            };
        }

        public static Sprite GetSpecialBack(PlayerCardSpecialType specialType)
        {
            return specialType switch
            {
                PlayerCardSpecialType.AllStar => _allStarBack ??= Load("PlayerCard_AllStar_BackOverlay"),
                PlayerCardSpecialType.Mvp => _mvpBack ??= Load("PlayerCard_MVP_BackOverlay"),
                PlayerCardSpecialType.GoldenGlove => _goldenGloveBack ??= Load("PlayerCard_GoldenGlove_BackOverlay"),
                _ => null
            };
        }

        private static Sprite Load(string assetName)
        {
            return Resources.Load<Sprite>(Root + assetName);
        }
    }
}
