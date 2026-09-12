using System;
using System.Globalization;

namespace Baseball.Core.Historical
{
    /// <summary>
    /// 구단 수가 모자란 조를 한 시즌 동안 채우는 CPU 임시 구단의 덱 종류다.
    /// 선언 순서가 강도 순서이며, 덱에 맞는 포지션 카드가 없을 때 가까운 등급부터 대체 후보를 찾는 기준이 된다.
    /// </summary>
    public enum LeagueFillerDeckType
    {
        YearTeam,
        Ex,
        AllStar,
        Mvp,
        CareerHigh,
        Legend
    }

    /// <summary>
    /// CPU 임시 구단의 TeamSeasonKey 규칙이다. 시즌·등급·조·슬롯을 모두 담아 전 시즌 기록과 겹치지 않게 하고,
    /// 덱 종류와 원본 연도 구단을 Key에 넣어 로스터 없이도 이력 화면이 이름을 복원할 수 있게 한다.
    /// </summary>
    public static class LeagueFillerTeamKey
    {
        private const string Prefix = "FILLER:";
        private const char Separator = ':';
        private const int FixedPartCount = 5;

        public static string Create(int seasonNumber, LeagueGrade grade, int groupIndex, int slot,
            LeagueFillerDeckType deckType, string sourceTeamSeasonKey = null)
        {
            if (seasonNumber <= 0 || groupIndex < 0 || slot < 0)
                throw new ArgumentOutOfRangeException(nameof(seasonNumber), "시즌·조·슬롯 번호가 올바르지 않습니다.");
            if (!Enum.IsDefined(typeof(LeagueGrade), grade))
                throw new ArgumentOutOfRangeException(nameof(grade));
            if (!Enum.IsDefined(typeof(LeagueFillerDeckType), deckType))
                throw new ArgumentOutOfRangeException(nameof(deckType));
            bool hasSource = !string.IsNullOrWhiteSpace(sourceTeamSeasonKey);
            if ((deckType == LeagueFillerDeckType.YearTeam) != hasSource)
                throw new ArgumentException("연도 구단 덱만 원본 TeamSeasonKey를 가진다.", nameof(sourceTeamSeasonKey));

            string key = string.Concat(
                Prefix,
                seasonNumber.ToString(CultureInfo.InvariantCulture), ":",
                ((int)grade).ToString("D2", CultureInfo.InvariantCulture), ":",
                groupIndex.ToString("D4", CultureInfo.InvariantCulture), ":",
                slot.ToString("D2", CultureInfo.InvariantCulture), ":",
                deckType.ToString());
            return hasSource ? key + Separator + sourceTeamSeasonKey.Trim() : key;
        }

        public static bool IsFillerKey(string teamSeasonKey) =>
            teamSeasonKey != null && teamSeasonKey.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>임시 구단 Key에서 덱 종류와 연도 구단 덱의 원본 TeamSeasonKey를 복원한다.</summary>
        public static bool TryParse(string teamSeasonKey, out LeagueFillerDeckType deckType, out string sourceTeamSeasonKey)
        {
            deckType = default;
            sourceTeamSeasonKey = null;
            if (!IsFillerKey(teamSeasonKey)) return false;

            // 원본 TeamSeasonKey에 구분자가 섞여도 깨지지 않도록 고정 필드 수만큼만 자른다.
            string[] parts = teamSeasonKey.Substring(Prefix.Length).Split(new[] { Separator }, FixedPartCount + 1);
            if (parts.Length < FixedPartCount) return false;
            if (!Enum.TryParse(parts[4], false, out deckType) || !Enum.IsDefined(typeof(LeagueFillerDeckType), deckType))
                return false;
            if (parts.Length > FixedPartCount) sourceTeamSeasonKey = parts[FixedPartCount];
            return (deckType == LeagueFillerDeckType.YearTeam) == !string.IsNullOrEmpty(sourceTeamSeasonKey);
        }

        /// <summary>연도 구단 덱을 제외한 덱의 한국어 구단명이다. 순위표·일정·중계가 같은 표기를 쓴다.</summary>
        public static string GetDeckTeamName(LeagueFillerDeckType deckType)
        {
            return deckType switch
            {
                LeagueFillerDeckType.Ex => "EX 선발팀",
                LeagueFillerDeckType.AllStar => "올스타 선발팀",
                LeagueFillerDeckType.Mvp => "MVP 선발팀",
                LeagueFillerDeckType.CareerHigh => "커리어하이 선발팀",
                LeagueFillerDeckType.Legend => "레전드 선발팀",
                _ => throw new ArgumentOutOfRangeException(nameof(deckType))
            };
        }
    }
}
