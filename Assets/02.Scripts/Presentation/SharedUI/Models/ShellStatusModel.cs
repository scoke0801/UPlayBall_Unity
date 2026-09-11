using System;
using System.Collections.Generic;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// 상단 상태 슬롯의 시각적 중요도를 나타낸다.
    /// </summary>
    public enum ShellStatusEmphasis
    {
        Normal = 0,
        Positive = 1,
        Warning = 2,
        Critical = 3
    }

    /// <summary>
    /// 모드가 상단 바에 공급하는 하나의 의미 있는 상태 값을 표현한다.
    /// </summary>
    public sealed class ShellStatusSlotModel
    {
        /// <summary>
        /// 상태 슬롯의 식별자, 라벨, 값을 만든다.
        /// </summary>
        public ShellStatusSlotModel(
            string slotId,
            string label,
            string value,
            ShellStatusEmphasis emphasis = ShellStatusEmphasis.Normal,
            string tooltip = null)
        {
            if (string.IsNullOrWhiteSpace(slotId))
                throw new ArgumentException("상태 슬롯 식별자는 비어 있을 수 없습니다.", nameof(slotId));

            SlotId = slotId;
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Emphasis = emphasis;
            Tooltip = tooltip ?? string.Empty;
        }

        /// <summary>
        /// 갱신 시 같은 슬롯을 식별하는 안정적인 ID다.
        /// </summary>
        public string SlotId { get; }

        /// <summary>
        /// 값의 의미를 짧게 설명하는 라벨이다.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// 이미 형식화된 표시 값이다.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 색상과 아이콘 선택에 사용할 중요도다.
        /// </summary>
        public ShellStatusEmphasis Emphasis { get; }

        /// <summary>
        /// 상세 설명이 필요할 때 표시할 Tooltip 문구다.
        /// </summary>
        public string Tooltip { get; }
    }

    /// <summary>
    /// 공용 상단 바가 표시할 시즌, 구단, 다음 경기와 모드 전용 상태를 묶는다.
    /// </summary>
    public sealed class ShellStatusModel
    {
        private readonly ShellStatusSlotModel[] _modeSlots;

        /// <summary>
        /// 공통 상태와 모드가 공급한 추가 상태 슬롯으로 모델을 만든다.
        /// </summary>
        public ShellStatusModel(
            string seasonText,
            string dateText,
            string leagueText,
            string teamName,
            string rankText,
            string nextMatchText,
            IReadOnlyList<ShellStatusSlotModel> modeSlots = null)
        {
            SeasonText = seasonText ?? string.Empty;
            DateText = dateText ?? string.Empty;
            LeagueText = leagueText ?? string.Empty;
            TeamName = teamName ?? string.Empty;
            RankText = rankText ?? string.Empty;
            NextMatchText = nextMatchText ?? string.Empty;
            _modeSlots = CopySlots(modeSlots);
        }

        /// <summary>
        /// 현재 시즌 또는 연도 표시다.
        /// </summary>
        public string SeasonText { get; }

        /// <summary>
        /// 현재 날짜 또는 주차 표시다.
        /// </summary>
        public string DateText { get; }

        /// <summary>
        /// 현재 League Grade 표시다.
        /// </summary>
        public string LeagueText { get; }

        /// <summary>
        /// 현재 소속 구단 표시다.
        /// </summary>
        public string TeamName { get; }

        /// <summary>
        /// 현재 순위 표시다.
        /// </summary>
        public string RankText { get; }

        /// <summary>
        /// 다음 경기까지의 상태 표시다.
        /// </summary>
        public string NextMatchText { get; }

        /// <summary>
        /// 구단주 자원 또는 선수 상태처럼 모드가 공급한 추가 슬롯이다.
        /// </summary>
        public IReadOnlyList<ShellStatusSlotModel> ModeSlots => _modeSlots;

        private static ShellStatusSlotModel[] CopySlots(IReadOnlyList<ShellStatusSlotModel> slots)
        {
            if (slots == null || slots.Count == 0)
                return Array.Empty<ShellStatusSlotModel>();

            var copy = new ShellStatusSlotModel[slots.Count];
            for (int i = 0; i < slots.Count; i++)
                copy[i] = slots[i] ?? throw new ArgumentException("상태 슬롯은 null일 수 없습니다.", nameof(slots));
            return copy;
        }
    }

    /// <summary>
    /// 공용 셸이 모드 State를 직접 탐색하지 않도록 상태 Snapshot과 변경 신호를 공급한다.
    /// </summary>
    public interface IUiShellStatusProvider
    {
        /// <summary>
        /// 표시해야 할 상태가 바뀌었음을 알린다.
        /// </summary>
        event Action StatusChanged;

        /// <summary>
        /// 현재 시점의 불변 상단 상태 Snapshot을 반환한다.
        /// </summary>
        ShellStatusModel GetCurrentStatus();
    }

    /// <summary>
    /// 모드별 상태 공급자가 변경 알림을 일관되게 발생시키도록 돕는 기반 클래스다.
    /// </summary>
    public abstract class UiShellStatusProviderBase : IUiShellStatusProvider
    {
        /// <summary>
        /// 표시해야 할 상태가 바뀌었음을 알린다.
        /// </summary>
        public event Action StatusChanged;

        /// <summary>
        /// 현재 시점의 불변 상단 상태 Snapshot을 반환한다.
        /// </summary>
        public abstract ShellStatusModel GetCurrentStatus();

        /// <summary>
        /// 파생 공급자가 상태 변경을 구독자에게 전달한다.
        /// </summary>
        protected void NotifyStatusChanged()
        {
            StatusChanged?.Invoke();
        }
    }
}
