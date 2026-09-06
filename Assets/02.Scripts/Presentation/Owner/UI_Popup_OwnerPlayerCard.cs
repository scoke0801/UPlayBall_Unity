using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Presentation.UI;
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
        private static readonly Color Ink = new Color(0.04f, 0.07f, 0.12f);
        private static readonly Color Gold = new Color(0.89f, 0.76f, 0.47f);

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
            Image frame = Surface(parent, "ExistingCardFrame", Color.white, 0, 0, 1, 1).GetComponent<Image>();
            frame.sprite = Resources.Load<Sprite>("UI/PlayerCards/PlayerCard_MainFrame_V2");
            Surface(parent, "PortraitBackdrop", new Color(0.15f, 0.20f, 0.27f), 0.045f, 0.43f, 0.955f, 0.94f);
            Image portrait = Surface(parent, "Silhouette", Color.white, 0.13f, 0.44f, 0.87f, 0.91f).GetComponent<Image>();
            portrait.sprite = Resources.Load<Sprite>("UI/PlayerCards/PlayerPortrait_UpperSilhouette_V1");
            portrait.preserveAspect = true;
            Label(parent, "Edition", OwnerCollectionPresentationBuilder.FormatEdition(card.Edition), 0.06f, 0.91f, 0.94f, 0.98f, 19, Gold);
            Label(parent, "Year", card.OriginYear.ToString(), 0.69f, 0.82f, 0.94f, 0.91f, 23, Gold);
            Surface(parent, "NameBand", new Color(0.85f, 0.82f, 0.72f), 0.04f, 0.38f, 0.96f, 0.45f);
            Label(parent, "Name", card.DisplayName, 0.07f, 0.38f, 0.93f, 0.45f, 24, Ink);
            Surface(parent, "Stats", Ink, 0.04f, 0.06f, 0.96f, 0.375f);
            string[] labels = pitcher ? new[] { "체력", "구속", "구위", "변화구", "제구력", "정신력" } :
                new[] { "교타력", "장타력", "주력", "송구력", "수비력", "정신력" };
            PlayerAbility[] abilities = pitcher ? new[] { PlayerAbility.Stamina, PlayerAbility.Velocity, PlayerAbility.Stuff,
                PlayerAbility.Breaking, PlayerAbility.Control, PlayerAbility.PitcherMental } :
                new[] { PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed, PlayerAbility.Arm, PlayerAbility.Defense, PlayerAbility.BatterMental };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = 0.325f - i * 0.044f;
                int? value = card.GetAbility(abilities[i]);
                Label(parent, "Ability" + i, labels[i], 0.065f, y, 0.25f, y + 0.04f, 13, Color.white);
                Surface(parent, "Track" + i, new Color(0.31f, 0.34f, 0.39f), 0.27f, y + 0.014f, 0.79f, y + 0.027f);
                if (value.HasValue) Surface(parent, "Fill" + i, Gold, 0.27f, y + 0.014f,
                    0.27f + 0.52f * Mathf.Clamp01(value.Value / (float)AbilityRatings.Maximum), y + 0.027f);
                Label(parent, "Value" + i, value?.ToString() ?? "—", 0.80f, y, 0.94f, y + 0.04f, 14, Gold);
            }
            Surface(parent, "CostBand", new Color(0.36f, 0.29f, 0.16f), 0.04f, 0.02f, 0.96f, 0.07f);
            Label(parent, "Cost", "COST   " + card.Cost + "     ·     시즌 기본 능력치", 0.06f, 0.02f, 0.94f, 0.07f, 14, Color.white);
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
            float height = Mathf.Min(bounds.height * 0.86f, bounds.width * 0.84f * 1.5f);
            _cardRoot.sizeDelta = new Vector2(height / 1.5f, height);
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
