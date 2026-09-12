using System;
using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>감독 인선을 건드리지 않고 여섯 경기 운영 축만 독립 편집하는 감독방침 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerManagerPolicy : MonoBehaviour, IUiCancelHandler
    {
        private static readonly string[] AxisNames =
        {
            "타격방침", "도루시도", "번트시도", "대타기용", "선발교체", "중간교체"
        };

        private RectTransform _root;
        private readonly OwnerPolicyStepSelector[] _policySelectors = new OwnerPolicyStepSelector[AxisNames.Length];
        private readonly Text[] _valueLabels = new Text[AxisNames.Length];
        private Text _staff;
        private Text _profile;
        private Text _status;
        private OwnerDugoutSnapshot _snapshot;

        public event Action<OwnerDugoutConfigurationCommand> PolicyConfirmed;

        public static UI_Scene_OwnerManagerPolicy CreateRuntime(Transform parent)
        {
            var host = new GameObject(nameof(UI_Scene_OwnerManagerPolicy), typeof(RectTransform));
            host.transform.SetParent(parent, false);
            OwnerWorkspaceUiFactory.Stretch(host.GetComponent<RectTransform>());
            var view = host.AddComponent<UI_Scene_OwnerManagerPolicy>();
            view.Build();
            return view;
        }

        public void Bind(OwnerDugoutSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            int minimum = DugoutPolicySettings.NeutralLevel - snapshot.AllowedPolicyOffset;
            int maximum = DugoutPolicySettings.NeutralLevel + snapshot.AllowedPolicyOffset;
            for (int index = 0; index < _policySelectors.Length; index++)
            {
                _policySelectors[index].SetRange(minimum, maximum);
                _policySelectors[index].SetValue(
                    snapshot.Policy.GetLevel((DugoutPolicyAxis)index),
                    false);
                RefreshAxis(index);
            }
            OwnerDugoutStaffCandidate manager = snapshot.GetManager(snapshot.SelectedManagerId);
            OwnerDugoutStaffCandidate coach = snapshot.GetHeadCoach(snapshot.SelectedHeadCoachId);
            _staff.text = manager.DisplayName + " · " + manager.Specialty + "\n" +
                          coach.DisplayName + " · " + coach.Specialty + "\n\n" +
                          "감독 신뢰도 " + snapshot.ManagerTrust + "/100";
            RefreshPolicyDescription();
            SetFeedback(string.Empty, false);
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

        /// <summary>저장되지 않은 감독방침이 있을 때만 저장값으로 되돌린다.</summary>
        public bool TryHandleCancel()
        {
            if (_snapshot == null)
                return false;
            for (int index = 0; index < _policySelectors.Length; index++)
            {
                if (_policySelectors[index].Value != _snapshot.Policy.GetLevel((DugoutPolicyAxis)index))
                {
                    Restore();
                    return true;
                }
            }
            return false;
        }

        private void Build()
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(transform, "OwnerManagerPolicyWorkspace", true);
            RectTransform policy = OwnerDugoutDetailUiFactory.CreatePanel(_root, "PolicyPanel", 0.02f, 0.10f, 0.61f, 0.975f);
            RectTransform context = OwnerDugoutDetailUiFactory.CreatePanel(_root, "ContextPanel", 0.63f, 0.10f, 0.98f, 0.975f);
            OwnerDugoutDetailUiFactory.CreateLabel(policy, "Title", "감독 작전 방침", 0.04f, 0.91f, 0.96f, 0.98f, 21, FontStyle.Bold);
            OwnerDugoutDetailUiFactory.CreateLabel(policy, "Hint", "원하는 단계를 눌러 운영 방침을 선택하세요.", 0.04f, 0.86f, 0.96f, 0.91f, 12);
            for (int index = 0; index < _policySelectors.Length; index++) BuildAxis(policy, index);
            OwnerDugoutDetailUiFactory.CreateButton(policy, "Neutral", "모두 중립", 0.67f, 0.035f, 0.96f, 0.10f, ResetNeutral);

            OwnerDugoutDetailUiFactory.CreateLabel(context, "StaffTitle", "현재 인선", 0.06f, 0.91f, 0.94f, 0.98f, 19, FontStyle.Bold);
            _staff = OwnerDugoutDetailUiFactory.CreateLabel(context, "Staff", string.Empty, 0.06f, 0.62f, 0.94f, 0.89f, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            _profile = OwnerDugoutDetailUiFactory.CreateLabel(context, "Profile", string.Empty, 0.06f, 0.20f, 0.94f, 0.58f, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            OwnerDugoutDetailUiFactory.CreateLabel(context, "Rule", "감독 또는 수석코치 변경은 ‘덕아웃’ 탭에서 진행합니다.",
                0.06f, 0.08f, 0.94f, 0.16f, 12, FontStyle.Normal, TextAnchor.UpperLeft);

            _status = OwnerDugoutDetailUiFactory.CreateLabel(_root, "Status", string.Empty, 0.02f, 0.025f, 0.68f, 0.085f, 13);
            OwnerDugoutDetailUiFactory.CreateButton(_root, "Restore", "되돌리기", 0.70f, 0.02f, 0.82f, 0.085f, Restore);
            OwnerDugoutDetailUiFactory.CreateButton(_root, "Confirm", "결정", 0.84f, 0.02f, 0.98f, 0.085f, Confirm);
        }

        private void BuildAxis(Transform parent, int index)
        {
            float top = 0.83f - index * 0.12f;
            OwnerDugoutDetailUiFactory.CreateLabel(parent, "AxisName" + index, AxisNames[index], 0.04f, top - 0.065f, 0.23f, top, 14, FontStyle.Bold);
            RectTransform selectorRect = OwnerDugoutDetailUiFactory.CreateRect(
                parent,
                "AxisSteps" + index,
                0.25f,
                top - 0.055f,
                0.82f,
                top - 0.005f);
            var selector = new OwnerPolicyStepSelector(selectorRect, new Color(0.22f, 0.43f, 0.58f));
            _policySelectors[index] = selector;
            int axisIndex = index;
            selector.ValueChanged += _ => OnAxisChanged(axisIndex);
            _valueLabels[index] = OwnerDugoutDetailUiFactory.CreateLabel(parent, "AxisValue" + index, "중립", 0.84f, top - 0.065f, 0.96f, top, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        private void OnAxisChanged(int index)
        {
            RefreshAxis(index);
            RefreshPolicyDescription();
            SetFeedback("임시 방침을 조정했습니다. 결정하면 다음 경기부터 적용됩니다.", false);
        }

        private void RefreshAxis(int index)
        {
            int value = _policySelectors[index].Value;
            _valueLabels[index].text = value < DugoutPolicySettings.NeutralLevel ? "소극" : value > DugoutPolicySettings.NeutralLevel ? "적극" : "중립";
        }

        private void ResetNeutral()
        {
            for (int index = 0; index < _policySelectors.Length; index++)
                _policySelectors[index].SetValue(DugoutPolicySettings.NeutralLevel);
        }

        private void RefreshPolicyDescription()
        {
            if (_snapshot == null) return;
            var lines = new string[AxisNames.Length];
            for (int index = 0; index < lines.Length; index++)
                lines[index] = AxisNames[index] + " · " + DescribePolicy((DugoutPolicyAxis)index,
                    _policySelectors[index].Value);
            _profile.text = "선택한 운영 방침\n\n" + string.Join("\n", lines);
        }

        private static string DescribePolicy(DugoutPolicyAxis axis, int level)
        {
            if (level == DugoutPolicySettings.NeutralLevel) return "균형 유지";
            bool isAggressive = level > DugoutPolicySettings.NeutralLevel;
            return axis switch
            {
                DugoutPolicyAxis.BattingApproach => isAggressive ? "장타 중시" : "정확한 타격 중시",
                DugoutPolicyAxis.RunningAggression => isAggressive ? "적극적인 도루" : "안정적인 주루",
                DugoutPolicyAxis.SmallBallPreference => isAggressive ? "번트 기회 활용" : "타격 기회 중시",
                DugoutPolicyAxis.PinchHitAggression => isAggressive ? "적극적인 대타 기용" : "선발 타자 신뢰",
                DugoutPolicyAxis.HookSpeed => isAggressive ? "빠른 선발 교체" : "선발에게 긴 이닝 맡김",
                DugoutPolicyAxis.BullpenAggression => isAggressive ? "빠른 불펜 교체" : "불펜 투수에게 기회 부여",
                _ => "균형 유지"
            };
        }

        private DugoutPolicySettings CreateDraftPolicy()
        {
            return new DugoutPolicySettings(
                _policySelectors[0].Value,
                _policySelectors[1].Value,
                _policySelectors[2].Value,
                _policySelectors[3].Value,
                _policySelectors[4].Value,
                _policySelectors[5].Value);
        }

        private void Restore()
        {
            if (_snapshot == null) return;
            for (int index = 0; index < _policySelectors.Length; index++)
                _policySelectors[index].SetValue(_snapshot.Policy.GetLevel((DugoutPolicyAxis)index));
            SetFeedback("저장된 감독방침으로 되돌렸습니다.", false);
        }

        private void Confirm()
        {
            if (_snapshot == null) return;
            DugoutPolicySettings policy = CreateDraftPolicy();
            PolicyConfirmed?.Invoke(new OwnerDugoutConfigurationCommand(
                _snapshot.SelectedManagerId,
                _snapshot.SelectedHeadCoachId,
                policy));
        }

        private void OnDestroy()
        {
            PolicyConfirmed = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }
}
