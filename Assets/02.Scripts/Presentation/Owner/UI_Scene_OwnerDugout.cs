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
    public sealed class UI_Scene_OwnerDugout : MonoBehaviour, IUiCancelHandler
    {
        private const string ManagerPortraitPath = "UI/OwnerDugout/OwnerManager_Silhouette_V1";
        private const string HeadCoachPortraitPath = "UI/OwnerDugout/OwnerHeadCoach_Silhouette_V1";
        private static readonly Color Canvas = CareerUiTheme.ReferenceCanvas;
        private static readonly Color Paper = CareerUiTheme.ReferencePanel;
        private static readonly Color PaperSubtle = CareerUiTheme.ReferencePanelHeader;
        private static readonly Color Border = CareerUiTheme.ReferenceBorder;
        private static readonly Color Ink = CareerUiTheme.ReferenceText;
        private static readonly Color MutedInk = CareerUiTheme.ReferenceTextSecondary;
        private static readonly Color Blue = CareerUiTheme.ReferenceAccent;
        private static readonly Color Red = CareerUiTheme.Loss;
        private readonly OwnerPolicyStepSelector[] _policySelectors = new OwnerPolicyStepSelector[6];
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
        private readonly Text[,] _summaryValues = new Text[4, 3];
        private readonly Image[,] _summaryFills = new Image[4, 3];
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

        /// <summary>인선 Overlay를 닫거나 저장되지 않은 덕아웃 편집을 원본으로 되돌린다.</summary>
        public bool TryHandleCancel()
        {
            if (_selectionOverlay != null && _selectionOverlay.gameObject.activeSelf)
            {
                CloseSelection();
                return true;
            }
            if (!HasDraftChanges())
                return false;
            RestoreSnapshot();
            return true;
        }

        private bool HasDraftChanges()
        {
            if (_snapshot == null ||
                !string.Equals(_draftManagerId, _snapshot.SelectedManagerId, StringComparison.Ordinal) ||
                !string.Equals(_draftHeadCoachId, _snapshot.SelectedHeadCoachId, StringComparison.Ordinal))
                return _snapshot != null;
            for (int index = 0; index < _policySelectors.Length; index++)
            {
                if (_policySelectors[index].Value != _snapshot.Policy.GetLevel((DugoutPolicyAxis)index))
                    return true;
            }
            return false;
        }

        private void Build(RectTransform host)
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(host, "OwnerDugoutWorkspace", false);
            _root.offsetMin = new Vector2(16f, 16f);
            _root.offsetMax = new Vector2(-16f, -16f);
            Surface(_root, Canvas);
            _root.gameObject.AddComponent<CareerUiPreserveTextColor>();

            // 전체 참조의 40:15:45 열 비율을 유지하고 상세 참조의 여섯 방침 행을 넣는다.
            RectTransform board = Box(_root, "DugoutBoard", 0.015f, 0.10f, 0.985f, 0.975f, Canvas);
            RectTransform policy = Box(board, "PolicyPanel", 0f, 0f, 0.40f, 1f, Paper);
            RectTransform policyHeader = Box(policy, "HeaderSurface", 0f, 0.93f, 1f, 1f, PaperSubtle);
            RectTransform policyAccent = Rect(policyHeader, "HeaderAccent", 0f, 0f, 1f, 0.045f);
            Surface(policyAccent, Blue);
            Label(policyHeader, "PolicyTitle", "작전 방침", 0.04f, 0f, 0.96f, 1f, 20, Ink,
                TextAnchor.MiddleLeft);
            CreatePolicy(policy, 0, "타격방침", "단타", "장타", "정확한 타격을 중시합니다.", "큰 것 한방을 적극적으로 노립니다.");
            CreatePolicy(policy, 1, "도루시도", "소극적", "적극적", "안정적인 주루를 중시합니다.", "기동력을 중시하는 야구를 펼칩니다.");
            CreatePolicy(policy, 2, "번트시도", "소극적", "적극적", "번트보다는 타격을 선호합니다.", "번트로 진루 기회를 만듭니다.");
            CreatePolicy(policy, 3, "대타기용", "소극적", "적극적", "선발 타자의 타격을 믿습니다.", "상황에 맞춰 대타를 적극 기용합니다.");
            CreatePolicy(policy, 4, "선발교체", "느리게", "빠르게", "선발을 믿고 긴 이닝을 책임지게 합니다.", "선발 투수를 일찍 교체합니다.");
            CreatePolicy(policy, 5, "중간교체", "느리게", "빠르게", "중계 투수에게 충분한 기회를 줍니다.", "중계 투수를 평소보다 일찍 교체합니다.");
            ActionButton(policy, "ResetPolicy", "모두 중립", 0.02f, 0.075f, 0.98f, 0.16f, ResetPolicy);
            Label(policy, "PolicyHint", "원하는 단계를 눌러 설명을 확인하세요.", 0.03f, 0.005f, 0.97f, 0.07f, 14, MutedInk);

            RectTransform staff = Box(board, "StaffColumn", 0.41f, 0f, 0.55f, 1f, Canvas);
            CreateStaff(staff, "Manager", "감독", ManagerPortraitPath,
                0.515f, 0.98f);
            CreateStaff(staff, "HeadCoach", "수석코치", HeadCoachPortraitPath,
                0.02f, 0.485f);
            RectTransform cards = Box(board, "CardSlots", 0.56f, 0f, 1f, 1f, Canvas);
            CreateSummaryCard(cards, 0, "공격 운영", new[] { "타격 접근", "도루 시도", "번트 시도" },
                "수치가 높을수록 강공과 기동력을 선호합니다.", Blue);
            CreateSummaryCard(cards, 1, "교체 운영", new[] { "대타 기용", "선발 훅", "불펜 투입" },
                "수치가 높을수록 빠르고 적극적으로 교체합니다.", Blue);
            CreateSummaryCard(cards, 2, "감독 고유 성향", new[] { "역할 고정", "상대 맞춤", "수비 교체" },
                "인선에 따라 달라지며 선수 능력치를 직접 올리지 않습니다.", Red);
            CreateSummaryCard(cards, 3, "신뢰와 지시 범위", new[] { "신뢰도", "조정 자유도", "선택 단계" },
                "신뢰도와 관계없이 -2부터 +2까지 선택할 수 있습니다.", Red);

            _status = Label(_root, "PreviewStatus", "덕아웃 데이터를 불러오는 중입니다.",
                0.02f, 0.01f, 0.65f, 0.08f, 16, Ink);
            _confirmButton = ActionButton(_root, "Confirm", "결정", 0.68f, 0.015f, 0.82f, 0.075f, ConfirmConfiguration);
            OwnerUiButtonSkin.Apply(_confirmButton, OwnerButtonRole.Primary);
            ActionButton(_root, "Cancel", "취소", 0.84f, 0.015f, 0.98f, 0.075f, RestoreSnapshot);
            _confirmButton.interactable = false;
            BuildSelectionOverlay();
            CareerUiSkin.Apply(_root);
            for (int index = 0; index < _policySelectors.Length; index++)
                _policySelectors[index].RefreshVisuals();
        }

        private void CreatePolicy(RectTransform parent, int index, string title, string low, string high,
            string lowDescription, string highDescription)
        {
            float top = 0.925f - index * 0.125f;
            RectTransform row = Box(parent, "PolicyRow" + index, 0.02f, top - 0.12f, 0.98f, top,
                index % 2 == 0 ? Paper : PaperSubtle);
            Color accent = index < 4 ? Blue : Red;
            Label(row, "Name", title, 0f, 0f, 0.22f, 1f, 20, accent);
            Label(row, "Low", low, 0.25f, 0.69f, 0.49f, 0.98f, 13, Ink, TextAnchor.MiddleLeft);
            Label(row, "High", high, 0.70f, 0.69f, 0.97f, 0.98f, 13, Ink, TextAnchor.MiddleRight);
            Text description = Label(row, "Description", "균형 잡힌 방침을 사용합니다.",
                0.24f, 0.03f, 0.98f, 0.39f, 16, accent);
            RectTransform control = Rect(row, "PolicySteps", 0.27f, 0.42f, 0.94f, 0.72f);
            var selector = new OwnerPolicyStepSelector(control, accent);
            selector.ValueChanged += value =>
            {
                description.text = value < 2f ? lowDescription : value > 2f ? highDescription : "균형 잡힌 방침을 사용합니다.";
                if (_snapshot != null) _status.text = "변경 사항이 있습니다. 결정하면 다음 경기부터 적용됩니다.";
            };
            _policySelectors[index] = selector;
        }

        private void CreateStaff(
            RectTransform parent,
            string name,
            string title,
            string portraitPath,
            float bottom,
            float top)
        {
            RectTransform panel = Box(parent, name, 0.06f, bottom, 0.94f, top, Paper);
            RectTransform header = Box(panel, "HeaderSurface", 0f, 0.85f, 1f, 1f, PaperSubtle);
            RectTransform accent = Rect(header, "HeaderAccent", 0f, 0f, 1f, 0.045f);
            Surface(accent, Blue);
            Label(header, "Title", title, 0.08f, 0f, 0.92f, 1f, 18, Ink, TextAnchor.MiddleLeft);
            RectTransform card = Box(panel, "StaffCard", 0.17f, 0.27f, 0.83f, 0.81f,
                CareerUiTheme.PortraitBackdrop);
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

        private void CreateSummaryCard(
            RectTransform parent,
            int index,
            string title,
            string[] metricNames,
            string note,
            Color accent)
        {
            int column = index % 2;
            int row = index / 2;
            RectTransform card = Box(parent, "SummaryCard" + index,
                0.025f + column * 0.495f, 0.515f - row * 0.495f,
                0.48f + column * 0.495f, 0.98f - row * 0.495f,
                Paper);

            RectTransform header = Box(card, "Header", 0.025f, 0.82f, 0.975f, 0.975f,
                PaperSubtle);
            RectTransform headerAccent = Rect(header, "Accent", 0f, 0f, 0.018f, 1f);
            Surface(headerAccent, accent);
            Label(header, "Title", title, 0.07f, 0.05f, 0.96f, 0.95f, 17, Ink, TextAnchor.MiddleLeft);

            for (int metricIndex = 0; metricIndex < 3; metricIndex++)
            {
                float top = 0.77f - metricIndex * 0.19f;
                Label(card, "MetricName" + metricIndex, metricNames[metricIndex],
                    0.06f, top - 0.11f, 0.36f, top, 14, Ink, TextAnchor.MiddleLeft);
                RectTransform track = Rect(card, "MetricTrack" + metricIndex, 0.38f, top - 0.078f, 0.75f, top - 0.035f);
                Surface(track, CareerUiTheme.ReferenceButton);
                RectTransform fill = Rect(track, "Fill", 0f, 0f, 0.5f, 1f);
                Surface(fill, accent);
                _summaryFills[index, metricIndex] = fill.GetComponent<Image>();
                _summaryValues[index, metricIndex] = Label(card, "MetricValue" + metricIndex, "--",
                    0.77f, top - 0.11f, 0.95f, top, 14, accent, TextAnchor.MiddleRight);
            }

            Label(card, "Note", note, 0.06f, 0.035f, 0.94f, 0.18f, 12, MutedInk,
                TextAnchor.MiddleLeft);
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
            Surface(_selectionOverlay, CareerUiTheme.InputBlocker);
            SetSkinRole(_selectionOverlay, CareerUiVisualRole.InputBlocker);
            RectTransform dialog = Box(_selectionOverlay, "StaffSelectionDialog", 0.17f, 0.08f, 0.83f, 0.92f, Paper);
            RectTransform dialogHeader = Box(dialog, "HeaderSurface", 0f, 0.92f, 1f, 1f, PaperSubtle);
            RectTransform dialogAccent = Rect(dialogHeader, "HeaderAccent", 0f, 0f, 1f, 0.035f);
            Surface(dialogAccent, Blue);
            _selectionTitle = Label(dialogHeader, "Title", "감독 선택", 0.04f, 0f, 0.85f, 1f, 20, Ink,
                TextAnchor.MiddleLeft);
            ActionButton(dialog, "Close", "×", 0.92f, 0.935f, 0.98f, 0.99f, CloseSelection);
            RectTransform preview = Box(dialog, "SelectedCard", 0.025f, 0.17f, 0.40f, 0.91f, PaperSubtle);
            _selectionDetail = Label(preview, "Detail", "후보를 선택하세요.", 0.08f, 0.08f, 0.92f, 0.92f, 17, Ink);
            _selectionInventory = Box(dialog, "StaffInventory", 0.425f, 0.17f, 0.975f, 0.91f, Paper);
            _selectionEmpty = Label(_selectionInventory, "EmptyState", string.Empty, 0.08f, 0.12f, 0.92f, 0.88f, 22, Ink);
            _selectionConfirmButton = ActionButton(dialog, "Confirm", "결정", 0.28f, 0.035f, 0.49f, 0.12f, ApplySelectedCandidate);
            OwnerUiButtonSkin.Apply(_selectionConfirmButton, OwnerButtonRole.Primary);
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
            for (int index = 0; index < _policySelectors.Length; index++)
                _policySelectors[index].SetValue(DugoutPolicySettings.NeutralLevel);
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
            for (int index = 0; index < _policySelectors.Length; index++)
            {
                _policySelectors[index].SetRange(minimum, maximum);
                _policySelectors[index].SetValue(
                    _snapshot.Policy.GetLevel((DugoutPolicyAxis)index),
                    true);
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
                _policySelectors[0].Value,
                _policySelectors[1].Value,
                _policySelectors[2].Value,
                _policySelectors[3].Value,
                _policySelectors[4].Value,
                _policySelectors[5].Value);
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
                    for (int index = 0; index < _policySelectors.Length; index++)
                    {
                        _policySelectors[index].SetRange(1, 3);
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
            SetSummaryMetric(0, 0, profile.BattingApproach, 100, profile.BattingApproach.ToString());
            SetSummaryMetric(0, 1, profile.RunningAggression, 100, profile.RunningAggression.ToString());
            SetSummaryMetric(0, 2, profile.SmallBallPreference, 100, profile.SmallBallPreference.ToString());
            SetSummaryMetric(1, 0, profile.PinchHitAggression, 100, profile.PinchHitAggression.ToString());
            SetSummaryMetric(1, 1, profile.HookSpeed, 100, profile.HookSpeed.ToString());
            SetSummaryMetric(1, 2, profile.BullpenAggression, 100, profile.BullpenAggression.ToString());
            SetSummaryMetric(2, 0, profile.BullpenRoleRigidity, 100, profile.BullpenRoleRigidity.ToString());
            SetSummaryMetric(2, 1, profile.MatchupPreference, 100, profile.MatchupPreference.ToString());
            SetSummaryMetric(2, 2, profile.DefensiveAggression, 100, profile.DefensiveAggression.ToString());
            SetSummaryMetric(3, 0, _snapshot.ManagerTrust, 100, _snapshot.ManagerTrust + " / 100");
            SetSummaryMetric(3, 1, _snapshot.AllowedPolicyOffset, 2, "±" + _snapshot.AllowedPolicyOffset);
            SetSummaryMetric(3, 2, 5, 5, "5단계 모두 가능");
        }

        private void SetSummaryMetric(int cardIndex, int metricIndex, int value, int maximum, string displayValue)
        {
            float ratio = maximum <= 0 ? 0f : Mathf.Clamp01(value / (float)maximum);
            RectTransform fill = _summaryFills[cardIndex, metricIndex].rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(ratio, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            _summaryValues[cardIndex, metricIndex].text = displayValue;
        }

        private static void CardBack(RectTransform parent)
        {
            RectTransform inset = Box(parent, "CardBackInset", 0.06f, 0.04f, 0.94f, 0.96f,
                PaperSubtle);
            Label(inset, "CardBackLabel", "UPlayBall", 0.05f, 0.40f, 0.95f, 0.60f, 25, Blue);
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
            SetSkinRole(rect, CareerUiVisualRole.DataImage);
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
