using System;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>확정된 합성·특수 영입 결과를 공통 플립북 화면으로 표시한다.</summary>
    public sealed class UI_Popup_OwnerCardResult : MonoBehaviour, IUiCancelHandler, ICancelHandler
    {
        [SerializeField, Min(1f)] private float _framesPerSecond = 12f;
        private RawImage _effect;
        private RectTransform _card;
        private float _elapsed;
        private GameObject _previousFocus;
        private Action _closed;

        /// <summary>성공 결과와 종류별 스타일을 소비하며 게임 상태는 변경하지 않는다.</summary>
        public static UI_Popup_OwnerCardResult Show(RectTransform host, OwnerCollectionCardSnapshot card,
            OwnerCardResultKind kind, Action closed = null, GameObject returnFocus = null)
        {
            var root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerCardResult), host);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerCardResult>();
            view._previousFocus = returnFocus != null ? returnFocus : EventSystem.current?.currentSelectedGameObject;
            view._closed = closed;
            root.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            OwnerCardResultStyle style = OwnerCardResultStyle.Resolve(kind, card);
            view._framesPerSecond = style.FramesPerSecond;
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .88f);
            var panel = OwnerDugoutDetailUiFactory.CreatePanel(root, "Result", .35f, .13f, .65f, .87f);
            UIOwnerFrontOfficePanel.Apply(panel, "ManagerReport");
            Label(panel, "Title", style.Title, .08f, .88f, .92f, .96f, 30, style.Accent);
            var cardView = PlayerMiniCardView.CreateRuntime(panel, "ResultCard");
            view._card = (RectTransform)cardView.transform;
            // 카드의 표시 크기를 유지하고 외곽 여백만 줄인다.
            Place(view._card, .122f, .22f, .878f, .85f);
            cardView.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card, false), PlayerPortraitSprites.GetDefault(card.Position));
            var cardInput = cardView.GetComponent<CanvasGroup>() ?? cardView.gameObject.AddComponent<CanvasGroup>();
            cardInput.interactable = false; cardInput.blocksRaycasts = false;
            var stage = OwnerRuntimeUiFactory.CreateRect("EffectStage", panel);
            Place(stage, .03f, .19f, .97f, .85f);
            var effectRect = OwnerRuntimeUiFactory.CreateRect("ResultFlipbook", stage);
            var aspect = effectRect.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 1;
            view._effect = effectRect.gameObject.AddComponent<RawImage>();
            view._effect.raycastTarget = false;
            view._effect.texture = Resources.Load<Texture2D>(style.TexturePath);
            view._effect.enabled = view._effect.texture != null;
            view.SetFrame(0);
            Label(panel, "Summary", style.Summary,
                .05f, .12f, .95f, .21f, 24, Color.white);
            var close = OwnerRuntimeUiFactory.CreateButton("Close", panel, "확인", Color.white, 20);
            Place((RectTransform)close.transform, .122f, .03f, .878f, .11f);
            OwnerUiButtonSkin.Apply(close, OwnerButtonRole.Primary);
            close.navigation = new Navigation { mode = Navigation.Mode.None };
            close.onClick.AddListener(view.Close);
            root.SetAsLastSibling(); close.Select();
            return view;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            int frame = Mathf.FloorToInt(_elapsed * _framesPerSecond);
            if (frame >= 16) { _effect.enabled = false; _card.localScale = Vector3.one; return; }
            SetFrame(frame);
            float pulse = Mathf.Sin(Mathf.Clamp01(_elapsed / (16f / _framesPerSecond)) * Mathf.PI);
            _card.localScale = Vector3.one * (1f + pulse * .045f);
        }
        private void SetFrame(int frame) => _effect.uvRect = new Rect((frame % 4) * .25f, (3 - frame / 4) * .25f, .25f, .25f);
        /// <summary>연출 중에도 결과 화면을 즉시 닫는다.</summary>
        public bool TryHandleCancel() { Close(); return true; }
        public void OnCancel(BaseEventData eventData) { eventData.Use(); Close(); }
        private void Close() { gameObject.SetActive(false); Destroy(gameObject); }
        private void OnDisable()
        {
            Action closed = _closed;
            _closed = null;
            closed?.Invoke();
            if (_previousFocus != null && _previousFocus.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(_previousFocus);
        }
        private static void Label(Transform parent, string name, string text, float x0, float y0, float x1, float y1, int size, Color color)
        {
            var label = OwnerRuntimeUiFactory.CreateText(name, parent, text, size, FontStyle.Normal, TextAnchor.MiddleCenter, color);
            Place(label.rectTransform, x0, y0, x1, y1);
        }
        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>카드 결과의 연출 종류다.</summary>
    public enum OwnerCardResultKind { Synthesis, LegendRecruit, CareerHighRecruit }

    /// <summary>세 결과의 자산·문구·재생 속도를 한곳에서 정의한다.</summary>
    internal readonly struct OwnerCardResultStyle
    {
        private OwnerCardResultStyle(string title, string path, string summary, Color accent, float fps)
        { Title = title; TexturePath = path; Summary = summary; Accent = accent; FramesPerSecond = fps; }
        public string Title { get; }
        public string TexturePath { get; }
        public string Summary { get; }
        public Color Accent { get; }
        public float FramesPerSecond { get; }

        public static OwnerCardResultStyle Resolve(OwnerCardResultKind kind, OwnerCollectionCardSnapshot card)
        {
            switch (kind)
            {
                case OwnerCardResultKind.LegendRecruit:
                    return new OwnerCardResultStyle("레전드 영입 완료", "UI/SpecialRecruitFx/LegendAtlas",
                        card.DisplayName, OwnerDashboardStyle.Gold, 10f);
                case OwnerCardResultKind.CareerHighRecruit:
                    return new OwnerCardResultStyle("커리어 하이 영입 완료", "UI/SpecialRecruitFx/CareerHighAtlas",
                        card.DisplayName, new Color32(135, 191, 255, 255), 12f);
                default:
                    return new OwnerCardResultStyle("합성 성공", "UI/CardSynthesisFx/SynthesisAtlas",
                        card.DisplayName + "  +" + (card.EnhancementLevel - 1) + " → +" + card.EnhancementLevel,
                        OwnerDashboardStyle.Gold, 12f);
            }
        }
    }
}
