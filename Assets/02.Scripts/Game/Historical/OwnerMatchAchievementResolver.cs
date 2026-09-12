using Baseball.Game.Career;

namespace Baseball.Game.Historical
{
    /// <summary>경기 확정 직후의 업적 지급을 두 실행 경로에서 동일하게 처리한다.</summary>
    public static class OwnerMatchAchievementResolver
    {
        public const string LosingStreakSignatureCardId = "OWNER-TACTIC-BREAK-LOSING-STREAK";

        /// <summary>3연패를 끊은 승리 직후 업적 전용 전술을 한 번 지급한다.</summary>
        public static bool TryUnlockLosingStreakSignature(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime.TacticCollection.Contains(LosingStreakSignatureCardId)) return false;
            var games = runtime.ManagerMode.LiveSeason.Schedule.Games;
            var latestResults = new bool[4];
            int resultCount = 0;
            int playerTeamId = runtime.ManagerMode.LiveSeason.PlayerTeamId;
            for (int index = 0; index < games.Count; index++)
            {
                ScheduledGameState game = games[index];
                if (!game.IsCompleted || !game.IncludesTeam(playerTeamId)) continue;
                bool isWin = game.AwayTeamId == playerTeamId
                    ? game.AwayRuns > game.HomeRuns : game.HomeRuns > game.AwayRuns;
                if (resultCount < latestResults.Length) latestResults[resultCount++] = isWin;
                else
                {
                    latestResults[0] = latestResults[1];
                    latestResults[1] = latestResults[2];
                    latestResults[2] = latestResults[3];
                    latestResults[3] = isWin;
                }
            }
            if (resultCount < latestResults.Length || latestResults[0] || latestResults[1] || latestResults[2] || !latestResults[3])
                return false;
            runtime.TacticCollection.Acquire(LosingStreakSignatureCardId);
            return true;
        }
    }
}
