using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed class OwnerSeasonReviewPresentationTests
    {
        [Test]
        public void KBO네라운드를시즌보고에빠짐없이표시한다()
        {
            var host = new GameObject("PopupHost", typeof(RectTransform));
            try
            {
                var view = UI_Popup_OwnerSeasonReview.CreateRuntime(host.GetComponent<RectTransform>());
                view.Bind(new OwnerSeasonReviewSnapshot(1, LeagueGrade.Rookie, null, "A",
                    1, 10, 80, 60, 4, 700, 650, true, false, true, null, null,
                    new[]
                    {
                        new OwnerPostseasonSeriesReview("wc", OwnerPostseasonRound.WildCard, "D", "E", 1, 0, 2, true),
                        new OwnerPostseasonSeriesReview("semi", OwnerPostseasonRound.SemiPlayoff, "C", "D", 3, 0, 3, true),
                        new OwnerPostseasonSeriesReview("po", OwnerPostseasonRound.Playoff, "B", "C", 3, 0, 3, true),
                        new OwnerPostseasonSeriesReview("final", OwnerPostseasonRound.Championship, "A", "B", 0, 0, 4, false)
                    }), key => "구단 " + key, 1);
                view.Show();
                for (int index = 0; index < 4; index++)
                    Assert.That(view.transform.Find("SeasonReview/ResultHero/Series" + index).gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find("SeasonReview/ResultHero/Series3/Round").GetComponent<Text>().text,
                    Does.Contain("한국시리즈"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void 두구단챔피언십에는추가결승대기카드를표시하지않는다()
        {
            var host = new GameObject("PopupHost", typeof(RectTransform));
            try
            {
                var view = UI_Popup_OwnerSeasonReview.CreateRuntime(host.GetComponent<RectTransform>());
                view.Bind(new OwnerSeasonReviewSnapshot(1, LeagueGrade.Rookie, null, "A",
                    1, 2, 80, 60, 4, 700, 650, true, false, true, null, null,
                    new[] { new OwnerPostseasonSeriesReview("final", OwnerPostseasonRound.Championship,
                        "A", "B", 0, 0, 3, false) }), key => "구단 " + key, 1);
                view.Show();
                Assert.That(view.transform.Find("SeasonReview/ResultHero/Series0").gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find("SeasonReview/ResultHero/Series1").gameObject.activeSelf, Is.False);
                Assert.That(view.transform.Find("SeasonReview/ResultHero/Series2").gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void 포스트시즌경기는실제관전으로열고결과공개후대진복귀를제공한다()
        {
            var fixtureType = System.Reflection.Assembly.Load("Baseball.Game.Tests")
                .GetType("Baseball.Tests.EditMode.Game.Historical.ManagerModeMatchServiceTests");
            object[] fixture = { null, null };
            fixtureType.GetMethod("CreateRuntime", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static).Invoke(null, fixture);
            var runtime = (ManagerHistoricalRuntimeState)fixture[0];
            var provider = (IHistoricalContentProvider)fixture[1];
            foreach (var group in runtime.LeagueWorld.Groups)
                foreach (var game in group.Season.Schedule.Games)
                    if (!game.IsCompleted)
                        game.Complete(game.AwayTeamId == group.Season.PlayerTeamId ? 5 : 1,
                            game.AwayTeamId == group.Season.PlayerTeamId ? 1 : 5);
            var managerObject = new GameObject("PostseasonManager");
            managerObject.SetActive(false);
            var host = new GameObject("Workspace", typeof(RectTransform));
            var settings = Baseball.Presentation.Match.OwnerMatchPresentationSettings.Load();
            try
            {
                var manager = managerObject.AddComponent<OwnerModeManager>();
                var balance = Baseball.Core.Balance.BalanceTable.CreateDefault();
                typeof(OwnerModeManager).GetProperty("Runtime").SetValue(manager, runtime);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(OwnerModeManager).GetField("_balance", flags).SetValue(manager, balance);
                typeof(OwnerModeManager).GetField("_contentProvider", flags).SetValue(manager, provider);
                typeof(OwnerModeManager).GetField("_matchService", flags).SetValue(manager,
                    new ManagerModeMatchService(provider, balance));
                Assert.That(manager.BeginPostseasonSimulation(), Is.True, manager.LastError);
                while (!manager.IsNextPostseasonGamePlayerMatch)
                    Assert.That(manager.AdvancePostseasonSimulationFrame(), Is.True, manager.LastError);
                int before = manager.PostseasonSimulationProgress.CompletedGames;
                // 실제 셸은 관전 실행 전에 축하 판정용 시즌 보고를 읽는다.
                var gate = new OwnerPostseasonCelebrationGate();
                Assert.DoesNotThrow(() => gate.Begin(manager.CreateSeasonReview()));
                Assert.That(manager.PostseasonSimulationProgress.CompletedGames, Is.EqualTo(before));
                Assert.Throws<System.InvalidOperationException>(() => manager.Save());
                Assert.Throws<System.InvalidOperationException>(() => manager.StartNewGame());
                // 이전 경기에서 즉시 결과를 선택했어도 명시적인 관전 요청은 중계를 연다.
                Baseball.Presentation.Match.OwnerMatchPresentationSettings.SetViewingMode(
                    Baseball.Presentation.Match.OwnerMatchViewingMode.ResultOnly);
                var view = Baseball.Presentation.Match.UI_Scene_OwnerMatchSpectator.CreateRuntime(host.GetComponent<RectTransform>());
                int completionCount = 0;
                view.PresentationCompleted += () => completionCount++;
                view.PlayNextGame(manager);
                Assert.That(manager.IsPostseasonSimulationRunning, Is.False);
                Assert.That(manager.PostseasonSimulationProgress.CompletedGames, Is.EqualTo(before + 1));
                Assert.That(view.IsPresenting, Is.True);
                Assert.That(completionCount, Is.Zero);
                Assert.That(view.IsComplete, Is.False, "최종 결과를 중계 전에 공개하면 안 된다.");
                view.transform.Find("BroadcastCanvas/RevealAll").GetComponent<Button>().onClick.Invoke();
                Assert.That(view.IsComplete, Is.True);
                Assert.That(completionCount, Is.EqualTo(1));
                var back = view.transform.Find("BroadcastCanvas/ReturnHome").GetComponent<Button>();
                Assert.That(back.GetComponentInChildren<Text>().text, Is.EqualTo("대진으로 돌아가기"));
                bool returned = false;
                view.HomeRequested += () => returned = true;
                back.onClick.Invoke();
                Assert.That(returned, Is.True);
                Assert.That(manager.PostseasonSimulationProgress.CompletedGames, Is.EqualTo(before + 1));
            }
            finally
            {
                Baseball.Presentation.Match.OwnerMatchPresentationSettings.SetViewingMode(settings.ViewingMode);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(managerObject);
            }
        }

        [TestCase(0, 0, false, "준결승 1차전", "다음 경기 관전")]
        [TestCase(1, 1, false, "준결승 3차전", "다음 경기 관전")]
        [TestCase(2, 1, true, "다음 라운드", "다음 경기 관전")]
        [TestCase(1, 2, true, "가을 야구 종료", "남은 리그 마감")]
        public void 다음차전과탈락및결승대기를구분한다(int wins, int losses, bool completed, string title, string action)
        {
            var host = new GameObject("PopupHost", typeof(RectTransform));
            try
            {
                var view = UI_Popup_OwnerSeasonReview.CreateRuntime(host.GetComponent<RectTransform>());
                view.Bind(new OwnerSeasonReviewSnapshot(5, LeagueGrade.Rookie, null, "A",
                    2, 9, 78, 65, 1, 700, 660, true, false, false, 0, 38, true, null, null,
                    new[] { new OwnerPostseasonSeriesReview("semi", OwnerPostseasonRound.Semifinal,
                        "A", "B", wins, losses, 2, completed) }), key => key == "A" ? "서울 구단" : "부산 구단", 1);
                view.Show();
                Transform modal = view.transform.Find("SeasonReview");
                Assert.That(modal.Find("ResultHero/Summary").GetComponent<Text>().text, Does.Contain(title));
                Assert.That(modal.Find("Primary/Label").GetComponent<Text>().text, Is.EqualTo(action));
                Assert.That(modal.Find("ResultHero/Series0/Score").GetComponent<Text>().text, Is.EqualTo($"{wins} : {losses}"));
                if (wins == 1 && losses == 1 && !completed)
                    Assert.That(modal.Find("ResultHero/Status").GetComponent<Text>().text, Does.Contain("최종전"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void 미진출은남은리그마감으로안내하고결산잠금과취소포커스를유지한다()
        {
            var eventsObject = new GameObject("EventSystem", typeof(EventSystem));
            var host = new GameObject("PopupHost", typeof(RectTransform));
            var previous = new GameObject("Previous", typeof(RectTransform), typeof(Button));
            try
            {
                var events = eventsObject.GetComponent<EventSystem>();
                events.SetSelectedGameObject(previous);
                var view = UI_Popup_OwnerSeasonReview.CreateRuntime(host.GetComponent<RectTransform>());
                view.Bind(new OwnerSeasonReviewSnapshot(1, LeagueGrade.Rookie, null, "TEAM-A",
                    10, 10, 40, 104, 0, 400, 1000, false, false, false, 0, 37, false,
                    OwnerTeamPostseasonResult.DidNotQualify, null,
                    new OwnerPostseasonSeriesReview[0]), key => null, 1);
                view.Show();
                Transform modal = view.transform.Find("SeasonReview");
                var primary = modal.Find("Primary").GetComponent<Button>();
                Assert.That(primary.GetComponentInChildren<Text>().text, Is.EqualTo("남은 리그 마감"));
                Assert.That(modal.Find("Tab2").GetComponent<Button>().interactable, Is.False);
                Assert.That(modal.Find("Hint").GetComponent<Text>().text, Does.Contain("모든 조"));
                foreach (Button button in view.GetComponentsInChildren<Button>())
                {
                    if (!button.interactable) continue;
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                    foreach (Selectable next in new[] { button.navigation.selectOnLeft, button.navigation.selectOnRight,
                        button.navigation.selectOnUp, button.navigation.selectOnDown })
                    {
                        Assert.That(next.transform.IsChildOf(view.transform), Is.True);
                        Assert.That(next.interactable, Is.True);
                    }
                }
                bool closed = false;
                view.SkipBracketReveal();
                view.CloseRequested += () => { closed = true; view.Hide(); };
                view.OnCancel(new BaseEventData(events));
                Assert.That(closed, Is.True);
                Assert.That(events.currentSelectedGameObject, Is.EqualTo(previous));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(previous);
                Object.DestroyImmediate(eventsObject);
            }
        }

        [TestCase(1280, 720, true)]
        [TestCase(1920, 1080, true)]
        [TestCase(2560, 1440, true)]
        [TestCase(3440, 1440, true)]
        [TestCase(1280, 720, false)]
        [TestCase(1920, 1080, false)]
        [TestCase(2560, 1440, false)]
        [TestCase(3440, 1440, false)]
        public void 결과화면은각해상도에서기록과안내를분리하고버튼대비를유지한다(int width, int height, bool completed)
        {
            var host = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            RenderTexture previous = RenderTexture.active;
            try
            {
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                // 실제 셸의 CanvasScaler와 같은 논리 크기를 사용한다.
                float scale = Mathf.Sqrt(width / 1920f * height / 1080f);
                var hostRect = host.GetComponent<RectTransform>();
                hostRect.sizeDelta = new Vector2(width / scale, height / scale);
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = hostRect.rect.height / 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CareerUiTheme.Background;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                var view = UI_Popup_OwnerSeasonReview.CreateRuntime(hostRect);
                var snapshot = new OwnerSeasonReviewSnapshot(
                    20, LeagueGrade.Rookie, LeagueGrade.Minor, "TEAM-A",
                    1, 10, 100, 44, 0, 1234, 567, true, true, true,
                    OwnerTeamPostseasonResult.Champion, "TEAM-A",
                    new[] {
                        new OwnerPostseasonSeriesReview("semi-a", OwnerPostseasonRound.Semifinal,
                            "TEAM-A", "TEAM-B", 3, 2, 3, true),
                        new OwnerPostseasonSeriesReview("semi-b", OwnerPostseasonRound.Semifinal,
                            "TEAM-C", "TEAM-D", 3, 1, 3, true),
                        new OwnerPostseasonSeriesReview("final", OwnerPostseasonRound.Championship,
                            "TEAM-A", "TEAM-C", 3, 2, 3, true) });
                if (!completed)
                    snapshot = new OwnerSeasonReviewSnapshot(5, LeagueGrade.Rookie, null, "TEAM-A",
                        2, 9, 78, 65, 1, 700, 660, true, false, false, 0, 38, true, null, null,
                        new[] {
                            new OwnerPostseasonSeriesReview("semi-a", OwnerPostseasonRound.Semifinal,
                                "TEAM-C", "TEAM-D", 2, 0, 2, true),
                            new OwnerPostseasonSeriesReview("semi-b", OwnerPostseasonRound.Semifinal,
                                "TEAM-A", "TEAM-B", 1, 1, 2, false) });
                for (int page = 0; page < (completed ? 3 : 2); page++)
                {
                    view.Bind(snapshot, key => key == "TEAM-A" ? "서울 챔피언스 베이스볼" : "부산 인터내셔널 마리너스", page);
                    view.Show();
                    CareerUiSkin.Apply(view.transform);
                    view.SkipBracketReveal();
                    Canvas.ForceUpdateCanvases();
                    RectTransform modal = (RectTransform)view.transform.Find("SeasonReview");
                    AssertContained(hostRect, modal);
                    RectTransform insight = (RectTransform)modal.Find("NextStep");
                    for (int i = 0; i < 3; i++)
                    {
                        RectTransform metric = (RectTransform)modal.Find("Metric" + i);
                        Assert.That(GetBounds(modal, metric).Overlaps(GetBounds(modal, insight)), Is.False);
                    }
                    foreach (Text text in view.GetComponentsInChildren<Text>())
                    {
                        AssertContained((RectTransform)text.transform.parent, text.rectTransform);
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f), text.name + " 세로 잘림");
                        if (text.name == "Teams" || text.name == "Score")
                            Assert.That(text.color.grayscale, Is.GreaterThan(0.85f), "대진 카드 본문 대비");
                    }
                    Button primary = modal.Find("Primary").GetComponent<Button>();
                    Assert.That(primary.GetComponent<OwnerUiButtonSkin>(), Is.Not.Null);
                    Assert.That(((Image)primary.targetGraphic).sprite, Is.Not.Null);
                    Assert.That(primary.transform.Find("Label").GetComponent<Text>().color.grayscale, Is.GreaterThan(0.85f));
                    string[] args = System.Environment.GetCommandLineArgs();
                    int reportIndex = System.Array.IndexOf(args, "-seasonReviewReport");
                    if (reportIndex < 0 || reportIndex + 1 >= args.Length) continue;
                    System.IO.Directory.CreateDirectory(args[reportIndex + 1]);
                    camera.Render();
                    RenderTexture.active = target;
                    var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    try
                    {
                        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        pixels.Apply();
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(args[reportIndex + 1], $"season-{width}x{height}-page{page}-{(completed ? "final" : "live")}.png"), pixels.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(pixels); }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
            }
        }

        private static void AssertContained(RectTransform parent, RectTransform child)
        {
            Rect bounds = GetBounds(parent, child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - 1f), child.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - 1f), child.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + 1f), child.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + 1f), child.name);
        }

        private static Rect GetBounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3 min = parent.InverseTransformPoint(corners[0]);
            Vector3 max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [Test]
        public void Bind_페넌트레이스부터포스트시즌과결산까지한팝업에서탐색한다()
        {
            var hostObject = new GameObject("PopupHost", typeof(RectTransform));
            UI_Popup_OwnerSeasonReview view = UI_Popup_OwnerSeasonReview.CreateRuntime(
                hostObject.GetComponent<RectTransform>());
            var snapshot = new OwnerSeasonReviewSnapshot(
                3, LeagueGrade.Rookie, LeagueGrade.Minor, "TEAM-A",
                2, 10, 87, 2, 55, 633, 588,
                true, true, true, OwnerTeamPostseasonResult.RunnerUp, "TEAM-B",
                new[]
                {
                    new OwnerPostseasonSeriesReview("championship", OwnerPostseasonRound.Championship,
                        "TEAM-A", "TEAM-B", 2, 3, 3, true)
                });

            view.Bind(snapshot, key => key == "TEAM-A" ? "서울 베어스" : "부산 마리너스", 0);
            view.Show();

            Text summary = view.transform.Find("SeasonReview/ResultHero/Summary").GetComponent<Text>();
            Button primary = view.transform.Find("SeasonReview/Primary").GetComponent<Button>();
            Assert.That(summary.text, Does.Contain("2위"));

            primary.onClick.Invoke();
            Assert.That(summary.text, Is.EqualTo("포스트시즌 준우승"));
            primary.onClick.Invoke();
            Assert.That(summary.text, Does.Contain("정규시즌 2위"));

            Object.DestroyImmediate(hostObject);
        }

        [Test]
        public void Bind_우리조만완료된경우남은월드포스트시즌을다시진행할수있다()
        {
            var hostObject = new GameObject("PopupHost", typeof(RectTransform));
            UI_Popup_OwnerSeasonReview view = UI_Popup_OwnerSeasonReview.CreateRuntime(
                hostObject.GetComponent<RectTransform>());
            var snapshot = new OwnerSeasonReviewSnapshot(
                1, LeagueGrade.Rookie, null, "TEAM-A",
                2, 10, 80, 62, 2, 615, 537,
                true, true, false, 1, 4, true,
                OwnerTeamPostseasonResult.Champion, "TEAM-A",
                new[]
                {
                    new OwnerPostseasonSeriesReview("championship", OwnerPostseasonRound.Championship,
                        "TEAM-A", "TEAM-B", 3, 1, 3, true)
                });
            bool requested = false;
            view.PostseasonRequested += () => requested = true;

            view.Bind(snapshot, key => key, 1);
            Button primary = view.transform.Find("SeasonReview/Primary").GetComponent<Button>();
            Text label = primary.transform.Find("Label").GetComponent<Text>();

            Assert.That(label.text, Is.EqualTo("남은 리그 마감"));
            primary.onClick.Invoke();
            Assert.That(requested, Is.True);

            Object.DestroyImmediate(hostObject);
        }
    }
}
