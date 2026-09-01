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
    /// <summary>실제 Player Loop에서 선수 상세 구조와 내부 탭 전환을 검증한다.</summary>
    public sealed class PlayerUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerTab_선수상태성장기록을렌더하고내부탭을전환한다()
        {
            yield return null;

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 83_001UL);
            GameManager.EnsureExists()
                .EnsureManager<CareerManager>("CareerManager")
                .BeginCareer(career, configuration.Balance);
            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            Transform sceneLayer = uiManager.Root.GetLayerRoot(UILayer.Scene);

            UI_Scene_CareerDashboard home = Object.FindFirstObjectByType<UI_Scene_CareerDashboard>(
                FindObjectsInactive.Include);
            if (home == null)
                home = UI_Scene_CareerDashboard.CreateRuntime(sceneLayer);
            UI_Scene_Player player = Object.FindFirstObjectByType<UI_Scene_Player>(FindObjectsInactive.Include);
            if (player == null)
                player = UI_Scene_Player.CreateRuntime(sceneLayer);

            Assert.That(CareerTabNavigation.Show(CareerMainTab.Player), Is.True);
            yield return null;

            Assert.That(player.IsVisible, Is.True);
            Assert.That(home.IsVisible, Is.False);
            Assert.That(player.transform.Find("Content/PlayerCard"), Is.Not.Null);
            Assert.That(player.transform.Find("Content/ProfilePage/BasicInfo"), Is.Not.Null);
            Assert.That(player.transform.Find("Content/ProfilePage/KeyAbilities"), Is.Not.Null);
            Assert.That(player.transform.Find("Content/ProfilePage/SeasonRecord"), Is.Not.Null);
            Assert.That(player.transform.Find("Content/ProfilePage/BoardPreview"), Is.Not.Null);
            Assert.That(player.transform.Find("Content/ProfilePage/OwnedSkills"), Is.Not.Null);
            Assert.That(player.transform.Find("Content/ProfilePage/RecentForm"), Is.Not.Null);
            Transform activePlayerTab = player.transform.Find("Content/Tabs/Tab_선수");
            Transform inactiveHomeTab = player.transform.Find("Content/Tabs/Tab_홈");
            Assert.That(activePlayerTab, Is.Not.Null);
            Assert.That(inactiveHomeTab, Is.Not.Null);
            Assert.That(
                activePlayerTab.GetComponent<Image>().color,
                Is.Not.EqualTo(inactiveHomeTab.GetComponent<Image>().color),
                "현재 탭은 비활성 탭과 다른 시각 상태여야 한다.");

            Transform playerCard = player.transform.Find("Content/PlayerCard/Card");
            Assert.That(playerCard, Is.Not.Null);
            Assert.That(playerCard.Find("Front/NeutralFrame"), Is.Not.Null);
            Assert.That(playerCard.Find("Front/TeamColorOverlay"), Is.Not.Null);
            Assert.That(playerCard.Find("Front/SpecialCardOverlay"), Is.Not.Null);
            Assert.That(playerCard.Find("Front/Portrait"), Is.Not.Null);
            Assert.That(playerCard.Find("Front/CommonTopMeta"), Is.Not.Null);
            Assert.That(playerCard.Find("Front/TopTeamEmblem"), Is.Not.Null);
            Assert.That(playerCard.Find("Front/AwardSlot_2"), Is.Not.Null);
            Assert.That(playerCard.Find("Front/AwardMark_2"), Is.Not.Null);
            Assert.That(playerCard.Find("Front/StatLabel_5"), Is.Not.Null,
                "선수 카드에는 여섯 능력치 행이 있어야 한다.");
            Assert.That(playerCard.Find("Back/NeutralFrame"), Is.Not.Null);
            Assert.That(playerCard.Find("Back/TeamColorOverlay"), Is.Not.Null);
            Assert.That(playerCard.Find("Back/SpecialCardOverlay"), Is.Not.Null);

            UIPlayerCard cardView = playerCard.GetComponent<UIPlayerCard>();
            cardView.SetSpecialType(PlayerCardSpecialType.Mvp);
            Assert.That(cardView.SpecialType, Is.EqualTo(PlayerCardSpecialType.Mvp));
            Assert.That(playerCard.Find("Front/SpecialCardOverlay").GetComponent<Image>().enabled, Is.True);
            Assert.That(playerCard.Find("Back/SpecialCardOverlay").GetComponent<Image>().enabled, Is.True);
            cardView.SetSpecialType(PlayerCardSpecialType.GoldenGlove);
            Assert.That(playerCard.Find("Front/CommonTopMeta").GetComponent<Image>().enabled, Is.True);
            cardView.SetSpecialType(PlayerCardSpecialType.None);
            Assert.That(playerCard.Find("Front/SpecialCardOverlay").GetComponent<Image>().enabled, Is.False);
            Assert.That(cardView.IsShowingBack, Is.False);
            playerCard.GetComponent<Button>().onClick.Invoke();
            Assert.That(cardView.IsShowingBack, Is.True);
            Assert.That(playerCard.Find("Front").gameObject.activeSelf, Is.False);
            Assert.That(playerCard.Find("Back").gameObject.activeSelf, Is.True);
            playerCard.GetComponent<Button>().onClick.Invoke();

            RectTransform basicInfoDivider = player.transform
                .Find("Content/ProfilePage/BasicInfo/ColumnDivider")
                .GetComponent<RectTransform>();
            Assert.That(basicInfoDivider.rect.height, Is.GreaterThan(100f));
            RectTransform basicInfoTitleDivider = GetRect(
                player.transform, "Content/ProfilePage/BasicInfo/TitleDivider");
            RectTransform firstBasicInfoLabel = GetRect(
                player.transform, "Content/ProfilePage/BasicInfo/Label_소속");
            Assert.That(GetTop(firstBasicInfoLabel), Is.LessThan(GetBottom(basicInfoTitleDivider)),
                "기본 정보의 첫 행은 제목 구분선을 침범하면 안 된다.");

            RectTransform boardLastRow = player.transform
                .Find("Content/ProfilePage/BoardPreview/Cell_0_3")
                .GetComponent<RectTransform>();
            RectTransform boardButton = player.transform
                .Find("Content/ProfilePage/BoardPreview/OpenBoard")
                .GetComponent<RectTransform>();
            Assert.That(GetTop(boardButton), Is.LessThan(GetBottom(boardLastRow)),
                "성장판 관리 버튼은 4×4 슬롯과 겹치면 안 된다.");

            player.transform.Find("Content/DetailTab_Attributes").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(player.transform.Find("Content/AttributesPage"), Is.Not.Null);

            player.transform.Find("Content/DetailTab_Board").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(player.transform.Find("Content/BoardPage/Board"), Is.Not.Null);
            Assert.That(player.transform.Find("Content/BoardPage/BoardEffects"), Is.Not.Null);

            player.transform.Find("Content/DetailTab_Career").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(player.transform.Find("Content/CareerPage/CareerSummary"), Is.Not.Null);
            Assert.That(player.transform.Find("Content/CareerPage/CareerTotals"), Is.Not.Null);
            RectTransform careerTab = GetRect(player.transform, "Content/DetailTab_Career");
            RectTransform careerPage = GetRect(player.transform, "Content/CareerPage");
            Assert.That(GetRight(careerTab), Is.LessThanOrEqualTo(GetRight(careerPage)),
                "경력 탭은 상세 본문 우측 경계를 넘어가면 안 된다.");

            Assert.That(CareerTabNavigation.Show(CareerMainTab.Home), Is.True);
            yield return null;
            Assert.That(player.IsVisible, Is.False);
            Assert.That(home.IsVisible, Is.True);
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
            flow.SubmitIdentity("선수 UI 테스트", "대한민국");
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

        private static float GetTop(RectTransform rect)
        {
            return rect.anchoredPosition.y + rect.rect.height * 0.5f;
        }

        private static float GetBottom(RectTransform rect)
        {
            return rect.anchoredPosition.y - rect.rect.height * 0.5f;
        }

        private static float GetRight(RectTransform rect)
        {
            return rect.anchoredPosition.x + rect.rect.width * 0.5f;
        }

        private static RectTransform GetRect(Transform root, string path)
        {
            Transform target = root.Find(path);
            Assert.That(target, Is.Not.Null, path);
            return (RectTransform)target;
        }
    }
}
