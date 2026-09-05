using System;
using System.Collections.Generic;
using System.Globalization;

namespace Baseball.Core.Historical
{
    /// <summary>원본을 이동시키지 않고 특수 합성팀이 참조하는 한 로스터 엔트리다.</summary>
    public readonly struct SpecialCompositeRosterEntry
    {
        public SpecialCompositeRosterEntry(
            string playerSeasonId,
            string cardId,
            ActiveRosterRole role)
        {
            if (string.IsNullOrWhiteSpace(playerSeasonId))
                throw new ArgumentException("PlayerSeasonId는 비어 있을 수 없습니다.", nameof(playerSeasonId));
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));

            PlayerSeasonId = playerSeasonId.Trim();
            CardId = cardId.Trim();
            Role = role;
        }

        public string PlayerSeasonId { get; }
        public string CardId { get; }
        public ActiveRosterRole Role { get; }
    }

    /// <summary>한 OriginYear에 추가되는 비-Franchise 특수 합성팀의 불변 25인 결과다.</summary>
    public sealed class SpecialCompositeTeamDefinition
    {
        private const int RosterSize = 25;
        private const string KeyPrefix = "COMPOSITE:";
        private const string KeySeparator = ":";
        private readonly SpecialCompositeRosterEntry[] _roster;

        public SpecialCompositeTeamDefinition(
            SpecialCompositeTeamType teamType,
            int originYear,
            IReadOnlyList<SpecialCompositeRosterEntry> roster)
        {
            if (originYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            if (roster == null)
                throw new ArgumentNullException(nameof(roster));
            if (roster.Count != RosterSize)
                throw new ArgumentException("특수 합성팀은 정확히 25명이어야 합니다.", nameof(roster));

            var playerIds = new HashSet<string>(StringComparer.Ordinal);
            int roleCount = Enum.GetValues(typeof(ActiveRosterRole)).Length;
            var roleCounts = new int[roleCount];
            _roster = new SpecialCompositeRosterEntry[roster.Count];
            for (int index = 0; index < roster.Count; index++)
            {
                SpecialCompositeRosterEntry entry = roster[index];
                if (!playerIds.Add(entry.PlayerSeasonId))
                    throw new ArgumentException("한 합성팀에 같은 PlayerSeasonId를 중복 배치할 수 없습니다.", nameof(roster));
                int roleIndex = (int)entry.Role;
                if (roleIndex < 0 || roleIndex >= roleCounts.Length)
                    throw new ArgumentException("알 수 없는 ActiveRosterRole이 있습니다.", nameof(roster));
                roleCounts[roleIndex]++;
                _roster[index] = entry;
            }

            for (int roleIndex = 0; roleIndex < roleCounts.Length; roleIndex++)
            {
                ActiveRosterRole role = (ActiveRosterRole)roleIndex;
                int expected = role == ActiveRosterRole.BenchHitter
                    ? ActiveRosterCompositionRule.BenchHitterCount
                    : 1;
                if (roleCounts[roleIndex] != expected)
                    throw new ArgumentException("특수 합성팀 역할 구성이 공통 ActiveRoster 규칙과 다릅니다.", nameof(roster));
            }

            TeamType = teamType;
            OriginYear = originYear;
            TeamSeasonKey = CreateStableTeamSeasonKey(originYear, teamType);
        }

        public SpecialCompositeTeamType TeamType { get; }
        public int OriginYear { get; }
        public string TeamSeasonKey { get; }
        public IReadOnlyList<SpecialCompositeRosterEntry> Roster => _roster;

        public static string CreateStableTeamSeasonKey(int originYear, SpecialCompositeTeamType teamType)
        {
            if (originYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            return KeyPrefix + originYear + KeySeparator + teamType;
        }

        /// <summary>합성팀 TeamSeasonKey를 되돌려 원본 연도와 종류를 복원한다.</summary>
        public static bool TryParseTeamSeasonKey(
            string teamSeasonKey,
            out int originYear,
            out SpecialCompositeTeamType teamType)
        {
            originYear = 0;
            teamType = default;
            if (string.IsNullOrWhiteSpace(teamSeasonKey)) return false;

            string key = teamSeasonKey.Trim();
            if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal)) return false;

            int separatorIndex = key.IndexOf(KeySeparator, KeyPrefix.Length, StringComparison.Ordinal);
            if (separatorIndex < 0) return false;

            string yearText = key.Substring(KeyPrefix.Length, separatorIndex - KeyPrefix.Length);
            if (!int.TryParse(yearText, NumberStyles.None, CultureInfo.InvariantCulture, out originYear) ||
                originYear <= 0)
                return false;

            string typeText = key.Substring(separatorIndex + KeySeparator.Length);
            return Enum.TryParse(typeText, false, out teamType) &&
                   Enum.IsDefined(typeof(SpecialCompositeTeamType), teamType);
        }

        /// <summary>합성팀 TeamSeasonKey를 플레이어에게 보여줄 한국어 구단명으로 바꾼다.</summary>
        public static bool TryCreateDisplayName(string teamSeasonKey, out string displayName)
        {
            displayName = null;
            if (!TryParseTeamSeasonKey(teamSeasonKey, out int originYear, out SpecialCompositeTeamType teamType))
                return false;

            displayName = originYear.ToString(CultureInfo.InvariantCulture) + " " + GetTeamTypeName(teamType);
            return true;
        }

        /// <summary>합성팀 종류의 한국어 이름이며 일정·기록·중계에서 같은 표기를 쓴다.</summary>
        public static string GetTeamTypeName(SpecialCompositeTeamType teamType)
        {
            return teamType switch
            {
                SpecialCompositeTeamType.AllStarComposite => "올스타",
                SpecialCompositeTeamType.GoldenGloveComposite => "골든글러브",
                SpecialCompositeTeamType.YearSelectComposite => "올해의 선수",
                _ => throw new ArgumentOutOfRangeException(nameof(teamType))
            };
        }
    }

    /// <summary>AllStar, GoldenGlove, YearSelect 우선순위로 완성된 세 합성팀 묶음이다.</summary>
    public sealed class SpecialCompositeTeamSet
    {
        private readonly SpecialCompositeTeamDefinition[] _teams;

        public SpecialCompositeTeamSet(IReadOnlyList<SpecialCompositeTeamDefinition> teams)
        {
            if (teams == null)
                throw new ArgumentNullException(nameof(teams));
            if (teams.Count != 3)
                throw new ArgumentException("특수 합성팀 세 종류가 모두 필요합니다.", nameof(teams));

            _teams = new SpecialCompositeTeamDefinition[teams.Count];
            var teamTypes = new HashSet<SpecialCompositeTeamType>();
            var playerIds = new HashSet<string>(StringComparer.Ordinal);
            int originYear = teams[0]?.OriginYear ?? 0;
            for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
            {
                SpecialCompositeTeamDefinition team = teams[teamIndex]
                    ?? throw new ArgumentException("null 합성팀이 있습니다.", nameof(teams));
                if (team.OriginYear != originYear)
                    throw new ArgumentException("세 합성팀의 OriginYear가 같아야 합니다.", nameof(teams));
                if (!teamTypes.Add(team.TeamType))
                    throw new ArgumentException("같은 합성팀 종류를 중복 저장할 수 없습니다.", nameof(teams));
                for (int rosterIndex = 0; rosterIndex < team.Roster.Count; rosterIndex++)
                {
                    if (!playerIds.Add(team.Roster[rosterIndex].PlayerSeasonId))
                        throw new ArgumentException("세 합성팀 사이에 같은 PlayerSeasonId를 중복 배치할 수 없습니다.", nameof(teams));
                }
                _teams[teamIndex] = team;
            }

            OriginYear = originYear;
        }

        public int OriginYear { get; }
        public IReadOnlyList<SpecialCompositeTeamDefinition> Teams => _teams;

        public SpecialCompositeTeamDefinition Get(SpecialCompositeTeamType teamType)
        {
            for (int index = 0; index < _teams.Length; index++)
                if (_teams[index].TeamType == teamType) return _teams[index];
            throw new InvalidOperationException("요청한 특수 합성팀이 없습니다.");
        }
    }
}
