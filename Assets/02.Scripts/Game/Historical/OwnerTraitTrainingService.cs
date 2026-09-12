using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>선택 시점의 비용·경험치와 확정 시점의 재검증 결과를 공유한다.</summary>
    public readonly struct OwnerTraitTrainingPreview
    {
        public OwnerTraitTrainingPreview(int experience, int total, CardTraitRank rank, int points, long money)
        { Experience = experience; TotalExperience = total; Rank = rank; Points = points; Money = money; }
        public int Experience { get; }
        public int TotalExperience { get; }
        public CardTraitRank Rank { get; }
        public int Points { get; }
        public long Money { get; }
    }

    /// <summary>구단주 전용 특성 경제·훈련·후보 선택을 처리한다. UI는 이 명령을 통해서만 상태를 바꾼다.</summary>
    public static class OwnerTraitTrainingService
    {
        public static PlayerSeasonDefinition Season(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out var card))
                throw new InvalidOperationException("선수 카드를 찾을 수 없습니다.");
            return runtime.WorldCardCatalog.GetPlayerSeason(card);
        }

        public static OwnedPlayerCardState RequireAvailable(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (OwnerScheduleGateService.GetPhase(runtime) != OwnerSeasonPhase.Offseason)
                throw new InvalidOperationException("특성훈련은 오프시즌에 진행할 수 있습니다.");
            var card = OwnerPermanentGrowthService.RequireAvailable(runtime, cardId, OwnerGrowthAction.TraitTraining);
            var person = Season(runtime, cardId).PlayerPersonId;
            if (runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey).TryGetPlayer(person, out var status)
                && status.Availability != PlayerAvailabilityStatus.Available)
                throw new InvalidOperationException("회복 중이거나 출전할 수 없는 선수는 훈련에 참여할 수 없습니다.");
            int season = runtime.ManagerMode.LiveSeason.SeasonNumber;
            if (OwnerPermanentGrowthService.Count(card, OwnerGrowthSource.Mentoring, season) > 0)
                throw new InvalidOperationException("이번 오프시즌 훈련 파트너 활동에 참여한 선수입니다.");
            return card;
        }

        public static int RemainingUses(OwnedPlayerCardState card, int season, OwnerTraitTrainingBalance balance) =>
            Math.Max(0, balance.partnerUses - (card.Trait.partnerSeason == season ? card.Trait.partnerUses : 0));

        public static bool IsStarter(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            foreach (var entry in runtime.GetRoster(runtime.PlayerTeamSeasonKey).Entries)
                if (entry.CardId == cardId)
                    return entry.Role < ActiveRosterRole.BenchHitter || entry.Role >= ActiveRosterRole.StartingPitcher1 && entry.Role <= ActiveRosterRole.StartingPitcher5
                        || entry.Role == ActiveRosterRole.Setup || entry.Role == ActiveRosterRole.Closer;
            return false;
        }

        public static int PartnerExperience(ManagerHistoricalRuntimeState runtime, string cardId, string partnerId,
            OwnerTraitTrainingBalance balance)
        {
            RequireAvailable(runtime, cardId);
            var partner = RequireAvailable(runtime, partnerId);
            var targetSeason = Season(runtime, cardId); var partnerSeason = Season(runtime, partnerId);
            if (cardId == partnerId || targetSeason.PlayerPersonId == partnerSeason.PlayerPersonId)
                throw new InvalidOperationException("대상과 다른 선수를 파트너로 선택하세요.");
            if (partner.Trait.HasCandidates) throw new InvalidOperationException("특성 후보 선택을 먼저 마쳐 주세요.");
            if (RemainingUses(partner, runtime.ManagerMode.LiveSeason.SeasonNumber, balance) == 0)
                throw new InvalidOperationException("이번 오프시즌 파트너 훈련 횟수를 모두 사용했습니다.");
            int bonus = targetSeason.Position == partnerSeason.Position ? balance.samePositionPercent : 0;
            if (targetSeason.OriginFranchiseId == partnerSeason.OriginFranchiseId) bonus += balance.sameTeamPercent;
            // 현재 시즌 우수 성적은 기록 판정이 확정된 경우에만 추가한다. 원 시즌 명성을 대신 쓰지 않는다.
            return (IsStarter(runtime, partnerId) ? balance.starterExperience : balance.reserveExperience) * (100 + bonus) / 100;
        }

        public static OwnerTraitTrainingPreview Preview(ManagerHistoricalRuntimeState runtime, string cardId,
            IReadOnlyList<string> partners, OwnerTraitTrainingBalance balance)
        {
            balance.Validate();
            var card = RequireAvailable(runtime, cardId);
            if (card.Trait.HasCandidates) throw new InvalidOperationException("선수에게 부여할 특성을 먼저 선택하세요.");
            if (card.Trait.rank == CardTraitRank.S) throw new InvalidOperationException("이 선수의 특성은 최고 등급입니다.");
            int target = (int)balance.GetRank(card.Trait.experience);
            if (partners == null || partners.Count == 0) throw new InvalidOperationException("훈련 파트너를 선택하세요.");
            if (partners.Count > balance.slots[target]) throw new InvalidOperationException("선택 가능한 파트너 슬롯을 초과했습니다.");
            int experience = 0;
            var people = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < partners.Count; i++)
            {
                if (!people.Add(Season(runtime, partners[i]).PlayerPersonId))
                    throw new InvalidOperationException("같은 선수를 중복 지정할 수 없습니다.");
                experience = checked(experience + PartnerExperience(runtime, cardId, partners[i], balance));
            }
            int total = Math.Min(balance.experience[3], checked(card.Trait.experience + experience));
            int points = checked((int)Math.Ceiling(balance.basePointCost * balance.costMultipliers[target] * partners.Count));
            long money = checked((long)Math.Ceiling(balance.baseMoneyCost * balance.costMultipliers[target] * partners.Count));
            if (card.Trait.trait == CardTraitKind.None && total >= balance.experience[0]) Eligible(runtime, cardId, balance);
            return new OwnerTraitTrainingPreview(total - card.Trait.experience, total, balance.GetRank(total), points, money);
        }

        public static void Train(ManagerHistoricalRuntimeState runtime, string cardId, IReadOnlyList<string> partners,
            OwnerTraitTrainingBalance balance, int expectedSeason, int expectedRevision, IRandomSource random)
        {
            CheckRevision(runtime, cardId, expectedSeason, expectedRevision);
            var preview = Preview(runtime, cardId, partners, balance);
            var card = RequireAvailable(runtime, cardId);
            var next = card.Trait.Copy();
            next.experience = preview.TotalExperience;
            if (next.trait == CardTraitKind.None && preview.Rank != CardTraitRank.None)
                Draw(runtime, cardId, next, balance, random);
            else if (next.trait != CardTraitKind.None) next.rank = preview.Rank;
            Spend(runtime, preview.Points, preview.Money);
            foreach (string id in partners)
            {
                runtime.TryGetOwnedCard(id, out var partner);
                if (partner.Trait.partnerSeason != expectedSeason) { partner.Trait.partnerSeason = expectedSeason; partner.Trait.partnerUses = 0; }
                partner.Trait.partnerUses++; partner.Trait.revision++;
            }
            next.trainingSeason = expectedSeason; next.revision++; card.Trait = next;
        }

        public static void Choose(ManagerHistoricalRuntimeState runtime, string cardId, CardTraitKind kind,
            OwnerTraitTrainingBalance balance, int expectedSeason, int expectedRevision)
        {
            CheckRevision(runtime, cardId, expectedSeason, expectedRevision);
            var card = RequireAvailable(runtime, cardId);
            if (!card.Trait.HasCandidates || Array.IndexOf(card.Trait.candidates, kind) < 0)
                throw new InvalidOperationException("제시된 특성 후보를 선택하세요.");
            if (!Eligible(runtime, cardId, balance).Contains(kind)) throw new InvalidOperationException("이 선수에게 적용할 수 없는 특성입니다.");
            card.Trait.trait = kind; card.Trait.rank = balance.GetRank(card.Trait.experience);
            card.Trait.candidates = Array.Empty<CardTraitKind>(); card.Trait.revision++;
        }

        public static void Reroll(ManagerHistoricalRuntimeState runtime, string cardId, OwnerTraitTrainingBalance balance,
            int expectedSeason, int expectedRevision, IRandomSource random)
        {
            CheckRevision(runtime, cardId, expectedSeason, expectedRevision);
            var card = RequireAvailable(runtime, cardId);
            if (!card.Trait.HasCandidates) throw new InvalidOperationException("재추첨할 후보가 없습니다.");
            var next = card.Trait.Copy();
            Draw(runtime, cardId, next, balance, random);
            Spend(runtime, next.freeRerollSeason == expectedSeason ? balance.rerollCost : 0, 0);
            next.freeRerollSeason = expectedSeason; next.revision++; card.Trait = next;
        }

        public static void Change(ManagerHistoricalRuntimeState runtime, string cardId, OwnerTraitTrainingBalance balance,
            int expectedSeason, int expectedRevision, IRandomSource random)
        {
            CheckRevision(runtime, cardId, expectedSeason, expectedRevision);
            var card = RequireAvailable(runtime, cardId);
            if (card.Trait.trait == CardTraitKind.None || card.Trait.HasCandidates)
                throw new InvalidOperationException("현재 특성을 확정한 뒤 변경할 수 있습니다.");
            var next = card.Trait.Copy();
            Draw(runtime, cardId, next, balance, random);
            Spend(runtime, next.freeChangeSeason == expectedSeason ? balance.changeCost : 0, 0);
            next.freeChangeSeason = expectedSeason; next.revision++; card.Trait = next;
        }

        public static List<CardTraitKind> Eligible(ManagerHistoricalRuntimeState runtime, string cardId, OwnerTraitTrainingBalance balance)
        {
            var season = Season(runtime, cardId); var result = new List<CardTraitKind>();
            foreach (var definition in balance.definitions)
            {
                if (definition.playerType != season.PlayerType || definition.position != PlayerPosition.Unknown && definition.position != season.Position) continue;
                if (definition.kind == CardTraitKind.Defense && season.Position == PlayerPosition.DesignatedHitter) continue;
                if (definition.minimumAbility > 0 && season.CreateBaseAttributes().Get(definition.ability) < definition.minimumAbility) continue;
                result.Add(definition.kind);
            }
            result.Sort();
            if (result.Count < 3) throw new InvalidOperationException("선수 역할에 맞는 특성 후보가 세 개 미만입니다. 특성 설정을 확인해 주세요.");
            return result;
        }

        private static void Draw(ManagerHistoricalRuntimeState runtime, string cardId, PlayerTraitProgress next,
            OwnerTraitTrainingBalance balance, IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var pool = Eligible(runtime, cardId, balance);
            next.candidateSeed = (ulong)(random.NextDouble() * uint.MaxValue);
            var draw = new Pcg32Random(next.candidateSeed, (ulong)++next.candidateSequence);
            next.candidates = new CardTraitKind[3];
            for (int i = 0; i < 3; i++)
            {
                int index = (int)(draw.NextDouble() * pool.Count);
                next.candidates[i] = pool[index]; pool.RemoveAt(index);
            }
        }

        public static bool GrantOffseasonReward(ManagerHistoricalRuntimeState runtime, OwnerTraitTrainingBalance balance)
        {
            if (OwnerScheduleGateService.GetPhase(runtime) != OwnerSeasonPhase.Offseason) return false;
            int season = runtime.ManagerMode.LiveSeason.SeasonNumber;
            var state = runtime.PlayerGrowth.Traits;
            if (state.rewardedSeason >= season) return false;
            state.points = checked(state.points + balance.offseasonReward); state.rewardedSeason = season; state.revision++;
            return true;
        }

        public static void GrantMatchReward(ManagerHistoricalRuntimeState runtime, OwnerTraitTrainingBalance balance)
        {
            var state = runtime.PlayerGrowth.Traits;
            state.points = checked(state.points + balance.gameReward); state.rewardedGames++; state.revision++;
        }

        private static void CheckRevision(ManagerHistoricalRuntimeState runtime, string cardId, int season, int revision)
        {
            var card = RequireAvailable(runtime, cardId);
            if (runtime.ManagerMode.LiveSeason.SeasonNumber != season || card.Trait.revision != revision)
                throw new InvalidOperationException("선수 또는 시즌 상태가 바뀌었습니다. 선택을 다시 확인하세요.");
        }

        private static void Spend(ManagerHistoricalRuntimeState runtime, int points, long money)
        {
            if (runtime.PlayerGrowth.Traits.points < points) throw new InvalidOperationException("특성 훈련 포인트가 부족합니다.");
            if (runtime.Economy.Money < money) throw new InvalidOperationException("구단 자금이 부족합니다.");
            if (money > 0 && !runtime.Economy.TrySpendMoney(money)) throw new InvalidOperationException("구단 자금을 사용할 수 없습니다.");
            runtime.PlayerGrowth.Traits.points -= points; runtime.PlayerGrowth.Traits.revision++;
        }
    }
}
