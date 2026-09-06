using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Presentation.UI;
using Baseball.Presentation.SharedUI;
using Baseball.Core.Historical;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Baseball.Presentation.Owner
{
    /// <summary>보유 선수의 카드 앞면·뒷면과 실제 시즌 기본 능력치를 읽기 전용으로 표시한다.</summary>
    public sealed partial class UI_Popup_OwnerPlayerCard : MonoBehaviour
    {
        private static UI_Popup_OwnerPlayerCard _current;
        private Transform _source;
        private RectTransform _cardRoot;
        private RectTransform _front;
        private RectTransform _back;
        private bool _isFlipping;
        private bool _isBack;
        private static readonly Color Ink = new Color32(8, 10, 16, 255);
        private static readonly Color Gold = new Color32(218, 223, 232, 255);

        /// <summary>한 번에 하나의 상세 팝업을 최상단 Canvas에 연다.</summary>
        public static void Show(Transform source, OwnerCollectionCardSnapshot card)
        {
            if (_current != null) _current.Close();
            Canvas canvas = source.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerPlayerCard), canvas.transform);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerPlayerCard>();
            view._source = source;
            _current = view;
            var layer = root.gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true; layer.sortingOrder = 200;
            root.gameObject.AddComponent<GraphicRaycaster>();
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.84f);
            root.gameObject.AddComponent<Button>().onClick.AddListener(view.Close);
            RectTransform panel = Surface(root, "CardDetail", Ink, 0.5f, 0.5f, 0.5f, 0.5f);
            view._cardRoot = panel;
            view.ResizeCard();
            panel.GetComponent<Image>().raycastTarget = true;
            Button flip = panel.gameObject.AddComponent<Button>();
            flip.transition = Selectable.Transition.None;
            flip.onClick.AddListener(view.Flip);
            RectTransform close = Surface(root, "Close", Ink, 0.93f, 0.93f, 0.99f, 0.98f);
            close.GetComponent<Image>().raycastTarget = true;
            close.gameObject.AddComponent<Button>().onClick.AddListener(view.Close);
            Label(close, "Label", "닫기 ×", 0, 0, 1, 1, 14, Color.white);
            bool pitcher = card.Position == PlayerPosition.StartingPitcher || card.Position == PlayerPosition.ReliefPitcher;
            view._front = Surface(panel, "Front", Ink, 0, 0, 1, 1);
            view._back = Surface(panel, "Back", Ink, 0, 0, 1, 1);
            view.BuildFront(view._front, card, pitcher);
            view.BuildReferenceBack(view._back, card, pitcher);
            view._back.gameObject.SetActive(false);
            Label(root, "FlipHint", "카드 클릭: 앞/뒤 전환   ·   바깥 클릭: 닫기", 0.25f, 0.01f, 0.75f, 0.05f, 13, Color.white);
        }

        private void BuildFront(RectTransform parent, OwnerCollectionCardSnapshot card, bool pitcher)
        {
            BuildCardBorder(parent);
            RectTransform backdrop = Gradient(parent, "PortraitBackdrop", new Color32(21, 29, 61, 255),
                new Color32(3, 7, 19, 255), .018f, .46f, .982f, .982f);
            BuildPortraitLines(backdrop);
            Image portrait = Surface(parent, "Silhouette", Color.white, .08f, .46f, .92f, .945f).GetComponent<Image>();
            portrait.sprite = PlayerPortraitSprites.GetDefault(card.Position);
            portrait.preserveAspect = true;
            Gradient(parent, "HeaderBand", new Color32(52, 61, 78, 255), Ink, .02f, .934f, .98f, .982f);
            Label(parent, "Edition", OwnerCollectionPresentationBuilder.FormatEdition(card.Edition), .04f, .935f, .73f, .98f, 15, Color.white);
            Label(parent, "Enhancement", "+" + card.EnhancementLevel, .76f, .86f, .95f, .93f, 27, Gold);
            Label(parent, "EnhancementLabel", "강화", .76f, .825f, .95f, .86f, 11, Gold);
            if (card.IsLocked) Label(parent, "Locked", "잠금", .04f, .85f, .23f, .90f, 12, Gold);
            Gradient(parent, "PositionBadge", new Color32(106, 118, 140, 255), Ink, .04f, .485f, .29f, .555f);
            Label(parent, "Position", OwnerCollectionPresentationBuilder.FormatPosition(card.Position), .045f, .49f, .285f, .55f, 12, Color.white);
            Color nameTop = GetEditionColor(card.Edition);
            Gradient(parent, "NameBand", nameTop, Ink, .018f, .335f, .982f, .46f);
            Surface(parent, "NameHighlight", Gold, .02f, .456f, .98f, .459f);
            Label(parent, "Name", card.DisplayName, .05f, .35f, .77f, .445f, 26, Color.white);
            Gradient(parent, "YearBadge", new Color32(83, 87, 96, 255), Ink, .79f, .365f, .95f, .43f);
            Label(parent, "Year", (card.OriginYear % 100).ToString("00") + "′", .79f, .365f, .95f, .43f, 20, Color.white);
            Surface(parent, "Stats", Ink, .018f, .071f, .982f, .335f);
            string[] labels = pitcher ? new[] { "체력", "구속", "구위", "변화구", "제구력", "정신력" } :
                new[] { "교타력", "장타력", "주력", "송구력", "수비력", "정신력" };
            PlayerAbility[] abilities = pitcher ? new[] { PlayerAbility.Stamina, PlayerAbility.Velocity, PlayerAbility.Stuff,
                PlayerAbility.Breaking, PlayerAbility.Control, PlayerAbility.PitcherMental } :
                new[] { PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed, PlayerAbility.Arm, PlayerAbility.Defense, PlayerAbility.BatterMental };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = .285f - i * .039f;
                int? value = card.GetAbility(abilities[i]);
                Label(parent, "Ability" + i, labels[i], .04f, y, .235f, y + .04f, 14, Color.white);
                Gradient(parent, "Track" + i, new Color32(93, 97, 107, 255), new Color32(44, 47, 55, 255),
                    .25f, y + .014f, .79f, y + .031f);
                if (value.HasValue) Gradient(parent, "Fill" + i, Color.white, new Color32(194, 205, 225, 255),
                    .25f, y + .014f, .25f + .54f * Mathf.Clamp01(value.Value / (float)AbilityRatings.Maximum), y + .031f);
                Label(parent, "Value" + i, value?.ToString() ?? "—", .80f, y, .96f, y + .04f, 16, Color.white);
            }
            Gradient(parent, "CostBand", new Color32(100, 107, 120, 255), Ink, .018f, .018f, .982f, .073f);
            Label(parent, "CostLabel", "비용", .035f, .02f, .205f, .07f, 14, Gold);
            for (int index = 0; index < 10; index++)
            {
                float x = .22f + index * .058f;
                Label(parent, "CostStar" + index, "★", x, .022f, x + .056f, .069f, 16,
                    index < card.Cost ? Color.white : new Color32(37, 41, 49, 255));
            }
            Label(parent, "Cost", card.Cost.ToString(), .825f, .018f, .96f, .073f, 27, Color.white);
        }

        private static Color GetEditionColor(PlayerCardEdition edition)
        {
            return edition switch
            {
                PlayerCardEdition.AllStar => new Color32(129, 148, 179, 255),
                PlayerCardEdition.GoldenGlove => new Color32(144, 106, 57, 255),
                PlayerCardEdition.Mvp => new Color32(182, 150, 77, 255),
                _ => new Color32(55, 60, 72, 255)
            };
        }

        private static void BuildCardBorder(RectTransform parent)
        {
            Surface(parent, "OuterBorder", Ink, 0, 0, 1, 1);
            Gradient(parent, "MetalBorder", new Color32(145, 152, 170, 255), new Color32(58, 62, 74, 255), .009f, .009f, .991f, .991f);
            Surface(parent, "InnerBorder", Ink, .016f, .016f, .984f, .984f);
        }

        private static void BuildPortraitLines(RectTransform parent)
        {
            parent.gameObject.AddComponent<RectMask2D>();
            for (int index = 0; index < 7; index++)
            {
                RectTransform line = Surface(parent, "BackdropLine" + index, new Color(0.36f, .43f, .65f, .12f),
                    .06f + index * .14f, -.25f, .062f + index * .14f, 1.25f);
                line.localEulerAngles = new Vector3(0, 0, -24);
            }
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

        private static void Label(Transform parent, string name, string value, float x0, float y0, float x1, float y1, int size, Color color)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, FontStyle.Bold, TextAnchor.MiddleCenter, color);
            OwnerRuntimeUiFactory.SetAnchors(text.rectTransform, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 9; text.resizeTextMaxSize = size;
            text.raycastTarget = false;
        }

        private void LateUpdate()
        {
            if (_source == null || !_source.gameObject.activeInHierarchy) { Close(); return; }
            ResizeCard();
        }

        private void ResizeCard()
        {
            Rect bounds = ((RectTransform)transform).rect;
            const float aspect = 228f / 320f;
            float height = Mathf.Min(bounds.height * 0.86f, bounds.width * 0.84f / aspect);
            _cardRoot.sizeDelta = new Vector2(height * aspect, height);
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
        private void Close() { gameObject.SetActive(false); if (_current == this) _current = null; Destroy(gameObject); }
    }
}
