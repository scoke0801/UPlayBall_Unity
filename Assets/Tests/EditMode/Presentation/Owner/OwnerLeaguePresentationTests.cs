using System;
using System.IO;
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
        public void Standings_내구단행에만구단주명을표시한다()
        {
            var root = new GameObject("OwnerLeagueOwnerNameTests_Root", typeof(RectTransform));
            try
            {
                var schedule = new ScheduleScreenSnapshot(
                    "2028 시즌",
                    "루키",
                    "1주차",
                    "a",
                    new[] { Game("1", 1, "a", "b", 5, 2) });
                UI_Scene_OwnerLeague view = UI_Scene_OwnerLeague.CreateRuntime(
                    root.GetComponent<RectTransform>());
                view.Bind(new OwnerLeaguePresentationModel(schedule, "승리요정"));
                view.ShowTab(0);

                string focusName = view.transform.Find("LeagueTable/Team_0/Cell_1").GetComponent<Text>().text;
                string rivalName = view.transform.Find("LeagueTable/Team_1/Cell_1").GetComponent<Text>().text;
                Assert.That(focusName, Is.EqualTo("a 구단 · 구단주 승리요정"));
                Assert.That(rivalName, Is.EqualTo("b 구단"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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

        [Test]
        public void RankHistoryView_페이지이동버튼과하단범례영역이겹치지않는다()
        {
            var root = new GameObject(
                "OwnerLeagueRankHistoryLayoutTests_Root",
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

                RectTransform previous = (RectTransform)view.transform.Find("Previous");
                RectTransform next = (RectTransform)view.transform.Find("Next");
                RectTransform legend = (RectTransform)view.transform.Find("Legend");
                RectTransform focusLegend = (RectTransform)view.transform.Find("FocusLegend");

                AssertVerticalGap(legend, previous);
                AssertVerticalGap(focusLegend, next);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void Visual_최대길이구단주명이있는순위표를출력한다(int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("그래픽 장치가 필요합니다.");
            var root = new GameObject(
                "OwnerLeagueVisualTests_Root",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            var cameraObject = new GameObject("OwnerLeagueVisualTests_Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            Texture2D texture = null;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.targetTexture = target;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                var games = new[]
                {
                    Game("1", 1, "a", "b", 5, 2),
                    Game("2", 1, "c", "d", 4, 1),
                    Game("3", 1, "e", "f", 3, 2),
                    Game("4", 1, "g", "h", 2, 1),
                    Game("5", 1, "i", "j", 1, 0)
                };
                var schedule = new ScheduleScreenSnapshot(
                    "2028 시즌",
                    "루키",
                    "1주차",
                    "a",
                    games);
                UI_Scene_OwnerLeague view = UI_Scene_OwnerLeague.CreateRuntime(
                    root.GetComponent<RectTransform>());
                view.Bind(new OwnerLeaguePresentationModel(schedule, "가나다라마바사아자차카타"));
                view.ShowTab(0);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(root.GetComponent<RectTransform>());
                Canvas.ForceUpdateCanvases();

                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                string output = Environment.GetEnvironmentVariable("BASEBALL_LEAGUE_VISUAL_OUTPUT")
                    ?? Path.GetFullPath("docs/reports/owner-league");
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "standings-" + width + "x" + height + ".png"),
                    texture.EncodeToPNG());
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                cameraObject.GetComponent<Camera>().targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static OwnerLeaguePresentationModel Build(params ScheduleGameSnapshot[] games) =>
            new OwnerLeaguePresentationModel(new ScheduleScreenSnapshot("2028 시즌", "루키", "1주차", "a", games));

        private static ScheduleGameSnapshot Game(string id, int round, string away, string home, int awayRuns, int homeRuns,
            bool completed = true) => new ScheduleGameSnapshot(id, round, round + "R",
                new ScheduleTeamSnapshot(away, away + " 구단"), new ScheduleTeamSnapshot(home, home + " 구단"),
                completed, awayRuns, homeRuns, ScheduleFocusSide.None);

        private static void AssertVerticalGap(RectTransform lower, RectTransform upper)
        {
            var lowerCorners = new Vector3[4];
            var upperCorners = new Vector3[4];
            lower.GetWorldCorners(lowerCorners);
            upper.GetWorldCorners(upperCorners);
            Assert.That(lowerCorners[1].y, Is.LessThan(upperCorners[0].y));
        }
    }
}
