using System;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>세 매니저의 초상과 타입을 비교하고 선택 즉시 교체를 요청한다.</summary>
    public sealed class UI_Popup_OwnerFrontManager : MonoBehaviour
    {
        private static readonly string[] ManagerIds =
        {
            FrontManagerIds.DefaultAnalysis, FrontManagerIds.DefaultTest, FrontManagerIds.DefaultEnergetic
        };
        private static readonly string[] TypeLabels = { "분석형 매니저", "현장형 매니저", "활력형 매니저" };
        private readonly Button[] _choices = new Button[ManagerIds.Length];
        private readonly Text[] _states = new Text[ManagerIds.Length];
        private RectTransform _modal;
        private Text _message;
        private Button _close;
        public event Action<string> SelectionRequested;
        public event Action CloseRequested;

        /// <summary>공용 팝업 슬롯에 재사용 가능한 선택 화면을 구성한다.</summary>
        public static UI_Popup_OwnerFrontManager CreateRuntime(RectTransform host)
        {
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerFrontManager), host);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerFrontManager>();
            view.Build();
            root.gameObject.SetActive(false);
            return view;
        }

        private void Build()
        {
            var blocker = gameObject.AddComponent<Image>();
            blocker.color = CareerUiTheme.InputBlocker;
            blocker.raycastTarget = true;
            gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.InputBlocker);
            var panel = OwnerWorkspaceUiFactory.CreatePanel(transform, "ManagerSelection", "매니저 교체");
            _modal = panel.Root;
            _modal.anchorMin = _modal.anchorMax = new Vector2(.5f, .5f);
            _modal.sizeDelta = new Vector2(1000f, 660f);
            for (int index = 0; index < ManagerIds.Length; index++)
            {
                int choiceIndex = index;
                float left = index / 3f + .012f;
                float right = (index + 1) / 3f - .012f;
                var card = OwnerWorkspaceUiFactory.CreatePanel(panel.Content, "Manager" + index, TypeLabels[index]);
                Place(card.Root, left, .21f, right, .98f);
                Image portrait = OwnerRuntimeUiFactory.CreateImage("Portrait", card.Content, Color.white);
                portrait.sprite = FrontManagerPortraitSprites.LoadForManager(ManagerIds[index], "FM_NEUTRAL");
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
                Place(portrait.rectTransform, .02f, .18f, .98f, 1f);
                _choices[index] = OwnerWorkspaceUiFactory.CreateButton(card.Content, "SelectManager", "선택하기",
                    () => SelectionRequested?.Invoke(ManagerIds[choiceIndex]));
                Place((RectTransform)_choices[index].transform, .04f, 0f, .96f, .15f);
                _states[index] = _choices[index].GetComponentInChildren<Text>();
            }
            _message = OwnerWorkspaceUiFactory.CreateText(panel.Content, "Message",
                "함께할 프런트 매니저를 선택하세요.", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
            Place(_message.rectTransform, .025f, .025f, .72f, .17f);
            _close = OwnerWorkspaceUiFactory.CreateButton(panel.Content, "Close", "닫기", () => CloseRequested?.Invoke());
            Place((RectTransform)_close.transform, .76f, .045f, .975f, .145f);
        }

        /// <summary>현재 매니저를 표시하고 선택 입력을 팝업 내부에 한정한다.</summary>
        public void Show(string currentManagerId)
        {
            _message.text = "함께할 프런트 매니저를 선택하세요.";
            Button firstChoice = null;
            for (int index = 0; index < ManagerIds.Length; index++)
            {
                bool current = string.Equals(ManagerIds[index], currentManagerId, StringComparison.Ordinal);
                _choices[index].interactable = !current;
                _states[index].text = current ? "현재 매니저" : "선택하기";
                if (!current && firstChoice == null) firstChoice = _choices[index];
            }
            Button previous = _close;
            for (int index = 0; index < _choices.Length; index++)
            {
                if (!_choices[index].interactable) continue;
                Link(previous, _choices[index]);
                previous = _choices[index];
            }
            Link(previous, _close);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            FitModal();
            (firstChoice ?? _close).Select();
        }

        /// <summary>실패 사유를 표시하고 재선택 또는 닫기를 허용한다.</summary>
        public void ShowError() => _message.text = "매니저 선택을 저장하지 못했습니다. 다시 선택해 주세요.";

        /// <summary>선택을 변경하지 않고 팝업을 숨긴다.</summary>
        public void Hide() => gameObject.SetActive(false);

        private void OnRectTransformDimensionsChange()
        {
            if (_modal != null) FitModal();
        }

        private void FitModal()
        {
            Rect bounds = ((RectTransform)transform).rect;
            float scale = Mathf.Min(1f, Mathf.Min(bounds.width / 1048f, bounds.height / 708f));
            _modal.localScale = Vector3.one * Mathf.Max(.1f, scale);
        }

        private static void Link(Button previous, Button next)
        {
            Navigation before = previous.navigation;
            before.mode = Navigation.Mode.Explicit;
            before.selectOnRight = before.selectOnDown = next;
            previous.navigation = before;
            Navigation after = next.navigation;
            after.mode = Navigation.Mode.Explicit;
            after.selectOnLeft = after.selectOnUp = previous;
            next.navigation = after;
        }

        private static void Place(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
