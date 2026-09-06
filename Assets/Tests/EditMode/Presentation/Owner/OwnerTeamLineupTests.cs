using System;
using System.IO;
using System.Linq;
using Baseball.Core.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.Presentation.Owner
{
    /// <summary>상대 라인업의 진입·복귀, 읽기 전용 카드 교체와 실제 uGUI 배치를 검증한다.</summary>
    public sealed class OwnerTeamLineupTests
    {
        [Test]
        public void Navigation_컨디션탭을상대라인업으로이관한다()
        {
            var profile = OwnerModeUiProfileFactory.Create();
            Assert.That(profile.ResolveRouteId(OwnerNavigationRoutes.MatchCenterCondition),
                Is.EqualTo(OwnerNavigationRoutes.MatchCenterOpponentLineup));
            var tabs = profile.ContextNavigation.FindEntry(OwnerNavigationRoutes.MatchCenter).Children;
            Assert.That(tabs.Select(tab => tab.DisplayName),
                Is.EqualTo(new[] { "상대 분석", "우리 라인업", "상대 라인업", "전술카드" }));
        }

        [Test]
        public void Standings_구단선택과복귀후다른구단선택이같은보드를갱신한다()
        {
            var root = new GameObject("LineupTest", typeof(RectTransform));
            try
            {
                var shell = SharedGameShellView.CreateRuntime(root.transform);
                var coordinator = shell.gameObject.AddComponent<OwnerSharedInformationWorkspaceCoordinator>();
                coordinator.Initialize(shell);
                coordinator.SetTeamLineupResolver(CreateSnapshot);
                var schedule = new ScheduleScreenSnapshot("2024 시즌", "루키 리그", "1주차", "a",
                    new[] { new ScheduleGameSnapshot("1", 1, "1R", new ScheduleTeamSnapshot("a", "가 구단"),
                        new ScheduleTeamSnapshot("b", "나 구단"), false, 0, 0, ScheduleFocusSide.None) });
                coordinator.BindSchedule(schedule, OwnerModeUiProfileFactory.Create().Capabilities);
                Assert.That(coordinator.TryShowRoute(OwnerNavigationRoutes.LeagueStandings), Is.True);
                var league = shell.GetComponentInChildren<UI_Scene_OwnerLeague>(true);
                league.transform.Find("LeagueTable/Team_0").GetComponent<Button>().onClick.Invoke();
                var view = shell.GetComponentInChildren<UI_Scene_OwnerTeamLineup>(true);
                string first = view.Snapshot.TeamName;
                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(league.gameObject.activeSelf, Is.False);
                view.transform.Find("CloseLineup").GetComponent<Button>().onClick.Invoke();
                Assert.That(league.gameObject.activeSelf, Is.True);
                league.transform.Find("LeagueTable/Team_1").GetComponent<Button>().onClick.Invoke();
                Assert.That(view.Snapshot.TeamName, Is.Not.EqualTo(first));
                Assert.That(view.GetComponentsInChildren<PlayerMiniCardView>().Length, Is.EqualTo(25));
                Assert.That(view.GetComponentsInChildren<PlayerMiniCardView>().All(card => !card.Model.IsInteractable), Is.True);
                Assert.That(coordinator.TryCloseTeamLineup(), Is.True);
                Assert.That(coordinator.TryCloseTeamLineup(), Is.False);
                coordinator.TryShowRoute(OwnerNavigationRoutes.LeagueStandings);
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void TeamColor_상대구단표시명에서내부FranchiseKey를노출하지않는다()
        {
            const string franchiseId = "FRANCHISE_1b36b987034cef53c24a";
            var definition = new TeamColorDefinition(
                "YearFranchise:2024:" + franchiseId + ":25",
                TeamColorFamily.YearFranchise,
                25,
                TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 1),
                TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 1),
                originYear: 2024,
                originFranchiseId: franchiseId,
                displayName: "2024 " + franchiseId + " · 완성된 연대기");

            string result = OwnerTeamColorDisplayFormatter.FormatWorldName(
                definition,
                definition.DisplayName,
                _ => "인천 타이드",
                null);

            Assert.That(result, Is.EqualTo("2024 인천 타이드 · 완성된 연대기"));
            Assert.That(result, Does.Not.Contain(franchiseId));
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        public void Visual_카드25장을겹침없이출력한다(int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("그래픽 장치가 필요합니다.");
            var root = new GameObject("LineupCanvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("LineupCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            Texture2D texture = null;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.targetTexture = target;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                var view = UI_Scene_OwnerTeamLineup.CreateRuntime((RectTransform)root.transform);
                view.Bind(CreateSnapshot("수원 가디언즈"), true);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
                Canvas.ForceUpdateCanvases();
                var board = view.transform.Find("BoardHost/LineupBoard");
                foreach (string rowName in new[] { "Hitters", "Pitchers" })
                {
                    var cards = board.Find(rowName).GetComponentsInChildren<PlayerMiniCardView>();
                    float previousRight = float.NegativeInfinity;
                    foreach (var card in cards)
                    {
                        var corners = new Vector3[4];
                        ((RectTransform)card.transform).GetWorldCorners(corners);
                        Assert.That(corners[0].x, Is.GreaterThan(previousRight));
                        Assert.That(corners[2].y, Is.GreaterThan(corners[0].y));
                        previousRight = corners[2].x;
                    }
                }
                Assert.That(board.Find("TeamColors/TeamColor_0").GetComponent<RawImage>().texture, Is.Not.Null);
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                string output = Environment.GetEnvironmentVariable("BASEBALL_LINEUP_VISUAL_OUTPUT")
                    ?? Path.GetFullPath("docs/reports/owner-team-lineup");
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "lineup-" + width + ".png"), texture.EncodeToPNG());
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

        private static OwnerTeamLineupSnapshot CreateSnapshot(string team)
        {
            string[] positions = { "포수", "1루수", "2루수", "3루수", "유격수", "좌익수", "지명타자", "우익수", "중견수" };
            string[] names = { "김민준", "이서준", "박도윤", "최지호", "정시우", "강하준", "윤주원", "임지훈", "한현우", "오민재", "서건우", "신우진", "권준서", "황서진" };
            var hitters = Enumerable.Range(0, 14).Select(i => new PlayerMiniCardModel(team + i, names[i],
                (i < 9 ? i + 1 : i - 8) + "번", "24", "C " + (i % 5 + 4), "",
                i < 9 ? positions[i] : "벤치", teamAccentHex: "#B1A858", isInteractable: false)).ToArray();
            var pitchers = Enumerable.Range(0, 11).Select(i => new PlayerMiniCardModel(team + "p" + i, names[13-i],
                i < 5 ? (i + 1) + "선발" : i < 9 ? (i - 4) + "번" : i == 9 ? "셋업" : "마무리",
                "24", "C 7", "", i < 5 ? "선발" : i < 9 ? "중계" : i == 9 ? "셋업" : "마무리",
                teamAccentHex: "#B1A858", isInteractable: false)).ToArray();
            return new OwnerTeamLineupSnapshot(team, "공개 등록 기준 라인업", "편성 비용 137", hitters, pitchers,
                new[] { "팀컬러 적용 없음", "팀컬러 적용 없음" });
        }
    }
}
