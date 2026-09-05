using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using Baseball.Simulation.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 경기 진행 화면이 흐름 상태를 사람이 읽는 문구와 색으로 옮기는 부분이다.
    /// </summary>
    public sealed partial class UI_Scene_CareerMatch
    {
        /// <summary>
        /// 기본 초상 스프라이트를 쓰고, 리소스가 없을 때만 이름 첫 글자로 대체한다.
        /// </summary>
        private static void CreatePlayerPortrait(
            RectTransform parent, string playerName, PlayerPosition position, Vector2 anchoredPosition)
        {
            RectTransform frame = CreateImage(
                "Portrait", parent, CareerUiTheme.PortraitBackdrop,
                new Vector2(116f, 116f), anchoredPosition);
            CreateImage("PortraitBorder", frame, BorderColor, new Vector2(116f, 2f), new Vector2(0f, 57f));

            Sprite portrait = PlayerPortraitSprites.GetDefault(position);
            if (portrait != null)
            {
                RectTransform portraitRect = CreateImage(
                    "PortraitImage", frame, Color.white, new Vector2(112f, 112f), Vector2.zero);
                Image portraitImage = portraitRect.GetComponent<Image>();
                portraitImage.sprite = portrait;
                portraitImage.preserveAspect = true;
                return;
            }

            string initial = string.IsNullOrEmpty(playerName) ? "P" : playerName.Substring(0, 1);
            CreateText(
                "Initial", frame, initial, 48, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, true);
        }

        private static string GetPlayerStateLabel(PlayerMatchState state)
        {
            return state switch
            {
                PlayerMatchState.Bench => "벤치 대기",
                PlayerMatchState.StarterWaiting => "선발 출전",
                PlayerMatchState.OnDeck => "다음 타자",
                PlayerMatchState.AtBat => "타석",
                PlayerMatchState.OnBase => "출루 중",
                PlayerMatchState.Fielding => "수비 중",
                PlayerMatchState.SubstitutedOut => "오늘 경기 종료",
                _ => "출전 없음"
            };
        }

        private static Color GetPlayerStateColor(PlayerMatchState state)
        {
            return state switch
            {
                PlayerMatchState.AtBat or PlayerMatchState.OnDeck => GoldColor,
                PlayerMatchState.OnBase => RoleColor,
                PlayerMatchState.StarterWaiting or PlayerMatchState.Fielding => AccentColor,
                PlayerMatchState.SubstitutedOut or PlayerMatchState.NotPlaying => MutedTextColor,
                _ => SecondaryTextColor
            };
        }

        /// <summary>
        /// 상태 배지 아래에 붙는 한 줄로, 시스템이 보장하는 역할만 쓴다.
        /// 출전 확률처럼 시뮬레이션이 산출하지 않는 값은 만들지 않는다.
        /// </summary>
        private string GetPlayerStateDetail(CareerMatchSession session, MatchProgressViewState view)
        {
            switch (view.PlayerState)
            {
                case PlayerMatchState.Bench:
                    return session.CanReceiveBattingDecisions ? "대타 후보" : "교체 대기";
                case PlayerMatchState.OnDeck:
                    return "곧 타석에 들어갑니다";
                case PlayerMatchState.AtBat:
                    return "내 타석 진행 중";
                case PlayerMatchState.OnBase:
                    return "주루는 자동으로 진행됩니다";
                case PlayerMatchState.Fielding:
                    return $"{GetPositionCode(_manager.CurrentCareer.MyPlayer.PrimaryPosition)} 수비";
                case PlayerMatchState.SubstitutedOut:
                    return "교체되어 오늘 출전이 끝났습니다";
                case PlayerMatchState.StarterWaiting:
                    return view.PlateAppearancesUntilPlayerAtBat > 0
                        ? $"다음 타석까지 {view.PlateAppearancesUntilPlayerAtBat}명"
                        : "다음 공격을 기다립니다";
                default:
                    return "오늘은 출전 대기 없이 관전합니다";
            }
        }

        private string GetNextEventGuide(CareerMatchSession session, MatchProgressViewState view)
        {
            if (!session.CanReceiveBattingDecisions)
                return "오늘은 경기 중 입력이 없습니다.\n결과와 기용 판단을 경기 후 확인합니다.";

            return view.PlayerState switch
            {
                PlayerMatchState.Bench =>
                    "감독이 교체 출전을 결정하면\n자동 진행이 멈추고 타석 입력이 열립니다.",
                PlayerMatchState.AtBat =>
                    "타격 접근을 고르고 투구를 진행합니다.\n선택은 실제 확률에 반영됩니다.",
                PlayerMatchState.OnDeck =>
                    "다음 타석에서 자동 진행이 멈춥니다.",
                PlayerMatchState.OnBase =>
                    "주루 결과가 나오면 다시 자동 중계로 이어집니다.",
                PlayerMatchState.SubstitutedOut =>
                    "남은 경기는 자동으로 진행됩니다.",
                _ => view.PlateAppearancesUntilPlayerAtBat > 0
                    ? $"내 타석까지 {view.PlateAppearancesUntilPlayerAtBat}명 남았습니다.\n그 타석에서 자동 진행이 멈춥니다."
                    : "내 타석에서 자동 진행이 멈춥니다."
            };
        }

        private static string GetSubstitutionPriorityLabel(CareerMatchSession session)
        {
            if (session.PlayerRole == PlayerGameRole.StartingBatter)
                return "선발 출전";
            return session.CanReceiveBattingDecisions ? "대타 후보" : "교체 대기";
        }

        private static string GetConditionLabel(int condition)
        {
            if (condition >= 85)
                return "매우 좋음";
            if (condition >= 70)
                return "좋음";
            if (condition >= 50)
                return "보통";
            return "나쁨";
        }

        private static Color GetConditionColor(int condition)
        {
            if (condition >= 70)
                return RoleColor;
            return condition >= 50 ? GoldColor : DangerColor;
        }

        /// <summary>
        /// 상단 바 우측에 유지되는 자동 진행 상태 요약이다.
        /// </summary>
        private string GetFlowStatusLine(MatchProgressViewState view)
        {
            return view.Flow switch
            {
                MatchFlowState.AutoRunning => $"AUTO · {GetPlaybackSpeedLabel()}",
                MatchFlowState.SideChange => "공수 교대",
                MatchFlowState.PlayerCallUp => "감독 호출",
                MatchFlowState.PlayerAtBat => "내 타석 · 입력 대기",
                MatchFlowState.PlayerAtBatResult => "내 타석 결과",
                MatchFlowState.PlayerSubstitutedOut => "교체 아웃",
                MatchFlowState.GameEnded => "경기 종료",
                _ => "일시 정지"
            };
        }

        private static Color GetFlowStatusColor(MatchProgressViewState view)
        {
            return view.Flow switch
            {
                MatchFlowState.PlayerAtBat or MatchFlowState.PlayerCallUp => RoleColor,
                MatchFlowState.PlayerAtBatResult => GoldColor,
                MatchFlowState.Paused or MatchFlowState.PlayerSubstitutedOut => MutedTextColor,
                _ => AccentColor
            };
        }

        /// <summary>
        /// 중앙 무대 하단에 남기는 최근 플레이 한 줄이다.
        /// </summary>
        private string GetLatestPlayDescription(CareerMatchSession session)
        {
            IReadOnlyList<MatchEvent> events = session.Events;
            for (int index = _playback.VisibleEventCount - 1; index >= 0; index--)
            {
                if (!IsVisibleLogEvent(events[index], session.ControlledPlayerId))
                    continue;

                string description = DescribeTimelineEvent(session.Input, events, index);
                if (!string.IsNullOrEmpty(description))
                    return description;
            }
            return "첫 타자의 결과를 기다리고 있습니다.";
        }

        /// <summary>
        /// 내 선수 타석 화면에서 직전 투구 결과를 보여 준다.
        /// </summary>
        private string GetLatestPitchDescription(CareerMatchSession session)
        {
            IReadOnlyList<MatchEvent> events = session.Events;
            for (int index = _playback.VisibleEventCount - 1; index >= 0; index--)
            {
                MatchEvent matchEvent = events[index];
                if (matchEvent.EventType == MatchEventType.PlateAppearanceEnded)
                    break;
                if (matchEvent.EventType != MatchEventType.Pitch ||
                    matchEvent.BatterId != session.ControlledPlayerId)
                {
                    continue;
                }

                return $"최근 투구 · {GetPitchResultLabel(matchEvent.PitchResult)}";
            }
            return "이번 타석의 첫 투구입니다.";
        }

        private string BuildBatterContextLine(CareerMatchSession session, InningHalf half, int batterId)
        {
            int battingOrderIndex = FindBattingOrderIndex(GetBattingTeam(session, half), batterId);
            PlayerTodayLine today = CalculateTodayLine(
                session.Events,
                _playback.VisibleEventCount,
                batterId);
            string order = battingOrderIndex >= 0 ? $"{battingOrderIndex + 1}번 타자" : "교체 출전";
            return $"{order}  ·  오늘 {today.PlateAppearances}타석 {today.Hits}안타";
        }

        private string BuildCallUpDescription(CareerMatchSession session)
        {
            PositionPlayerSubstitutionPlan plan = FindControlledSubstitutionPlan(session);
            if (plan == null)
                return "감독이 교체 출전을 지시했습니다.";

            return $"{plan.BattingOrderIndex + 1}번 타순을 이어받아 대타로 출전합니다.";
        }

        private static PositionPlayerSubstitutionPlan FindControlledSubstitutionPlan(
            CareerMatchSession session)
        {
            if (session.Input.AwayTeam.PositionPlayerSubstitution?.Player.PlayerId ==
                session.ControlledPlayerId)
            {
                return session.Input.AwayTeam.PositionPlayerSubstitution;
            }
            if (session.Input.HomeTeam.PositionPlayerSubstitution?.Player.PlayerId ==
                session.ControlledPlayerId)
            {
                return session.Input.HomeTeam.PositionPlayerSubstitution;
            }
            return null;
        }

        private static Team GetBattingTeam(CareerMatchSession session, InningHalf half)
        {
            return half == InningHalf.Top ? session.Input.AwayTeam : session.Input.HomeTeam;
        }

        private static int FindBattingOrderIndex(Team team, int playerId)
        {
            for (int index = 0; index < team.Lineup.Count; index++)
            {
                if (team.Lineup[index].Player.PlayerId == playerId)
                    return index;
            }
            return team.PositionPlayerSubstitution?.Player.PlayerId == playerId
                ? team.PositionPlayerSubstitution.BattingOrderIndex
                : -1;
        }

        /// <summary>
        /// 타임라인 한 줄의 본문이다. 이닝은 그룹 머리글이 이미 알려 주므로 붙이지 않는다.
        /// </summary>
        private static string DescribeTimelineEvent(
            MatchInput input,
            IReadOnlyList<MatchEvent> events,
            int eventIndex)
        {
            MatchEvent matchEvent = events[eventIndex];
            string batterName = FindPlayerName(input, matchEvent.BatterId);
            string playerName = FindPlayerName(input, matchEvent.PlayerId);
            return matchEvent.EventType switch
            {
                MatchEventType.Pitch =>
                    $"{batterName} · {GetPitchResultLabel(matchEvent.PitchResult)}",
                MatchEventType.RunnerAdvance =>
                    $"{playerName} · {GetBaseLabel(matchEvent.FromBase)} → {GetBaseLabel(matchEvent.ToBase)}",
                MatchEventType.Score => $"{playerName} 득점",
                MatchEventType.PlateAppearanceEnded =>
                    $"{batterName} · " +
                    GetPlateAppearanceResultLabel(
                        matchEvent.PlateAppearanceResult,
                        CountOutsInPlateAppearance(events, eventIndex)),
                MatchEventType.PlayerSubstitution => $"{batterName} IN  ·  {playerName} OUT",
                MatchEventType.PitcherRemoved =>
                    $"{playerName} 강판 · {GetDecisionReasonLabel(matchEvent.ReasonCode)}",
                MatchEventType.PitcherEntered =>
                    $"{playerName} 등판 · {GetDecisionReasonLabel(matchEvent.ReasonCode)}",
                MatchEventType.PinchHitterEntered => $"대타 {batterName} · {playerName} 교체",
                MatchEventType.PinchRunnerEntered => $"대주자 {playerName} 투입",
                MatchEventType.DefensiveReplacement => $"수비 강화 · {batterName} 투입",
                MatchEventType.StealSucceeded => $"{playerName} 도루 성공",
                MatchEventType.CaughtStealing => $"{playerName} 도루 실패",
                MatchEventType.IntentionalWalk => $"{batterName} 고의사구",
                MatchEventType.BuntResolved =>
                    $"{batterName} · {GetPlateAppearanceResultLabel(matchEvent.PlateAppearanceResult)}",
                MatchEventType.FieldingError => $"{playerName} 포구 실책",
                MatchEventType.ThrowingError => $"{playerName} 송구 실책",
                MatchEventType.DoublePlay => "병살 플레이",
                MatchEventType.FieldersChoice => "야수선택",
                MatchEventType.HalfInningEnded => "이닝 종료",
                _ => string.Empty
            };
        }

        private static string GetTimelineTrailingLabel(MatchEvent matchEvent)
        {
            return matchEvent.EventType switch
            {
                MatchEventType.Score or MatchEventType.HalfInningEnded =>
                    $"{matchEvent.AwayScore} : {matchEvent.HomeScore}",
                MatchEventType.PlateAppearanceEnded => $"{matchEvent.Outs}아웃",
                MatchEventType.PlayerSubstitution => "선수 교체",
                MatchEventType.PitcherEntered or MatchEventType.PitcherRemoved => "투수 교체",
                MatchEventType.PinchHitterEntered => "대타",
                MatchEventType.PinchRunnerEntered => "대주자",
                MatchEventType.FieldingError or MatchEventType.ThrowingError => "실책",
                MatchEventType.StealSucceeded => "도루",
                MatchEventType.CaughtStealing => "도루자",
                MatchEventType.RunnerAdvance => GetBaseLabel(matchEvent.ToBase),
                MatchEventType.Pitch => $"{matchEvent.Balls}-{matchEvent.Strikes}",
                _ => string.Empty
            };
        }

        private static string GetDecisionReasonLabel(DecisionReasonCode reason)
        {
            return reason switch
            {
                DecisionReasonCode.Fatigue => "피로 누적",
                DecisionReasonCode.PitchLimit => "투구 제한",
                DecisionReasonCode.TimesThroughOrder => "타순 세 번째 대면",
                DecisionReasonCode.Performance => "연속 출루와 실점",
                DecisionReasonCode.HighLeverage => "승부처 대응",
                DecisionReasonCode.Matchup => "상성 대응",
                DecisionReasonCode.Injury => "부상",
                DecisionReasonCode.ScheduledUsage => "예정된 기용",
                DecisionReasonCode.DefensiveStrategy => "수비 전략",
                DecisionReasonCode.Emergency => "비상 등판",
                DecisionReasonCode.ExpectedValue => "기대값 우위",
                DecisionReasonCode.PlayerPolicy => "선수 방침",
                _ => "감독 판단"
            };
        }

        private static string GetPrimaryActionLabel(MatchPrimaryAction action)
        {
            return action switch
            {
                MatchPrimaryAction.Pause => "일시 정지   P",
                MatchPrimaryAction.AdvanceToPlayerEntry => "내 선수 출전까지 자동 진행",
                MatchPrimaryAction.AdvanceToPlayerAtBat => "내 다음 타석까지 자동 진행",
                MatchPrimaryAction.EnterPlateAppearance => "타석으로 이동   SPACE",
                MatchPrimaryAction.NextPitch => "다음 투구   SPACE",
                MatchPrimaryAction.ContinueMatch => "경기 계속 진행",
                MatchPrimaryAction.FinishMatch => "경기 종료까지 진행",
                _ => "경기 결과 확인   SPACE"
            };
        }

        /// <summary>
        /// 다음에 자동 진행이 멈추는 조건이다. 사용자는 배속보다 어디서 멈추는지를 먼저 묻는다.
        /// </summary>
        private static string GetStopConditionLabel(
            CareerMatchSession session,
            MatchProgressViewState view)
        {
            if (!session.CanReceiveBattingDecisions)
                return "정지 없음 · 경기 종료까지 진행";
            return view.PlayerState switch
            {
                PlayerMatchState.Bench => "내 선수의 교체 출전",
                PlayerMatchState.SubstitutedOut => "정지 없음 · 경기 종료까지 진행",
                _ => view.PlateAppearancesUntilPlayerAtBat > 0
                    ? $"내 선수의 타석 · {view.PlateAppearancesUntilPlayerAtBat}명 뒤"
                    : "내 선수의 타석"
            };
        }
    }
}
