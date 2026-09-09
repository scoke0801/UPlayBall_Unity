using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>특수 영입 예약과 합성 영수증을 저장하는 최소 거래 자료다.</summary>
    [Serializable]
    public sealed class SpecialCardTransactionSaveData
    {
        public string transactionId;
        public string targetCardId;
        public string[] materialCardIds;
        public bool isCommitted;
    }

    public sealed partial class ManagerHistoricalRuntimeState
    {
        private readonly Dictionary<string, SpecialCardTransactionSaveData> _specialCardTransactions =
            new Dictionary<string, SpecialCardTransactionSaveData>(StringComparer.Ordinal);

        /// <summary>영입 화면과 확정 거래가 동일한 재료 보호 규칙을 사용한다.</summary>
        public bool CanUseSpecialRecruitMaterial(string cardId)
        {
            return TryGetOwnedCard(cardId, out var card) && !card.IsLocked && !card.IsFavorite &&
                !Wishlist.Contains(cardId) && !IsCardInUse(cardId) && !IsCardReserved(cardId);
        }

        /// <summary>현재 콘텐츠에서 특수 영입할 수 있는 카드를 ID 순으로 조회한다.</summary>
        public IReadOnlyList<PlayerCardDefinition> GetSpecialRecruitTargets(PlayerCardEdition edition)
        {
            var result = new List<PlayerCardDefinition>();
            if (WorldCardCatalog.SpecialCards == null) return result;
            foreach (var card in WorldCardCatalog.Cards)
                if (card.IsUniqueOwnedCard && card.Edition == edition) result.Add(card);
            result.Sort((a, b) => string.CompareOrdinal(a.CardId, b.CardId));
            return result.AsReadOnly();
        }

        /// <summary>사용자가 선택한 8장을 검증한 뒤 확정 전까지 카드 사용을 예약한다.</summary>
        public void ReserveSpecialRecruit(string transactionId, string targetCardId,
            IReadOnlyList<string> materials, SpecialCardCatalog content = null)
        {
            content ??= WorldCardCatalog.SpecialCards;
            RequireId(transactionId, nameof(transactionId));
            if (content == null || !ReferenceEquals(content.Cards, WorldCardCatalog))
                throw new ArgumentException("현재 월드의 검증된 특수 카드 콘텐츠가 필요합니다.");
            if (_specialCardTransactions.TryGetValue(transactionId, out var existing))
            {
                RequireSameRequest(existing, targetCardId, materials);
                return;
            }
            if (TryGetOwnedCard(targetCardId, out _)) throw new InvalidOperationException("이미 보유한 특수 카드입니다.");
            SpecialRecruitRecipe recipe = content.GetRequiredRecipe(targetCardId);
            ValidateRecruitMaterials(targetCardId, materials, recipe);
            ValidateConsumableMaterials(materials, transactionId);
            _specialCardTransactions.Add(transactionId, CreateTransaction(transactionId, targetCardId, materials, false));
        }

        /// <summary>예약을 취소하여 재료를 해제한다. 확정 거래는 취소할 수 없다.</summary>
        public bool CancelSpecialRecruit(string transactionId)
        {
            if (!_specialCardTransactions.TryGetValue(transactionId, out var transaction) || transaction.isCommitted)
                return false;
            return _specialCardTransactions.Remove(transactionId);
        }

        /// <summary>예약한 재료를 재검증하고 8장 소모·특수 카드 지급·영수증을 한 번만 확정한다.</summary>
        public string CommitSpecialRecruit(string transactionId, SpecialCardCatalog content = null)
        {
            content ??= WorldCardCatalog.SpecialCards;
            if (!_specialCardTransactions.TryGetValue(transactionId, out var transaction))
                throw new InvalidOperationException("영입 재료가 예약되지 않았습니다.");
            if (transaction.materialCardIds.Length != SpecialRecruitRecipe.RequiredMaterialCount)
                throw new InvalidOperationException("일반 합성 거래는 특수 영입 확정에 사용할 수 없습니다.");
            if (transaction.isCommitted) return transaction.targetCardId;
            if (content == null || !ReferenceEquals(content.Cards, WorldCardCatalog))
                throw new ArgumentException("현재 월드의 특수 카드 콘텐츠가 필요합니다.");
            if (TryGetOwnedCard(transaction.targetCardId, out _)) throw new InvalidOperationException("이미 보유한 특수 카드입니다.");
            ValidateRecruitMaterials(transaction.targetCardId, transaction.materialCardIds,
                content.GetRequiredRecipe(transaction.targetCardId));
            ValidateConsumableMaterials(transaction.materialCardIds, transactionId);
            ConsumeAndGrant(transaction);
            return transaction.targetCardId;
        }

        /// <summary>합성 결과를 확정 영수증에 남겨 같은 요청의 난수 재추첨과 재료 재소모를 막는다.</summary>
        public string CombineCards(string transactionId, IReadOnlyList<string> materials,
            IReadOnlyList<CardCombinationOutcome> outcomes, IRandomSource random)
        {
            RequireId(transactionId, nameof(transactionId));
            if (_specialCardTransactions.TryGetValue(transactionId, out var existing))
            {
                RequireSameRequest(existing, existing.targetCardId, materials);
                if (!existing.isCommitted || materials.Count != CardCombinationResolver.RequiredMaterialCount)
                    throw new InvalidOperationException("다른 영입 요청에 사용된 거래 ID입니다.");
                return existing.targetCardId;
            }
            ValidateConsumableMaterials(materials, transactionId);
            PlayerCardDefinition result = CardCombinationResolver.Roll(WorldCardCatalog, materials, outcomes, random);
            // 지급 실패가 재료 소모 뒤에 발생하지 않도록 정수 한계도 사전에 검증한다.
            if (TryGetOwnedCard(result.CardId, out var owned) && owned.DuplicateCount == int.MaxValue)
                throw new InvalidOperationException("보유 카드 수가 한계에 도달했습니다.");
            var transaction = CreateTransaction(transactionId, result.CardId, materials, false);
            ConsumeAndGrant(transaction);
            _specialCardTransactions.Add(transactionId, transaction);
            return result.CardId;
        }

        /// <summary>예약 재료는 판매·강화·1군 변경 UI에서도 같은 상태로 확인한다.</summary>
        public bool IsCardReserved(string cardId)
        {
            foreach (var transaction in _specialCardTransactions.Values)
                if (!transaction.isCommitted && Array.IndexOf(transaction.materialCardIds, cardId) >= 0) return true;
            return false;
        }

        /// <summary>외부 변경에 영향받지 않는 안정 순서의 거래 저장 복사본을 만든다.</summary>
        public SpecialCardTransactionSaveData[] CreateSpecialCardTransactionsSave()
        {
            var ids = new List<string>(_specialCardTransactions.Keys);
            ids.Sort(StringComparer.Ordinal);
            var result = new SpecialCardTransactionSaveData[ids.Count];
            for (int index = 0; index < result.Length; index++)
            {
                var value = _specialCardTransactions[ids[index]];
                result[index] = CreateTransaction(value.transactionId, value.targetCardId, value.materialCardIds, value.isCommitted);
            }
            return result;
        }

        /// <summary>참조와 예약 충돌을 모두 검사한 뒤 거래 원장을 한 번 복원한다.</summary>
        public void RestoreSpecialCardTransactions(IReadOnlyList<SpecialCardTransactionSaveData> transactions)
        {
            if (transactions == null) throw new ArgumentNullException(nameof(transactions));
            if (_specialCardTransactions.Count != 0) throw new InvalidOperationException("이미 복원한 거래 원장입니다.");
            var restored = new Dictionary<string, SpecialCardTransactionSaveData>(StringComparer.Ordinal);
            var reserved = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in transactions)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.transactionId) ||
                    !WorldCardCatalog.TryGetCard(value.targetCardId, out var target) || value.materialCardIds == null ||
                    (value.materialCardIds.Length != 5 && value.materialCardIds.Length != 8))
                    throw new ArgumentException("특수 카드 거래 저장 형식이 잘못되었습니다.");
                if (target.IsUniqueOwnedCard != (value.materialCardIds.Length == 8))
                    throw new ArgumentException("거래 종류와 대상 카드가 다릅니다.");
                if (!value.isCommitted)
                {
                    if (!target.IsUniqueOwnedCard || WorldCardCatalog.SpecialCards == null || TryGetOwnedCard(value.targetCardId, out _))
                        throw new ArgumentException("복원할 특수 영입 예약의 대상이 올바르지 않습니다.");
                    ValidateRecruitMaterials(value.targetCardId, value.materialCardIds,
                        WorldCardCatalog.SpecialCards.GetRequiredRecipe(value.targetCardId));
                    ValidateConsumableMaterials(value.materialCardIds, value.transactionId);
                }
                var transactionCards = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in value.materialCardIds)
                {
                    if (!WorldCardCatalog.TryGetCard(id, out _)) throw new ArgumentException("거래 재료 카드가 없습니다.");
                    if (!value.isCommitted && transactionCards.Add(id) && !reserved.Add(id))
                        throw new ArgumentException("예약 재료가 중복되거나 보유하지 않은 카드입니다.");
                }
                restored.Add(value.transactionId, CreateTransaction(value.transactionId, value.targetCardId,
                    value.materialCardIds, value.isCommitted));
            }
            foreach (var pair in restored) _specialCardTransactions.Add(pair.Key, pair.Value);
        }

        private void ValidateRecruitMaterials(string targetId, IReadOnlyList<string> materials, SpecialRecruitRecipe recipe)
        {
            if (materials == null || materials.Count != SpecialRecruitRecipe.RequiredMaterialCount)
                throw new ArgumentException("특수 영입 재료는 정확히 8장입니다.");
            WorldCardCatalog.TryGetCard(targetId, out var target);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var years = new HashSet<int>();
            for (int index = 0; index < materials.Count; index++)
            {
                string id = materials[index];
                if ((target.Edition == PlayerCardEdition.CareerHigh && !ids.Add(id)) || !recipe.MaterialGroups[index].Contains(id) ||
                    !WorldCardCatalog.TryGetCard(id, out var card))
                    throw new ArgumentException("재료 그룹에 없는 카드이거나 중복 카드입니다.");
                if (target.Edition == PlayerCardEdition.CareerHigh && !years.Add(WorldCardCatalog.GetPlayerSeason(card).OriginYear))
                    throw new ArgumentException("커리어 하이에는 서로 다른 연도 8장이 필요합니다.");
            }
        }

        private void ValidateConsumableMaterials(IReadOnlyList<string> materials, string transactionId)
        {
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in materials)
            {
                if (string.IsNullOrWhiteSpace(id) || !TryGetOwnedCard(id, out var card))
                    throw new InvalidOperationException("보유하지 않은 재료 카드입니다.");
                if (card.IsLocked || card.IsFavorite || Wishlist.Contains(id) || IsCardInUse(id))
                    throw new InvalidOperationException("잠금·위시·기용·성장 중인 카드는 재료로 사용할 수 없습니다.");
                foreach (var pending in _specialCardTransactions.Values)
                    if (!pending.isCommitted && pending.transactionId != transactionId &&
                        Array.IndexOf(pending.materialCardIds, id) >= 0)
                        throw new InvalidOperationException("다른 영입에 예약된 재료입니다.");
                counts.TryGetValue(id, out int count);
                counts[id] = checked(count + 1);
                if ((long)counts[id] > (long)card.DuplicateCount + 1)
                    throw new InvalidOperationException("재료 카드 수량이 부족합니다.");
            }
        }

        private bool IsCardInUse(string id)
        {
            foreach (var entry in GetRoster(PlayerTeamSeasonKey).Entries)
                if (entry.CardId == id) return true;
            foreach (var study in PlayerGrowth.StudyProjects)
                if (study.CardId == id) return true;
            if (ManagerMode == null) return false;
            foreach (var preset in ManagerMode.LineupPresets)
            {
                foreach (var slot in preset.StartingLineupSlots) if (slot.CardId == id) return true;
                if (Contains(preset.BattingOrderCardIds, id) || Contains(preset.BenchPriorityCardIds, id) ||
                    Contains(preset.StarterRotationCardIds, id) || Contains(preset.BullpenAssignmentCardIds, id) ||
                    preset.SetupPitcherCardId == id || preset.CloserPitcherCardId == id) return true;
            }
            return false;
        }

        private void ConsumeAndGrant(SpecialCardTransactionSaveData transaction)
        {
            foreach (string id in transaction.materialCardIds)
            {
                var card = _ownedCardsById[id];
                if (card.TryConsumeDuplicate()) continue;
                _ownedCardsById.Remove(id);
                _ownedCards.Remove(card);
            }
            CommitCardAcquisition(transaction.targetCardId);
            transaction.isCommitted = true;
        }

        private static SpecialCardTransactionSaveData CreateTransaction(string id, string target,
            IReadOnlyList<string> materials, bool committed) => new SpecialCardTransactionSaveData
        {
            transactionId = id, targetCardId = target,
            materialCardIds = new List<string>(materials).ToArray(), isCommitted = committed
        };

        private static void RequireSameRequest(SpecialCardTransactionSaveData existing, string target, IReadOnlyList<string> materials)
        {
            if (target != existing.targetCardId || materials == null || materials.Count != existing.materialCardIds.Length)
                throw new InvalidOperationException("거래 ID가 다른 요청에 재사용되었습니다.");
            for (int index = 0; index < materials.Count; index++)
                if (materials[index] != existing.materialCardIds[index])
                    throw new InvalidOperationException("거래 ID가 다른 재료에 재사용되었습니다.");
        }
    }
}
