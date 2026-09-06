using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>두 장착 슬롯과 전체 발동 진행도를 분리해 보여주는 구단주 TeamColor 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerTeamColor : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _candidateContent;
        private readonly Text[] _slotLabels = new Text[2];
        private Text _detail;
        private Text _status;
        private Button _equipButton;
        private OwnerTeamColorSnapshot _snapshot;
        private OwnerTeamColorCandidateSnapshot _selectedCandidate;
        private readonly string[] _draftIds = new string[2];
        private int _selectedSlot;
        private bool _showOnlyActive;

        public event Action<string[]> SelectionConfirmed;

        public static UI_Scene_OwnerTeamColor CreateRuntime(Transform parent)
        {
            var host = new GameObject(nameof(UI_Scene_OwnerTeamColor), typeof(RectTransform));
            host.transform.SetParent(parent, false);
            OwnerWorkspaceUiFactory.Stretch(host.GetComponent<RectTransform>());
            var view = host.AddComponent<UI_Scene_OwnerTeamColor>();
            view.Build();
            return view;
        }

        public void Bind(OwnerTeamColorSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            for (int index = 0; index < _draftIds.Length; index++) _draftIds[index] = snapshot.EquippedIds[index];
            _selectedSlot = 0;
            _selectedCandidate = null;
            _showOnlyActive = false;
            RefreshAll();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        public void SetFeedback(string message, bool isError)
        {
            if (_status == null) return;
            _status.text = message ?? string.Empty;
            _status.color = isError ? new Color(0.72f, 0.16f, 0.12f) : new Color(0.12f, 0.35f, 0.20f);
        }

        private void Build()
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(transform, "OwnerTeamColorWorkspace", true);
            RectTransform slots = OwnerDugoutDetailUiFactory.CreatePanel(_root, "EquippedSlots", 0.015f, 0.10f, 0.285f, 0.975f);
            RectTransform candidates = OwnerDugoutDetailUiFactory.CreatePanel(_root, "CandidateList", 0.30f, 0.10f, 0.695f, 0.975f);
            RectTransform detail = OwnerDugoutDetailUiFactory.CreatePanel(_root, "DetailPanel", 0.71f, 0.10f, 0.985f, 0.975f);

            OwnerDugoutDetailUiFactory.CreateLabel(slots, "Title", "장착 팀컬러", 0.06f, 0.90f, 0.94f, 0.98f, 20, FontStyle.Bold);
            OwnerDugoutDetailUiFactory.CreateLabel(slots, "Preset", "선택 프리셋", 0.06f, 0.84f, 0.94f, 0.90f, 13);
            for (int index = 0; index < _slotLabels.Length; index++)
            {
                int slotIndex = index;
                Button button = OwnerDugoutDetailUiFactory.CreateButton(slots, "Slot" + index, "슬롯 " + (index + 1),
                    0.06f, 0.66f - index * 0.21f, 0.94f, 0.82f - index * 0.21f, () => SelectSlot(slotIndex));
                _slotLabels[index] = button.transform.Find("Label").GetComponent<Text>();
                _slotLabels[index].fontSize = 13;
            }
            OwnerDugoutDetailUiFactory.CreateButton(slots, "Clear", "선택 슬롯 해제", 0.06f, 0.30f, 0.94f, 0.37f, ClearSelectedSlot);
            OwnerDugoutDetailUiFactory.CreateLabel(slots, "Rule", "같은 팀컬러 중복 장착 불가\n최고 단계 전용 계열은 한 단계만 적용\n발동 인원을 채운 팀컬러만 장착 가능",
                0.06f, 0.09f, 0.94f, 0.27f, 13, FontStyle.Normal, TextAnchor.UpperLeft);

            OwnerDugoutDetailUiFactory.CreateLabel(candidates, "Title", "팀컬러 목록", 0.04f, 0.91f, 0.55f, 0.98f, 20, FontStyle.Bold);
            OwnerDugoutDetailUiFactory.CreateButton(candidates, "All", "전체", 0.58f, 0.91f, 0.76f, 0.975f, () => SetFilter(false));
            OwnerDugoutDetailUiFactory.CreateButton(candidates, "Active", "발동", 0.78f, 0.91f, 0.96f, 0.975f, () => SetFilter(true));
            _candidateContent = OwnerDugoutDetailUiFactory.CreateScrollContent(candidates, "Scroll", 0.035f, 0.04f, 0.965f, 0.89f, out _);

            OwnerDugoutDetailUiFactory.CreateLabel(detail, "Title", "조건과 적용 대상", 0.06f, 0.90f, 0.94f, 0.98f, 20, FontStyle.Bold);
            _detail = OwnerDugoutDetailUiFactory.CreateLabel(detail, "Description", "목록에서 팀컬러를 선택하세요.",
                0.06f, 0.25f, 0.94f, 0.89f, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            _detail.verticalOverflow = VerticalWrapMode.Overflow;
            _equipButton = OwnerDugoutDetailUiFactory.CreateButton(detail, "Equip", "선택 슬롯에 장착",
                0.06f, 0.14f, 0.94f, 0.22f, EquipSelected);
            _equipButton.interactable = false;

            _status = OwnerDugoutDetailUiFactory.CreateLabel(_root, "Status", string.Empty, 0.02f, 0.025f, 0.68f, 0.085f, 13);
            OwnerDugoutDetailUiFactory.CreateButton(_root, "Restore", "되돌리기", 0.70f, 0.02f, 0.82f, 0.085f, Restore);
            OwnerDugoutDetailUiFactory.CreateButton(_root, "Confirm", "결정", 0.84f, 0.02f, 0.98f, 0.085f, Confirm);
        }

        private void RefreshAll()
        {
            if (_snapshot == null) return;
            RefreshSlots();
            RebuildCandidates();
            _detail.text = "목록에서 팀컬러를 선택하면 필요 인원, 현재 적용 대상과 능력치 효과를 확인할 수 있습니다.";
            _equipButton.interactable = false;
            SetFeedback($"{_snapshot.PresetName} 프리셋 · 변경 전까지 저장되지 않습니다.", false);
        }

        private void RefreshSlots()
        {
            for (int index = 0; index < _slotLabels.Length; index++)
            {
                OwnerTeamColorCandidateSnapshot candidate = Find(_draftIds[index]);
                string marker = index == _selectedSlot ? "▶ " : string.Empty;
                _slotLabels[index].text = marker + "슬롯 " + (index + 1) + "\n" + (candidate?.Name ?? "비어 있음");
            }
        }

        private void RebuildCandidates()
        {
            OwnerDugoutDetailUiFactory.ClearChildren(_candidateContent);
            var visible = new List<OwnerTeamColorCandidateSnapshot>();
            for (int index = 0; index < _snapshot.Candidates.Count; index++)
                if (!_showOnlyActive || _snapshot.Candidates[index].IsActive) visible.Add(_snapshot.Candidates[index]);
            float height = Mathf.Max(1f, visible.Count * 62f);
            _candidateContent.sizeDelta = new Vector2(0f, height);
            for (int index = 0; index < visible.Count; index++)
            {
                OwnerTeamColorCandidateSnapshot candidate = visible[index];
                float top = 1f - index * 62f / height;
                float bottom = 1f - (index + 1) * 62f / height + 4f / height;
                Button button = OwnerDugoutDetailUiFactory.CreateButton(_candidateContent, "Candidate" + index,
                    (candidate.IsActive ? "발동  " : "미발동  ") + candidate.Name + "  " + candidate.ProgressText,
                    0.01f, bottom, 0.99f, top, () => SelectCandidate(candidate));
                button.transform.Find("Label").GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            }
        }

        private void SelectSlot(int slotIndex)
        {
            _selectedSlot = slotIndex;
            RefreshSlots();
        }

        private void SelectCandidate(OwnerTeamColorCandidateSnapshot candidate)
        {
            _selectedCandidate = candidate;
            string players = candidate.EligiblePlayerNames.Count == 0
                ? "없음"
                : string.Join(", ", candidate.EligiblePlayerNames);
            _detail.text = candidate.Name + "\n\n" + candidate.Definition.Description + "\n\n" +
                           "발동 인원  " + candidate.ProgressText + "\n" +
                           OwnerDugoutLoadoutPresentationBuilder.DescribeTeamColorEffect(candidate.Definition) + "\n\n" +
                           "적용 대상\n" + players;
            _equipButton.interactable = candidate.IsActive;
            if (!candidate.IsActive) SetFeedback("발동 인원을 채우지 못해 현재 장착할 수 없습니다.", true);
        }

        private void EquipSelected()
        {
            if (_selectedCandidate == null || !_selectedCandidate.IsActive) return;
            int other = 1 - _selectedSlot;
            if (string.Equals(_draftIds[other], _selectedCandidate.Id, StringComparison.Ordinal))
            {
                SetFeedback("같은 팀컬러는 두 슬롯에 중복 장착할 수 없습니다.", true);
                return;
            }
            OwnerTeamColorCandidateSnapshot otherCandidate = Find(_draftIds[other]);
            if (otherCandidate != null &&
                otherCandidate.Definition.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                _selectedCandidate.Definition.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                string.Equals(otherCandidate.StackGroup, _selectedCandidate.StackGroup, StringComparison.Ordinal))
            {
                SetFeedback("같은 최고 단계 전용 계열은 한 단계만 장착할 수 있습니다.", true);
                return;
            }
            _draftIds[_selectedSlot] = _selectedCandidate.Id;
            RefreshSlots();
            SetFeedback("임시 장착했습니다. 결정하면 선택 프리셋에 적용됩니다.", false);
        }

        private void ClearSelectedSlot()
        {
            _draftIds[_selectedSlot] = null;
            RefreshSlots();
            SetFeedback("선택 슬롯을 비웠습니다. 결정 전에는 저장되지 않습니다.", false);
        }

        private void SetFilter(bool activeOnly)
        {
            _showOnlyActive = activeOnly;
            RebuildCandidates();
        }

        private void Restore()
        {
            if (_snapshot == null) return;
            for (int index = 0; index < _draftIds.Length; index++) _draftIds[index] = _snapshot.EquippedIds[index];
            RefreshSlots();
            SetFeedback("저장된 팀컬러 구성으로 되돌렸습니다.", false);
        }

        private void Confirm()
        {
            var result = new string[_draftIds.Length];
            Array.Copy(_draftIds, result, result.Length);
            SelectionConfirmed?.Invoke(result);
        }

        private OwnerTeamColorCandidateSnapshot Find(string id)
        {
            if (string.IsNullOrEmpty(id) || _snapshot == null) return null;
            for (int index = 0; index < _snapshot.Candidates.Count; index++)
                if (string.Equals(_snapshot.Candidates[index].Id, id, StringComparison.Ordinal)) return _snapshot.Candidates[index];
            return null;
        }

        private void OnDestroy()
        {
            SelectionConfirmed = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }
}
