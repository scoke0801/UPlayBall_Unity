using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;

namespace Baseball.Game.Historical
{
    /// <summary>서포트 사용 직전 대상·재고·슬롯을 다시 검사하고 같은 원장에 적용한다.</summary>
    public static class OwnerSupportService
    {
        /// <summary>현재와 완료 시즌의 최고 리그로 해금하며 강등 후에도 유지한다.</summary>
        public static bool IsUnlocked(ManagerHistoricalRuntimeState runtime, OwnerSupportDefinition definition)
        {
            var highest = runtime.League.Grade;
            foreach (var season in runtime.ManagerMode.CompletedSeasons)
                if (season.LeagueGrade > highest) highest = season.LeagueGrade;
            return highest >= definition.unlockGrade;
        }

        private static void ValidateUnlock(ManagerHistoricalRuntimeState runtime, OwnerSupportDefinition definition)
        {
            if (!IsUnlocked(runtime, definition))
                throw new InvalidOperationException("해금 리그에 진출한 뒤 사용할 수 있습니다.");
        }

        public static List<OwnedPlayerCardState> ResolveTargets(ManagerHistoricalRuntimeState runtime,
            OwnerSupportDefinition definition, string cardId)
        {
            definition.Validate();
            var result = new List<OwnedPlayerCardState>();
            foreach (var entry in runtime.GetRoster(runtime.PlayerTeamSeasonKey).Entries)
            {
                if (definition.scope == OwnerSupportScope.Player && entry.CardId != cardId) continue;
                if (!runtime.TryGetOwnedCard(entry.CardId, out var owned)) continue;
                if (!runtime.WorldCardCatalog.TryGetCard(entry.CardId, out var card)) continue;
                var season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                if (definition.target == OwnerSupportTarget.Batter && season.PlayerType != PlayerType.Batter) continue;
                if (definition.target == OwnerSupportTarget.Pitcher && season.PlayerType != PlayerType.Pitcher) continue;
                // 나이는 역사 카드의 해당 시즌 나이를 기준으로 하며 현재 시스템 날짜를 읽지 않는다.
                if (definition.maximumAge > 0 && (!runtime.WorldCardCatalog.TryGetPlayerPerson(season.PlayerPersonId, out var person)
                    || season.OriginYear - person.BirthYear > definition.maximumAge)) continue;
                result.Add(owned);
            }
            return result;
        }
        public static void Purchase(ManagerHistoricalRuntimeState runtime, OwnerSupportDefinition definition)
        {
            definition.Validate();
            ValidateUnlock(runtime, definition);
            if (runtime.PlayerGrowth.Support.GetCount(definition.id) == int.MaxValue) throw new InvalidOperationException("보유 한도입니다.");
            if (!runtime.Economy.TrySpendMoney(definition.price)) throw new InvalidOperationException("서포트 구매에 필요한 PT가 부족합니다.");
            runtime.PlayerGrowth.Support.Add(definition.id);
        }
        public static void Equip(ManagerHistoricalRuntimeState runtime, OwnerSupportDefinition definition, string cardId)
        {
            ValidateUnlock(runtime, definition);
            var targets = ResolveTargets(runtime, definition, cardId);
            if (targets.Count == 0) throw new InvalidOperationException("조건에 맞는 1군 선수가 없습니다.");
            var support = runtime.PlayerGrowth.Support;
            foreach (var owned in runtime.OwnedCards)
                if (owned.Training.Ledger.Contains("support_" + support.NextSequence)) throw new InvalidOperationException("서포트 적용 순서가 올바르지 않습니다.");
            var assignment = support.Equip(definition, definition.scope == OwnerSupportScope.Team ? "" : cardId);
            foreach (var target in targets)
                target.Training.Ledger.Add(new OwnerGrowthModifier(assignment.SourceId, OwnerGrowthSource.Support,
                    definition.displayName, ResolveBonuses(runtime, target.CardId, definition), runtime.ManagerMode.LiveSeason.SeasonNumber, 2));
        }
        /// <summary>선수 유형에 해당하는 능력치만 원장과 미리보기에 반환한다.</summary>
        public static int[] ResolveBonuses(ManagerHistoricalRuntimeState runtime, string cardId, OwnerSupportDefinition definition)
        {
            runtime.WorldCardCatalog.TryGetCard(cardId, out var card);
            int start = runtime.WorldCardCatalog.GetPlayerSeason(card).PlayerType == PlayerType.Batter ? 0 : 6;
            var result = new int[12];
            for (int i = start; i < start + 6; i++) result[i] = definition.bonuses[i];
            return result;
        }
        /// <summary>실제 경기와 카드 컨디션 미리보기가 동일한 활성 출처를 사용한다.</summary>
        public static int GetConditionBonus(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (!runtime.TryGetOwnedCard(cardId, out var owned)) return 0;
            int total = 0;
            foreach (var assignment in runtime.PlayerGrowth.Support.Assignments)
                foreach (var entry in owned.Training.Ledger.Entries)
                    if (entry.SourceId == assignment.SourceId && entry.IsActive) total += assignment.Definition.conditionPoints;
            return total;
        }
        /// <summary>플레이어 구단 경기만 소모한다. AI 다른 조와 연습경기는 호출하지 않는다.</summary>
        public static void CompleteMatch(ManagerHistoricalRuntimeState runtime)
        {
            foreach (var owned in runtime.OwnedCards) owned.Training.Ledger.AdvanceMatch();
            var assignments = runtime.PlayerGrowth.Support.Assignments;
            for (int i = assignments.Count - 1; i >= 0; i--)
                if (assignments[i].AdvanceMatch()) runtime.PlayerGrowth.Support.RemoveAt(i);
        }
        /// <summary>1군 이동 시 개인 효과를 종료하고 팀 효과의 대상만 다시 계산한다.</summary>
        public static void SynchronizeRoster(ManagerHistoricalRuntimeState runtime)
        {
            var support = runtime.PlayerGrowth.Support;
            for (int i = support.Assignments.Count - 1; i >= 0; i--)
            {
                var assignment = support.Assignments[i];
                if (assignment.Definition == null) throw new InvalidOperationException("서포트 적용 조건이 없습니다.");
                var targets = ResolveTargets(runtime, assignment.Definition, assignment.CardId);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var target in targets) ids.Add(target.CardId);
                foreach (var owned in runtime.OwnedCards)
                {
                    var ledger = owned.Training.Ledger;
                    bool isTarget = ids.Contains(owned.CardId);
                    if (isTarget && !ledger.Contains(assignment.SourceId))
                        ledger.Add(new OwnerGrowthModifier(assignment.SourceId, OwnerGrowthSource.Support,
                            assignment.Definition.displayName, ResolveBonuses(runtime, owned.CardId, assignment.Definition),
                            runtime.ManagerMode.LiveSeason.SeasonNumber, assignment.RemainingGames));
                    ledger.SetSupportApplicability(assignment.SourceId, isTarget, assignment.RemainingGames);
                }
                if (!assignment.IsTeam && targets.Count == 0) support.RemoveAt(i);
            }
        }
    }
}
