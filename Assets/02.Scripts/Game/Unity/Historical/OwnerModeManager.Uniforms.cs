using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        /// <summary>경기 의상에 사용할 실제 참가 구단 계보를 반환하며 계보 없는 합성팀은 빈 값이다.</summary>
        public string GetTeamUniformFranchiseId(string teamSeasonKey)
        {
            if (LeagueFillerTeamKey.TryParse(teamSeasonKey, out LeagueFillerDeckType deck, out string source))
                return deck == LeagueFillerDeckType.YearTeam ? GetTeamUniformFranchiseId(source) : string.Empty;
            HistoricalBakedContent content = _contentProvider.Load();
            return content.TryGetTeamSeason(teamSeasonKey, out TeamSeasonDefinition team)
                ? team.FranchiseId : string.Empty;
        }
    }
}
