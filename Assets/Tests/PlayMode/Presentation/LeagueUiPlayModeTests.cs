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
    /// <summary>실제 Player Loop에서 리그 화면 생성과 홈 왕복 탭 전환을 검증한다.</summary>
    public sealed class LeagueUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator LeagueTab_순위리더보드일정을렌더하고홈과왕복한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 91_001UL);
            var seasonService = new CareerSeasonService(career, configuration.Balance);
            for (int index = 0; index < 8; index++)
                seasonService.AdvanceNextRound();

            CareerManager careerManager = GameManager.EnsureExists()
                .EnsureManager<CareerManager>("CareerManager");
            careerManager.BeginCareer(career, configuration.Balance);
            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            Transform sceneLayer = uiManager.Root.GetLayerRoot(UILayer.Scene);

            UI_Scene_CareerDashboard home = Object.FindFirstObjectByType<UI_Scene_CareerDashboard>(
                FindObjectsInactive.Include);
            if (home == null)
                home = UI_Scene_CareerDashboard.CreateRuntime(sceneLayer);
            UI_Scene_League league = Object.FindFirstObjectByType<UI_Scene_League>(FindObjectsInactive.Include);
            if (league == null)
                league = UI_Scene_League.CreateRuntime(sceneLayer);

            Assert.That(CareerTabNavigation.Show(CareerMainTab.League), Is.True);
            yield return null;

            Assert.That(league.IsVisible, Is.True);
            Assert.That(home.IsVisible, Is.False);
            Assert.That(league.transform.Find("Content/Standings"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/BattingLeaders"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/PitchingLeaders"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/TeamMetrics"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/LeagueFocus"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/Schedule"), Is.Not.Null);
            Assert.That(league.transform.Find("Content/Tabs/Tab_리그/ActiveGlow"), Is.Not.Null);

            AssertLeaguePanelsUseSkinWithinBounds(league.transform);

            Button stolenBasesTab = league.transform
                .Find("Content/BattingLeaders/ContentSafeArea/Category_도루")
                .GetComponent<Button>();
            AssertCategoryButtonHasNoAccentLine(stolenBasesTab);
            stolenBasesTab.onClick.Invoke();
            yield return null;

            LeagueBattingLeaderView stolenBasesLeader = careerManager.LeagueHub
                .GetBattingLeaderboard(LeagueBattingCategory.StolenBases)
                .Leaders[0];
            Text stolenBasesValue = league.transform
                .Find(
                    $"Content/BattingLeaders/ContentSafeArea/" +
                    $"Batter_{stolenBasesLeader.PlayerId}/StolenBases")
                .GetComponent<Text>();
            Assert.That(stolenBasesValue.text, Is.EqualTo(stolenBasesLeader.StolenBases.ToString()));

            Button savesTab = league.transform
                .Find("Content/PitchingLeaders/ContentSafeArea/Category_세이브")
                .GetComponent<Button>();
            AssertCategoryButtonHasNoAccentLine(savesTab);
            savesTab.onClick.Invoke();
            yield return null;

            LeaguePitchingLeaderView savesLeader = careerManager.LeagueHub
                .GetPitchingLeaderboard(LeaguePitchingCategory.Saves)
                .Leaders[0];
            Text savesValue = league.transform
                .Find(
                    $"Content/PitchingLeaders/ContentSafeArea/" +
                    $"Pitcher_{savesLeader.PlayerId}/Saves")
                .GetComponent<Text>();
            Assert.That(savesValue.text, Is.EqualTo(savesLeader.Saves.ToString()));

            Assert.That(CareerTabNavigation.Show(CareerMainTab.Home), Is.True);
            yield return null;
            Assert.That(league.IsVisible, Is.False);
            Assert.That(home.IsVisible, Is.True);
        }

        private static void AssertLeaguePanelsUseSkinWithinBounds(Transform league)
        {
            string[] panelNames =
            {
                "Standings",
                "BattingLeaders",
                "PitchingLeaders",
                "TeamMetrics",
                "LeagueFocus",
                "Schedule"
            };

            for (int index = 0; index < panelNames.Length; index++)
            {
                Transform panel = league.Find("Content/" + panelNames[index]);
                Assert.That(panel, Is.Not.Null, panelNames[index]);
                CareerUiFrame frame = panel.GetComponent<CareerUiFrame>();
                Assert.That(frame, Is.Not.Null, $"{panelNames[index]} 패널에 공통 Frame이 필요합니다.");
                Assert.That(panel.GetComponent<RectMask2D>(), Is.Not.Null,
                    $"{panelNames[index]} 패널은 시각 영역 밖을 그리면 안 됩니다.");
                Assert.That(frame.DecorativeFrame.sprite, Is.Not.Null,
                    $"{panelNames[index]} 패널에 공통 Skin Sprite가 필요합니다.");
                Assert.That(frame.DecorativeFrame.GetComponent<CareerUiVisualElement>().Role,
                    Is.EqualTo(CareerUiVisualRole.DecorativeFrame));
                AssertContained((RectTransform)panel, frame.HeaderRoot, panelNames[index] + " Header");
                AssertContained((RectTransform)panel, frame.ContentSafeArea, panelNames[index] + " Content");
                AssertContained((RectTransform)panel, frame.InteractionRoot, panelNames[index] + " Interaction");
                AssertDirectChildrenContained(frame.HeaderRoot, panelNames[index] + " Header");
                AssertDirectChildrenContained(frame.ContentSafeArea, panelNames[index] + " Content");
                Assert.That(frame.HeaderRoot.Find("HeaderLine"), Is.Null,
                    $"{panelNames[index]} 패널 제목에 파란 강조선이 남으면 안 됩니다.");
            }
        }

        private static void AssertDirectChildrenContained(RectTransform container, string label)
        {
            for (int index = 0; index < container.childCount; index++)
            {
                if (container.GetChild(index) is RectTransform child)
                    AssertContained(container, child, $"{label}/{child.name}");
            }
        }

        private static void AssertCategoryButtonHasNoAccentLine(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.transform.Find("Selected"), Is.Null,
                "선택 버튼에 파란 밑줄이 남으면 안 됩니다.");
            Assert.That(button.GetComponent<Image>().sprite, Is.Not.Null,
                "선택 상태는 공통 버튼 Skin으로 표현해야 합니다.");
            Assert.That(button.GetComponent<CareerUiVisualElement>().Role,
                Is.EqualTo(CareerUiVisualRole.FramedControl));
        }

        private static void AssertContained(RectTransform container, RectTransform child, string label)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            for (int index = 0; index < corners.Length; index++)
            {
                Vector2 localPoint = container.InverseTransformPoint(corners[index]);
                Assert.That(container.rect.Contains(localPoint), Is.True,
                    $"{label}은(는) 패널 영역 안에 있어야 합니다.");
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.HasInstance)
                Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }

        private static CareerState CreateStartedCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("리그 UI 테스트", "대한민국");
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
