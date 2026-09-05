using System;
using Baseball.Game.Career;
using Baseball.Game.Guide;
using Baseball.Game.Manager;
using Baseball.Presentation.Career;
using UnityEngine;

namespace Baseball.Presentation.Guide
{
    /// <summary>Guide CTA를 현재 존재하는 Career UI와 향후 Owner UI 진입점으로 연결한다.</summary>
    public sealed class FrontManagerGuideCtaRouter
    {
        public static event Action<GuideCtaAction, string> OwnerRouteRequested;

        public bool CanRoute(GuideMessage message)
        {
            if (message == null || !message.Cta.HasValue)
                return false;
            return message.Mode == GuideModeScope.Career
                ? IsCareerAction(message.Cta.Value.Action)
                : OwnerRouteRequested != null;
        }

        public bool TryRoute(GuideMessage message)
        {
            if (!CanRoute(message))
                return false;
            GuideCtaAction action = message.Cta.Value.Action;
            if (message.Mode == GuideModeScope.Owner)
            {
                OwnerRouteRequested?.Invoke(action, message.EventId);
                return true;
            }

            switch (action)
            {
                case GuideCtaAction.OpenCareerHome:
                case GuideCtaAction.OpenDailyBriefing:
                case GuideCtaAction.OpenNotificationCenter:
                case GuideCtaAction.OpenSeasonReview:
                    return CareerTabNavigation.Show(CareerMainTab.Home);
                case GuideCtaAction.OpenCareerCondition:
                case GuideCtaAction.OpenCareerUsage:
                case GuideCtaAction.OpenPlayerGameLog:
                    return CareerTabNavigation.Show(CareerMainTab.Player);
                case GuideCtaAction.OpenAwardRecord:
                case GuideCtaAction.OpenCareerAwards:
                case GuideCtaAction.OpenClubRecords:
                case GuideCtaAction.OpenPitchingStats:
                case GuideCtaAction.OpenPostseasonAwards:
                case GuideCtaAction.OpenSeasonHistory:
                    return CareerTabNavigation.Show(CareerMainTab.Records);
                case GuideCtaAction.OpenContractOffer:
                case GuideCtaAction.OpenContractResult:
                case GuideCtaAction.OpenTradeInquiry:
                    return CareerTabNavigation.Show(CareerMainTab.Contract);
                case GuideCtaAction.OpenLeagueResult:
                    return CareerTabNavigation.Show(CareerMainTab.League);
                case GuideCtaAction.OpenAllStarGame:
                case GuideCtaAction.OpenOpponentAnalysis:
                case GuideCtaAction.OpenPostseasonSchedule:
                case GuideCtaAction.OpenRecentMatches:
                case GuideCtaAction.OpenSchedule:
                    return CareerTabNavigation.Show(CareerMainTab.Schedule);
                case GuideCtaAction.OpenBullpen:
                case GuideCtaAction.OpenLineup:
                case GuideCtaAction.OpenPitchingRole:
                case GuideCtaAction.OpenPitchingStaff:
                case GuideCtaAction.OpenNewTeam:
                case GuideCtaAction.OpenRoster:
                case GuideCtaAction.OpenStartingPitcher:
                case GuideCtaAction.OpenTodayLineup:
                    return CareerTabNavigation.Show(CareerMainTab.Team);
                case GuideCtaAction.OpenGuideSettings:
                    UI_Popup_CareerSettings.ShowRuntime();
                    return true;
                case GuideCtaAction.OpenNews:
                case GuideCtaAction.OpenNewsArticle:
                    return ShowCareerNews();
                case GuideCtaAction.OpenMatchLog:
                case GuideCtaAction.OpenMatchSummary:
                case GuideCtaAction.OpenTacticLog:
                    return ShowCareerMatch();
                case GuideCtaAction.StartMatch:
                    return StartCareerMatch();
                default:
                    return false;
            }
        }

        private static bool IsCareerAction(GuideCtaAction action)
        {
            return action switch
            {
                GuideCtaAction.OpenCareerHome or
                GuideCtaAction.OpenDailyBriefing or
                GuideCtaAction.OpenNotificationCenter or
                GuideCtaAction.OpenSeasonReview or
                GuideCtaAction.OpenCareerCondition or
                GuideCtaAction.OpenCareerUsage or
                GuideCtaAction.OpenPlayerGameLog or
                GuideCtaAction.OpenAwardRecord or
                GuideCtaAction.OpenCareerAwards or
                GuideCtaAction.OpenClubRecords or
                GuideCtaAction.OpenPitchingStats or
                GuideCtaAction.OpenPostseasonAwards or
                GuideCtaAction.OpenSeasonHistory or
                GuideCtaAction.OpenContractOffer or
                GuideCtaAction.OpenContractResult or
                GuideCtaAction.OpenTradeInquiry or
                GuideCtaAction.OpenLeagueResult or
                GuideCtaAction.OpenAllStarGame or
                GuideCtaAction.OpenOpponentAnalysis or
                GuideCtaAction.OpenPostseasonSchedule or
                GuideCtaAction.OpenRecentMatches or
                GuideCtaAction.OpenSchedule or
                GuideCtaAction.OpenBullpen or
                GuideCtaAction.OpenLineup or
                GuideCtaAction.OpenPitchingRole or
                GuideCtaAction.OpenPitchingStaff or
                GuideCtaAction.OpenNewTeam or
                GuideCtaAction.OpenRoster or
                GuideCtaAction.OpenStartingPitcher or
                GuideCtaAction.OpenTodayLineup or
                GuideCtaAction.OpenGuideSettings or
                GuideCtaAction.OpenNews or
                GuideCtaAction.OpenNewsArticle or
                GuideCtaAction.OpenMatchLog or
                GuideCtaAction.OpenMatchSummary or
                GuideCtaAction.OpenTacticLog or
                GuideCtaAction.StartMatch => true,
                _ => false
            };
        }

        private static bool ShowCareerNews()
        {
            UI_Popup_CareerNews news = UnityEngine.Object.FindFirstObjectByType<UI_Popup_CareerNews>(
                FindObjectsInactive.Include);
            if (news == null)
                return false;
            news.Show();
            return true;
        }

        private static bool ShowCareerMatch()
        {
            UI_Scene_CareerMatch match = UnityEngine.Object.FindFirstObjectByType<UI_Scene_CareerMatch>(
                FindObjectsInactive.Include);
            if (match == null)
                return false;
            match.Show();
            return true;
        }

        private static bool StartCareerMatch()
        {
            CareerManager manager = GameManager.Instance != null &&
                                    GameManager.Instance.TryGetManager(out CareerManager registered)
                ? registered
                : null;
            if (manager == null || !manager.HasActiveCareer)
                return false;
            if (!manager.HasActiveMatch && !manager.PrepareNextGame())
                return false;
            return ShowCareerMatch();
        }
    }
}
