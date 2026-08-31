using System.Collections;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.Career;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baseball.Tests.PlayMode.Presentation
{
    /// <summary>런타임에 뒤늦게 생성된 UI가 공통 스킨을 자동으로 이어받는지 검증한다.</summary>
    public sealed class CareerUiSkinPlayModeTests
    {
        [UnityTest]
        public IEnumerator DynamicContent_다음Frame에공통ButtonSkin을적용한다()
        {
            var root = new GameObject("CareerUiSkinPlayModeTests_Root", typeof(RectTransform));
            var contentObject = new GameObject("DynamicContent", typeof(RectTransform));
            contentObject.transform.SetParent(root.transform, false);
            CareerUiSkin.Apply(root.transform);

            var buttonObject = new GameObject(
                "MatchProgress",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(contentObject.transform, false);
            buttonObject.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 86f);
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            Text label = labelObject.GetComponent<Text>();
            label.fontSize = 20;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            yield return null;

            Button button = buttonObject.GetComponent<Button>();
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
            Assert.That(button.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(button.GetComponent<CareerUiShine>(), Is.Not.Null);
            Assert.That(button.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(label.resizeTextForBestFit, Is.True);

            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator Dashboard_주요콘텐츠를안전영역에배치하고공통Frame을적용한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 92_101UL);
            GameManager.EnsureExists()
                .EnsureManager<CareerManager>("CareerManager")
                .BeginCareer(career, configuration.Balance);
            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            Transform sceneLayer = uiManager.Root.GetLayerRoot(UILayer.Scene);
            UI_Scene_CareerDashboard dashboard = UI_Scene_CareerDashboard.CreateRuntime(sceneLayer);

            dashboard.Show();
            yield return null;
            Canvas.ForceUpdateCanvases();

            const string root = "Content/DashboardContentSafeArea/";
            RectTransform competitionSafeArea = GetRect(
                dashboard.transform, root + "BottomRow/CompetitionPanel/ContentSafeArea");
            RectTransform roleBadge = GetRect(
                dashboard.transform, root + "BottomRow/CompetitionPanel/ContentSafeArea/RoleBadge");
            RectTransform competitionList = GetRect(
                dashboard.transform, root + "BottomRow/CompetitionPanel/ContentSafeArea/CompetitionList");
            AssertContained(competitionSafeArea, roleBadge, "현재 역할 영역");
            AssertContained(competitionSafeArea, competitionList, "포지션 경쟁 목록");
            Assert.That(GetTop(competitionList), Is.LessThan(GetBottom(roleBadge)),
                "현재 역할과 경쟁 목록은 서로 겹치면 안 됩니다.");
            RectTransform firstCompetitor = GetRect(
                competitionList, "Viewport/Content/Competitor_0");
            RectTransform competitorOverall = GetRect(firstCompetitor, "Overall");
            AssertContained(firstCompetitor, competitorOverall, "경쟁 선수 OVR");
            Assert.That(
                GetRight(competitorOverall),
                Is.LessThanOrEqualTo(firstCompetitor.rect.xMax - CareerUiTheme.Space4),
                "경쟁 선수 OVR은 오른쪽 Frame 안전 여백을 침범하면 안 됩니다.");

            RectTransform upcomingSafeArea = GetRect(
                dashboard.transform, root + "BottomRow/UpcomingPanel/ContentSafeArea");
            RectTransform upcomingList = GetRect(
                dashboard.transform, root + "BottomRow/UpcomingPanel/ContentSafeArea/UpcomingList");
            RectTransform upcomingMore = GetRect(
                dashboard.transform, root + "BottomRow/UpcomingPanel/ContentSafeArea/More");
            AssertContained(upcomingSafeArea, upcomingList, "예정 경기 목록");
            AssertContained(upcomingSafeArea, upcomingMore, "예정 경기 보조 문구");
            Assert.That(GetTop(upcomingMore), Is.LessThan(GetBottom(upcomingList)),
                "예정 경기 목록과 보조 문구는 서로 겹치면 안 됩니다.");

            RectTransform seasonSafeArea = GetRect(
                dashboard.transform, root + "TopRow/SeasonPanel/ContentSafeArea");
            RectTransform rankTile = GetRect(seasonSafeArea, "RankTile");
            RectTransform recordTile = GetRect(seasonSafeArea, "RecordTile");
            RectTransform statistics = GetRect(seasonSafeArea, "StatisticsSection");
            RectTransform recent = GetRect(seasonSafeArea, "RecentSection");
            AssertContained(seasonSafeArea, rankTile, "팀 순위 타일");
            AssertContained(seasonSafeArea, recordTile, "팀 성적 타일");
            AssertContained(seasonSafeArea, statistics, "선수 시즌 성적");
            AssertContained(seasonSafeArea, recent, "최근 5경기");
            Assert.That(GetTop(statistics), Is.LessThan(GetBottom(rankTile)),
                "선수 시즌 성적은 팀 순위 타일과 겹치면 안 됩니다.");
            Assert.That(GetTop(statistics), Is.LessThan(GetBottom(recordTile)),
                "선수 시즌 성적은 팀 성적 타일과 겹치면 안 됩니다.");
            Assert.That(GetTop(recent), Is.LessThan(GetBottom(statistics)),
                "최근 5경기는 선수 시즌 성적과 겹치면 안 됩니다.");

            Transform recentFrame = recent.Find("FramedSurface");
            Assert.That(recentFrame, Is.Not.Null, "최근 5경기 영역에는 공통 Frame이 있어야 합니다.");
            Assert.That(recentFrame.GetComponent<CareerUiVisualElement>().Role,
                Is.EqualTo(CareerUiVisualRole.FramedSurface));

            RectTransform sampleSize = GetRect(statistics, "SampleSize");
            Assert.That(GetRight(sampleSize), Is.LessThanOrEqualTo(statistics.rect.xMax - 24f),
                "시즌 표본 문구는 우측 Frame 안전 여백을 침범하면 안 됩니다.");

            RectTransform playerSafeArea = GetRect(
                dashboard.transform, root + "TopRow/PlayerPanel/ContentSafeArea");
            RectTransform statusSummary = GetRect(playerSafeArea, "PlayerStatusSummary");
            AssertContained(playerSafeArea, statusSummary, "선수 상태 요약");
            RectTransform condition = GetRect(statusSummary, "Condition");
            RectTransform evaluation = GetRect(statusSummary, "Evaluation");
            AssertContained(statusSummary, condition, "컨디션 카드");
            AssertContained(statusSummary, evaluation, "감독 평가 카드");
            Assert.That(GetRight(condition), Is.LessThan(GetLeft(evaluation)),
                "컨디션과 감독 평가 카드는 서로 겹치면 안 됩니다.");
            AssertStatusMetricSkinned(condition, "컨디션");
            AssertStatusMetricSkinned(evaluation, "감독 평가");
        }

        private static void AssertStatusMetricSkinned(RectTransform metric, string label)
        {
            RectTransform frame = GetRect(metric, "FramedSurface");
            CareerUiVisualElement visual = frame.GetComponent<CareerUiVisualElement>();
            Assert.That(visual, Is.Not.Null, $"{label} 카드에 시각 역할이 지정되어야 합니다.");
            Assert.That(visual.Role, Is.EqualTo(CareerUiVisualRole.FramedSurface));

            RectTransform track = GetRect(metric, "Track");
            RectTransform fill = GetRect(track, "Fill");
            AssertContained(metric, GetRect(metric, "Label"), $"{label} 라벨");
            AssertContained(metric, GetRect(metric, "Value"), $"{label} 값");
            AssertContained(metric, track, $"{label} 진행 바");
            Assert.That(track.GetComponent<Image>().sprite, Is.Not.Null,
                $"{label} 진행 바 Track에 공통 스킨이 적용되어야 합니다.");
            Assert.That(fill.GetComponent<Image>().sprite, Is.Not.Null,
                $"{label} 진행 바 Fill에 공통 스킨이 적용되어야 합니다.");
        }

        private static void AssertContained(RectTransform container, RectTransform child, string label)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            for (int index = 0; index < corners.Length; index++)
            {
                Vector2 localPoint = container.InverseTransformPoint(corners[index]);
                Assert.That(container.rect.Contains(localPoint), Is.True,
                    $"{label}은(는) ContentSafeArea 안에 있어야 합니다.");
            }
        }

        private static RectTransform GetRect(Transform root, string path)
        {
            Transform target = root.Find(path);
            Assert.That(target, Is.Not.Null, path);
            return (RectTransform)target;
        }

        private static float GetTop(RectTransform rect)
        {
            return rect.anchoredPosition.y + rect.rect.yMax;
        }

        private static float GetBottom(RectTransform rect)
        {
            return rect.anchoredPosition.y + rect.rect.yMin;
        }

        private static float GetRight(RectTransform rect)
        {
            return rect.anchoredPosition.x + rect.rect.xMax;
        }

        private static float GetLeft(RectTransform rect)
        {
            return rect.anchoredPosition.x + rect.rect.xMin;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.HasInstance)
                Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }

        private static CareerState CreateStartedCareer(
            NewGameConfiguration configuration,
            ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("홈 UI 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(63, 58, 60, 53, 66, 60));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }
    }
}
