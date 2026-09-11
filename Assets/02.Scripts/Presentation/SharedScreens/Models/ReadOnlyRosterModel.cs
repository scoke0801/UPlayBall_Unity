using System;
using System.Collections.Generic;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 읽기 전용 Roster에서 한 선수의 강조 또는 경고 상태를 구분한다.
    /// </summary>
    public enum RosterPlayerVisualState
    {
        Normal = 0,
        Highlighted = 1,
        Warning = 2,
        Unavailable = 3
    }

    /// <summary>
    /// 읽기 전용 필터가 Core 포지션을 다시 해석하지 않도록 선수의 표시 부문을 구분한다.
    /// </summary>
    public enum RosterPlayerKind
    {
        Batter = 0,
        Pitcher = 1
    }

    /// <summary>
    /// Core 선수나 소유 카드 State를 참조하지 않는 읽기 전용 Roster 선수 표시다.
    /// </summary>
    public sealed class ReadOnlyRosterPlayerModel
    {
        /// <summary>
        /// 확정된 선수 표시 문자열과 강조 근거로 Roster Row를 만든다.
        /// </summary>
        public ReadOnlyRosterPlayerModel(
            string playerId,
            string displayName,
            string positionLabel,
            string roleLabel,
            string overallText,
            string conditionText,
            string primaryRecordText,
            string secondaryRecordText = null,
            RosterPlayerVisualState visualState = RosterPlayerVisualState.Normal,
            string highlightReason = null,
            bool isInNextGamePlan = false,
            RosterPlayerKind kind = RosterPlayerKind.Batter)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Roster 선수 ID는 비어 있을 수 없습니다.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Roster 선수 이름은 비어 있을 수 없습니다.", nameof(displayName));

            PlayerId = playerId;
            DisplayName = displayName;
            PositionLabel = positionLabel ?? string.Empty;
            RoleLabel = roleLabel ?? string.Empty;
            OverallText = overallText ?? string.Empty;
            ConditionText = conditionText ?? string.Empty;
            PrimaryRecordText = primaryRecordText ?? string.Empty;
            SecondaryRecordText = secondaryRecordText ?? string.Empty;
            VisualState = visualState;
            HighlightReason = highlightReason ?? string.Empty;
            IsInNextGamePlan = isInNextGamePlan;
            Kind = kind;
        }

        public string PlayerId { get; }
        public string DisplayName { get; }
        public string PositionLabel { get; }
        public string RoleLabel { get; }
        public string OverallText { get; }
        public string ConditionText { get; }
        public string PrimaryRecordText { get; }
        public string SecondaryRecordText { get; }
        public RosterPlayerVisualState VisualState { get; }
        public string HighlightReason { get; }
        public bool IsInNextGamePlan { get; }
        public RosterPlayerKind Kind { get; }
    }

    /// <summary>
    /// 주전 타순, Rotation, Bullpen처럼 의미가 같은 Roster 선수 묶음이다.
    /// </summary>
    public sealed class ReadOnlyRosterGroupModel
    {
        private readonly ReadOnlyRosterPlayerModel[] _players;

        /// <summary>
        /// 그룹 ID와 순서가 보존된 선수 목록을 만든다.
        /// </summary>
        public ReadOnlyRosterGroupModel(
            string groupId,
            string displayName,
            IReadOnlyList<ReadOnlyRosterPlayerModel> players)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                throw new ArgumentException("Roster 그룹 ID는 비어 있을 수 없습니다.", nameof(groupId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Roster 그룹 이름은 비어 있을 수 없습니다.", nameof(displayName));

            GroupId = groupId;
            DisplayName = displayName;
            _players = CopyPlayers(players);
        }

        public string GroupId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<ReadOnlyRosterPlayerModel> Players => _players;

        private static ReadOnlyRosterPlayerModel[] CopyPlayers(IReadOnlyList<ReadOnlyRosterPlayerModel> players)
        {
            if (players == null || players.Count == 0)
                return Array.Empty<ReadOnlyRosterPlayerModel>();

            var copy = new ReadOnlyRosterPlayerModel[players.Count];
            for (int i = 0; i < players.Count; i++)
                copy[i] = players[i] ?? throw new ArgumentException("Roster 선수는 null일 수 없습니다.", nameof(players));
            return copy;
        }
    }

    /// <summary>
    /// 편집 Command를 갖지 않고 구단과 기용 결과만 설명하는 공용 Roster Snapshot이다.
    /// </summary>
    public sealed class ReadOnlyRosterModel
    {
        private readonly ReadOnlyRosterGroupModel[] _groups;

        /// <summary>
        /// 구단 표시 정보와 역할별 Roster 그룹을 읽기 전용으로 복사한다.
        /// </summary>
        public ReadOnlyRosterModel(
            string teamId,
            string teamName,
            string seasonLabel,
            string summary,
            IReadOnlyList<ReadOnlyRosterGroupModel> groups)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("구단 ID는 비어 있을 수 없습니다.", nameof(teamId));
            if (string.IsNullOrWhiteSpace(teamName))
                throw new ArgumentException("구단 이름은 비어 있을 수 없습니다.", nameof(teamName));

            TeamId = teamId;
            TeamName = teamName;
            SeasonLabel = seasonLabel ?? string.Empty;
            Summary = summary ?? string.Empty;
            _groups = CopyGroups(groups);
        }

        public string TeamId { get; }
        public string TeamName { get; }
        public string SeasonLabel { get; }
        public string Summary { get; }
        public IReadOnlyList<ReadOnlyRosterGroupModel> Groups => _groups;
        public bool IsReadOnly => true;

        /// <summary>지정 표시 부문의 선수만 남긴 새 읽기 전용 Roster를 반환한다.</summary>
        public ReadOnlyRosterModel FilterByKind(RosterPlayerKind kind)
        {
            var groups = new List<ReadOnlyRosterGroupModel>(_groups.Length);
            int filteredPlayerCount = 0;
            for (int groupIndex = 0; groupIndex < _groups.Length; groupIndex++)
            {
                ReadOnlyRosterGroupModel group = _groups[groupIndex];
                var players = new List<ReadOnlyRosterPlayerModel>(group.Players.Count);
                for (int playerIndex = 0; playerIndex < group.Players.Count; playerIndex++)
                {
                    ReadOnlyRosterPlayerModel player = group.Players[playerIndex];
                    if (player.Kind == kind)
                    {
                        players.Add(player);
                        filteredPlayerCount++;
                    }
                }
                if (players.Count > 0)
                    groups.Add(new ReadOnlyRosterGroupModel(group.GroupId, group.DisplayName, players));
            }

            return new ReadOnlyRosterModel(
                TeamId,
                TeamName,
                SeasonLabel,
                $"표시 {filteredPlayerCount}명 / {CountPlayers()}명 · 읽기 전용",
                groups);
        }

        private int CountPlayers()
        {
            int count = 0;
            for (int groupIndex = 0; groupIndex < _groups.Length; groupIndex++)
                count += _groups[groupIndex].Players.Count;
            return count;
        }

        private static ReadOnlyRosterGroupModel[] CopyGroups(IReadOnlyList<ReadOnlyRosterGroupModel> groups)
        {
            if (groups == null || groups.Count == 0)
                return Array.Empty<ReadOnlyRosterGroupModel>();

            var copy = new ReadOnlyRosterGroupModel[groups.Count];
            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < groups.Count; i++)
            {
                ReadOnlyRosterGroupModel group = groups[i] ??
                    throw new ArgumentException("Roster 그룹은 null일 수 없습니다.", nameof(groups));
                if (!groupIds.Add(group.GroupId))
                    throw new ArgumentException($"중복 Roster 그룹 ID입니다: {group.GroupId}", nameof(groups));
                copy[i] = group;
            }
            return copy;
        }
    }
}
