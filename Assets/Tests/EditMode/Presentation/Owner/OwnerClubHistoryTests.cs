using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedScreens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>역사의 집계 근거와 시즌 이동·수상 필터·해상도별 가독성을 검증한다.</summary>
    public sealed class OwnerClubHistoryTests
    {
        [Test]
        public void 통산비율은원본합계이며미확정선두는수상이아니다()
        {
            var model = BuildModel();
            var totals = model.BuildTotals(null);
            Assert.That(totals.Rows.First(row => row.RowId == "타율").FindCell("Value").DisplayValue, Is.EqualTo("0.280"));
            Assert.That(model.CountHonors(null, 0), Is.EqualTo(2));
            Assert.That(model.CountHonors(LeagueGrade.Major, 0), Is.Zero);
            Assert.That(model.CountHonors(null, 1), Is.EqualTo(1));
            Assert.That(model.CountHonors(null, 2), Is.EqualTo(1));
            Assert.That(model.BuildSeasons(null, 1).Rows.Count, Is.EqualTo(1));
            Assert.That(model.BuildSeasons(null, -2).Rows.Count, Is.EqualTo(2));
            Assert.That(model.BuildTotals(LeagueGrade.Galaxy).Rows, Is.Empty);
            Assert.That(model.BuildBest(null).Rows.Count, Is.GreaterThan(0));
        }

        [Test]
        public void 기록행에서시즌상세와선수기록으로연결하고돌아온다()
        {
            var host = new GameObject("History", typeof(RectTransform));
            try
            {
                host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 840);
                var view = UI_Scene_OwnerClubHistory.CreateRuntime(host.transform); view.Bind(BuildModel());
                var table = view.GetComponentInChildren<RecordTableView>();
                table.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Row_", StringComparison.Ordinal)).ElementAt(1).onClick.Invoke();
                int season = 0; view.SeasonPlayersRequested += number => season = number;
                view.transform.Find("SeasonPlayers").GetComponent<Button>().onClick.Invoke();
                Assert.That(season, Is.EqualTo(2)); Assert.That(view.TryGoBack(), Is.True);
                Assert.That(table.Model.Rows.Count, Is.EqualTo(3)); Assert.That(view.TryGoBack(), Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void 공동최고와규정미달을구분한다()
        {
            var season = CreateSeason(1, true, 100, 30);
            var same = season.Statistics.RegularSeason.GetOrCreate(12, "공동기록선수", 1, PlayerPosition.FirstBase);
            Set(same.Batting, "AtBats", 100); Set(same.Batting, "PlateAppearances", 110); Set(same.Batting, "Hits", 30);
            var shortSeason = season.Statistics.RegularSeason.GetOrCreate(13, "규정미달", 1, PlayerPosition.FirstBase);
            Set(shortSeason.Batting, "AtBats", 1); Set(shortSeason.Batting, "PlateAppearances", 1); Set(shortSeason.Batting, "Hits", 1);
            var factory = new OwnerSharedInformationSnapshotFactory();
            var entry = factory.CreateHistorySeason(season, LeagueGrade.Rookie, "내 구단", null, false);
            var model = new OwnerClubHistoryPresentationModel("내 구단", new[] { entry },
                OwnerSharedInformationSnapshotFactory.CreateHistoryColumns());
            var rows = model.BuildBest(null).Rows.Where(row => row.RowId.Contains(":BattingAverage:"));
            Assert.That(rows.Count(), Is.EqualTo(2));
            Assert.That(rows.Any(row => row.FindCell("Player").DisplayValue == "규정미달"), Is.False);
        }

        private static IEnumerable<TestCaseData> VisualCases()
        {
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3440, 1440) })
                for (int tab = 0; tab < 8; tab++) yield return new TestCaseData(size.x, size.y, tab);
        }

        [Test]
        public void 트로피선택은달성시즌을좁히고다시누르면전체수상으로돌아온다()
        {
            var host = new GameObject("History", typeof(RectTransform));
            try
            {
                host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 840);
                var view = UI_Scene_OwnerClubHistory.CreateRuntime(host.transform);
                view.Bind(BuildModel());
                view.transform.Find("Tab3").GetComponent<Button>().onClick.Invoke();
                var table = view.GetComponentInChildren<RecordTableView>();
                Assert.That(table.Model.Columns.Select(column => column.ColumnId),
                    Is.EquivalentTo(new[] { "Season", "League", "Pennant", "Postseason" }));
                var champion = view.transform.Find("TrophyRoom/Honor1").GetComponent<Button>();
                champion.onClick.Invoke();
                Assert.That(table.Model.Rows.Count, Is.EqualTo(1));
                Assert.That(champion.transform.Find("ContentSafeRect/SelectedIndicator").gameObject.activeSelf, Is.True);
                champion.onClick.Invoke();
                Assert.That(table.Model.Rows.Count, Is.EqualTo(2));
                view.transform.Find("Grade10").GetComponent<Button>().onClick.Invoke();
                Assert.That(table.Model.Rows, Is.Empty);
                Assert.That(champion.transform.Find("ContentSafeRect/SelectionHint").GetComponent<Text>().text,
                    Is.EqualTo("아직 획득하지 못한 타이틀"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCaseSource(nameof(VisualCases))]
        public void 기록실을해상도별로렌더링한다(int width, int height, int tab)
        {
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24); Texture2D texture = null;
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white; camera.targetTexture = target;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var workspace = new GameObject("Workspace", typeof(RectTransform)).GetComponent<RectTransform>(); workspace.SetParent(root.transform, false);
                workspace.anchorMin = new Vector2(.015f, .08f); workspace.anchorMax = new Vector2(.985f, .86f); workspace.offsetMin = workspace.offsetMax = Vector2.zero;
                var view = UI_Scene_OwnerClubHistory.CreateRuntime(workspace); view.Bind(BuildModel());
                view.transform.Find("Tab" + (tab < 4 ? tab : tab >= 6 ? 3 : 0)).GetComponent<Button>().onClick.Invoke();
                if (tab == 4)
                    view.GetComponentInChildren<RecordTableView>().GetComponentsInChildren<Button>()
                        .First(button => button.name.StartsWith("Row_", StringComparison.Ordinal)).onClick.Invoke();
                if (tab == 5) view.transform.Find("Grade10").GetComponent<Button>().onClick.Invoke();
                if (tab == 6) view.transform.Find("TrophyRoom/Honor1").GetComponent<Button>().onClick.Invoke();
                if (tab == 7) view.transform.Find("Grade10").GetComponent<Button>().onClick.Invoke();
                Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform); Canvas.ForceUpdateCanvases();
                Assert.That(view.GetComponentInChildren<RecordTableView>().VisualStyle, Is.EqualTo(RecordTableVisualStyle.ReferenceLight));
                if (tab == 3) Assert.That(view.GetComponentInChildren<RawImage>().texture, Is.Not.Null);
                if (tab == 3 || tab >= 6)
                {
                    foreach (Text text in view.transform.Find("TrophyRoom").GetComponentsInChildren<Text>())
                    {
                        Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), text.name);
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name);
                        RectTransform safe = text.transform.parent as RectTransform;
                        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safe, text.rectTransform);
                        Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(safe.rect.xMin - 1), text.name);
                        Assert.That(bounds.max.x, Is.LessThanOrEqualTo(safe.rect.xMax + 1), text.name);
                        Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(safe.rect.yMin - 1), text.name);
                        Assert.That(bounds.max.y, Is.LessThanOrEqualTo(safe.rect.yMax + 1), text.name);
                    }
                    foreach (RawImage art in view.transform.Find("TrophyRoom").GetComponentsInChildren<RawImage>())
                    {
                        var host = (RectTransform)art.transform.parent;
                        Assert.That(art.rectTransform.rect.width, Is.LessThanOrEqualTo(host.rect.width + 1));
                        Assert.That(art.rectTransform.rect.height, Is.LessThanOrEqualTo(host.rect.height + 1));
                    }
                }
                foreach (Button button in view.GetComponentsInChildren<Button>())
                {
                    if (!button.name.StartsWith("Tab") && !button.name.StartsWith("Grade") && !button.name.StartsWith("Honor")) continue;
                    var text = button.GetComponentInChildren<Text>();
                    Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), button.name);
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), button.name);
                }
                camera.Render(); foreach (Text text in view.GetComponentsInChildren<Text>()) text.SetVerticesDirty();
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                string output = Environment.GetEnvironmentVariable("BASEBALL_HISTORY_VISUAL_OUTPUT");
                if (!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, $"history-{tab}-{width}x{height}.png"), texture.EncodeToPNG()); }
            }
            finally
            {
                RenderTexture.active = previous; if (texture != null) Object.DestroyImmediate(texture);
                cameraObject.GetComponent<Camera>().targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root);
            }
        }

        private static OwnerClubHistoryPresentationModel BuildModel()
        {
            var factory = new OwnerSharedInformationSnapshotFactory();
            var a = CreateSeason(1, true, 100, 40); var b = CreateSeason(2, true, 300, 60); var c = CreateSeason(3, false, 100, 40);
            var entries = new[] {
                factory.CreateHistorySeason(a, LeagueGrade.Rookie, "부산 블루웨이브 베이스볼 클럽", Postseason(a, true), false),
                factory.CreateHistorySeason(b, LeagueGrade.Minor, "부산 블루웨이브 베이스볼 클럽", Postseason(b, false), false),
                factory.CreateHistorySeason(c, LeagueGrade.Major, "부산 블루웨이브 베이스볼 클럽", null, true) };
            return new OwnerClubHistoryPresentationModel("부산 블루웨이브 베이스볼 클럽", entries,
                OwnerSharedInformationSnapshotFactory.CreateHistoryColumns());
        }

        private static ManagerLiveSeasonState CreateSeason(int number, bool completed, int atBats, int hits)
        {
            var first = new ScheduledGameState(1, 1, 10, 2, 1); first.Complete(1, 5);
            var last = new ScheduledGameState(2, 2, 11, 1, 2); if (completed) last.Complete(4, 1);
            var season = new ManagerLiveSeasonState("season-" + number, number, 2025 + number, 0, 1,
                new[] { new ManagerTeamReference(1, "owner"), new ManagerTeamReference(2, "opponent") }, new SeasonScheduleState(new[] { first, last }));
            var player = season.Statistics.RegularSeason.GetOrCreate(11, "김블루웨이브", 1, PlayerPosition.FirstBase);
            Set(player.Batting, "AtBats", atBats); Set(player.Batting, "PlateAppearances", atBats + 10); Set(player.Batting, "Hits", hits);
            Set(player.Batting, "HomeRuns", 10); Set(player.Batting, "RunsBattedIn", 33);
            Set(player.Pitching, "OutsRecorded", 55); Set(player.Pitching, "EarnedRuns", 3); Set(player.Pitching, "Strikeouts", 18);
            return season;
        }
        private static OwnerPostseasonState Postseason(ManagerLiveSeasonState season, bool champion)
        {
            var games = new ScheduledGameState[4];
            for (int i = 0; i < games.Length; i++)
            {
                games[i] = new ScheduledGameState(i + 10, i + 1, (ulong)i + 10, 2, 1);
                games[i].Complete(champion ? 1 : 4, champion ? 4 : 1);
            }
            return new OwnerPostseasonState(season.SeasonId, new[] { 1, 2 }, new[] {
                new OwnerPostseasonSeriesState("final", OwnerPostseasonRound.Championship, 1, 2, 7, games,
                    higherSeedWins: champion ? 4 : 0, lowerSeedWins: champion ? 0 : 4) });
        }
        private static void Set(object target, string property, int value) => target.GetType().GetProperty(property).SetValue(target, value);
    }
}
