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
                var opponent = Opponent(); view.BindOpponent(opponent, Array.Empty<TeamColorDefinition>());
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
