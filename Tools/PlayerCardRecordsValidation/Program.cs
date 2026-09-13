using System;
using System.Text.Json;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Historical;

// 에디터 없이 경기 집계의 순서·최근 범위·저장 복원 계약을 확인한다.
var state = new LeagueSeasonStatisticsState();
var batter = state.RegularSeason.GetOrCreate(1, "타자", 1, PlayerPosition.Catcher);
var pitcher = state.RegularSeason.GetOrCreate(2, "투수", 1, PlayerPosition.StartingPitcher);
for (int i = 1; i <= 8; i++)
{
    batter.Add(new PlayerGameStatistics(1, "타자", 1, PlayerPosition.Catcher)
    {
        HasBattingLine = true, AtBats = 4, Hits = i % 3, HomeRuns = i % 2,
        Contribution = new PlayerGameContributionState(i, i, false, false, 0, 0)
    });
    pitcher.Add(new PlayerGameStatistics(2, "투수", 1, PlayerPosition.StartingPitcher)
    {
        HasPitchingLine = true, OutsRecorded = i, EarnedRuns = 1, PitchingStrikeouts = 3,
        Contribution = new PlayerGameContributionState(i, i, false, false, 0, 0)
    });
}
Check(batter.RecentGames.Count == 5 && batter.RecentGames[0].gameId == 4 && batter.RecentGames[4].gameId == 8, "최근 5건 순서");
Check(batter.Batting.AtBats == 32, "시즌 누적 보존");
batter.Add(new PlayerGameStatistics(1, "타자", 1, PlayerPosition.Catcher));
Check(batter.RecentGames.Count == 5 && batter.RecentGames[4].gameId == 8, "미출전 제외");
var options = new JsonSerializerOptions { IncludeFields = true };
string json = JsonSerializer.Serialize(LeagueSeasonStatisticsSaveMapper.CreateSaveData(state), options);
var restored = LeagueSeasonStatisticsSaveMapper.Restore(JsonSerializer.Deserialize<LeagueSeasonStatisticsSaveData>(json, options));
Check(restored.RegularSeason.GetPlayer(1).RecentGames[0].gameId == 4, "JSON 최근 기록 복원");
Check(restored.RegularSeason.GetPlayer(1).Batting.AtBats == 32, "복원 중 누적 중복 없음");
Check(restored.RegularSeason.GetPlayer(2).RecentGames[4].outsRecorded == 8 && restored.RegularSeason.GetPlayer(2).RecentGames[4].strikeouts == 3, "투수 아웃·삼진 복원");
Check(restored.Postseason.Players.Count == 0, "정규시즌 범위 분리");
Check(new LeagueSeasonStatisticsState().RegularSeason.Players.Count == 0, "새 시즌 초기화");
Console.WriteLine("콘솔 검증 8건 통과");
static void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("통과: " + name);
}
