using Baseball.Core.Historical;
using Baseball.Simulation.Random;
using System.Collections.Generic;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        public OwnerTraitTrainingBalance TraitBalance => _balance.TraitTraining;

        /// <summary>매 오프시즌 한 번 지급하고 저장 성공 후에만 공개한다.</summary>
        public void PrepareTraitTraining()
        {
            if (OwnerScheduleGateService.GetPhase(Runtime) != OwnerSeasonPhase.Offseason ||
                Runtime.PlayerGrowth.Traits.rewardedSeason >= Runtime.ManagerMode.LiveSeason.SeasonNumber) return;
            CommitDevelopment(runtime => OwnerTraitTrainingService.GrantOffseasonReward(runtime, TraitBalance));
        }
        public void TrainTrait(string cardId, IReadOnlyList<string> partners, int season, int revision) =>
            CommitDevelopment(runtime => OwnerTraitTrainingService.Train(runtime, cardId, partners, TraitBalance,
                season, revision, TraitRandom(runtime, cardId)));
        public void ChooseTrait(string cardId, CardTraitKind kind, int season, int revision) =>
            CommitDevelopment(runtime => OwnerTraitTrainingService.Choose(runtime, cardId, kind, TraitBalance, season, revision));
        public void RerollTrait(string cardId, int season, int revision) =>
            CommitDevelopment(runtime => OwnerTraitTrainingService.Reroll(runtime, cardId, TraitBalance, season, revision, TraitRandom(runtime, cardId)));
        public void ChangeTrait(string cardId, int season, int revision) =>
            CommitDevelopment(runtime => OwnerTraitTrainingService.Change(runtime, cardId, TraitBalance, season, revision, TraitRandom(runtime, cardId)));
        public void SetTraitPreferences(bool guideSeen, bool skipAnimation, bool skipConfirmation) =>
            CommitDevelopment(runtime => { runtime.PlayerGrowth.Traits.hasSeenGuide = guideSeen;
                runtime.PlayerGrowth.Traits.skipAnimation = skipAnimation; runtime.PlayerGrowth.Traits.skipConfirmation = skipConfirmation; });
        private static Pcg32Random TraitRandom(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            runtime.TryGetOwnedCard(cardId, out var card);
            ulong seed = runtime.WorldHistory.WorldHistorySeed;
            // 문자열 HashCode 대신 플랫폼과 무관한 순서 고정 해시를 사용한다.
            unchecked { foreach (char ch in cardId) seed = (seed ^ ch) * 1099511628211UL; }
            return new Pcg32Random(seed, (ulong)card.Trait.candidateSequence + 3701UL);
        }
    }
}
