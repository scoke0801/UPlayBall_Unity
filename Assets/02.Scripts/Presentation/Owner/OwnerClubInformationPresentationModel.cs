using System;
using Baseball.Core.Historical;
using Baseball.Presentation.SharedScreens;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단 정보 레퍼런스 화면에 필요한 현재 Save의 확정 값만 제공한다.</summary>
    public sealed class OwnerClubInformationPresentationModel
    {
        public OwnerClubInformationPresentationModel(
            OwnerHomeSnapshot home,
            OwnerCollectionSnapshot collection,
            OwnerClubOperationSnapshot operation,
            ScheduleScreenSnapshot schedule)
        {
            if (home == null) throw new ArgumentNullException(nameof(home));
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));

            TeamName = home.TeamName;
            LeagueLabel = string.Concat(schedule.SeasonLabel, " · ", schedule.LeagueLabel);
            LocationLabel = "가상 프로야구 리그";
            OwnerName = "구단주";
            OwnedPlayerCount = collection.Cards.Count;
            ActiveRosterText = string.Concat(home.ActiveRosterCount, "/", home.ActiveRosterCapacity);
            FanBaseText = Math.Round(operation.FanBase).ToString("N0");
            PopularityText = Math.Round(operation.Popularity).ToString("N0");
            StadiumText = string.Concat("구장 Lv.", operation.StadiumLevel, " · ", operation.StadiumCapacity.ToString("N0"), "석");

            int normal = 0;
            int allStar = 0;
            int goldenGlove = 0;
            int mvp = 0;
            for (int index = 0; index < collection.Cards.Count; index++)
            {
                switch (collection.Cards[index].Edition)
                {
                    case PlayerCardEdition.AllStar: allStar++; break;
                    case PlayerCardEdition.GoldenGlove: goldenGlove++; break;
                    case PlayerCardEdition.Mvp: mvp++; break;
                    default: normal++; break;
                }
            }
            NormalCardCount = normal;
            AllStarCardCount = allStar;
            GoldenGloveCardCount = goldenGlove;
            MvpCardCount = mvp;

            var league = new OwnerLeaguePresentationModel(schedule);
            for (int index = 0; index < league.Standings.Count; index++)
            {
                OwnerLeaguePresentationModel.TeamRecord team = league.Standings[index];
                if (!string.Equals(team.Id, schedule.FocusTeamId, StringComparison.Ordinal)) continue;
                Rank = team.Rank;
                Wins = team.Wins;
                Losses = team.Losses;
                Ties = team.Ties;
                Runs = team.Runs;
                RunsAllowed = team.RunsAllowed;
                break;
            }
        }

        public string TeamName { get; }
        public string LeagueLabel { get; }
        public string LocationLabel { get; }
        public string OwnerName { get; }
        public string FanBaseText { get; }
        public string PopularityText { get; }
        public string StadiumText { get; }
        public string ActiveRosterText { get; }
        public int OwnedPlayerCount { get; }
        public int NormalCardCount { get; }
        public int AllStarCardCount { get; }
        public int GoldenGloveCardCount { get; }
        public int MvpCardCount { get; }
        public int Rank { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Ties { get; }
        public int Runs { get; }
        public int RunsAllowed { get; }
        public int Games => Wins + Losses + Ties;
        public string WinningPercentage => Wins + Losses == 0 ? "—" : ((double)Wins / (Wins + Losses)).ToString("0.000");
    }
}
