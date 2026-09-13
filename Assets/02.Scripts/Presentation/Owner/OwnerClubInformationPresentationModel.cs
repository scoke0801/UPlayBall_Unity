using System;
using System.Collections.Generic;
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
            ScheduleScreenSnapshot schedule,
            string ownerName = "구단주",
            string frontManagerId = "FRONT_MANAGER_DEFAULT_01",
            string sourceTeamName = null,
            string region = null,
            OwnerClubHistoryPresentationModel history = null,
            string motto = null)
        {
            if (home == null) throw new ArgumentNullException(nameof(home));
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));

            TeamName = home.TeamName;
            LeagueLabel = string.Concat(schedule.SeasonLabel, " · ", schedule.LeagueLabel);
            // 구단주가 붙인 구단명은 엠블렘 카탈로그에 없으므로 선택한 원본 구단명으로 엠블렘을 찾는다.
            EmblemTeamName = string.IsNullOrWhiteSpace(sourceTeamName) ? home.TeamName : sourceTeamName.Trim();
            LocationLabel = string.IsNullOrWhiteSpace(region) ? "—" : region.Trim();
            OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "구단주" : ownerName.Trim();
            FrontManagerId = frontManagerId ?? string.Empty;
            Motto = motto ?? Baseball.Game.Historical.OwnerProfileState.DefaultMotto;
            OwnedPlayerCount = collection.Cards.Count;
            ActiveRosterText = string.Concat(home.ActiveRosterCount, "/", home.ActiveRosterCapacity);
            FanBaseText = Math.Round(operation.FanBase).ToString("N0");
            PopularityText = Math.Round(operation.Popularity).ToString("N0");
            Popularity = (float)operation.Popularity;
            FanBase = (float)operation.FanBase;
            HasHistory = history != null;
            PreviousSeasons = BuildPreviousSeasons(history);
            Championships = history?.CountHonors(null, 1) ?? 0;
            RunnerUps = history?.CountHonors(null, 2) ?? 0;
            if (history != null)
            {
                for (int index = 1; index < history.Seasons.Count; index++)
                {
                    var newer = history.Seasons[index - 1];
                    var older = history.Seasons[index];
                    // 빠진 시즌을 건너뛰어 승강 횟수를 추측하지 않는다.
                    if (!older.IsCompleted || newer.Number != older.Number + 1) continue;
                    if ((int)newer.Grade > (int)older.Grade) Promotions++;
                    if ((int)newer.Grade < (int)older.Grade) Relegations++;
                }
            }
            StadiumText = string.Concat("구장 ", operation.StadiumLevel, "단계 · ", operation.StadiumCapacity.ToString("N0"), "석");

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
        public string EmblemTeamName { get; }
        public string LocationLabel { get; }
        public string OwnerName { get; }
        public string FrontManagerId { get; }
        public string Motto { get; }
        public string FanBaseText { get; }
        public string PopularityText { get; }
        public float Popularity { get; }
        public float FanBase { get; }
        public bool HasHistory { get; }
        public RecordTableModel PreviousSeasons { get; }
        public int Championships { get; }
        public int RunnerUps { get; }
        public int Promotions { get; }
        public int Relegations { get; }
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

        /// <summary>현재 시즌을 제외한 완료 시즌을 기록실과 같은 원본 값과 최신순으로 표시한다.</summary>
        private static RecordTableModel BuildPreviousSeasons(OwnerClubHistoryPresentationModel history)
        {
            var columns = new[]
            {
                new RecordTableColumnModel("Season", "시즌", RecordSortValueKind.Number, false, widthWeight: 1.5f),
                new RecordTableColumnModel("League", "리그", RecordSortValueKind.Text, false, widthWeight: 1.3f),
                new RecordTableColumnModel("Rank", "순위", RecordSortValueKind.Number, false),
                new RecordTableColumnModel("Wins", "승", RecordSortValueKind.Number, false),
                new RecordTableColumnModel("Losses", "패", RecordSortValueKind.Number, false),
                new RecordTableColumnModel("Ties", "무", RecordSortValueKind.Number, false)
            };
            var rows = new List<RecordTableRowModel>();
            if (history != null)
                foreach (var season in history.Seasons)
                {
                    // 현재 시즌은 정규 일정이 끝나도 위쪽 현재 리그 성적에서 표시한다.
                    if (!season.IsCompleted || season.Row.IsHighlighted) continue;
                    rows.Add(season.Row);
                }
            return new RecordTableModel(columns, rows);
        }
    }
}
