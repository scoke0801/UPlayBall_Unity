using System;
using System.Linq;
using Baseball.Game.Historical;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed class LegendaryPracticeUiTests
    {
        [Test]
        public void 전체역대강팀의실제가상이름과공개편성이경기입력에대응한다()
        {
            var root = new GameObject("PracticeIdentityTest");
            bool originalMode = DevelopmentRealIdentitySettings.IsEnabled;
            try
            {
                var provider = Baseball.Game.Data.NewGameDefinition.LoadHistoricalContentProvider();
                var content = provider.Load();
                var registry = new Baseball.Simulation.Historical.WorldIdentityGenerator().Generate(
                    content.PlayerPersons, content.TeamSeasons, content.IdentityNameCatalog, 973UL);
                var allCards = content.NormalCards.Concat(content.SpecialCards.Cards).ToArray();
                var cardCatalog = new WorldCardCatalog(content.PlayerSeasons, allCards, content.PlayerPersons,
                    content.SpecialCards.Lineages, content.SpecialCards.Recipes);
                var fixtureType = System.Reflection.Assembly.Load("Baseball.Game.Tests")
                    .GetType("Baseball.Tests.EditMode.Game.Historical.ManagerHistoricalSaveTests")
                    .GetNestedType("Fixture", System.Reflection.BindingFlags.NonPublic);
                var fixture = fixtureType.GetMethod("Create").Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
                var runtime = (ManagerHistoricalRuntimeState)fixture.GetType().GetProperty("State").GetValue(fixture);
                const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                // 진행용 최소 Fixture에 표시 조회에 필요한 정본 카드와 Identity만 주입한다. 저장·경기는 실행하지 않는다.
                typeof(ManagerHistoricalRuntimeState).GetField("<WorldCardCatalog>k__BackingField", fields).SetValue(runtime, cardCatalog);
                typeof(ManagerHistoricalRuntimeState).GetField("<IdentityRegistry>k__BackingField", fields).SetValue(runtime, registry);
                var manager = root.AddComponent<OwnerModeManager>();
                typeof(OwnerModeManager).GetProperty("Runtime").SetValue(manager, runtime);
                typeof(OwnerModeManager).GetField("_contentProvider", fields).SetValue(manager, provider);
                typeof(OwnerModeManager).GetField("_balance", fields).SetValue(manager,
                    Baseball.Game.Data.NewGameDefinition.LoadOwnerModeBalanceTable());
                var factory = new OwnerModeRuntimeSnapshotFactory();
                foreach (bool real in new[] { true, false })
                {
                    DevelopmentRealIdentitySettings.SetEnabled(real);
                    foreach (var team in manager.GetPracticeCatalog().teams)
                    {
                        var opponents = manager.GetPracticeOpponent(team.challengeTeamId, out var colors);
                        var cards = manager.GetPracticeCards(team.challengeTeamId);
                        var snapshot = factory.CreatePracticeLineup(manager, team.challengeTeamId, opponents, colors);
                        Assert.That(snapshot.Hitters.Count, Is.EqualTo(14));
                        Assert.That(snapshot.Pitchers.Count, Is.EqualTo(11));
                        foreach (var detail in snapshot.HitterDetails.Concat(snapshot.PitcherDetails)) Assert.That(detail, Is.Not.Null);
                        for (int i = 0; i < 9; i++)
                        {
                            var card = cards[opponents[0].StartingLineup[i].Player.PlayerId - OwnerModeManager.PracticePlayerIdBase - 1];
                            var season = manager.GetPracticePlayerSeason(card);
                            Assert.That(snapshot.Hitters[i].PlayerId, Is.EqualTo(card.CardId));
                            Assert.That(snapshot.Hitters[i].DisplayName, Is.EqualTo(registry.GetPresentationPlayerName(season.PlayerPersonId)));
                        }
                        Assert.That(string.Join(" ", snapshot.TeamColors), Does.Not.Contain("FRANCHISE_"));
                        Assert.That(string.Join(" ", snapshot.TeamColors), Does.Not.Contain(team.year + " " + team.year));
                        Assert.That(snapshot.TeamName, Is.EqualTo(manager.GetTeamIdentityDisplayName(team.teamSeasonKey)));
                    }
                }
            }
            finally
            {
                DevelopmentRealIdentitySettings.SetEnabled(originalMode);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static LegendaryPracticeCatalog Catalog()
        {
            var asset = Resources.Load<TextAsset>("NewGame/LegendaryPracticeCatalog");
            Assert.That(asset, Is.Not.Null, "실제 배포 카탈로그가 필요합니다.");
            var result = JsonUtility.FromJson<LegendaryPracticeCatalog>(asset.text); result.Validate();
            Assert.That(result.simulationVersion, Is.EqualTo(LegendaryPracticeCatalog.CreateSimulationVersion(
                Resources.Load<TextAsset>("NewGame/MiniGameBalance").text,
                Resources.Load<TextAsset>("NewGame/MatchRatingCurve").text)));
            return result;
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void 공용스킨과열배치를해상도별로그린다(int width, int height)
        {
            var root = new GameObject("PracticeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24); var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var shell = SharedGameShellView.CreateRuntime(root.transform);
                shell.BindProfile(OwnerModeUiProfileFactory.Create());
                shell.SetInspectorVisible(false); shell.SetActionBarVisible(false);
                shell.BindContext(new ShellContextModel(OwnerNavigationRoutes.LegendaryPractice, "역대 강팀", "3승으로 다음 역사에 도전하세요", "연습경기", true, "홈으로"));
                shell.BindStatus(new ShellStatusModel("2026 시즌", "3월 28일", "루키 리그", "서울 드래곤즈", "1위", "정규시즌 대기",
                    new[] { new ShellStatusSlotModel("money", "골드", "250,000"), new ShellStatusSlotModel("development", "육성", "1,200") }));
                var view = UI_Scene_LegendaryPractice.CreateRuntime(shell.MainWorkspaceHost);
                var catalog = Catalog(); var state = new LegendaryPracticeState();
                view.Bind(catalog, state, t => t.year + " 서울 드래곤즈");
                Button[] pool = root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Team")).ToArray();
                Assert.That(pool.Length, Is.EqualTo(10));
                root.GetComponentsInChildren<Button>().Single(b => b.name == "Next").onClick.Invoke();
                CollectionAssert.AreEqual(pool, root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Team")).ToArray());
                root.GetComponentsInChildren<Button>().Single(b => b.name == "Previous").onClick.Invoke();
                view.ShowError("먼저 앞선 팀에 3승을 달성해 주세요.");
                Assert.That(root.GetComponentsInChildren<Button>().Single(b => b.name == "Start").interactable, Is.False);
                view.Bind(catalog, state, t => t.year + " 서울 드래곤즈");
                var opponent = Opponent();
                var hitters = Enumerable.Range(0, 14).Select(i => new PlayerMiniCardModel("h" + i,
                    "공개야수" + i, (i + 1) + "번", "96", "", "", isInteractable: false)).ToArray();
                var pitchers = Enumerable.Range(0, 11).Select(i => new PlayerMiniCardModel("p" + i,
                    "공개투수" + i, i < 5 ? (i + 1) + "선발" : "중계", "96", "", "", isInteractable: false)).ToArray();
                var lineup = new OwnerTeamLineupSnapshot("1996 현대 유니콘스", "역대 강팀 도전 라인업", "편성 비용 119",
                    hitters, pitchers, new[] { "1996 현대 유니콘스 · 완성된 연대기", "1996 현대 유니콘스 · 한 시즌의 중심" });
                view.BindOpponent(opponent, lineup);
                string rotation = root.GetComponentsInChildren<Text>().Single(t => t.name == "Rotation").text;
                Assert.That(rotation, Does.Contain("공개투수0"));
                Assert.That(rotation, Does.Not.Contain(opponent[0].StartingPitcher.Player.Name));
                Assert.That(rotation, Does.Not.Contain("FRANCHISE_"));
                var cards = new PlayerMiniCardModel[3];
                for (int i = 0; i < 3; i++) cards[i] = new PlayerMiniCardModel((i + 1).ToString(), opponent[0].StartingLineup[i].Player.Name,
                    "주전 야수", "2015", "", "레어", stats: new[] { new PlayerMiniCardStatModel("교타", 85, 200),
                        new PlayerMiniCardStatModel("장타", 91, 200), new PlayerMiniCardStatModel("주력", 72, 200) }, frameEdition: PlayerCardEdition.Rare, cost: 7);
                view.BindFeaturedCards(cards);
                view.BindPlayerRoster(opponent[0]);
                Canvas.ForceUpdateCanvases(); camera.Render();
                foreach (var text in root.GetComponentsInChildren<Text>()) text.SetAllDirty();
                Canvas.ForceUpdateCanvases(); camera.Render();
                var corners = new Vector3[4];
                foreach (var button in pool)
                {
                    var safe = (RectTransform)button.transform.parent;
                    ((RectTransform)button.transform).GetWorldCorners(corners);
                    foreach (var point in corners)
                    { var local = safe.InverseTransformPoint(point);
                        Assert.That(local.x, Is.InRange(safe.rect.xMin - 1, safe.rect.xMax + 1), button.name);
                        Assert.That(local.y, Is.InRange(safe.rect.yMin - 1, safe.rect.yMax + 1), button.name); }
                }
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                string output = System.IO.Path.GetFullPath("../screenshots"); System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, "practice-" + width + ".png"), image.EncodeToPNG());
                root.GetComponentsInChildren<Button>().Single(b => b.name == "Lineup").onClick.Invoke();
                var lineupBoard = view.GetComponentInChildren<UI_Scene_OwnerTeamLineup>();
                Assert.That(lineupBoard, Is.Not.Null);
                Assert.That(lineupBoard.GetComponentsInChildren<PlayerMiniCardView>().Length, Is.EqualTo(25));
                Assert.That(lineupBoard.transform.Find("CloseLineup").GetComponentInChildren<Text>().text,
                    Is.EqualTo("역대 강팀으로 돌아가기"));
                Assert.That(view.transform.Find("Opponent").gameObject.activeSelf, Is.False);
                Canvas.ForceUpdateCanvases(); camera.Render();
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, "practice-lineup-" + width + ".png"), image.EncodeToPNG());
                Assert.That(view.TryGoBack(), Is.True);
                Assert.That(view.SelectedTeamId, Is.EqualTo(catalog.teams[99].challengeTeamId));
                Assert.That(view.transform.Find("Opponent").gameObject.activeSelf, Is.True);
                Assert.That(view.TryGoBack(), Is.False);
                if (width == 1920)
                {
                    root.GetComponentsInChildren<Button>().Single(b => b.name == "Compare").onClick.Invoke();
                    Assert.That(root.GetComponentsInChildren<Text>().Single(t => t.name == "LineupDetails").text, Does.Contain("선발 제구"));
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, "practice-comparison.png"), image.EncodeToPNG());
                    Assert.That(view.TryGoBack(), Is.True);
                    string first = catalog.teams[99].challengeTeamId;
                    for (int attempt = 1; attempt <= 3; attempt++) state.Commit(catalog, first, attempt, 4, 1, 1);
                    view.Bind(catalog, state, t => t.year + " 서울 드래곤즈");
                    Assert.That(root.GetComponentsInChildren<Button>().Single(b => b.name == "Start").GetComponentInChildren<Text>().text, Is.EqualTo("보상 받기"));
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, "practice-reward.png"), image.EncodeToPNG());
                    pool[1].onClick.Invoke();
                    Assert.That(root.GetComponentsInChildren<Button>().Single(b => b.name == "Start").interactable, Is.True);
                    view.SetBusy(true);
                    Assert.That(root.GetComponentsInChildren<Button>().Single(b => b.name == "Start").IsInteractable(), Is.False);
                }
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            { RenderTexture.active = previous; cameraObject.GetComponent<Camera>().targetTexture = null; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root); }
        }

        private static MatchRosterSnapshot[] Opponent()
        {
            string[] names = { "김진우", "이성훈", "박준혁", "최민재", "정동현", "강태준", "조현우", "윤성민", "장준서" };
            Baseball.Core.Players.Player Player(int id, string name, PlayerPosition position) => new Baseball.Core.Players.Player(id, name, position, Handedness.Right, Handedness.Right,
                new BatterAttributes(80, 90, 70, 50, 70, 80), new PitcherAttributes(80, 90, 80, 70, 85, 80));
            var lineup = new LineupSlot[9];
            for (int i = 0; i < 9; i++) lineup[i] = new LineupSlot(Player(i + 1, names[i], (PlayerPosition)(i + 1)), (PlayerPosition)(i + 1));
            var bullpen = new PitcherRosterEntry[6];
            for (int i = 0; i < 6; i++) bullpen[i] = new PitcherRosterEntry(Player(i + 20, "김민수", PlayerPosition.ReliefPitcher), PitcherRole.MiddleRelief);
            var result = new MatchRosterSnapshot[5]; string[] pitchers = { "강민호", "최준호", "윤성훈", "장민석", "박도현" };
            for (int i = 0; i < 5; i++) result[i] = new MatchRosterSnapshot(2, "서울 드래곤즈", new Lineup(lineup),
                new PitcherRosterEntry(Player(i + 10, pitchers[i], PlayerPosition.StartingPitcher), PitcherRole.Starter), bullpen,
                Array.Empty<Baseball.Core.Players.Player>(), default, RunningApproach.Balanced);
            return result;
        }
    }
}
