using System.Collections;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.Career;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baseball.Tests.PlayMode.Presentation
{
    /// <summary>실제 Player Loop에서 성장 화면의 핵심 패널과 공용 탭 전환을 검증한다.</summary>
    public sealed class GrowthUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator GrowthTab_선수성장판상점액션을렌더하고홈과왕복한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 93_001UL);
            CareerManager careerManager = GameManager.EnsureExists()
                .EnsureManager<CareerManager>("CareerManager");
            careerManager.BeginCareer(career, configuration.Balance);
            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            Transform sceneLayer = uiManager.Root.GetLayerRoot(UILayer.Scene);

            UI_Scene_CareerDashboard home = Object.FindFirstObjectByType<UI_Scene_CareerDashboard>(
                FindObjectsInactive.Include);
            if (home == null)
                home = UI_Scene_CareerDashboard.CreateRuntime(sceneLayer);
            UI_Scene_CareerGrowth growth = Object.FindFirstObjectByType<UI_Scene_CareerGrowth>(
                FindObjectsInactive.Include);
            if (growth == null)
                growth = UI_Scene_CareerGrowth.CreateRuntime(sceneLayer);

            Assert.That(CareerTabNavigation.Show(CareerMainTab.Growth), Is.True);
            yield return null;

            Assert.That(growth.IsVisible, Is.True);
            Assert.That(home.IsVisible, Is.False);
            Assert.That(growth.transform.Find("Content/PlayerPanel"), Is.Not.Null);
            Assert.That(growth.transform.Find("Content/SkillBoard"), Is.Not.Null);
            Assert.That(growth.transform.Find("Content/SelectedBlock"), Is.Not.Null);
            Assert.That(growth.transform.Find("Content/BlockShop"), Is.Not.Null);
            Assert.That(growth.transform.Find("Content/OffseasonActions"), Is.Not.Null);
            Assert.That(growth.transform.Find("Content/Tabs/Tab_성장/ActiveGlow"), Is.Not.Null);
            Transform portrait = growth.transform.Find("Content/PlayerPanel/PlayerCard/PlayerPortrait");
            Assert.That(portrait, Is.Not.Null);
            Assert.That(portrait.GetComponent<UnityEngine.UI.Image>().sprite, Is.Not.Null);
            AssertGrowthPlayerCardLayout(growth.transform);
            AssertGrowthBoardHeaderLayout(growth.transform);
            AssertTetrominoCells(
                growth.transform.Find("Content/BlockShop/Shop_Contact"),
                "ShopShapeCell_");

            long moneyBefore = career.AvailableMoney;
            Assert.That(careerManager.PurchaseSkillBlock(Baseball.Core.Growth.SkillBlockCategory.Contact),
                Is.True, careerManager.LastError);
            yield return null;
            Assert.That(careerManager.GrowthDashboard.OwnedBlocks, Has.Length.EqualTo(1));
            Assert.That(career.AvailableMoney,
                Is.EqualTo(moneyBefore - configuration.Balance.Growth.SkillGacha.SinglePrice));
            int instanceId = careerManager.GrowthDashboard.OwnedBlocks[0].InstanceId;
            AssertTetrominoCells(
                growth.transform.Find("Content/SkillBoard/Owned_" + instanceId),
                "ShapeCell_");

            Assert.That(CareerTabNavigation.Show(CareerMainTab.Home), Is.True);
            yield return null;
            Assert.That(growth.IsVisible, Is.False);
            Assert.That(home.IsVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator Home_정규시즌종료후포스트시즌결산오프시즌을연결한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 93_002UL);
            var seasonService = new CareerSeasonService(career, configuration.Balance);
            while (career.League.CurrentSeason.Phase == SeasonPhase.RegularSeason)
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
            UI_Scene_CareerGrowth growth = Object.FindFirstObjectByType<UI_Scene_CareerGrowth>(
                FindObjectsInactive.Include);
            if (growth == null)
                growth = UI_Scene_CareerGrowth.CreateRuntime(sceneLayer);

            Assert.That(CareerTabNavigation.Show(CareerMainTab.Home), Is.True);
            yield return null;

            Assert.That(career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Postseason));
            Transform playerCard = home.transform.Find("Content/PlayerPanel/PlayerCard");
            Transform nameStrip = playerCard.Find("NameStrip");
            Transform position = playerCard.Find("Position");
            Transform playerName = playerCard.Find("PlayerName");
            RectTransform overallArea = GetRect(
                home.transform, "Content/PlayerPanel/PlayerCard/CardGlow");
            RectTransform portraitBackdrop = GetRect(
                home.transform, "Content/PlayerPanel/PlayerCard/PortraitBackdrop");
            Assert.That(playerCard.Find("UniformNumber"), Is.Null);
            Assert.That(GetRight(overallArea), Is.LessThanOrEqualTo(GetLeft(portraitBackdrop)),
                "홈 선수 카드의 OVR 영역은 선수 초상 영역을 침범하면 안 된다.");
            Assert.That(nameStrip.GetSiblingIndex(), Is.LessThan(position.GetSiblingIndex()));
            Assert.That(nameStrip.GetSiblingIndex(), Is.LessThan(playerName.GetSiblingIndex()));

            var roleText = home.transform.Find("Content/CompetitionPanel/RoleBadge/Role")
                .GetComponent<UnityEngine.UI.Text>();
            Assert.That(roleText.text, Is.EqualTo(GetExpectedRoleLabel(career.CurrentContract.ExpectedRole)));

            Transform postseasonButton = home.transform.Find("Content/NextGamePanel/AdvancePostseason");
            Assert.That(postseasonButton, Is.Not.Null);
            postseasonButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.That(career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.SeasonReview));
            Transform settlementButton = home.transform.Find("Content/NextGamePanel/BeginOffseason");
            Assert.That(settlementButton, Is.Not.Null);
            long moneyBeforeSettlement = career.AvailableMoney;
            settlementButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.That(career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Offseason));
            Assert.That(career.AvailableMoney, Is.GreaterThan(moneyBeforeSettlement));
            Transform growthButton = home.transform.Find("Content/NextGamePanel/OpenGrowth");
            Assert.That(growthButton, Is.Not.Null);
            growthButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.That(growth.IsVisible, Is.True);
            Assert.That(home.IsVisible, Is.False);

            int weekBefore = career.CurrentOffseason.CurrentWeek;
            Transform program = growth.transform.Find(
                "Content/OffseasonActions/Program_personal_batting");
            Assert.That(program, Is.Not.Null);
            program.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Transform execute = growth.transform.Find("Content/OffseasonActions/ExecuteActivity");
            Assert.That(execute, Is.Not.Null);
            execute.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.That(career.CurrentOffseason.CurrentWeek, Is.EqualTo(weekBefore + 3));
            Assert.That(careerManager.GrowthDashboard.IsActivityInProgress, Is.False);
            RectTransform growthLogHeader = GetRect(growth.transform, "Content/GrowthLog/Header");
            RectTransform firstGrowthLogRow = GetRect(growth.transform, "Content/GrowthLog/Date_0");
            Assert.That(GetTop(firstGrowthLogRow), Is.LessThan(GetBottom(growthLogHeader)),
                "성장 로그의 첫 행은 패널 헤더를 침범하면 안 된다.");
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
            flow.SubmitIdentity("성장 UI 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }

        private static string GetExpectedRoleLabel(Baseball.Core.Teams.ExpectedRole role)
        {
            return role switch
            {
                Baseball.Core.Teams.ExpectedRole.StartingCompetition => "주전 경쟁",
                Baseball.Core.Teams.ExpectedRole.RosterCompetition => "로스터 경쟁",
                _ => "벤치 경쟁"
            };
        }

        private static void AssertTetrominoCells(Transform parent, string childPrefix)
        {
            Assert.That(parent, Is.Not.Null);
            int count = 0;
            for (int index = 0; index < parent.childCount; index++)
            {
                if (parent.GetChild(index).name.StartsWith(childPrefix, System.StringComparison.Ordinal))
                    count++;
            }
            Assert.That(count, Is.EqualTo(4));
        }

        private static void AssertGrowthPlayerCardLayout(Transform growth)
        {
            RectTransform card = GetRect(growth, "Content/PlayerPanel/PlayerCard");
            RectTransform number = GetRect(growth, "Content/PlayerPanel/PlayerCard/Number");
            RectTransform position = GetRect(growth, "Content/PlayerPanel/PlayerCard/Position");
            RectTransform team = GetRect(growth, "Content/PlayerPanel/PlayerCard/Team");

            Assert.That(GetLeft(number), Is.GreaterThan(GetLeft(card)),
                "등번호는 선수 카드의 좌측 테두리에 붙거나 넘어가면 안 된다.");
            Assert.That(GetRight(position), Is.LessThan(GetLeft(team)),
                "포지션과 구단명은 서로 겹치면 안 된다.");
        }

        private static void AssertGrowthBoardHeaderLayout(Transform growth)
        {
            RectTransform header = GetRect(growth, "Content/SkillBoard/Header");
            RectTransform redesign = GetRect(growth, "Content/SkillBoard/Redesign");

            Assert.That(GetTop(redesign), Is.LessThanOrEqualTo(GetTop(header)));
            Assert.That(GetBottom(redesign), Is.GreaterThanOrEqualTo(GetBottom(header)),
                "안전 회수 버튼은 성장판 헤더의 상하 경계를 넘어가면 안 된다.");
        }

        private static RectTransform GetRect(Transform root, string path)
        {
            Transform target = root.Find(path);
            Assert.That(target, Is.Not.Null, path);
            return (RectTransform)target;
        }

        private static float GetTop(RectTransform rect) => rect.anchoredPosition.y + rect.rect.yMax;
        private static float GetBottom(RectTransform rect) => rect.anchoredPosition.y + rect.rect.yMin;
        private static float GetLeft(RectTransform rect) => rect.anchoredPosition.x + rect.rect.xMin;
        private static float GetRight(RectTransform rect) => rect.anchoredPosition.x + rect.rect.xMax;
    }
}
