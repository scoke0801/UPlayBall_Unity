using System;
using System.Collections.Generic;
using Baseball.Game.Career;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Player
{
    /// <summary>
    /// 선수 Home Snapshot을 공용 GlobalTopBar의 상태 모델로 변환하고 변경을 알린다.
    /// </summary>
    public sealed class PlayerShellStatusProvider : UiShellStatusProviderBase
    {
        private const int CriticalConditionThreshold = 30;
        private const int WarningConditionThreshold = 50;
        private const int PositiveConditionThreshold = 85;
        private PlayerHomePresentationModel _current;

        /// <summary>첫 Home Snapshot으로 선수 모드 셸 상태 공급자를 만든다.</summary>
        public PlayerShellStatusProvider(PlayerHomePresentationModel initial)
        {
            _current = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        /// <summary>
        /// CareerChanged 이후 새 Snapshot으로 교체하고 셸에 갱신을 알린다.
        /// </summary>
        public void Update(PlayerHomePresentationModel current)
        {
            _current = current ?? throw new ArgumentNullException(nameof(current));
            NotifyStatusChanged();
        }

        /// <summary>
        /// Owner 경제를 탐색하지 않고 선수 개인 상태만 상단 슬롯에 공급한다.
        /// </summary>
        public override ShellStatusModel GetCurrentStatus()
        {
            PlayerHomeIdentityModel identity = _current.Identity;
            var slots = new List<ShellStatusSlotModel>(4)
            {
                new ShellStatusSlotModel(
                    "player.condition",
                    "컨디션",
                    _current.Condition.ToString(),
                    GetConditionEmphasis(_current.Condition)),
                new ShellStatusSlotModel(
                    "player.role",
                    "역할",
                    PlayerCareerText.FormatExpectedRole(_current.Usage.ExpectedRole)),
                new ShellStatusSlotModel(
                    "player.money",
                    "개인 자금",
                    PlayerCareerText.FormatMoney(_current.AvailableMoney))
            };

            if (_current.Fatigue.HasValue)
            {
                slots.Add(new ShellStatusSlotModel(
                    "player.fatigue",
                    "피로",
                    _current.Fatigue.Value.ToString()));
            }

            return new ShellStatusModel(
                $"{identity.SeasonYear} 시즌",
                _current.HasNextMatch ? $"다음 경기 {_current.NextMatch.Date:M월 d일}" : string.Empty,
                PlayerCareerText.FormatLeague(identity.LeagueLevel),
                identity.TeamName,
                _current.TeamRank > 0 ? $"{_current.TeamRank}위" : "순위 미정",
                BuildNextMatchText(_current.NextMatch),
                slots);
        }

        private static string BuildNextMatchText(PlayerNextMatchModel nextMatch)
        {
            if (nextMatch == null)
                return "예정 경기 없음";

            string venue = nextMatch.IsHome ? "홈" : "원정";
            return $"{nextMatch.OpponentName} · {venue}";
        }

        private static ShellStatusEmphasis GetConditionEmphasis(int condition)
        {
            if (condition < CriticalConditionThreshold)
                return ShellStatusEmphasis.Critical;
            if (condition < WarningConditionThreshold)
                return ShellStatusEmphasis.Warning;
            if (condition >= PositiveConditionThreshold)
                return ShellStatusEmphasis.Positive;
            return ShellStatusEmphasis.Normal;
        }
    }

    /// <summary>
    /// 공용 셸에 전달할 선수 모드의 짧은 사용자 표시 문자열을 한 곳에서 만든다.
    /// </summary>
    public static class PlayerCareerText
    {
        /// <summary>시즌 단위 예상 역할을 선수용 한국어 문구로 변환한다.</summary>
        public static string FormatExpectedRole(Baseball.Core.Teams.ExpectedRole role)
        {
            return role switch
            {
                Baseball.Core.Teams.ExpectedRole.StartingCompetition => "주전 경쟁",
                Baseball.Core.Teams.ExpectedRole.RosterCompetition => "1군 경쟁",
                Baseball.Core.Teams.ExpectedRole.BenchCompetition => "백업 경쟁",
                _ => "역할 정보 없음"
            };
        }

        /// <summary>LeagueLevel을 사용자에게 노출할 정식 등급명으로 변환한다.</summary>
        public static string FormatLeague(LeagueLevel level)
        {
            return level switch
            {
                LeagueLevel.Rookie => "Rookie League",
                LeagueLevel.Minor => "Minor League",
                LeagueLevel.Major => "Major League",
                LeagueLevel.World => "World League",
                LeagueLevel.AllStar => "All-Star League",
                LeagueLevel.Classic => "Classic League",
                LeagueLevel.Winners => "Winners League",
                LeagueLevel.Champion => "Champion League",
                LeagueLevel.Master => "Master League",
                LeagueLevel.Galaxy => "Galaxy League",
                _ => "리그 정보 없음"
            };
        }

        /// <summary>개인 자금을 자리 구분이 있는 숫자로 표시한다.</summary>
        public static string FormatMoney(long money)
        {
            return $"{money:N0}";
        }
    }
}
