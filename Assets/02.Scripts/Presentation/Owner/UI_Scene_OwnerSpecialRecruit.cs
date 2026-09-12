using System;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>레전드 영입과 커리어하이 합성의 공용 화면 및 콘텐츠 미등록 상태를 표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed partial class UI_Scene_OwnerSpecialRecruit : MonoBehaviour, IUiCancelHandler
    {
        public const string LegendRoute = OwnerNavigationRoutes.SpecialRecruitLegend;
        public const string CareerHighRoute = OwnerNavigationRoutes.SpecialRecruitCareerHigh;
        private RectTransform _root;
        private RectTransform _help;
        private Text _helpText;
        private bool _isCareerHigh;

        public event Action CloseRequested;

        /// <summary>실제 공용 셸 작업 영역에 특수 영입 화면을 생성한다.</summary>
        public static UI_Scene_OwnerSpecialRecruit CreateRuntime(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Scene_OwnerSpecialRecruit), parent);
            OwnerRuntimeUiFactory.Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();
            var view = root.gameObject.AddComponent<UI_Scene_OwnerSpecialRecruit>();
            view._root = root;
            view.Build();
            view.ShowRoute(LegendRoute);
            view.EnsureContent();
            view.SelectTarget(-1);
            return view;
        }

        /// <summary>작업 영역의 표시 상태와 도움말 팝업을 함께 관리한다.</summary>
        public void SetVisible(bool visible)
        {
            _root.gameObject.SetActive(visible);
            if (!visible)
            {
                _help.gameObject.SetActive(false);
                ClosePicker();
                _isConfirming = false;
            }
        }

        /// <summary>영입 안내를 닫아 셸의 취소 입력을 소비한다.</summary>
        public bool TryHandleCancel()
        {
            if (_targets != null && _targets.IsOpen) { _targets.Close(); return true; }
            if (_help != null && _help.gameObject.activeSelf)
            {
                _help.gameObject.SetActive(false);
                SetModalActive(false);
                _root.Find("Help").GetComponent<Button>().Select();
                return true;
            }
            if (_picker != null && _picker.gameObject.activeSelf) { ClosePicker(); return true; }
            if (!_isConfirming) return false;
            _isConfirming = false;
            _transactionId = null;
            RefreshSelection();
            return true;
        }

        /// <summary>레퍼런스에 따른 영입 화면을 선택한다.</summary>
        public void ShowRoute(string route)
        {
            _targets?.Close();
            _isCareerHigh = route != null && route.IndexOf("career", StringComparison.OrdinalIgnoreCase) >= 0;
            _root.Find("Title").GetComponent<Text>().text = _isCareerHigh ? "커리어하이 영입" : "레전드 영입";
            _root.Find("Subtitle").GetComponent<Text>().text = _isCareerHigh
                ? "최고의 시즌을 영입하세요 · 같은 선수·구단의 서로 다른 연도 일반 카드 8장"
                : "구단의 역사를 만든 선수 · 지정된 재료 카드 8장을 모아 영입하세요";
            _help.gameObject.SetActive(false);
        }

        private void Build()
        {
            Label("Title", _root, "특수 영입", 26, Ink, .02f, .95f, .33f, .998f, TextAnchor.MiddleLeft);
            Label("Subtitle", _root, "", 15, Ink, .02f, .91f, .98f, .943f, TextAnchor.MiddleLeft);
            Label("Availability", _root, "최종 확인 후 재료 8장이 소모됩니다. 잠금·즐겨찾기·기용·위시·유학 중인 카드는 보호됩니다.",
                13, Ink, .03f, .015f, .69f, .09f, TextAnchor.MiddleLeft);
            Button("Help", _root, "?  영입 안내", .71f, .025f, .835f, .085f, ShowHelp);
            Button("Close", _root, "나가기", .85f, .025f, .97f, .085f, () => CloseRequested?.Invoke());
            BuildHelp();
        }

        private void BuildHelp()
        {
            _help = Surface("RecruitHelp", _root, CareerUiTheme.InputBlocker, 0, 0, 1, 1);
            _help.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            _help.GetComponent<Image>().raycastTarget = true;
            var panel = OwnerRuntimeUiFactory.CreatePanel("HelpSheet", _help, "특수 영입 안내");
            Place(panel.Root, .20f, .20f, .80f, .80f);
            RectTransform sheet = panel.Content;
            _helpText = Label("Body", sheet, string.Empty, 17, Ink, .04f, .24f, .96f, .96f, TextAnchor.UpperLeft);
            Button("Dismiss", sheet, "확인", .35f, .065f, .65f, .19f, () => TryHandleCancel());
            _help.gameObject.SetActive(false);
        }

        private void ShowHelp()
        {
            _helpText.text = _isCareerHigh
                ? "같은 선수와 구단의 서로 다른 연도 일반 카드 8장으로 커리어하이 카드를 영입합니다.\n\n재료 슬롯을 눌러 후보를 확인하세요. 자동 배치는 사용 가능한 카드만 등록합니다. 마지막 장을 소모하면 성장 상태도 사라집니다."
                : "레전드 선수마다 정해진 재료 카드 8장을 모아 영입합니다.\n\n재료 슬롯에서 필요한 카드와 보유 여부를 확인하세요. 재료는 최종 확인 후 소비되며 마지막 장의 성장 상태도 사라집니다.";
            _help.gameObject.SetActive(true);
            _help.SetAsLastSibling();
            SetModalActive(true);
            _help.GetComponentInChildren<Button>().Select();
        }
    }
}
