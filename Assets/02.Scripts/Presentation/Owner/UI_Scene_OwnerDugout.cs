using System;
using Baseball.Core.Historical;
using Baseball.Core.Teams;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>덕아웃 참조의 작전 방침, 감독·코치, 카드 네 칸을 표시하는 UI 전용 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerDugout : MonoBehaviour
    {
        private const string ManagerPortraitPath = "UI/OwnerDugout/OwnerManager_Silhouette_V1";
        private const string HeadCoachPortraitPath = "UI/OwnerDugout/OwnerHeadCoach_Silhouette_V1";
        private static readonly Color Paper = new Color(0.02f, 0.045f, 0.08f, 0.72f);
        private static readonly Color Border = CareerUiTheme.ShellGold;
        private static readonly Color Ink = CareerUiTheme.TextPrimary;
        private static readonly Color Blue = new Color(0.43f, 0.74f, 1f);
        private static readonly Color Red = new Color(1f, 0.48f, 0.54f);
        private readonly Slider[] _sliders = new Slider[6];
        private RectTransform _root;
        private RectTransform _selectionOverlay;
        private RectTransform _selectionInventory;
        private Text _selectionTitle;
        private Text _selectionEmpty;
        private Text _selectionDetail;
        private Text _status;
        private Text _managerName;
        private Text _managerEffect;
        private Text _headCoachName;
        private Text _headCoachEffect;
        private readonly Text[] _summaryCards = new Text[4];
        private Button _confirmButton;
        private Button _selectionConfirmButton;
        private OwnerDugoutSnapshot _snapshot;
        private string _draftManagerId = string.Empty;
        private string _draftHeadCoachId = string.Empty;
        private string _candidateId = string.Empty;
        private bool _isSelectingManager;

        public event Action<OwnerDugoutConfigurationCommand> ConfigurationConfirmed;

        /// <summary>셸의 전체 작업 영역에 덕아웃을 생성한다.</summary>
        public static UI_Scene_OwnerDugout CreateRuntime(RectTransform host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var view = new GameObject(nameof(UI_Scene_OwnerDugout)).AddComponent<UI_Scene_OwnerDugout>();
            view.Build(host);
            return view;
        }

        /// <summary>화면을 닫을 때 선택 창도 함께 닫는다.</summary>
        public void SetVisible(bool visible)
        {
            if (!visible) _selectionOverlay.gameObject.SetActive(false);
            _root.gameObject.SetActive(visible);
        }

        /// <summary>저장 원본과 실제 경기 적용값으로 화면의 임시 편집 상태를 초기화한다.</summary>
        public void Bind(OwnerDugoutSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            RestoreSnapshot();
            _confirmButton.interactable = true;
            _status.text = $"경기 적용 준비 · 감독 신뢰도 {snapshot.ManagerTrust}/100";
        }

        /// <summary>Game Command의 성공·실패를 덕아웃 상태 줄에 표시한다.</summary>
        public void SetFeedback(string message, bool isError)
        {
            _status.text = message ?? string.Empty;
            _status.color = isError ? Red : Ink;
        }

        private void Build(RectTransform host)
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(host, "OwnerDugoutWorkspace", false);
            _root.offsetMin = new Vector2(16f, 16f);
            _root.offsetMax = new Vector2(-16f, -16f);
            Surface(_root, Paper);
            SetSkinRole(_root, CareerUiVisualRole.TexturedPanel);
            _root.gameObject.AddComponent<CareerUiPreserveTextColor>();

            // 전체 참조의 40:15:45 열 비율을 유지하고 상세 참조의 여섯 방침 행을 넣는다.
            RectTransform board = Box(_root, "DugoutBoard", 0.015f, 0.10f, 0.985f, 0.975f, Paper);
            RectTransform policy = Box(board, "PolicyPanel", 0f, 0f, 0.40f, 1f, Paper);
            Label(policy, "PolicyTitle", "작전 방침", 0f, 0.93f, 1f, 1f, 24, Ink);
            CreatePolicy(policy, 0, "타격방침", "단타", "장타", "정확한 타격을 중시합니다.", "큰 것 한방을 적극적으로 노립니다.");
            CreatePolicy(policy, 1, "도루시도", "소극적", "적극적", "안정적인 주루를 중시합니다.", "기동력을 중시하는 야구를 펼칩니다.");
            CreatePolicy(policy, 2, "번트시도", "소극적", "적극적", "번트보다는 타격을 선호합니다.", "번트로 진루 기회를 만듭니다.");
            CreatePolicy(policy, 3, "대타기용", "소극적", "적극적", "선발 타자의 타격을 믿습니다.", "상황에 맞춰 대타를 적극 기용합니다.");
            CreatePolicy(policy, 4, "선발교체", "느리게", "빠르게", "선발을 믿고 긴 이닝을 책임지게 합니다.", "선발 투수를 일찍 교체합니다.");
            CreatePolicy(policy, 5, "중간교체", "느리게", "빠르게", "중계 투수에게 충분한 기회를 줍니다.", "중계 투수를 평소보다 일찍 교체합니다.");
            ActionButton(policy, "ResetPolicy", "작전 방침 없음", 0.02f, 0.075f, 0.98f, 0.16f, ResetPolicy);
            Label(policy, "PolicyHint", "방침을 움직여 설명을 확인하세요.", 0.03f, 0.005f, 0.97f, 0.07f, 16, Ink);

            RectTransform staff = Box(board, "StaffColumn", 0.41f, 0f, 0.55f, 1f, Paper);
            CreateStaff(staff, "Manager", "감독", ManagerPortraitPath,
                0.515f, 0.98f, new Color(0.22f, 0.17f, 0.07f, 0.72f));
            CreateStaff(staff, "HeadCoach", "수석코치", HeadCoachPortraitPath,
                0.02f, 0.485f, new Color(0.15f, 0.10f, 0.23f, 0.72f));
            RectTransform cards = Box(board, "CardSlots", 0.56f, 0f, 1f, 1f, Paper);
            for (int index = 0; index < 4; index++)
            {
                int column = index % 2;
                int row = index / 2;
                RectTransform card = Box(cards, "CardSlot" + index,
                    0.025f + column * 0.495f, 0.515f - row * 0.495f,
                    0.48f + column * 0.495f, 0.98f - row * 0.495f,
                    new Color(0.89f, 0.90f, 0.90f));
                _summaryCards[index] = Label(card, "Summary", "덕아웃 정보",
                    0.08f, 0.08f, 0.92f, 0.92f, 17, index < 2 ? Blue : Red);
            }

            _status = Label(_root, "PreviewStatus", "덕아웃 데이터를 불러오는 중입니다.",
                0.02f, 0.01f, 0.47f, 0.08f, 16, Ink);
            Button sell = ActionButton(_root, "Sell", "판매", 0.49f, 0.015f, 0.64f, 0.075f, null);
            _confirmButton = ActionButton(_root, "Confirm", "결정", 0.65f, 0.015f, 0.80f, 0.075f, ConfirmConfiguration);
            ActionButton(_root, "Cancel", "취소", 0.81f, 0.015f, 0.96f, 0.075f, RestoreSnapshot);
            // 감독·수석코치는 선수 카드 경제와 별도인 운영 인력이므로 판매 대상이 아니다.
            sell.interactable = false;
            _confirmButton.interactable = false;
            BuildSelectionOverlay();
            CareerUiSkin.Apply(_root);
        }

        private void CreatePolicy(RectTransform parent, int index, string title, string low, string high,
            string lowDescription, string highDescription)
        {
            float top = 0.925f - index * 0.125f;
            RectTransform row = Box(parent, "PolicyRow" + index, 0.02f, top - 0.12f, 0.98f, top, Paper);
            Color accent = index < 4 ? Blue : Red;
            Label(row, "Name", title, 0f, 0f, 0.22f, 1f, 20, accent);
            Label(row, "Low", low, 0.25f, 0.69f, 0.49f, 0.98f, 13, Ink, TextAnchor.MiddleLeft);
            Label(row, "High", high, 0.70f, 0.69f, 0.97f, 0.98f, 13, Ink, TextAnchor.MiddleRight);
            Text description = Label(row, "Description", "균형 잡힌 방침을 사용합니다.",
                0.24f, 0.03f, 0.98f, 0.39f, 16, accent);
            RectTransform control = Rect(row, "PolicySlider", 0.27f, 0.42f, 0.94f, 0.72f);
            Surface(control, Color.clear);
            var slider = control.gameObject.AddComponent<Slider>();
            RectTransform track = Rect(control, "Track", 0f, 0.30f, 1f, 0.70f);
            Surface(track, CareerUiTheme.ProgressTrack);
            SetSkinRole(track, CareerUiVisualRole.DataImage);
            RectTransform fillArea = Rect(track, "FillArea", 0f, 0f, 1f, 1f);
            RectTransform fill = Rect(fillArea, "Fill", 0f, 0f, 1f, 1f);
            Surface(fill, accent);
            SetSkinRole(fill, CareerUiVisualRole.DataImage);
            RectTransform handleArea = Rect(control, "HandleArea", 0f, 0f, 1f, 1f);
            RectTransform handle = Rect(handleArea, "Handle", 0f, 0f, 0f, 1f);
            Surface(handle, Color.white);
            SetSkinRole(handle, CareerUiVisualRole.DataImage);
            handle.sizeDelta = new Vector2(12f, 0f);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 4f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(2f);
            slider.onValueChanged.AddListener(value =>
            {
                description.text = value < 2f ? lowDescription : value > 2f ? highDescription : "균형 잡힌 방침을 사용합니다.";
                if (_snapshot != null) _status.text = "변경 사항이 있습니다. 결정하면 다음 경기부터 적용됩니다.";
            });
            _sliders[index] = slider;
        }

        private void CreateStaff(
            RectTransform parent,
            string name,
            string title,
            string portraitPath,
            float bottom,
            float top,
            Color tint)
        {
            RectTransform panel = Box(parent, name, 0.06f, bottom, 0.94f, top, tint);
            Label(panel, "Title", title, 0f, 0.85f, 1f, 1f, 21, name == "Manager" ? CareerUiTheme.AccentGold : new Color(0.78f, 0.65f, 0.94f));
            RectTransform card = Box(panel, "StaffCard", 0.17f, 0.27f, 0.83f, 0.81f, new Color(0.81f, 0.82f, 0.82f));
            if (!TryCreateStaffPortrait(card, portraitPath)) CardBack(card);
            Text currentName = Label(panel, "CurrentName", "미배정", 0.04f, 0.20f, 0.96f, 0.30f, 17, Ink);
            Text currentEffect = Label(panel, "CurrentEffect", "정보 없음", 0.04f, 0.135f, 0.96f, 0.22f, 12, Ink);
            if (name == "Manager")
            {
                _managerName = currentName;
                _managerEffect = currentEffect;
            }
            else
            {
                _headCoachName = currentName;
                _headCoachEffect = currentEffect;
            }
            ActionButton(panel, "Select", title == "감독" ? "감독 선택" : "코치 선택",
                0.10f, 0.02f, 0.90f, 0.13f, () => OpenSelection(title == "감독"));
        }

        private static bool TryCreateStaffPortrait(Transform parent, string resourcePath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null) return false;

            RectTransform portrait = Rect(parent, "Portrait", 0f, 0f, 1f, 1f);
            Image image = portrait.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return true;
        }

        private void BuildSelectionOverlay()
        {
            _selectionOverlay = Rect(_root, "StaffSelectionOverlay", 0f, 0f, 1f, 1f);
            Surface(_selectionOverlay, new Color(0f, 0f, 0f, 0.55f));
            SetSkinRole(_selectionOverlay, CareerUiVisualRole.InputBlocker);
            RectTransform dialog = Box(_selectionOverlay, "StaffSelectionDialog", 0.17f, 0.08f, 0.83f, 0.92f, Paper);
            SetSkinRole(dialog, CareerUiVisualRole.TexturedPanel);
            _selectionTitle = Label(dialog, "Title", "감독 선택", 0.025f, 0.92f, 0.85f, 0.995f, 24, Blue, TextAnchor.MiddleLeft);
            ActionButton(dialog, "Close", "×", 0.92f, 0.935f, 0.98f, 0.99f, CloseSelection);
            RectTransform preview = Box(dialog, "SelectedCard", 0.025f, 0.17f, 0.40f, 0.91f, new Color(0.90f, 0.86f, 0.72f));
            _selectionDetail = Label(preview, "Detail", "후보를 선택하세요.", 0.08f, 0.08f, 0.92f, 0.92f, 17, Ink);
            _selectionInventory = Box(dialog, "StaffInventory", 0.425f, 0.17f, 0.975f, 0.91f, Paper);
            _selectionEmpty = Label(_selectionInventory, "EmptyState", string.Empty, 0.08f, 0.12f, 0.92f, 0.88f, 22, Ink);
            _selectionConfirmButton = ActionButton(dialog, "Confirm", "결정", 0.28f, 0.035f, 0.49f, 0.12f, ApplySelectedCandidate);
            _selectionConfirmButton.interactable = false;
            ActionButton(dialog, "Exit", "나가기", 0.51f, 0.035f, 0.72f, 0.12f, CloseSelection);
            _selectionOverlay.gameObject.SetActive(false);
        }

        private void OpenSelection(bool isManager)
        {
            _isSelectingManager = isManager;
            _candidateId = string.Empty;
            string title = isManager ? "감독" : "수석코치";
            _selectionTitle.text = title + " 선택";
            _selectionDetail.text = "후보를 선택하면 전술 성향과 효과를 확인할 수 있습니다.";
            _selectionConfirmButton.interactable = false;
            RebuildCandidateButtons();
            _selectionOverlay.gameObject.SetActive(true);
            _selectionOverlay.SetAsLastSibling();
        }

        private void CloseSelection() => _selectionOverlay.gameObject.SetActive(false);

        private void ResetPolicy()
        {
            for (int index = 0; index < _sliders.Length; index++) _sliders[index].value = 2f;
            if (_snapshot != null) _status.text = "중립 방침으로 변경했습니다. 결정 전에는 저장되지 않습니다.";
        }

        private void RestoreSnapshot()
        {
            if (_snapshot == null)
            {
                ResetPolicy();
                return;
            }
            _draftManagerId = _snapshot.SelectedManagerId;
            _draftHeadCoachId = _snapshot.SelectedHeadCoachId;
            int minimum = DugoutPolicySettings.NeutralLevel - _snapshot.AllowedPolicyOffset;
            int maximum = DugoutPolicySettings.NeutralLevel + _snapshot.AllowedPolicyOffset;
            for (int index = 0; index < _sliders.Length; index++)
            {
                _sliders[index].minValue = minimum;
                _sliders[index].maxValue = maximum;
                _sliders[index].SetValueWithoutNotify(_snapshot.Policy.GetLevel((DugoutPolicyAxis)index));
            }
            RefreshStaffLabels();
            RefreshSummary();
            _status.color = Ink;
            _status.text = $"저장된 방침 · 감독 신뢰도 {_snapshot.ManagerTrust}/100";
        }

        private void ConfirmConfiguration()
        {
            if (_snapshot == null) return;
            var policy = new DugoutPolicySettings(
                (int)_sliders[0].value,
                (int)_sliders[1].value,
                (int)_sliders[2].value,
                (int)_sliders[3].value,
                (int)_sliders[4].value,
                (int)_sliders[5].value);
            ConfigurationConfirmed?.Invoke(new OwnerDugoutConfigurationCommand(
                _draftManagerId,
                _draftHeadCoachId,
                policy));
        }

        private void RebuildCandidateButtons()
        {
            for (int index = _selectionInventory.childCount - 1; index >= 0; index--)
            {
                Transform child = _selectionInventory.GetChild(index);
                if (child == _selectionEmpty.transform) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            if (_snapshot == null)
            {
                _selectionEmpty.text = "덕아웃 데이터를 불러오지 못했습니다.";
                _selectionEmpty.gameObject.SetActive(true);
                return;
            }
            _selectionEmpty.gameObject.SetActive(false);
            OwnerDugoutStaffCandidate[] source = _isSelectingManager ? _snapshot.Managers : _snapshot.HeadCoaches;
            for (int index = 0; index < source.Length; index++)
            {
                OwnerDugoutStaffCandidate candidate = source[index];
                float top = 0.96f - index * 0.15f;
                ActionButton(_selectionInventory, "Candidate" + index,
                    candidate.DisplayName + " · " + candidate.Specialty,
                    0.05f, top - 0.115f, 0.95f, top,
                    () => SelectCandidate(candidate));
            }
        }

        private void SelectCandidate(OwnerDugoutStaffCandidate candidate)
        {
            _candidateId = candidate.Id;
            _selectionDetail.text = candidate.DisplayName + "\n" + candidate.Specialty + "\n\n" +
                                    candidate.Description + "\n\n" + candidate.EffectDescription;
            _selectionConfirmButton.interactable = true;
        }

        private void ApplySelectedCandidate()
        {
            if (string.IsNullOrEmpty(_candidateId)) return;
            if (_isSelectingManager)
            {
                _draftManagerId = _candidateId;
                if (!string.Equals(_draftManagerId, _snapshot.SelectedManagerId, StringComparison.Ordinal))
                {
                    for (int index = 0; index < _sliders.Length; index++)
                    {
                        _sliders[index].minValue = 1f;
                        _sliders[index].maxValue = 3f;
                        _sliders[index].value = Mathf.Clamp(_sliders[index].value, 1f, 3f);
                    }
                }
            }
            else _draftHeadCoachId = _candidateId;
            RefreshStaffLabels();
            _status.text = "인선 변경 사항이 있습니다. 결정하면 다음 경기부터 적용됩니다.";
            CloseSelection();
        }

        private void RefreshStaffLabels()
        {
            if (_snapshot == null) return;
            OwnerDugoutStaffCandidate manager = _snapshot.GetManager(_draftManagerId);
            OwnerDugoutStaffCandidate coach = _snapshot.GetHeadCoach(_draftHeadCoachId);
            _managerName.text = manager.DisplayName + " · " + manager.Specialty;
            _managerEffect.text = manager.EffectDescription;
            _headCoachName.text = coach.DisplayName + " · " + coach.Specialty;
            _headCoachEffect.text = coach.EffectDescription;
        }

        private void RefreshSummary()
        {
            if (_snapshot == null) return;
            ManagerTacticalProfile profile = _snapshot.EffectiveProfile;
            _summaryCards[0].text = "타격·주루 판단\n\n타격 " + profile.BattingApproach +
                                    "\n도루 " + profile.RunningAggression +
                                    "\n번트 " + profile.SmallBallPreference;
            _summaryCards[1].text = "교체 판단\n\n대타 " + profile.PinchHitAggression +
                                    "\n선발 훅 " + profile.HookSpeed +
                                    "\n불펜 " + profile.BullpenAggression;
            _summaryCards[2].text = "감독 세부 성향\n\n역할 고정 " + profile.BullpenRoleRigidity +
                                    "\n상대 맞춤 " + profile.MatchupPreference +
                                    "\n수비 교체 " + profile.DefensiveAggression;
            _summaryCards[3].text = "신뢰도 " + _snapshot.ManagerTrust + "/100\n\n현재 조정 범위 ±" +
                                    _snapshot.AllowedPolicyOffset + "\n60 이상에서 ±2 해금\n능력치 직접 보정 없음";
        }

        private static void CardBack(RectTransform parent)
        {
            SetSkinRole(parent, CareerUiVisualRole.TexturedPanel);
            RectTransform inset = Box(parent, "CardBackInset", 0.06f, 0.04f, 0.94f, 0.96f, Paper);
            Label(inset, "CardBackLabel", "UPlayBall", 0.05f, 0.40f, 0.95f, 0.60f, 25, CareerUiTheme.AccentGold);
        }

        private static RectTransform Rect(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, left, bottom, right, top);
            return rect;
        }

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Surface(RectTransform rect, Color color)
        {
            rect.gameObject.AddComponent<Image>().color = color;
            SetSkinRole(rect, CareerUiVisualRole.FlatSurface);
        }

        private static void SetSkinRole(RectTransform rect, CareerUiVisualRole role)
        {
            var visual = rect.GetComponent<CareerUiVisualElement>() ?? rect.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(role);
            // 프레임 텍스처와 기존 Outline이 중복되지 않게 한다.
            Outline outline = rect.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }

        private static RectTransform Box(Transform parent, string name, float left, float bottom, float right, float top, Color color)
        {
            RectTransform rect = Rect(parent, name, left, bottom, right, top);
            Surface(rect, color);
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        private static Text Label(Transform parent, string name, string text, float left, float bottom, float right, float top,
            int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text label = OwnerWorkspaceUiFactory.CreateText(parent, name, text, size, FontStyle.Bold, alignment, color);
            label.color = color;
            Place(label.rectTransform, left, bottom, right, top);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = size;
            return label;
        }

        private static Button ActionButton(Transform parent, string name, string title, float left, float bottom,
            float right, float top, Action action)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, title, action);
            Place((RectTransform)button.transform, left, bottom, right, top);
            button.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.TexturedAction);
            button.transform.Find("Label").GetComponent<Text>().color = Ink;
            return button;
        }

        private void OnDestroy()
        {
            ConfigurationConfirmed = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }
}
