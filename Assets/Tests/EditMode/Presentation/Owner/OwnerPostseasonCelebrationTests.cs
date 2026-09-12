using System;
using System.IO;
using System.Reflection;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Career;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed class OwnerPostseasonCelebrationTests
    {
        [TestCase(1, true, true)]
        [TestCase(2, true, false)]
        [TestCase(1, false, false)]
        public void 정규시즌순위가확정된일위에게만페넌트연출을제공한다(int rank, bool initialized, bool expected)
        {
            var snapshot = new OwnerSeasonReviewSnapshot(2, LeagueGrade.Minor, null, "A", rank, 10,
                101, 42, 1, 800, 405, initialized, false, true, null, null, null);
            var result = OwnerPostseasonCelebration.CreatePennantWinner(snapshot);
            Assert.That(result != null, Is.EqualTo(expected));
            if (result == null) return;
            Assert.That(result.Kind, Is.EqualTo(OwnerPostseasonCelebrationKind.PennantWinner));
            Assert.That(result.Wins, Is.EqualTo(101));
            Assert.That(result.Losses, Is.EqualTo(42));
            Assert.That(result.Draws, Is.EqualTo(1));
        }

        [TestCase(true, "A", true)]
        [TestCase(true, "B", false)]
        [TestCase(false, "A", false)]
        public void 다른조가진행중이어도우리조우승을복구하고준우승은축하하지않는다(bool completed, string winner, bool expected)
        {
            var snapshot = new OwnerSeasonReviewSnapshot(2, LeagueGrade.Minor, null, "A", 1, 10,
                101, 42, 1, 800, 405, true, completed, false, 1, 37, true,
                winner == "A" ? OwnerTeamPostseasonResult.Champion : OwnerTeamPostseasonResult.RunnerUp,
                winner, new[] { new OwnerPostseasonSeriesReview("final", OwnerPostseasonRound.Championship,
                    "A", "B", winner == "A" ? 3 : 1, winner == "A" ? 1 : 3, 3, completed) });
            Assert.That(OwnerPostseasonCelebration.CreateChampion(snapshot) != null, Is.EqualTo(expected));
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void 페넌트연출은실제이미지와기록을표시하고시즌보고로복귀한다(int width, int height)
        {
            var host = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            var previousTarget = RenderTexture.active;
            var mode = CareerPresentationSettings.Mode;
            try
            {
                CareerPresentationSettings.Mode = CareerPresentationMode.ResultOnly;
                var hostRect = host.GetComponent<RectTransform>();
                hostRect.sizeDelta = new Vector2(width, height);
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = height / 2f;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                var view = UI_Popup_OwnerPostseasonCelebration.CreateRuntime(hostRect);
                var result = OwnerPostseasonCelebration.CreatePennantWinner(Snapshot(0, 0, false));
                view.Show(result, _ => "1994 부산 하버스", canViewMatchRecords: false);
                Assert.That(view.transform.Find("Celebration/Artwork").GetComponent<RawImage>().texture, Is.Not.Null);
                Assert.That(view.transform.Find("Celebration/Title").GetComponent<Text>().text, Is.EqualTo("정규시즌 1위"));
                Assert.That(view.transform.Find("Celebration/Records").gameObject.activeSelf, Is.False);
                bool continued = false;
                view.ContinueRequested += () => continued = true;
                Canvas.ForceUpdateCanvases();
                foreach (Text label in view.GetComponentsInChildren<Text>())
                {
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f), label.name);
                    var corners = new Vector3[4];
                    label.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                        Assert.That(hostRect.rect.Contains((Vector2)hostRect.InverseTransformPoint(corner)), Is.True, label.name);
                }
                SaveRender(camera, target, width, height, false, 9f);
                // 청색 원화와 충분히 다른 버건디 유니폼으로 하늘·페넌트 보호 영역을 시각 검수한다.
                view.Show(result, _ => "1994 부산 하버스", "FRANCHISE_76415bde64607643336a", false);
                Assert.That(view.transform.Find("Celebration/Artwork").GetComponent<RawImage>().material.name,
                    Does.Contain("pennant-winner-v1"));
                SaveRender(camera, target, width, height, false, 10f);
                view.OnCancel(null);
                Assert.That(continued, Is.True);
            }
            finally
            {
                CareerPresentationSettings.Mode = mode;
                RenderTexture.active = previousTarget;
                Object.DestroyImmediate(host); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target);
            }
        }

        private static OwnerSeasonReviewSnapshot Snapshot(int wins, int losses, bool completed, bool champion = false, int season = 1)
        {
            return new OwnerSeasonReviewSnapshot(season, LeagueGrade.Rookie, null, "A", 1, 4,
                80, 60, 4, 700, 650, true, completed && champion, true, null, null,
                new[] { new OwnerPostseasonSeriesReview("series", champion ? OwnerPostseasonRound.Championship : OwnerPostseasonRound.Semifinal,
                    "A", "B", wins, losses, 3, completed) });
        }

        [Test]
        public void 관전스냅샷없이확정된우승보고를열면연출을복구하고두번재생하지않는다()
        {
            var fixtureType = Assembly.Load("Baseball.Game.Tests")
                .GetType("Baseball.Tests.EditMode.Game.Historical.ManagerModeMatchServiceTests");
            object[] fixture = { null, null };
            fixtureType.GetMethod("CreateRuntime", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, fixture);
            var runtime = (ManagerHistoricalRuntimeState)fixture[0];
            var provider = (IHistoricalContentProvider)fixture[1];
            foreach (var leagueGroup in runtime.LeagueWorld.Groups)
                foreach (var game in leagueGroup.Season.Schedule.Games)
                    if (!game.IsCompleted)
                        game.Complete(game.AwayTeamId == leagueGroup.Season.PlayerTeamId ? 5 : 1,
                            game.AwayTeamId == leagueGroup.Season.PlayerTeamId ? 1 : 5);
            var balance = Baseball.Core.Balance.BalanceTable.CreateDefault();
            new OwnerPostseasonService(balance).EnsureInitialized(runtime);
            var group = runtime.LeagueWorld.GetGroup(runtime.PlayerTeamSeasonKey);
            int gameId = 900000;
            while (!group.Postseason.IsCompleted)
            {
                var series = group.Postseason.EnsureCurrentSeries(balance.Postseason.SemifinalSeriesGames,
                    balance.Postseason.ChampionshipSeriesGames);
                int winner = series.LowerSeedTeamId == group.Season.PlayerTeamId
                    ? series.LowerSeedTeamId : series.HigherSeedTeamId;
                var game = series.AppendNextGame(++gameId, 1);
                game.Complete(game.AwayTeamId == winner ? 5 : 1, game.HomeTeamId == winner ? 5 : 1);
                series.RecordCompletedGame(game);
                group.Postseason.EnsureCurrentSeries(balance.Postseason.SemifinalSeriesGames,
                    balance.Postseason.ChampionshipSeriesGames);
            }
            var root = new GameObject("Recovery", typeof(RectTransform));
            root.SetActive(false);
            var mode = CareerPresentationSettings.Mode;
            try
            {
                CareerPresentationSettings.Mode = CareerPresentationMode.ResultOnly;
                var manager = root.AddComponent<OwnerModeManager>();
                typeof(OwnerModeManager).GetProperty("Runtime").SetValue(manager, runtime);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(OwnerModeManager).GetField("_balance", flags).SetValue(manager, balance);
                typeof(OwnerModeManager).GetField("_contentProvider", flags).SetValue(manager, provider);
                var coordinator = root.AddComponent<OwnerModeShellCoordinator>();
                var shell = Baseball.Presentation.SharedUI.SharedGameShellView.CreateRuntime(root.transform);
                typeof(OwnerModeShellCoordinator).GetField("_manager", flags).SetValue(coordinator, manager);
                typeof(OwnerModeShellCoordinator).GetField("_shell", flags).SetValue(coordinator, shell);
                var show = typeof(OwnerModeShellCoordinator).GetMethod("ShowSeasonReview", flags);
                show.Invoke(coordinator, new object[] { 1 });
                var popup = (UI_Popup_OwnerPostseasonCelebration)typeof(OwnerModeShellCoordinator)
                    .GetField("_celebrationPopup", flags).GetValue(coordinator);
                Assert.That(popup, Is.Not.Null);
                Assert.That(popup.gameObject.activeSelf, Is.True);
                Assert.That(popup.transform.Find("Celebration/Title").GetComponent<Text>().text, Is.EqualTo("포스트시즌 우승"));
                Assert.That(popup.transform.Find("Celebration/Records").gameObject.activeSelf, Is.False);
                popup.transform.Find("Celebration/Continue").GetComponent<Button>().onClick.Invoke();
                Assert.That(popup.gameObject.activeSelf, Is.False);
                show.Invoke(coordinator, new object[] { 1 });
                Assert.That(popup.gameObject.activeSelf, Is.False);
            }
            finally { CareerPresentationSettings.Mode = mode; Object.DestroyImmediate(root); }
        }

        [Test]
        public void 결과공개전에는승리를숨기고공개후한번만전달한다()
        {
            var gate = new OwnerPostseasonCelebrationGate();
            gate.Begin(Snapshot(2, 1, false));
            Assert.That(gate.Reveal(Snapshot(3, 1, true), false), Is.Null);
            Assert.That(gate.Reveal(Snapshot(3, 1, true), true).Kind, Is.EqualTo(OwnerPostseasonCelebrationKind.SeriesVictory));
            Assert.That(gate.Reveal(Snapshot(3, 1, true), true), Is.Null);
            gate.Begin(Snapshot(2, 1, false, true));
            Assert.That(gate.Reveal(Snapshot(3, 1, true, true), true).Kind, Is.EqualTo(OwnerPostseasonCelebrationKind.Championship));
        }

        [Test]
        public void 일반승리와탈락과이전우승과시즌변경에는연출하지않는다()
        {
            Assert.That(OwnerPostseasonCelebration.Create(Snapshot(1, 1, false), Snapshot(2, 1, false)), Is.Null);
            Assert.That(OwnerPostseasonCelebration.Create(Snapshot(1, 2, false), Snapshot(1, 3, true)), Is.Null);
            Assert.That(OwnerPostseasonCelebration.Create(Snapshot(3, 1, true), Snapshot(3, 1, true)), Is.Null);
            Assert.That(OwnerPostseasonCelebration.Create(Snapshot(2, 1, false), Snapshot(3, 1, true, false, 2)), Is.Null);
            var gate = new OwnerPostseasonCelebrationGate();
            gate.Begin(Snapshot(2, 1, false));
            gate.Clear();
            Assert.That(gate.Reveal(Snapshot(3, 1, true), true), Is.Null);
        }

        [Test]
        public void 하위시드의우승도플레이어기준전적으로표시한다()
        {
            OwnerSeasonReviewSnapshot Create(int wins, bool completed) => new OwnerSeasonReviewSnapshot(
                1, LeagueGrade.Rookie, null, "B", 2, 4, 80, 60, 4, 700, 650, true, completed, true, null, null,
                new[] { new OwnerPostseasonSeriesReview("final", OwnerPostseasonRound.Championship, "A", "B", 1, wins, 3, completed) });
            var result = OwnerPostseasonCelebration.Create(Create(2, false), Create(3, true));
            Assert.That(result.Kind, Is.EqualTo(OwnerPostseasonCelebrationKind.Championship));
            Assert.That(result.TeamKey, Is.EqualTo("B"));
            Assert.That(result.OpponentKey, Is.EqualTo("A"));
            Assert.That(result.Wins, Is.EqualTo(3));
            Assert.That(result.Losses, Is.EqualTo(1));
        }

        [Test]
        public void 대진은순서대로등장하고건너뛰기후재개방은완성상태를유지한다()
        {
            var host = new GameObject("Host", typeof(RectTransform));
            var mode = CareerPresentationSettings.Mode;
            try
            {
                CareerPresentationSettings.Mode = CareerPresentationMode.Full;
                var view = UI_Popup_OwnerSeasonReview.CreateRuntime(host.GetComponent<RectTransform>());
                view.Bind(Snapshot(1, 1, false), key => "구단 " + key, 1);
                view.Show();
                Assert.That(view.IsBracketRevealing, Is.True);
                object sequence = typeof(UI_Popup_OwnerSeasonReview).GetField("_bracketSequence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Seek(sequence, 0.15f);
                var cards = view.transform.Find("SeasonReview/ResultHero").GetComponentsInChildren<CanvasGroup>();
                Assert.That(cards[0].alpha, Is.GreaterThan(0f).And.LessThan(1f));
                view.SkipBracketReveal();
                foreach (var card in cards) Assert.That(card.alpha, Is.EqualTo(1f));
                view.Hide(); view.Show();
                Assert.That(view.IsBracketRevealing, Is.False);
            }
            finally { CareerPresentationSettings.Mode = mode; Object.DestroyImmediate(host); }
        }

        [TestCase(1280, 720, false)]
        [TestCase(1920, 1080, false)]
        [TestCase(2560, 1440, false)]
        [TestCase(3440, 1440, false)]
        [TestCase(1280, 720, true)]
        [TestCase(1920, 1080, true)]
        [TestCase(2560, 1440, true)]
        [TestCase(3440, 1440, true)]
        public void 단계별연출과최종화면은안전영역을지키고항상종료할수있다(int width, int height, bool champion)
        {
            var host = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            var previousTarget = RenderTexture.active;
            var mode = CareerPresentationSettings.Mode;
            try
            {
                CareerPresentationSettings.Mode = CareerPresentationMode.Full;
                var hostRect = host.GetComponent<RectTransform>();
                float scale = Mathf.Sqrt(width / 1920f * height / 1080f);
                hostRect.sizeDelta = new Vector2(width / scale, height / scale);
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = hostRect.rect.height / 2;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                var view = UI_Popup_OwnerPostseasonCelebration.CreateRuntime(hostRect);
                var result = OwnerPostseasonCelebration.Create(Snapshot(2, 1, false, champion), Snapshot(3, 1, true, champion));
                view.Show(result, key => key == "A" ? "삼성 라이온즈" : "롯데 자이언츠");
                Assert.That(view.IsAnimating, Is.True);
                var art = view.GetComponentInChildren<RawImage>();
                Assert.That(art.texture, Is.Not.Null);
                object sequence = typeof(UI_Popup_OwnerPostseasonCelebration).GetField("_sequence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                foreach (float sample in new[] { 0.3f, 1.1f, 1.9f, 5f })
                {
                    if (sample < 5f) Seek(sequence, sample); else view.Skip();
                    Canvas.ForceUpdateCanvases();
                    foreach (Text text in view.GetComponentsInChildren<Text>())
                    {
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name);
                        var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                        foreach (Vector3 corner in corners)
                            Assert.That(hostRect.rect.Contains((Vector2)hostRect.InverseTransformPoint(corner)), Is.True, text.name);
                    }
                    foreach (Button button in view.GetComponentsInChildren<Button>()) Assert.That(button.interactable, Is.True);
                    SaveRender(camera, target, width, height, champion, sample);
                }
                Assert.That(view.IsAnimating, Is.False);
                bool continued = false, records = false;
                view.ContinueRequested += () => continued = true;
                view.RecordsRequested += () => records = true;
                view.transform.Find("Celebration/Continue").GetComponent<Button>().onClick.Invoke();
                view.OnCancel(null);
                Assert.That(continued && records, Is.True);
                view.Hide();
                CareerPresentationSettings.Mode = CareerPresentationMode.ResultOnly;
                view.Show(result, key => "구단");
                Assert.That(view.IsAnimating, Is.False);
                Assert.That(art.color.a, Is.EqualTo(1f));
            }
            finally
            {
                CareerPresentationSettings.Mode = mode;
                RenderTexture.active = previousTarget;
                Object.DestroyImmediate(host); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target);
            }
        }

        private static void Seek(object sequence, float time)
        {
            sequence.GetType().Assembly.GetType("DG.Tweening.TweenExtensions").GetMethod("Goto",
                new[] { sequence.GetType().BaseType, typeof(float), typeof(bool) }).Invoke(null, new[] { sequence, (object)time, false });
        }

        private static void SaveRender(Camera camera, RenderTexture target, int width, int height, bool champion, float sample)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-seasonReviewReport");
            if (index < 0 || index + 1 >= args.Length) return;
            Directory.CreateDirectory(args[index + 1]);
            camera.Render(); RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(args[index + 1], $"ceremony-{width}x{height}-{champion}-{sample:0.0}.png"), pixels.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(pixels); }
        }
    }
}
