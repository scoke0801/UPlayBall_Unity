using System.Collections;
using Baseball.Core.Growth;
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
            Assert.That(growth.transform.Find("Content/PlayerSummary"), Is.Not.Null);
            Assert.That(growth.transform.Find("Content/DraftBoard"), Is.Not.Null);
            Assert.That(growth.transform.Find("Content/BlockInventory"), Is.Not.Null);
            Assert.That(growth.transform.Find("Content/GrowthSubNavigation/OpenGachaOverlay"), Is.Not.Null);
            Assert.That(growth.transform.Find("Content/OffseasonActionWorkspace"), Is.Null,
                "오프시즌 액션은 성장 보드와 동시에 노출되면 안 됩니다.");
            Assert.That(growth.transform.Find("Content/Tabs/Tab_성장/ActiveGlow"), Is.Not.Null);
            growth.transform.Find("Content/GrowthSubNavigation/OpenGachaOverlay")
                .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Transform overlay = growth.transform.Find("Content/GrowthGachaOverlay");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.Find("GachaTier_Normal"), Is.Not.Null);
            Assert.That(overlay.Find("GachaTier_Rare"), Is.Not.Null);
            Assert.That(overlay.Find("GachaTier_Elite"), Is.Not.Null);
            Assert.That(overlay.Find("GachaTier_Unique"), Is.Not.Null);
            Assert.That(overlay.Find("GachaTier_Legendary"), Is.Not.Null);
            Assert.That(careerManager.GrowthDashboard.GachaOffers, Has.Length.EqualTo(5));
            AssertGachaOverlayLayout(overlay);

            long moneyBefore = career.AvailableMoney;
            UnityEngine.UI.Button buyStandard = overlay
                .Find("GachaPayment/GachaBuyOne")
                .GetComponent<UnityEngine.UI.Button>();
            buyStandard.onClick.Invoke();
            yield return null;
            Assert.That(careerManager.GrowthDashboard.OwnedBlocks, Has.Length.EqualTo(1));
            Assert.That(career.AvailableMoney,
                Is.EqualTo(moneyBefore - configuration.Balance.Growth.SkillGacha.SinglePrice));
            Assert.That(career.Economy.Transactions[^1].Amount,
                Is.EqualTo(-configuration.Balance.Growth.SkillGacha.SinglePrice));
            Assert.That(
                growth.transform.Find("Content/TopBar/MONEYSegment/Value")
                    .GetComponent<UnityEngine.UI.Text>().text,
                Is.EqualTo(FormatMoney(career.AvailableMoney)));
            Assert.That(growth.transform.Find("Content/GrowthGachaOverlay"), Is.Null);
            Assert.That(growth.transform.Find("Content/BlockInventory/SelectedBlockDetail/Name"), Is.Not.Null,
                "구매 직후 새 블록이 편집 대상으로 선택되어야 합니다.");
            Assert.That(careerManager.GrowthDashboard.CanEditBoard, Is.False,
                "정규 시즌에는 성장판을 열람할 수 있지만 배치와 회전은 잠겨야 합니다.");
            Transform selectedDetail = growth.transform.Find(
                "Content/BlockInventory/SelectedBlockDetail");
            AssertTetrominoCells(selectedDetail, "SelectedShapeCell_");
            AssertSelectedBlockDetailLayout(selectedDetail);
            Assert.That(
                growth.transform.Find("Content/BlockInventory/SelectedBlockDetail/RotateSelectedBlock")
                    .GetComponent<UnityEngine.UI.Button>().interactable,
                Is.EqualTo(
                    careerManager.GrowthDashboard.CanEditBoard &&
                    careerManager.GrowthDashboard.OwnedBlocks[0].CanRotate),
                "회전 가능 형태라도 정규 시즌 열람 모드에서는 회전 입력이 잠겨야 합니다.");

            growth.transform.Find("Content/GrowthSubNavigation/OffseasonActionsTab")
                .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Transform offseasonActions = growth.transform.Find("Content/OffseasonActionWorkspace");
            Assert.That(offseasonActions, Is.Not.Null);
            AssertOffseasonActionHeaderLayout(offseasonActions);
            Assert.That(growth.transform.Find("Content/DraftBoard"), Is.Null);

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
            while (career.CurrentLeague.CurrentSeason.Phase == SeasonPhase.RegularSeason)
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
            home = FindVisibleDashboard();
            Assert.That(home, Is.Not.Null);

            if (careerManager.Dashboard.PendingReaction != null)
            {
                Transform reactionPanel = FindDescendant(home.transform, "ReactionPanel");
                Assert.That(reactionPanel, Is.Not.Null,
                    "시즌 종료 경기에서 발생한 커리어 반응은 시즌 리뷰보다 먼저 해결해야 합니다.");
                Transform reactionOption = FindDescendant(reactionPanel, "ReactionOption_0");
                Assert.That(reactionOption, Is.Not.Null);
                reactionOption.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
            }

            SeasonState season = career.CurrentLeague.CurrentSeason;
            Assert.That(season.Phase, Is.EqualTo(SeasonPhase.Postseason));
            Assert.That(season.Review.Step, Is.EqualTo(SeasonReviewStep.RegularSeasonIntro));
            Transform seasonReviewRoot = FindDescendant(home.transform, "SeasonReviewRoot");
            Assert.That(
                seasonReviewRoot, Is.Not.Null,
                "정규시즌 종료 직후에는 홈 패널보다 시즌 리뷰가 먼저 표시되어야 합니다.");

            for (int step = 0; step < 3; step++)
            {
                Transform advanceReview = FindDescendant(home.transform, "AdvanceSeasonReview");
                Assert.That(advanceReview, Is.Not.Null,
                    $"포스트시즌 진입 전 시즌 리뷰 {step + 1}단계 진행 버튼이 필요합니다.");
                advanceReview.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
            }

            Assert.That(season.Review.Step, Is.EqualTo(SeasonReviewStep.PostseasonInProgress));
            Transform playerCard = home.transform.Find("Content/PlayerPanel/PlayerCard");
            Assert.That(playerCard, Is.Not.Null,
                "시즌 리뷰를 확인한 뒤 포스트시즌 진행 화면으로 돌아와야 합니다.");
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

            Transform postseasonButton = FindDescendant(home.transform, "AutoCompletePostseason");
            Assert.That(postseasonButton, Is.Not.Null);
            postseasonButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Transform confirmPostseason = FindDescendant(home.transform, "Confirm");
            Assert.That(confirmPostseason, Is.Not.Null);
            confirmPostseason.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.That(season.Phase, Is.EqualTo(SeasonPhase.SeasonReview));
            Assert.That(season.Review.Step, Is.EqualTo(SeasonReviewStep.PostseasonRecap));
            long moneyBeforeSettlement = career.AvailableMoney;

            int reviewAdvanceCount = 0;
            while (season.Review.Step != SeasonReviewStep.Finished && reviewAdvanceCount < 64)
            {
                Transform advanceReview = FindDescendant(home.transform, "AdvanceSeasonReview");
                Assert.That(advanceReview, Is.Not.Null,
                    $"시즌 리뷰 {season.Review.Step} 단계 진행 버튼이 필요합니다.");
                advanceReview.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                reviewAdvanceCount++;
                yield return null;
            }

            Assert.That(reviewAdvanceCount, Is.LessThan(64), "시즌 리뷰 진행이 완료되지 않았습니다.");
            Assert.That(season.Phase, Is.EqualTo(SeasonPhase.Offseason));
            Assert.That(season.Review.Step, Is.EqualTo(SeasonReviewStep.Finished));
            Assert.That(career.AvailableMoney, Is.GreaterThan(moneyBeforeSettlement));
            Transform growthButton = home.transform.Find("Content/NextGamePanel/OpenGrowth");
            Assert.That(growthButton, Is.Not.Null);
            growthButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.That(growth.IsVisible, Is.True);
            Assert.That(home.IsVisible, Is.False);

            growth.transform.Find("Content/GrowthSubNavigation/OpenGachaOverlay")
                .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            growth.transform.Find("Content/GrowthGachaOverlay/GachaPayment/GachaBuyOne")
                .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Transform draftCell = growth.transform.Find("Content/DraftBoard/BoardGrid/DraftCell_0_0");
            Assert.That(draftCell, Is.Not.Null);
            draftCell.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.That(careerManager.GrowthDashboard.PlacedBlocks, Is.Empty,
                "임시 배치는 변경 적용 전까지 실제 보드를 바꾸면 안 됩니다.");
            Transform applyBoard = growth.transform.Find("Content/DraftBoard/ApplyBoardDraft");
            applyBoard.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            applyBoard = growth.transform.Find("Content/DraftBoard/ApplyBoardDraft");
            applyBoard.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.That(careerManager.GrowthDashboard.PlacedBlocks, Has.Length.EqualTo(1));

            growth.transform.Find("Content/GrowthSubNavigation/OffseasonActionsTab")
                .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            int weekBefore = career.CurrentOffseason.CurrentWeek;
            Transform program = growth.transform.Find(
                "Content/OffseasonActionWorkspace/WorkspaceProgram_personal_batting");
            Assert.That(program, Is.Not.Null);
            program = growth.transform.Find(
                "Content/OffseasonActionWorkspace/WorkspaceProgram_personal_batting");
            program.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            UI_Popup_GrowthActivityConfirmation popup =
                Object.FindFirstObjectByType<UI_Popup_GrowthActivityConfirmation>(
                    FindObjectsInactive.Include);
            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.IsVisible, Is.True);
            Assert.That(popup.transform.Find("Content/Summary_Time"), Is.Not.Null);
            Assert.That(popup.transform.Find("Content/Summary_Cost"), Is.Not.Null);
            Assert.That(popup.transform.Find("Content/Summary_Condition"), Is.Not.Null);
            Assert.That(popup.transform.Find("Content/Summary_Completion"), Is.Not.Null);
            Assert.That(FindDescendant(popup.transform, "Program_bat_power_camp"), Is.Not.Null,
                "스크롤 Viewport 내부에도 같은 프로그램 선택 항목이 유지되어야 합니다.");
            Assert.That(popup.transform.Find("Content/Details/Intensity_Standard"), Is.Not.Null);
            Assert.That(popup.transform.Find("Content/Timeline/Week_12"), Is.Not.Null);

            Transform confirm = popup.transform.Find("Content/Footer/Confirm");
            Assert.That(confirm, Is.Not.Null);
            confirm.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.That(career.CurrentOffseason.CurrentWeek, Is.EqualTo(weekBefore + 3));
            Assert.That(careerManager.GrowthDashboard.IsActivityInProgress, Is.False);
            Assert.That(popup.IsVisible, Is.False);

            program = growth.transform.Find(
                "Content/OffseasonActionWorkspace/WorkspaceProgram_personal_batting");
            Assert.That(program, Is.Not.Null,
                "활동 실행으로 성장 화면이 다시 그려진 뒤 현재 프로그램 버튼을 다시 찾아야 합니다.");
            program.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Transform addToPlan = popup.transform.Find("Content/Footer/Plan");
            Assert.That(addToPlan, Is.Not.Null);
            addToPlan.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.That(careerManager.GrowthDashboard.PlannedActivities, Has.Length.EqualTo(1));
            Assert.That(career.CurrentOffseason.CurrentWeek, Is.EqualTo(weekBefore + 3));

            Transform executePlan = FindDescendant(growth.transform, "ExecuteWorkspaceActivity");
            Assert.That(executePlan, Is.Not.Null);
            var executePlanButton = executePlan.GetComponent<UnityEngine.UI.Button>();
            Assert.That(executePlanButton.interactable, Is.True,
                "유효한 성장 계획은 오프시즌 액션 화면에서 실행할 수 있어야 합니다.");
            executePlanButton.onClick.Invoke();
            yield return null;

            Assert.That(career.CurrentOffseason.CurrentWeek, Is.EqualTo(weekBefore + 6));
            Assert.That(careerManager.GrowthDashboard.PlannedActivities, Is.Empty);
            Assert.That(career.MyPlayer.StudyState.StudyUsedThisOffseason, Is.False);
            Assert.That(career.CurrentOffseason.Activities[0].Status,
                Is.EqualTo(OffseasonActivityStatus.Completed));
            Assert.That(career.CurrentOffseason.Activities[1].Status,
                Is.EqualTo(OffseasonActivityStatus.Completed));
            Assert.That(
                growth.transform.Find("Content/PlayerSummary/LatestGrowth/Value"),
                Is.Not.Null,
                "보드 편집 중에는 최근 성장 한 건만 선수 요약에 노출합니다.");
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
            flow.SubmitBatterAttributes(new BatterAttributes(63, 58, 60, 53, 66, 60));
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

        private static UI_Scene_CareerDashboard FindVisibleDashboard()
        {
            UI_Scene_CareerDashboard[] screens = Object.FindObjectsByType<UI_Scene_CareerDashboard>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < screens.Length; index++)
            {
                if (screens[index].IsVisible)
                    return screens[index];
            }

            return null;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (descendants[index].name == name)
                    return descendants[index];
            }

            return null;
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

        private static void AssertGachaOverlayLayout(Transform overlay)
        {
            RectTransform selection = GetRect(overlay, "GachaSelection");
            RectTransform payment = GetRect(overlay, "GachaPayment");
            string[] tierNames =
            {
                "GachaTier_Normal",
                "GachaTier_Rare",
                "GachaTier_Elite",
                "GachaTier_Unique",
                "GachaTier_Legendary"
            };

            for (int index = 0; index < tierNames.Length; index++)
            {
                RectTransform tier = GetRect(overlay, tierNames[index]);
                Assert.That(GetBottom(tier), Is.GreaterThan(GetTop(selection)),
                    tierNames[index] + " 카드와 선택 정보 패널은 겹치면 안 된다.");
            }

            Assert.That(GetBottom(selection), Is.GreaterThan(GetTop(payment)),
                "선택 정보와 결제 패널은 겹치면 안 된다.");
            RectTransform one = GetRect(payment, "GachaBuyOne");
            RectTransform five = GetRect(payment, "GachaBuyFive");
            Assert.That(GetRight(one), Is.LessThan(GetLeft(five)),
                "1회와 5회 구매 버튼은 겹치면 안 된다.");
            Assert.That(GetRight(five), Is.LessThanOrEqualTo(payment.rect.xMax),
                "5회 구매 버튼은 결제 패널 오른쪽 경계를 넘으면 안 된다.");
        }

        private static void AssertSelectedBlockDetailLayout(Transform detail)
        {
            RectTransform name = GetRect(detail, "Name");
            RectTransform info = GetRect(detail, "Info");
            float shapeRight = float.MinValue;
            for (int index = 0; index < detail.childCount; index++)
            {
                Transform child = detail.GetChild(index);
                if (!child.name.StartsWith("SelectedShapeCell_", System.StringComparison.Ordinal))
                    continue;
                shapeRight = Mathf.Max(shapeRight, GetRight((RectTransform)child));
            }

            Assert.That(shapeRight, Is.LessThan(GetLeft(name)),
                "선택 블록 형태와 이름은 겹치면 안 된다.");
            Assert.That(shapeRight, Is.LessThan(GetLeft(info)),
                "선택 블록 형태와 상세 설명은 겹치면 안 된다.");
        }

        private static void AssertOffseasonActionHeaderLayout(Transform panel)
        {
            RectTransform header = GetRect(panel, "Header");
            RectTransform headerLine = GetRect(header, "HeaderLine");
            RectTransform phase = GetRect(panel, "Phase");
            RectTransform economyGuide = GetRect(panel, "EconomyGuide");
            float headerLineBottom = header.anchoredPosition.y + GetBottom(headerLine);

            Assert.That(headerLine.rect.width, Is.LessThanOrEqualTo(280f),
                "넓은 오프시즌 패널의 헤더 장식선은 중앙 제목 영역을 침범하면 안 된다.");
            Assert.That(GetTop(phase), Is.LessThan(headerLineBottom),
                "오프시즌 상태 문구는 패널 헤더 구분선과 겹치면 안 된다.");
            Assert.That(GetTop(economyGuide), Is.LessThan(headerLineBottom),
                "오프시즌 비용 안내는 패널 헤더 구분선과 겹치면 안 된다.");
            Assert.That(GetRight(phase), Is.LessThan(GetLeft(economyGuide)),
                "오프시즌 상태와 비용 안내 영역은 서로 겹치면 안 된다.");
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

        private static string FormatMoney(long amount)
        {
            return amount >= 100_000_000L
                ? $"{amount / 100_000_000d:0.##}억원"
                : $"{amount / 10_000d:N0}만원";
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
