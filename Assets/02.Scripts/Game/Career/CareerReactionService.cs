using System;
using Baseball.Core.Players;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career.Narrative
{
    /// <summary>중요 경기만 짧은 반응 이벤트로 만들고 선택 효과를 커리어 상태에 반영한다.</summary>
    public sealed class CareerReactionService
    {
        private const int StandardCooldownRounds = 6;
        private readonly CareerState _career;

        public CareerReactionService(CareerState career)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
        }

        public bool TryCreateAfterMatch(MatchNarrativeSnapshot narrative)
        {
            if (narrative == null) throw new ArgumentNullException(nameof(narrative));
            if (_career.Narrative.PendingReaction != null)
                return false;

            CareerReactionTrigger? trigger = ResolveTrigger(narrative);
            if (!trigger.HasValue)
                return false;
            bool bypassCooldown = trigger is CareerReactionTrigger.FirstCareerHit or
                CareerReactionTrigger.FirstCareerHomeRun or CareerReactionTrigger.CareerDebut;
            if (!bypassCooldown && _career.Narrative.HasRecentReaction(
                    narrative.SeasonId,
                    narrative.PlayerLine.Round,
                    StandardCooldownRounds))
            {
                return false;
            }

            return _career.Narrative.TryQueue(BuildReaction(narrative, trigger.Value));
        }

        public void Resolve(int optionIndex)
        {
            CareerReactionEffect effect = _career.Narrative.ResolvePending(optionIndex);
            if (effect.ManagerTrust != 0)
            {
                _career.MyPlayer.ApplyGameFeedback(
                    conditionDelta: 0,
                    managerEvaluationDelta: effect.ManagerTrust,
                    minimumCondition: 0);
            }
        }

        public bool TryCreateContractOffer(int seasonId, int round, int gameId, string teamName)
        {
            return TryCreateCareerEvent(
                seasonId,
                round,
                gameId,
                CareerReactionTrigger.ContractOffer,
                $"{teamName}에서 연장 계약을 제안했습니다. 잔류 가능성을 어떻게 보고 있습니까?");
        }

        public bool TryCreateTradeDevelopment(
            int seasonId,
            int round,
            int gameId,
            string interestedTeamName,
            TradeInterestStage stage)
        {
            if (stage == TradeInterestStage.Interest)
                return false;
            string prompt = stage == TradeInterestStage.Negotiating
                ? $"{interestedTeamName}와의 트레이드가 협상 단계에 들어갔습니다. 이적 가능성을 어떻게 받아들이고 있습니까?"
                : $"{interestedTeamName} 이적설이 보도됐습니다. 현재 입장은 무엇입니까?";
            return TryCreateCareerEvent(
                seasonId,
                round,
                gameId,
                CareerReactionTrigger.TradeRumor,
                prompt);
        }

        private CareerReactionTrigger? ResolveTrigger(MatchNarrativeSnapshot narrative)
        {
            PlayerSeasonStatisticsState statistics = _career.CurrentLeague.CurrentSeason.PlayerStatistics;
            int previousCareerPlateAppearances = SumCareerPlateAppearances() +
                                                 statistics.PlateAppearances -
                                                 narrative.PlayerLine.PlateAppearances;
            int previousCareerHits = SumCareerHits() + statistics.Hits - narrative.PlayerLine.Hits;
            int previousCareerHomeRuns = SumCareerHomeRuns() +
                                         statistics.HomeRuns - narrative.PlayerLine.HomeRuns;
            if (previousCareerPlateAppearances == 0 && narrative.PlayerLine.PlateAppearances > 0)
                return CareerReactionTrigger.CareerDebut;
            if (previousCareerHomeRuns == 0 && narrative.PlayerLine.HomeRuns > 0)
                return CareerReactionTrigger.FirstCareerHomeRun;
            if (previousCareerHits == 0 && narrative.PlayerLine.Hits > 0)
                return CareerReactionTrigger.FirstCareerHit;
            if (narrative.HasTag(NarrativeTag.SlumpEnded))
                return CareerReactionTrigger.SlumpEnded;
            if (narrative.HitlessStreak == 3)
                return CareerReactionTrigger.SlumpStarted;
            if (narrative.HasTag(NarrativeTag.RoleAtRisk))
                return CareerReactionTrigger.RoleAtRisk;
            if (narrative.HasTag(NarrativeTag.OneRunGame) &&
                narrative.PlayerLine.TeamRuns < narrative.PlayerLine.OpponentRuns)
            {
                return CareerReactionTrigger.ImportantLoss;
            }
            if (narrative.HasTag(NarrativeTag.OneRunGame) &&
                narrative.PlayerLine.TeamRuns > narrative.PlayerLine.OpponentRuns &&
                IsStrongPerformance(narrative.PlayerLine, narrative.PlayerPosition))
            {
                return CareerReactionTrigger.ImportantWin;
            }
            return null;
        }

        private CareerReactionEventState BuildReaction(
            MatchNarrativeSnapshot narrative,
            CareerReactionTrigger trigger)
        {
            string prompt = trigger switch
            {
                CareerReactionTrigger.CareerDebut =>
                    "프로 첫 경기를 마쳤습니다. 오늘을 어떤 경기로 기억하겠습니까?",
                CareerReactionTrigger.FirstCareerHit =>
                    "커리어 첫 안타를 기록했습니다. 지금 가장 먼저 떠오르는 생각은 무엇입니까?",
                CareerReactionTrigger.FirstCareerHomeRun =>
                    "커리어 첫 홈런이 나왔습니다. 이 순간을 어떻게 받아들이고 있습니까?",
                CareerReactionTrigger.SlumpStarted =>
                    $"무안타 흐름이 {narrative.HitlessStreak}경기째입니다. 현재 타격감을 어떻게 보고 있습니까?",
                CareerReactionTrigger.SlumpEnded =>
                    $"{narrative.PreviousHitlessStreak + 1}경기 만에 안타가 나왔습니다. 반등의 계기가 될까요?",
                CareerReactionTrigger.RoleAtRisk =>
                    "최근 결과와 선발 경쟁에 대한 부담을 느끼고 있습니까?",
                CareerReactionTrigger.ImportantWin =>
                    "한 점 차 승리에 힘을 보탰습니다. 오늘 승리의 의미는 무엇입니까?",
                _ => "한 점 차 패배였습니다. 오늘 결과를 어떻게 받아들이고 있습니까?"
            };
            CareerReactionSpeaker speaker = trigger switch
            {
                CareerReactionTrigger.RoleAtRisk => CareerReactionSpeaker.Manager,
                CareerReactionTrigger.ImportantWin => CareerReactionSpeaker.Teammate,
                _ => CareerReactionSpeaker.Reporter
            };
            string speakerName = speaker switch
            {
                CareerReactionSpeaker.Manager => "감독",
                CareerReactionSpeaker.Teammate => "동료 선수",
                _ => "지역 스포츠 기자"
            };
            return new CareerReactionEventState(
                $"reaction_{narrative.SeasonId}_{narrative.GameId}_{trigger}",
                narrative.SeasonId,
                narrative.PlayerLine.Round,
                narrative.GameId,
                trigger,
                speaker,
                speakerName,
                prompt,
                BuildOptions(trigger));
        }

        private static CareerReactionOptionState[] BuildOptions(CareerReactionTrigger trigger)
        {
            string confident = trigger switch
            {
                CareerReactionTrigger.ContractOffer => "제안은 고맙지만 제 가치를 차분히 판단하겠습니다.",
                CareerReactionTrigger.TradeRumor => "어느 팀에서든 제 역할을 해낼 준비가 되어 있습니다.",
                CareerReactionTrigger.SlumpStarted or CareerReactionTrigger.RoleAtRisk =>
                    "결과가 나오지 않았을 뿐, 준비한 방향을 믿고 있습니다.",
                _ => "좋은 흐름을 이어갈 자신이 있습니다."
            };
            string accountable = trigger is CareerReactionTrigger.ContractOffer
                ? "시즌에 집중한 뒤 구단과 성실하게 대화하겠습니다."
                : trigger is CareerReactionTrigger.TradeRumor
                    ? "현재 팀에서 더 좋은 결과를 냈어야 했습니다."
                    : trigger is CareerReactionTrigger.ImportantWin or
                CareerReactionTrigger.FirstCareerHit or CareerReactionTrigger.FirstCareerHomeRun
                ? "기회를 살린 점은 기쁘지만 더 꾸준한 선수가 되겠습니다."
                : "제가 더 잘했어야 합니다. 다음 경기에서 책임지겠습니다.";
            return new[]
            {
                new CareerReactionOptionState(
                    CareerResponseStyle.Confident,
                    confident,
                    new CareerReactionEffect(0, 3, 0, 2, 0)),
                new CareerReactionOptionState(
                    CareerResponseStyle.Accountable,
                    accountable,
                    new CareerReactionEffect(2, -1, 2, 0, 1)),
                new CareerReactionOptionState(
                    CareerResponseStyle.TeamFirst,
                    "개인 기록보다 팀이 다음 경기에서 이기는 것이 우선입니다.",
                    new CareerReactionEffect(1, 0, 1, 1, 3))
            };
        }

        private bool TryCreateCareerEvent(
            int seasonId,
            int round,
            int gameId,
            CareerReactionTrigger trigger,
            string prompt)
        {
            if (_career.Narrative.PendingReaction != null ||
                _career.Narrative.HasRecentReaction(seasonId, round, StandardCooldownRounds))
            {
                return false;
            }
            return _career.Narrative.TryQueue(new CareerReactionEventState(
                $"reaction_{seasonId}_{gameId}_{trigger}",
                seasonId,
                round,
                gameId,
                trigger,
                CareerReactionSpeaker.Reporter,
                "전국 스포츠 기자",
                prompt,
                BuildOptions(trigger)));
        }

        private int SumCareerHits()
        {
            int total = 0;
            for (int index = 0; index < _career.SeasonHistory.Count; index++)
                total += _career.SeasonHistory[index].Statistics?.Hits ?? 0;
            return total;
        }

        private int SumCareerHomeRuns()
        {
            int total = 0;
            for (int index = 0; index < _career.SeasonHistory.Count; index++)
                total += _career.SeasonHistory[index].Statistics?.HomeRuns ?? 0;
            return total;
        }

        private int SumCareerPlateAppearances()
        {
            int total = 0;
            for (int index = 0; index < _career.SeasonHistory.Count; index++)
                total += _career.SeasonHistory[index].Statistics?.PlateAppearances ?? 0;
            return total;
        }

        private static bool IsStrongPerformance(CareerGameAdvanceResult result, PlayerPosition position)
        {
            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            return isPitcher
                ? result.OutsRecorded >= 18 && result.EarnedRuns <= 2
                : result.HomeRuns > 0 || result.Hits >= 2 || result.RunsBattedIn >= 2;
        }
    }
}
