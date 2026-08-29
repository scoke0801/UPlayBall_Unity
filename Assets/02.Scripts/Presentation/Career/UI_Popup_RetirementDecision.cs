using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>마지막 시즌 선언과 오프시즌 즉시 은퇴를 오입력 방지 확인 단계와 함께 제공한다.</summary>
    public sealed class UI_Popup_RetirementDecision : UIPopupBase
    {
        private static readonly Color BackdropColor = new(0.004f, 0.009f, 0.014f, 0.88f);
        private static readonly Color PanelColor = new(0.020f, 0.040f, 0.052f, 1f);
        private static readonly Color CardColor = new(0.045f, 0.075f, 0.090f, 1f);
        private static readonly Color AccentColor = new(0.78f, 0.61f, 0.28f, 1f);
        private static readonly Color PrimaryTextColor = new(0.95f, 0.95f, 0.91f, 1f);
        private static readonly Color SecondaryTextColor = new(0.66f, 0.72f, 0.73f, 1f);

        private CareerManager _manager;
        private RectTransform _content;
        private bool _isConfirming;

        public static UI_Popup_RetirementDecision ShowRuntime()
        {
            UI_Popup_RetirementDecision popup = Object.FindFirstObjectByType<UI_Popup_RetirementDecision>(
                FindObjectsInactive.Include);
            if (popup == null)
            {
                UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
                var popupObject = new GameObject(
                    nameof(UI_Popup_RetirementDecision), typeof(RectTransform), typeof(CanvasGroup));
                popupObject.transform.SetParent(uiManager.Root.GetLayerRoot(UILayer.Popup), false);
                popup = popupObject.AddComponent<UI_Popup_RetirementDecision>();
                Stretch(popupObject.GetComponent<RectTransform>());
            }
            popup.Show();
            return popup;
        }

        protected override void OnInitialize()
        {
            _manager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _content = (RectTransform)transform;
            Stretch(_content);
        }

        protected override void OnShow()
        {
            _isConfirming = false;
            Render();
        }

        private void Render()
        {
            ClearChildren(_content);
            RectTransform backdrop = CreateImage("Backdrop", _content, BackdropColor, Vector2.zero, Vector2.zero);
            Stretch(backdrop);
            backdrop.GetComponent<Image>().raycastTarget = true;
            RectTransform panel = CreateImage(
                "RetirementPanel", _content, PanelColor, new Vector2(820f, 500f), Vector2.zero);
            CreateText("Eyebrow", panel, "한 선수의 기록", 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(500f, 30f), new Vector2(0f, 190f), AccentColor);

            if (_manager.CurrentCareer == null)
            {
                RenderUnavailable(panel, "진행 중인 커리어가 없습니다.");
                return;
            }
            SeasonState season = _manager.CurrentCareer.CurrentLeague.CurrentSeason;
            if (_manager.IsFinalSeasonDeclared)
            {
                CreateText("Title", panel, "마지막 시즌을 진행 중입니다", 30, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(650f, 55f), new Vector2(0f, 125f), PrimaryTextColor);
                CreateText("Message", panel,
                    "팀의 마지막 정규시즌 또는 포스트시즌 경기가 끝나면\n은퇴 회고가 자동으로 시작됩니다.",
                    18, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(650f, 90f), new Vector2(0f, 25f), SecondaryTextColor);
                CreateCloseButton(panel);
                return;
            }

            bool canDeclare = season.Phase is SeasonPhase.Preseason or SeasonPhase.RegularSeason;
            bool canRetireNow = season.Phase is SeasonPhase.SeasonReview or SeasonPhase.Offseason;
            if (!canDeclare && !canRetireNow)
            {
                RenderUnavailable(panel, "현재 단계에서는 은퇴 결정을 확정할 수 없습니다.\n진행 중인 포스트시즌을 먼저 마쳐 주세요.");
                return;
            }

            string title = canDeclare ? "이번 시즌을 마지막으로 선언할까요?" : "지금 은퇴를 확정할까요?";
            string message = canDeclare
                ? "선언 후 시즌의 마지막 일정까지 진행합니다.\n마지막 경기 종료 뒤 회고 연출이 자동으로 시작됩니다."
                : "현재 시즌 기록을 고정하고 현역 로스터와 계약을 종료합니다.\n확정된 회고 기록은 이후 계산식이 바뀌어도 달라지지 않습니다.";
            if (_isConfirming)
            {
                title = canDeclare ? "마지막 시즌 선언을 확정합니다" : "선수 은퇴를 확정합니다";
                message = "이 선택은 현재 플레이 세션에서 되돌릴 수 없습니다.";
            }
            CreateText("Title", panel, title, 30, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 55f), new Vector2(0f, 125f), PrimaryTextColor);
            CreateText("Message", panel, message, 18, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(680f, 105f), new Vector2(0f, 28f), SecondaryTextColor);

            Button cancel = CreateButton("Cancel", panel, _isConfirming ? "다시 생각하기" : "닫기",
                new Vector2(260f, 58f), new Vector2(-145f, -135f), CardColor, out _);
            cancel.onClick.AddListener(() =>
            {
                if (_isConfirming)
                {
                    _isConfirming = false;
                    Render();
                }
                else
                {
                    Close();
                }
            });
            Button confirm = CreateButton("Confirm", panel,
                _isConfirming ? "확정" : canDeclare ? "마지막 시즌 선언" : "지금 은퇴",
                new Vector2(260f, 58f), new Vector2(145f, -135f), AccentColor, out _);
            confirm.onClick.AddListener(() =>
            {
                if (!_isConfirming)
                {
                    _isConfirming = true;
                    Render();
                    return;
                }
                bool succeeded = canDeclare
                    ? _manager.DeclareFinalSeason()
                    : _manager.RetireImmediately(RetirementReason.Voluntary);
                if (succeeded)
                    Close();
                else
                    RenderUnavailable(panel, _manager.LastError);
            });
            EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
        }

        private void RenderUnavailable(RectTransform panel, string message)
        {
            CreateText("Unavailable", panel, message, 20, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(660f, 130f), new Vector2(0f, 30f), SecondaryTextColor);
            CreateCloseButton(panel);
        }

        private void CreateCloseButton(RectTransform panel)
        {
            Button close = CreateButton("Close", panel, "닫기", new Vector2(260f, 58f),
                new Vector2(0f, -135f), CardColor, out _);
            close.onClick.AddListener(Close);
            EventSystem.current?.SetSelectedGameObject(close.gameObject);
        }

        private static RectTransform CreateImage(
            string name, Transform parent, Color color, Vector2 size, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateText(
            string name, Transform parent, string value, int size, FontStyle style,
            TextAnchor alignment, Vector2 dimensions, Vector2 position, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string name, Transform parent, string label, Vector2 size, Vector2 position,
            Color color, out Text text)
        {
            RectTransform rect = CreateImage(name, parent, color, size, position);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            text = CreateText("Label", rect, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter,
                size - new Vector2(10f, 8f), Vector2.zero, PrimaryTextColor);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
                Destroy(parent.GetChild(index).gameObject);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
