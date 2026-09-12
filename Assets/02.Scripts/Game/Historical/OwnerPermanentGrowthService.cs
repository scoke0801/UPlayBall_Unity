using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;

namespace Baseball.Game.Historical
{
    /// <summary>교정과 파트너의 미리보기 결과를 같은 검증으로 확정한다.</summary>
    public static class OwnerPermanentGrowthService
    {
        public static OwnedPlayerCardState RequireAvailable(ManagerHistoricalRuntimeState runtime, string cardId, OwnerGrowthAction action)
        {
            OwnerScheduleGateService.Evaluate(runtime, action).RequireAllowed();
            if (!runtime.TryGetOwnedCard(cardId, out var owned)) throw new InvalidOperationException("보유 선수를 선택하세요.");
            foreach (var study in runtime.PlayerGrowth.StudyProjects)
                if (study.CardId == cardId) throw new InvalidOperationException("유학 중인 선수는 귀환 후 참여할 수 있습니다.");
            foreach (var camp in runtime.PlayerGrowth.Camps)
                if (camp.CardId == cardId) throw new InvalidOperationException("전지훈련 중인 선수는 귀환 후 참여할 수 있습니다.");
            return owned;
        }
        public static int Count(OwnedPlayerCardState card, OwnerGrowthSource source, int season = -1)
        {
            int count = 0;
            foreach (var entry in card.Training.Ledger.Entries)
                if (entry.Source == source && (season < 0 || entry.SeasonNumber == season)) count++;
            return count;
        }
        public static int[] PreviewCorrection(ManagerHistoricalRuntimeState runtime, string cardId,
            PlayerAbility decrease, PlayerAbility increase, int amount)
        {
            var owned = RequireAvailable(runtime, cardId, OwnerGrowthAction.Correction);
            if (Count(owned, OwnerGrowthSource.Correction) >= 3) throw new InvalidOperationException("교정은 카드당 커리어 3회까지 가능합니다.");
            runtime.WorldCardCatalog.TryGetCard(cardId, out var definition);
            var season = runtime.WorldCardCatalog.GetPlayerSeason(definition);
            ValidateAbility(season.PlayerType, decrease); ValidateAbility(season.PlayerType, increase);
            if (decrease == increase || amount < 1 || amount > 3) throw new InvalidOperationException("서로 다른 능력치와 이동량 1~3을 선택하세요.");
            var bases = season.CreateBaseAttributes();
            int lower = bases.Get(decrease), higher = bases.Get(increase);
            if (amount > MoveLimit(lower) || amount > MoveLimit(higher)) throw new InvalidOperationException("기본 능력치 구간의 1회 이동 한도를 초과했습니다.");
            int existingLower = owned.Training.Ledger.Get(OwnerGrowthSource.Correction, decrease);
            int existingHigher = owned.Training.Ledger.Get(OwnerGrowthSource.Correction, increase);
            if (existingLower - amount < -DecreaseLimit(lower) || existingHigher + amount > IncreaseLimit(higher)
                || lower + existingLower - amount < 1 || higher + existingHigher + amount > AbilityRatings.Maximum)
                throw new InvalidOperationException("이 능력치의 누적 교정 한도를 초과했습니다.");
            var values = new int[PlayerAbilityCatalog.AbilityCount]; values[(int)decrease] = -amount; values[(int)increase] = amount;
            return values;
        }
        public static void Correct(ManagerHistoricalRuntimeState runtime, string cardId, PlayerAbility decrease,
            PlayerAbility increase, int amount, long cost, int expectedLedgerCount)
        {
            if (cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost));
            var values = PreviewCorrection(runtime, cardId, decrease, increase, amount);
            runtime.TryGetOwnedCard(cardId, out var owned);
            if (owned.Training.Ledger.Count != expectedLedgerCount) throw new InvalidOperationException("선수 성장이 변경되었습니다. 결과를 다시 확인하세요.");
            if (!runtime.Economy.TrySpendMoney(cost)) throw new InvalidOperationException("교정에 필요한 PT가 부족합니다.");
            owned.Training.Ledger.Add(new OwnerGrowthModifier("correction_" + expectedLedgerCount,
                OwnerGrowthSource.Correction, "능력치 교정", values, runtime.ManagerMode.LiveSeason.SeasonNumber));
        }
        public static OwnerPartnerPreview PreviewPartner(ManagerHistoricalRuntimeState runtime, string cardId, string partnerId, OwnerPartnerBalance balance = null)
        {
            balance = balance ?? new OwnerPartnerBalance(); balance.Validate();
            var owned = RequireAvailable(runtime, cardId, OwnerGrowthAction.TrainingPartner);
            var partner = RequireAvailable(runtime, partnerId, OwnerGrowthAction.TrainingPartner);
            int seasonNumber = runtime.ManagerMode.LiveSeason.SeasonNumber;
            if (cardId == partnerId || Count(owned, OwnerGrowthSource.Mentoring, seasonNumber) > 0 || Count(partner, OwnerGrowthSource.Mentoring, seasonNumber) > 0)
                throw new InvalidOperationException("선수는 한 오프시즌에 파트너 역할 중 하나로 한 번만 참여할 수 있습니다.");
            runtime.WorldCardCatalog.TryGetCard(cardId, out var card); runtime.WorldCardCatalog.TryGetCard(partnerId, out var partnerCard);
            var targetSeason = runtime.WorldCardCatalog.GetPlayerSeason(card);
            var partnerSeason = runtime.WorldCardCatalog.GetPlayerSeason(partnerCard);
            if (targetSeason.PlayerPersonId == partnerSeason.PlayerPersonId || targetSeason.PlayerType != partnerSeason.PlayerType)
                throw new InvalidOperationException("다른 선수 중 같은 투수·타자 유형을 선택하세요.");
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            var bases = targetSeason.CreateBaseAttributes(); var ceilings = targetSeason.CreateTrainingCeiling();
            var partnerBases = partnerSeason.CreateBaseAttributes();
            int start = targetSeason.PlayerType == PlayerType.Batter ? 0 : 6;
            double currentTotal = 0, ceilingTotal = 0, complement = 0;
            for (int i = start; i < start + 6; i++)
            {
                var ability = (PlayerAbility)i;
                int current = bases.Get(ability) + owned.Training.GetBonus(ability) + owned.Training.Ledger.Get(OwnerGrowthSource.Mentoring, ability);
                currentTotal += current; ceilingTotal += ceilings.Get(ability);
                complement += Math.Max(0, partnerBases.Get(ability) + partner.Training.GetBonus(ability) - current);
            }
            bool samePosition = targetSeason.Position == partnerSeason.Position;
            bool sameTeam = targetSeason.OriginFranchiseId == partnerSeason.OriginFranchiseId;
            double age = 0;
            if (runtime.WorldCardCatalog.TryGetPlayerPerson(targetSeason.PlayerPersonId, out var person)
                && runtime.WorldCardCatalog.TryGetPlayerPerson(partnerSeason.PlayerPersonId, out var mentorPerson))
                age = Math.Min(1, Math.Max(0, (partnerSeason.OriginYear - mentorPerson.BirthYear) - (targetSeason.OriginYear - person.BirthYear)) / balance.ageScale);
            // 성격 원본이 없는 역사 카드는 중립값으로 공개한다. 무작위 성격을 만들어 궁합을 바꾸지 않는다.
            double fit = age * balance.ageWeight + (samePosition ? balance.positionWeight : 0) + (sameTeam ? balance.teamWeight : 0)
                + Math.Min(1, complement / balance.complementScale) * balance.complementWeight + balance.personalityWeight * .5;
            int budget = (int)Math.Floor(Math.Max(0, 1 - currentTotal / Math.Max(1, ceilingTotal)) * balance.growthBudget * fit);
            int applied = 0;
            for (int point = 0; point < budget; point++)
            {
                int best = -1; double bestScore = 0;
                for (int i = start; i < start + 6; i++)
                {
                    var ability = (PlayerAbility)i;
                    if (targetSeason.Position == PlayerPosition.DesignatedHitter && ability == PlayerAbility.Defense) continue;
                    int current = bases.Get(ability) + owned.Training.GetBonus(ability) + owned.Training.Ledger.Get(OwnerGrowthSource.Mentoring, ability) + values[i];
                    if (current >= ceilings.Get(ability)) continue;
                    double score = partnerBases.Get(ability) + partner.Training.GetBonus(ability) - current;
                    if (ability == PlayerAbility.Bunt) score *= balance.buntWeight;
                    if (targetSeason.Position == PlayerPosition.ReliefPitcher && ability == PlayerAbility.Stamina) score *= balance.reliefStaminaWeight;
                    if (score > bestScore) { best = i; bestScore = score; }
                }
                if (best < 0) break;
                values[best]++; applied++;
            }
            if (applied == 0) throw new InvalidOperationException("역할에 유효한 능력차 또는 남은 성장 여지가 부족합니다.");
            return new OwnerPartnerPreview(partnerId, fit, values, (samePosition ? "같은 포지션 · " : "다른 포지션 · ")
                + (sameTeam ? "같은 구단 이력 · " : "다른 구단 이력 · ") + "성격 정보는 중립 적용");
        }
        public static List<OwnerPartnerPreview> Recommend(ManagerHistoricalRuntimeState runtime, string cardId, OwnerPartnerBalance balance = null)
        {
            var result = new List<OwnerPartnerPreview>();
            foreach (var partner in runtime.OwnedCards)
            {
                try { result.Add(PreviewPartner(runtime, cardId, partner.CardId, balance)); }
                catch (InvalidOperationException) { }
            }
            result.Sort((a, b) => { int compare = b.Total.CompareTo(a.Total); if (compare == 0) compare = b.Fit.CompareTo(a.Fit);
                return compare != 0 ? compare : string.CompareOrdinal(a.PartnerCardId, b.PartnerCardId); });
            return result;
        }
        public static void Partner(ManagerHistoricalRuntimeState runtime, string cardId, string partnerId, long cost, OwnerPartnerBalance balance = null)
        {
            if (cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost));
            var preview = PreviewPartner(runtime, cardId, partnerId, balance);
            runtime.TryGetOwnedCard(cardId, out var target); runtime.TryGetOwnedCard(partnerId, out var mentor);
            int season = runtime.ManagerMode.LiveSeason.SeasonNumber;
            if (!runtime.Economy.TrySpendMoney(cost)) throw new InvalidOperationException("파트너 훈련에 필요한 PT가 부족합니다.");
            target.Training.Ledger.Add(new OwnerGrowthModifier("partner_" + season, OwnerGrowthSource.Mentoring, "훈련 파트너 성장", preview.Values, season));
            mentor.Training.Ledger.Add(new OwnerGrowthModifier("partner_" + season, OwnerGrowthSource.Mentoring, "훈련 파트너 지도 참여", new int[PlayerAbilityCatalog.AbilityCount], season));
        }
        private static int IncreaseLimit(int value) => value >= 90 ? 1 : value >= 80 ? 2 : value >= 70 ? 4 : value >= 60 ? 6 : 9;
        private static int DecreaseLimit(int value) => value >= 90 ? 6 : value >= 80 ? 5 : value >= 70 ? 4 : value >= 60 ? 3 : 2;
        private static int MoveLimit(int value) => value >= 90 ? 1 : value >= 70 ? 2 : 3;
        private static void ValidateAbility(PlayerType type, PlayerAbility ability)
        {
            int index = (int)ability;
            if (index < 0 || index >= PlayerAbilityCatalog.AbilityCount || (type == PlayerType.Batter ? index >= 6 : index < 6))
                throw new InvalidOperationException("선수 유형에 맞는 능력치를 선택하세요.");
        }
    }
    public sealed class OwnerPartnerPreview
    {
        public OwnerPartnerPreview(string cardId, double fit, int[] values, string reason)
        { PartnerCardId = cardId; Fit = fit; Values = values; Reason = reason; foreach (int value in values) Total += value; }
        public string PartnerCardId { get; }
        public double Fit { get; }
        public int[] Values { get; }
        public string Reason { get; }
        public int Total { get; }
    }
}
