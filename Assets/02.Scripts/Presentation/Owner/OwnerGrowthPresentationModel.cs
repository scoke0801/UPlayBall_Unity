using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Growth;

namespace Baseball.Presentation.Owner
{
    /// <summary>유학 비용·기간·상한을 반영한 성장 결과와 차단 사유다.</summary>
    public sealed class OwnerStudyOption
    {
        public CardStudyProgramDefinition Program { get; }
        public string RewardText { get; }
        public string BlockedReason { get; }
        public bool CanStart => BlockedReason.Length == 0;

        public OwnerStudyOption(CardStudyProgramDefinition program, string rewardText, string blockedReason)
        {
            Program = program;
            RewardText = rewardText;
            BlockedReason = blockedReason ?? string.Empty;
        }
    }

    /// <summary>실제 보유 카드의 장착 목록과 카드별 유학 선택지를 복사한다.</summary>
    public sealed class OwnerGrowthCardSnapshot
    {
        public OwnerCollectionCardSnapshot Card { get; }
        public PlacedSkillBlock[] Placements { get; }
        public OwnerStudyOption[] Studies { get; }

        public OwnerGrowthCardSnapshot(OwnerCollectionCardSnapshot card,
            IReadOnlyList<PlacedSkillBlock> placements, IReadOnlyList<OwnerStudyOption> studies)
        {
            Card = card;
            Placements = OwnerPowerUpSnapshotCopy.Copy(placements);
            Studies = OwnerPowerUpSnapshotCopy.Copy(studies);
        }
    }

    /// <summary>스킬 배치와 유학 화면이 공유하는 런타임 조회 결과다.</summary>
    public sealed class OwnerGrowthSnapshot
    {
        public OwnerGrowthCardSnapshot[] Cards { get; }
        public SkillBlockInstance[] Inventory { get; }
        public SkillBlockDefinition[] Definitions { get; }
        public SkillBoardDefinition Board { get; }
        public int DevelopmentPoints { get; }
        public int StudyCount { get; }
        public int StudyCapacity { get; }

        public OwnerGrowthSnapshot(IReadOnlyList<OwnerGrowthCardSnapshot> cards,
            IReadOnlyList<SkillBlockInstance> inventory, SkillBlockDefinition[] definitions,
            SkillBoardDefinition board, int developmentPoints, int studyCount, int studyCapacity)
        {
            Cards = OwnerPowerUpSnapshotCopy.Copy(cards);
            Inventory = OwnerPowerUpSnapshotCopy.Copy(inventory);
            Definitions = OwnerPowerUpSnapshotCopy.Copy(definitions);
            Board = board;
            DevelopmentPoints = developmentPoints;
            StudyCount = studyCount;
            StudyCapacity = studyCapacity;
        }

        /// <summary>다른 카드의 장착 상태까지 포함해 공유 블록의 소유자를 조회한다.</summary>
        public string GetEquippedCardId(int instanceId)
        {
            foreach (OwnerGrowthCardSnapshot card in Cards)
                foreach (PlacedSkillBlock placement in card.Placements)
                    if (placement.Instance.InstanceId == instanceId) return card.Card.CardId;
            return string.Empty;
        }

        /// <summary>실제 Simulation 서비스로 복사본을 구성해 경계·회전·겹침을 검증한다.</summary>
        public SkillBoardState CreateBoardState(OwnerGrowthCardSnapshot card)
        {
            var state = new SkillBoardState(Board.BoardDefinitionId);
            foreach (SkillBlockInstance block in Inventory) state.AddOwnedBlock(block);
            var service = new SkillBoardService(Board, Definitions);
            foreach (PlacedSkillBlock placement in card.Placements)
                service.PlaceBlock(state, placement.Instance.InstanceId,
                    placement.OriginX, placement.OriginY, placement.RotationQuarterTurns);
            return state;
        }
    }

    /// <summary>구단주 성장 밸런스와 저장 상태에서 표시 데이터를 구성한다.</summary>
    public static class OwnerGrowthPresentationBuilder
    {
        /// <summary>실제 보유 카드와 밸런스 정의에서 두 성장 화면의 조회 결과를 만든다.</summary>
        public static OwnerGrowthSnapshot Build(OwnerModeManager manager, OwnerCollectionSnapshot collection)
        {
            var runtime = manager.Runtime;
            int capacity = manager.Balance.OwnerCardGrowth.GetStudyCapacity(
                runtime.ManagerMode.ClubOperation.GetFacility(FacilityType.TrainingCenter).Level);
            var cards = new List<OwnerGrowthCardSnapshot>();
            foreach (OwnerCollectionCardSnapshot card in collection.Cards)
            {
                if (!runtime.TryGetOwnedCard(card.CardId, out OwnedPlayerCardState owned) ||
                    !runtime.WorldCardCatalog.TryGetCard(card.CardId, out PlayerCardDefinition definition)) continue;
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(definition);
                var studies = new List<OwnerStudyOption>();
                foreach (CardStudyProgramDefinition program in manager.Balance.OwnerCardGrowth.StudyPrograms)
                {
                    if (program.PlayerType != season.PlayerType) continue;
                    var rewards = new StringBuilder();
                    int totalGain = 0;
                    foreach (AbilityChange reward in program.Rewards)
                    {
                        int current = season.CreateBaseAttributes().Get(reward.Ability) + owned.Training.GetBonus(reward.Ability);
                        int gain = Math.Min(reward.Amount, Math.Max(0, season.CreateTrainingCeiling().Get(reward.Ability) - current));
                        totalGain += gain;
                        rewards.Append(CareerSharedSnapshotFormatters.FormatAbility(reward.Ability))
                            .Append("  +").Append(gain).AppendLine();
                    }
                    string reason = card.IsActiveRoster ? "1군 등록 선수입니다. 선수단에서 등록을 해제한 뒤 신청하세요."
                        : owned.LastStudySeason == runtime.ManagerMode.LiveSeason.SeasonNumber ? "이번 시즌 유학을 이미 사용했습니다."
                        : runtime.PlayerGrowth.StudyProjects.Count >= capacity ? "유학 정원이 가득 찼습니다. 훈련 시설과 복귀 일정을 확인하세요."
                        : totalGain == 0 ? "이 과정의 성장 상한에 도달했습니다."
                        : runtime.Economy.DevelopmentPoints < program.DevelopmentPointCost ? "육성 포인트가 부족합니다."
                        : string.Empty;
                    studies.Add(new OwnerStudyOption(program, rewards.ToString().TrimEnd(), reason));
                }
                cards.Add(new OwnerGrowthCardSnapshot(card, owned.SkillBoard.Placements, studies));
            }
            return new OwnerGrowthSnapshot(cards, runtime.PlayerGrowth.Inventory.Blocks,
                manager.Balance.Growth.SkillBlocks, manager.Balance.Growth.SkillBoard,
                runtime.Economy.DevelopmentPoints, runtime.PlayerGrowth.StudyProjects.Count, capacity);
        }
    }
}
