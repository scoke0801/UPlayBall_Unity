using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>경기 준비 Resolver가 확정한 선수별 Condition 합성과 역할을 전달한다.</summary>
    public sealed class OwnerConditionPlayerSnapshot
    {
        public OwnerConditionPlayerSnapshot(
            string playerPersonId,
            string playerName,
            string positionText,
            bool isPitcher,
            PlayerAvailabilityStatus availability,
            EffectiveMatchCondition effectiveCondition)
        {
            if (string.IsNullOrWhiteSpace(playerPersonId))
                throw new ArgumentException("PlayerPersonId는 비어 있을 수 없습니다.", nameof(playerPersonId));
            if (string.IsNullOrWhiteSpace(playerName))
                throw new ArgumentException("선수 이름은 비어 있을 수 없습니다.", nameof(playerName));
            if (string.IsNullOrWhiteSpace(positionText))
                throw new ArgumentException("포지션 표기는 비어 있을 수 없습니다.", nameof(positionText));
            if (!Enum.IsDefined(typeof(PlayerAvailabilityStatus), availability))
                throw new ArgumentOutOfRangeException(nameof(availability));
            if (!isPitcher && effectiveCondition.BatteryChemistryModifier != 0)
                throw new ArgumentException("Battery Chemistry는 투수에게만 적용할 수 있습니다.", nameof(effectiveCondition));

            PlayerPersonId = playerPersonId.Trim();
            PlayerName = playerName.Trim();
            PositionText = positionText.Trim();
            IsPitcher = isPitcher;
            Availability = availability;
            EffectiveCondition = effectiveCondition;
        }

        public string PlayerPersonId { get; }
        public string PlayerName { get; }
        public string PositionText { get; }
        public bool IsPitcher { get; }
        public PlayerAvailabilityStatus Availability { get; }
        public EffectiveMatchCondition EffectiveCondition { get; }
    }

    /// <summary>선수별 Condition 행이 표시할 원본 값과 완성 문구다.</summary>
    public sealed class OwnerConditionPlayerPresentationRow
    {
        internal OwnerConditionPlayerPresentationRow(
            OwnerConditionPlayerSnapshot snapshot,
            int baseLevel,
            string baseConditionText,
            string assignmentText,
            string lineupChemistryText,
            string batteryChemistryText,
            int effectiveLevel,
            string effectiveConditionText,
            string availabilityText)
        {
            Snapshot = snapshot;
            BaseLevel = baseLevel;
            BaseConditionText = baseConditionText;
            AssignmentText = assignmentText;
            LineupChemistryText = lineupChemistryText;
            BatteryChemistryText = batteryChemistryText;
            EffectiveLevel = effectiveLevel;
            EffectiveConditionText = effectiveConditionText;
            AvailabilityText = availabilityText;
        }

        public OwnerConditionPlayerSnapshot Snapshot { get; }
        public int BaseLevel { get; }
        public string BaseConditionText { get; }
        public string AssignmentText { get; }
        public string LineupChemistryText { get; }
        public string BatteryChemistryText { get; }
        public int EffectiveLevel { get; }
        public string EffectiveConditionText { get; }
        public string AvailabilityText { get; }
    }

    /// <summary>Condition/Chemistry Runtime View에 필요한 불변 행 목록이다.</summary>
    public sealed class OwnerConditionChemistryPresentationModel
    {
        internal OwnerConditionChemistryPresentationModel(
            IReadOnlyList<OwnerConditionPlayerPresentationRow> players,
            string summaryText)
        {
            Players = players;
            SummaryText = summaryText;
        }

        public IReadOnlyList<OwnerConditionPlayerPresentationRow> Players { get; }
        public string SummaryText { get; }
    }

    /// <summary>Simulation이 합성한 EffectiveMatchCondition을 10단계 UI 문구로만 변환한다.</summary>
    public static class OwnerConditionChemistryPresentationBuilder
    {
        public static OwnerConditionChemistryPresentationModel Build(
            IReadOnlyList<OwnerConditionPlayerSnapshot> players,
            ConditionPresentationTable presentation)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            var rows = new OwnerConditionPlayerPresentationRow[players.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int availableCount = 0;
            for (int index = 0; index < players.Count; index++)
            {
                OwnerConditionPlayerSnapshot player = players[index]
                    ?? throw new ArgumentException("null 선수 Condition Snapshot이 있습니다.", nameof(players));
                if (!ids.Add(player.PlayerPersonId))
                    throw new ArgumentException("PlayerPersonId는 중복될 수 없습니다.", nameof(players));
                if (player.Availability == PlayerAvailabilityStatus.Available)
                    availableCount++;
                rows[index] = BuildPlayer(player, presentation);
            }

            return new OwnerConditionChemistryPresentationModel(
                rows,
                $"경기 준비 {players.Count:N0}명 · 출전 가능 {availableCount:N0}명 · Condition은 경기 Snapshot 기준");
        }

        private static OwnerConditionPlayerPresentationRow BuildPlayer(
            OwnerConditionPlayerSnapshot player,
            ConditionPresentationTable presentation)
        {
            EffectiveMatchCondition condition = player.EffectiveCondition;
            int baseLevel = presentation.GetLevel(condition.StoredBaseCondition);
            int effectiveLevel = presentation.GetLevel(condition.Value);
            ConditionPresentationBand baseBand = presentation.GetBand(condition.StoredBaseCondition);
            ConditionPresentationBand effectiveBand = presentation.GetBand(condition.Value);
            return new OwnerConditionPlayerPresentationRow(
                player,
                baseLevel,
                $"{FormatConditionLabel(baseBand.LabelKey)} · Lv.{baseLevel}",
                FormatModifier(condition.AssignmentModifier),
                FormatModifier(condition.LineupChemistryModifier),
                player.IsPitcher ? FormatModifier(condition.BatteryChemistryModifier) : "해당 없음",
                effectiveLevel,
                $"{FormatConditionLabel(effectiveBand.LabelKey)} · Lv.{effectiveLevel}",
                FormatAvailability(player.Availability));
        }

        /// <summary>연속 Condition 원값을 노출하지 않고 데이터화된 1~10단계와 한글 상태로 표현한다.</summary>
        public static string FormatCondition(int condition, ConditionPresentationTable presentation)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            int level = presentation.GetLevel(condition);
            return $"{FormatConditionLabel(presentation.GetBand(condition).LabelKey)} · Lv.{level}";
        }

        private static string FormatModifier(int value) => value > 0 ? $"+{value}" : value.ToString();

        private static string FormatAvailability(PlayerAvailabilityStatus availability)
        {
            switch (availability)
            {
                case PlayerAvailabilityStatus.Available: return "출전 가능";
                case PlayerAvailabilityStatus.DayToDay: return "상태 확인 필요";
                case PlayerAvailabilityStatus.Unavailable: return "출전 불가";
                default: throw new ArgumentOutOfRangeException(nameof(availability));
            }
        }

        private static string FormatConditionLabel(string labelKey)
        {
            switch (labelKey)
            {
                case "condition.worst": return "최악";
                case "condition.very_bad": return "매우 나쁨";
                case "condition.bad": return "나쁨";
                case "condition.somewhat_bad": return "다소 나쁨";
                case "condition.normal": return "보통";
                case "condition.somewhat_good": return "다소 좋음";
                case "condition.good": return "좋음";
                case "condition.very_good": return "매우 좋음";
                case "condition.excellent": return "최상";
                case "condition.peak": return "절정";
                default: return labelKey;
            }
        }
    }
}
