using System;
using System.Linq;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedScreens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>리그 표가 완료 경기만 집계하고 상대 전적과 순위 이력을 보존하는지 검증한다.</summary>
    public sealed class OwnerLeaguePresentationTests
    {
        [Test]
        public void CompletedGames_승패무득실점과상대전적이대칭이다()
        {
            var model = Build(Game("1", 1, "a", "b", 5, 2), Game("2", 2, "b", "a", 3, 3),
                Game("3", 3, "a", "b", 0, 0, false));
            var a = model.Standings.Single(team => team.Id == "a");
            Assert.That(a.Games, Is.EqualTo(2));
            Assert.That(a.Wins, Is.EqualTo(1));
            Assert.That(a.Ties, Is.EqualTo(1));
            Assert.That(a.Runs, Is.EqualTo(8));
            Assert.That(a.RunsAllowed, Is.EqualTo(5));
            Assert.That(a.Percentage, Is.EqualTo(1));
            Assert.That(model.GetMatchup("a", "b").Wins, Is.EqualTo(model.GetMatchup("b", "a").Losses));
            Assert.That(model.GetMatchup("a", "b").Ties, Is.EqualTo(1));
            Assert.That(model.Rounds, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void RankHistory_동률과라운드단위집계가입력순서에무관하다()
        {
            var games = new[] { Game("1", 1, "a", "b", 5, 2), Game("2", 2, "a", "b", 0, 1),
                Game("3", 3, "a", "b", 0, 1) };
            var forward = Build(games);
            Array.Reverse(games);
            var reverse = Build(games);
            Assert.That(forward.Standings[0].Id, Is.EqualTo("b"));
            Assert.That(forward.Standings.Single(team => team.Id == "a").RankHistory, Is.EqualTo(new[] { 1, 1, 2 }));
            foreach (var team in forward.Standings)
                Assert.That(reverse.Standings.Single(other => other.Id == team.Id).RankHistory, Is.EqualTo(team.RankHistory));
            var sameRound = Build(Game("1", 1, "a", "b", 5, 2), Game("2", 1, "a", "b", 0, 1));
            Assert.That(sameRound.Rounds.Count, Is.EqualTo(1));
            Assert.That(sameRound.Standings.All(team => team.Rank == 1), Is.True);
        }

        [Test]
        public void PendingSeason_모든구단을표시하고가짜이력을만들지않는다()
        {
            var model = Build(Game("1", 1, "a", "b", 0, 0, false));
            Assert.That(model.Standings.Count, Is.EqualTo(2));
            Assert.That(model.Standings.All(team => team.Games == 0 && team.Rank == 1), Is.True);
            Assert.That(model.Rounds, Is.Empty);
            Assert.That(Build().Standings, Is.Empty);
        }

        [Test]
        public void RoundRobin_득점실점승패무총합이보존된다()
        {
            var games = new System.Collections.Generic.List<ScheduleGameSnapshot>();
            for (int round = 1; round <= 30; round++)
                games.Add(Game(round.ToString(), round, "a", "b", round % 7, round % 5));
            var model = Build(games.ToArray());
            Assert.That(model.Standings.Sum(team => team.Runs), Is.EqualTo(model.Standings.Sum(team => team.RunsAllowed)));
            Assert.That(model.Standings.Sum(team => team.Wins), Is.EqualTo(model.Standings.Sum(team => team.Losses)));
            Assert.That(model.Standings.Sum(team => team.Games), Is.EqualTo(60));
            Assert.That(model.Rounds.Count, Is.EqualTo(30));
        }

        [Test]
        public void RankHistoryView_그래프Mesh와CanvasRenderer를생성한다()
        {
            var root = new GameObject(
                "OwnerLeagueRankHistoryTests_Root",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var games = new ScheduleGameSnapshot[8];
                for (int round = 1; round <= games.Length; round++)
                    games[round - 1] = Game(round.ToString(), round, "a", "b", round % 4, (round + 1) % 4);

                UI_Scene_OwnerLeague view = UI_Scene_OwnerLeague.CreateRuntime(
                    root.GetComponent<RectTransform>());
                view.Bind(Build(games));
                view.ShowTab(3);
                Canvas.ForceUpdateCanvases();

                UILeagueRankChart chart = view.transform.Find("LeagueTable/RankHistory")
                    .GetComponent<UILeagueRankChart>();
                Mesh mesh = chart.canvasRenderer.GetMesh();

                Assert.That(chart.GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static OwnerLeaguePresentationModel Build(params ScheduleGameSnapshot[] games) =>
            new OwnerLeaguePresentationModel(new ScheduleScreenSnapshot("2028 시즌", "루키", "1주차", "a", games));

        private static ScheduleGameSnapshot Game(string id, int round, string away, string home, int awayRuns, int homeRuns,
            bool completed = true) => new ScheduleGameSnapshot(id, round, round + "R",
                new ScheduleTeamSnapshot(away, away + " 구단"), new ScheduleTeamSnapshot(home, home + " 구단"),
                completed, awayRuns, homeRuns, ScheduleFocusSide.None);
    }
}
