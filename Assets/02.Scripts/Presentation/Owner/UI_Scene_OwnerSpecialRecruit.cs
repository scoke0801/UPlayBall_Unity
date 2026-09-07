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
        private RectTransform _legend;
        private RectTransform _careerHigh;
        private RectTransform _help;
        private Text _helpText;
        private Button _legendTab;
        private Button _careerTab;
        private bool _isCareerHigh;

        public event Action<string> RouteRequested;
        public event Action CloseRequested;

        /// <summary>실제 공용 셸 작업 영역에 특수 영입 화면을 생성한다.</summary>
        public static UI_Scene_OwnerSpecialRecruit CreateRuntime(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Scene_OwnerSpecialRecruit), parent);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Scene_OwnerSpecialRecruit>();
            view._root = root;
            view.Build();
            view.ShowRoute(LegendRoute);
            return view;
        }

        /// <summary>작업 영역의 표시 상태와 도움말 팝업을 함께 관리한다.</summary>
        public void SetVisible(bool visible)
        {
            _root.gameObject.SetActive(visible);
            if (!visible) _help.gameObject.SetActive(false);
        }

        /// <summary>영입 안내를 닫아 셸의 취소 입력을 소비한다.</summary>
        public bool TryHandleCancel()
        {
            if (_help == null || !_help.gameObject.activeSelf) return false;
            _help.gameObject.SetActive(false);
            return true;
        }

        /// <summary>레퍼런스에 따른 영입 화면을 선택한다.</summary>
        public void ShowRoute(string route)
        {
            _isCareerHigh = route != null && route.IndexOf("career", StringComparison.OrdinalIgnoreCase) >= 0;
            _legend.gameObject.SetActive(!_isCareerHigh);
            _careerHigh.gameObject.SetActive(_isCareerHigh);
            _legendTab.interactable = _isCareerHigh;
            _careerTab.interactable = !_isCareerHigh;
            _help.gameObject.SetActive(false);
        }

        private void Build()
        {
            Surface("SilverWorkspace", _root, new Color32(220, 225, 230, 255), 0, 0, 1, 1);
            Label("Title", _root, "특수 영입", 23, Ink, .025f, .925f, .26f, .99f, TextAnchor.MiddleLeft);
            _legendTab = Button("LegendTab", _root, "레전드 영입", .53f, .93f, .735f, .985f,
                () => Navigate(LegendRoute));
            _careerTab = Button("CareerHighTab", _root, "커리어하이 영입", .745f, .93f, .955f, .985f,
                () => Navigate(CareerHighRoute));
            _legend = Rect("Legend", _root, .018f, .11f, .982f, .912f);
            _careerHigh = Rect("CareerHigh", _root, .018f, .11f, .982f, .912f);
            BuildLegend(_legend);
            BuildCareerHigh(_careerHigh);
            Label("Availability", _root, "등록된 영입 대상이 없습니다. 대상 카드가 준비되면 이곳에서 확인할 수 있습니다.",
                13, Ink, .03f, .015f, .69f, .09f, TextAnchor.MiddleLeft);
            Button("Help", _root, "?  영입 안내", .71f, .025f, .835f, .085f, ShowHelp);
            Button("Close", _root, "나가기", .85f, .025f, .97f, .085f, () => CloseRequested?.Invoke());
            BuildHelp();
        }

        private void Navigate(string route)
        {
            ShowRoute(route);
            RouteRequested?.Invoke(route);
        }

        private void BuildLegend(RectTransform parent)
        {
            SectionTitle(parent, "레전드 선수", 0, .935f, .56f, 1);
            SectionTitle(parent, "영입 필요 선수카드", .574f, .935f, 1, 1);
            RectTransform preview = Surface("LegendPreview", parent, new Color32(51, 39, 25, 255), 0, .18f, .275f, .925f);
            Frame(preview, "Legend", false);
            EmptyProfile(parent);
            BuildSelectors(parent, .018f, .29f, .54f);
            RectTransform materials = Surface("Materials", parent, new Color32(241, 239, 225, 255), .574f, .18f, 1, .925f);
            for (int index = 0; index < 8; index++)
            {
                float x = .024f + (index % 4) * .244f;
                float y = index < 4 ? .515f : .055f;
                MaterialSlot(materials, index, x, y, x + .22f, y + .43f, false);
            }
            Label("Progress", parent, "재료 등록  0 / 8", 16, Ink, .60f, .11f, .98f, .17f);
            Button("AutoPlace", parent, "자동 배치", .585f, .015f, .765f, .095f, null, false);
            Button("Recruit", parent, "선수 영입", .78f, .015f, .99f, .095f, null, false);
        }

        private void EmptyProfile(RectTransform parent)
        {
            RectTransform profile = Surface("ProfilePaper", parent, new Color32(255, 251, 235, 255), .29f, .18f, .56f, .925f);
            Border(profile, Gold);
            Label("ProfileTitle", profile, "레전드 선수 정보", 18, Gold, .06f, .84f, .94f, .96f);
            string[] labels = { "선수", "프랜차이즈", "베이스 시즌", "포지션", "Cost" };
            for (int index = 0; index < labels.Length; index++)
            {
                float y = .72f - index * .11f;
                Label("Field" + index, profile, labels[index], 13, Ink, .06f, y, .55f, y + .09f, TextAnchor.MiddleLeft);
                Label("Value" + index, profile, "—", 16, Ink, .56f, y, .94f, y + .09f);
                Surface("Rule" + index, profile, new Color32(215, 207, 181, 255), .06f, y, .94f, y + .002f);
            }
            Label("ProfileEmpty", profile, "영입 대상을 선택하면\n선수 정보가 표시됩니다.", 14, Ink, .08f, .04f, .92f, .18f);
        }

        private void BuildCareerHigh(RectTransform parent)
        {
            RectTransform backdrop = Surface("BlueStage", parent, new Color32(8, 26, 67, 255), 0, 0, 1, 1);
            Image stageImage = backdrop.GetComponent<Image>();
            stageImage.sprite = Resources.Load<Sprite>("UI/SpecialRecruit/CareerHigh_Backdrop_v1");
            stageImage.color = Color.white;
            SectionTitle(parent, "커리어하이 카드", .012f, .93f, .275f, .99f);
            RectTransform preview = Surface("CareerHighPreview", parent, new Color32(30, 24, 20, 255), .012f, .19f, .275f, .918f);
            Frame(preview, "CareerHigh", false);
            Button("FranchiseSelector", parent, "구단 선택  ▾", .018f, .025f, .267f, .095f, null, false);
            Button("TargetSelector", parent, "영입 대상 없음  ▾", .018f, .105f, .267f, .175f, null, false);
            Label("TimelineTitle", parent, "영입 필요 선수카드 등록", 19, new Color32(255, 224, 155, 255), .31f, .90f, .95f, .99f, TextAnchor.MiddleLeft);
            for (int index = 0; index < 10; index++)
            {
                float x = .295f + index * .0685f;
                float y = index % 2 == 0 ? .48f : .235f;
                MaterialSlot(parent, index, x, y, x + .063f, y + .36f, true);
            }
            Label("TimelineProgress", parent, "서로 다른 연도  0 / 8", 15, Color.white, .59f, .11f, .96f, .18f);
            Button("Synthesize", parent, "커리어하이 합성", .75f, .025f, .98f, .10f, null, false);
            Label("TimelineNote", parent, "동일 선수 · 동일 프랜차이즈의 서로 다른 연도 Normal 카드 8장", 12,
                new Color32(203, 219, 244, 255), .31f, .02f, .73f, .10f, TextAnchor.MiddleLeft);
        }

        private void BuildSelectors(RectTransform parent, float left, float middle, float right)
        {
            Button("FranchiseSelector", parent, "구단 선택  ▾", left, .02f, .267f, .145f, null, false);
            Button("TargetSelector", parent, "영입 대상 없음  ▾", middle, .02f, right, .145f, null, false);
        }

        private void BuildHelp()
        {
            _help = Surface("RecruitHelp", _root, new Color(0, 0, 0, .72f), 0, 0, 1, 1);
            _help.GetComponent<Image>().raycastTarget = true;
            RectTransform sheet = Surface("HelpSheet", _help, new Color32(249, 246, 233, 255), .22f, .23f, .78f, .77f);
            Border(sheet, Gold);
            Label("Title", sheet, "특수 영입 안내", 23, Ink, .06f, .77f, .94f, .94f);
            _helpText = Label("Body", sheet, string.Empty, 17, Ink, .08f, .24f, .92f, .73f, TextAnchor.UpperLeft);
            Button("Dismiss", sheet, "확인", .35f, .065f, .65f, .19f, () => _help.gameObject.SetActive(false));
            _help.gameObject.SetActive(false);
        }

        private void ShowHelp()
        {
            _helpText.text = _isCareerHigh
                ? "같은 선수와 프랜차이즈의 서로 다른 연도 Normal 카드 8장으로 커리어하이 카드를 합성합니다.\n\n영입 대상이 등록되면 해당 선수의 유효 시즌과 재료를 확인할 수 있습니다."
                : "레전드 선수마다 정해진 재료 카드 8장을 모아 영입합니다.\n\n영입 대상이 등록되면 필요한 카드와 보유 여부를 확인할 수 있습니다. 재료는 최종 확인 후 소비됩니다.";
            _help.gameObject.SetActive(true);
            _help.SetAsLastSibling();
        }
    }
}
