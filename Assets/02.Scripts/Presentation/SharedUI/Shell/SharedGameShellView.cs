using System;
using System.Collections.Generic;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// 1920×1080 PC Landscape 기준의 공용 Header, Navigation, Workspace 슬롯을 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed partial class SharedGameShellView : MonoBehaviour, ISharedGameShellView
    {
        /// <summary>
        /// Canvas Scaler가 사용하는 기준 너비다.
        /// </summary>
        public const float ReferenceWidth = 1920f;

        /// <summary>
        /// Canvas Scaler가 사용하는 기준 높이다.
        /// </summary>
        public const float ReferenceHeight = 1080f;

        [SerializeField] private RectTransform _globalTopBar;
        [SerializeField] private RectTransform _primaryNavigation;
        [SerializeField] private RectTransform _contextHeader;
        [SerializeField] private RectTransform _mainWorkspaceHost;
        [SerializeField] private RectTransform _rightInspectorHost;
        [SerializeField] private RectTransform _contextActionBarHost;
        [SerializeField] private RectTransform _popupHost;
        [SerializeField] private RectTransform _toastHost;
        [SerializeField] private RectTransform _tooltipHost;

        private Text _modeNameText;
        private Text _teamNameText;
        private Text _commonStatusText;
        private Text _nextMatchText;
        private Text _contextTitleText;
        private bool _isContextHeaderVisible = true;
        private Text _contextSummaryText;
        private Button _backButton;
        private Text _backButtonLabel;
        private Image _rootBackground;
        private Image _modeBackground;
        private RectTransform _statusSlotHost;
        private RectTransform _navigationEntryHost;
        private RectTransform _subTabHost;
        private readonly List<NavigationButtonBinding> _primaryButtons = new List<NavigationButtonBinding>();
        private readonly List<NavigationButtonBinding> _subTabButtons = new List<NavigationButtonBinding>();
        private GameModeUiProfile _profile;
        private string _activeRouteId = string.Empty;
        private bool _isInspectorVisible = true;
        private bool _isActionBarVisible = true;
        private bool _isChromeOverlayMode;

        /// <summary>
        /// 사용자가 활성 Navigation 항목을 선택하면 Route ID를 전달한다.
        /// </summary>
        public event Action<string> NavigationRequested;

        /// <summary>Context Screen에서 진입 원점으로 돌아가기를 요청한다.</summary>
        public event Action BackRequested;

        /// <summary>전역 설정 진입을 요청하면 현재 모드 Coordinator에 알린다.</summary>
        public event Action SettingsRequested;

        /// <summary>
        /// 화면별 Workspace가 들어갈 공용 콘텐츠 슬롯이다.
        /// </summary>
        public RectTransform MainWorkspaceHost
        {
            get
            {
                EnsureHierarchy();
                return _mainWorkspaceHost;
            }
        }

        /// <summary>
        /// 선택 선수나 구단 분석처럼 필요할 때만 표시하는 우측 Inspector 슬롯이다.
        /// </summary>
        public RectTransform RightInspectorHost
        {
            get
            {
                EnsureHierarchy();
                return _rightInspectorHost;
            }
        }

        /// <summary>
        /// 저장, 자동 구성, 비교처럼 현재 Workspace의 행동을 배치하는 슬롯이다.
        /// </summary>
        public RectTransform ContextActionBarHost
        {
            get
            {
                EnsureHierarchy();
                return _contextActionBarHost;
            }
        }

        /// <summary>
        /// 셸 위에 표시할 Popup의 합성 슬롯이다.
        /// </summary>
        public RectTransform PopupHost
        {
            get
            {
                EnsureHierarchy();
                return _popupHost;
            }
        }

        /// <summary>
        /// 짧은 결과 알림을 표시할 Toast 합성 슬롯이다.
        /// </summary>
        public RectTransform ToastHost
        {
            get
            {
                EnsureHierarchy();
                return _toastHost;
            }
        }

        /// <summary>
        /// Hover 설명을 표시할 Tooltip 합성 슬롯이다.
        /// </summary>
        public RectTransform TooltipHost
        {
            get
            {
                EnsureHierarchy();
                return _tooltipHost;
            }
        }

        /// <summary>
        /// 프리팹 통합 전에도 부모 아래에 완전한 공용 셸 계층을 생성한다.
        /// </summary>
        public static SharedGameShellView CreateRuntime(Transform parent, string objectName = "SharedGameShell")
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var shellObject = new GameObject(objectName, typeof(RectTransform));
            shellObject.transform.SetParent(parent, false);
            Stretch(shellObject.GetComponent<RectTransform>());
            return shellObject.AddComponent<SharedGameShellView>();
        }

        /// <summary>
        /// 모드 이름과 Capability로 필터링된 Navigation을 표시한다.
        /// </summary>
        public void BindProfile(GameModeUiProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            EnsureHierarchy();
            _modeNameText.text = profile.DisplayName;
            ApplyModeBackground(profile.BackgroundResourcePath);
            RenderPrimaryNavigation();
            RenderSubTabs();
        }

        /// <summary>
        /// 공통 상태와 모드 공급 상태 슬롯을 상단 바에 표시한다.
        /// </summary>
        public void BindStatus(ShellStatusModel status)
        {
            if (status == null)
                throw new ArgumentNullException(nameof(status));

            EnsureHierarchy();
            _teamNameText.text = status.TeamName;
            _commonStatusText.text = JoinStatus(status);
            _nextMatchText.text = status.NextMatchText;
            RenderStatusSlots(status.ModeSlots);
        }

        /// <summary>
        /// 현재 Route 선택 상태와 밀도 높은 Context Header 문구를 표시한다.
        /// </summary>
        public void BindContext(ShellContextModel context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            EnsureHierarchy();
            _activeRouteId = context.RouteId;
            _isContextHeaderVisible = true;
            _contextTitleText.text = string.IsNullOrEmpty(context.Eyebrow)
                ? context.Title
                : $"{context.Eyebrow} / {context.Title}";
            _contextSummaryText.text = context.Summary;
            _backButton.gameObject.SetActive(context.CanGoBack);
            _backButtonLabel.text = context.BackLabel;
            RefreshNavigationSelection(_primaryButtons);
            RenderSubTabs();
            UpdateWorkspaceOffsets();
        }

        /// <summary>
        /// 현재 화면이 분석 패널을 사용하지 않을 때 Workspace가 남는 너비를 채우게 한다.
        /// </summary>
        public void SetInspectorVisible(bool isVisible)
        {
            EnsureHierarchy();
            _isInspectorVisible = isVisible;
            _rightInspectorHost.gameObject.SetActive(!_isChromeOverlayMode && isVisible);
            UpdateWorkspaceOffsets();
        }

        /// <summary>현재 화면의 설명·보조 탭 영역을 숨기고 작업 영역에 높이를 돌려준다.</summary>
        public void SetContextHeaderVisible(bool isVisible)
        {
            EnsureHierarchy();
            _isContextHeaderVisible = isVisible;
            UpdateWorkspaceOffsets();
        }

        /// <summary>
        /// 현재 화면이 행동 버튼을 사용하지 않을 때 Workspace가 남는 높이를 채우게 한다.
        /// </summary>
        public void SetActionBarVisible(bool isVisible)
        {
            EnsureHierarchy();
            _isActionBarVisible = isVisible;
            _contextActionBarHost.gameObject.SetActive(!_isChromeOverlayMode && isVisible);
            UpdateWorkspaceOffsets();
        }

        /// <summary>
        /// Legacy Workspace를 단계적으로 이관하는 동안 단일 Header와 Navigation만 투명 Overlay로 표시한다.
        /// </summary>
        public void SetChromeOverlayMode(bool isEnabled)
        {
            EnsureHierarchy();
            _isChromeOverlayMode = isEnabled;
            _rootBackground.color = isEnabled ? Color.clear : Background;
            if (_modeBackground != null)
                _modeBackground.gameObject.SetActive(!isEnabled && _modeBackground.sprite != null);
            _mainWorkspaceHost.gameObject.SetActive(!isEnabled);
            _rightInspectorHost.gameObject.SetActive(!isEnabled && _isInspectorVisible);
            _contextActionBarHost.gameObject.SetActive(!isEnabled && _isActionBarVisible);
            _popupHost.gameObject.SetActive(!isEnabled);
            _toastHost.gameObject.SetActive(!isEnabled);
            _tooltipHost.gameObject.SetActive(!isEnabled);
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void HandleSettingsRequested()
        {
            SettingsRequested?.Invoke();
        }

        private void RenderPrimaryNavigation()
        {
            ClearChildren(_navigationEntryHost);
            _primaryButtons.Clear();

            IReadOnlyList<NavigationEntry> entries = _profile.Navigation.GetVisibleEntries(_profile.Capabilities);
            for (int i = 0; i < entries.Count; i++)
                _primaryButtons.Add(CreateNavigationButton(_navigationEntryHost, entries[i], false));

            RefreshNavigationSelection(_primaryButtons);
        }

        private void RenderSubTabs()
        {
            if (_subTabHost == null)
                return;

            ClearChildren(_subTabHost);
            _subTabButtons.Clear();
            if (_profile == null)
                return;

            NavigationEntry primary = _profile.FindNavigationGroup(_activeRouteId);
            if (primary == null)
                return;

            for (int i = 0; i < primary.Children.Count; i++)
            {
                NavigationEntry child = primary.Children[i];
                if (child.IsVisible(_profile.Capabilities))
                    _subTabButtons.Add(CreateNavigationButton(_subTabHost, child, true));
            }

            RefreshNavigationSelection(_subTabButtons);
        }

        private NavigationButtonBinding CreateNavigationButton(
            Transform parent,
            NavigationEntry entry,
            bool isSubTab)
        {
            RectTransform rect = CreateRect(entry.RouteId, parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = isSubTab ? 128f : 132f;
            layout.minWidth = isSubTab ? 106f : 108f;
            layout.flexibleHeight = 1f;

            Image background = rect.gameObject.AddComponent<Image>();
            background.color = isSubTab ? SubTabColor : NavigationColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.interactable = entry.IsEnabled;
            button.colors = CreateButtonColors(isSubTab);

            string displayName = entry.IsEnabled ? entry.DisplayName : $"{entry.DisplayName}  잠김";
            Text label = CreateText(
                "Label",
                rect,
                displayName,
                isSubTab ? 15 : 17,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                isSubTab ? CareerUiTheme.ReferenceText : TextPrimary);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(10f, 2f);
            label.rectTransform.offsetMax = new Vector2(-10f, -2f);

            RectTransform selectionAccent = CreateAnchoredImage(
                "SelectionAccent",
                rect,
                isSubTab ? CareerUiTheme.ReferenceAccent : CareerUiTheme.ReferenceAccentLight,
                Vector2.zero,
                new Vector2(1f, 0f),
                Vector2.zero,
                new Vector2(0f, 3f));
            selectionAccent.gameObject.SetActive(false);

            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = isSubTab
                ? CareerUiTheme.ReferenceBorder
                : CareerUiTheme.ShellDivider;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            string routeId = entry.RouteId;
            button.onClick.AddListener(() => NavigationRequested?.Invoke(routeId));
            return new NavigationButtonBinding(
                entry,
                background,
                label,
                selectionAccent.gameObject,
                isSubTab);
        }

        private void RenderStatusSlots(IReadOnlyList<ShellStatusSlotModel> slots)
        {
            ClearChildren(_statusSlotHost);
            for (int i = 0; i < slots.Count; i++)
            {
                ShellStatusSlotModel slot = slots[i];
                RectTransform slotRect = CreateRect(slot.SlotId, _statusSlotHost);
                var layout = slotRect.gameObject.AddComponent<LayoutElement>();
                layout.minWidth = 96f;
                layout.preferredWidth = 112f;
                layout.flexibleHeight = 1f;

                Image surface = slotRect.gameObject.AddComponent<Image>();
                surface.color = StatusSurface;
                surface.raycastTarget = false;

                Text label = CreateText(
                    "Label", slotRect, slot.Label.ToUpperInvariant(), 11, FontStyle.Bold,
                    TextAnchor.UpperCenter, TextMuted);
                SetAnchors(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 25f), new Vector2(-4f, -4f));

                Text value = CreateText(
                    "Value", slotRect, slot.Value, 16, FontStyle.Bold,
                    TextAnchor.LowerCenter, GetEmphasisColor(slot.Emphasis));
                SetAnchors(value.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -18f));
            }
        }

        private void RefreshNavigationSelection(List<NavigationButtonBinding> bindings)
        {
            if (_profile == null)
                return;

            NavigationEntry selectedPrimary = _profile.Navigation.FindPrimaryEntry(_profile.ResolveRouteId(_activeRouteId));
            for (int i = 0; i < bindings.Count; i++)
            {
                NavigationButtonBinding binding = bindings[i];
                bool isSelected = string.Equals(binding.Entry.RouteId, _activeRouteId, StringComparison.Ordinal) ||
                    ReferenceEquals(binding.Entry, selectedPrimary);
                binding.Background.color = isSelected
                    ? binding.IsSubTab ? CareerUiTheme.ReferencePanel : NavigationSelectedColor
                    : binding.IsSubTab ? SubTabColor : NavigationColor;
                binding.Label.color = binding.IsSubTab
                    ? isSelected ? CareerUiTheme.ReferenceAccent : CareerUiTheme.ReferenceText
                    : isSelected ? AccentLight : TextPrimary;
                binding.SelectionAccent.SetActive(isSelected);
            }
        }

        private void ApplyModeBackground(string resourcePath)
        {
            if (_modeBackground == null)
                return;

            _modeBackground.sprite = string.IsNullOrWhiteSpace(resourcePath)
                ? null
                : Resources.Load<Sprite>(resourcePath);
            _modeBackground.color = _modeBackground.sprite == null
                ? Color.clear
                : CareerUiTheme.ShellBackdropTint;
            _modeBackground.gameObject.SetActive(!_isChromeOverlayMode && _modeBackground.sprite != null);
        }

        private static string JoinStatus(ShellStatusModel status)
        {
            string[] values =
            {
                status.SeasonText,
                status.DateText,
                status.LeagueText,
                status.RankText
            };

            string result = string.Empty;
            for (int i = 0; i < values.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(values[i]))
                    continue;
                result = result.Length == 0 ? values[i] : $"{result}  ·  {values[i]}";
            }

            return result;
        }

        private static Color GetEmphasisColor(ShellStatusEmphasis emphasis)
        {
            switch (emphasis)
            {
                case ShellStatusEmphasis.Positive:
                    return Positive;
                case ShellStatusEmphasis.Warning:
                    return Warning;
                case ShellStatusEmphasis.Critical:
                    return Critical;
                default:
                    return TextPrimary;
            }
        }

        private sealed class NavigationButtonBinding
        {
            public NavigationButtonBinding(
                NavigationEntry entry,
                Image background,
                Text label,
                GameObject selectionAccent,
                bool isSubTab)
            {
                Entry = entry;
                Background = background;
                Label = label;
                SelectionAccent = selectionAccent;
                IsSubTab = isSubTab;
            }

            public NavigationEntry Entry { get; }
            public Image Background { get; }
            public Text Label { get; }
            public GameObject SelectionAccent { get; }
            public bool IsSubTab { get; }
        }
    }
}
