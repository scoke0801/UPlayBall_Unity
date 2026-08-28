using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Simulation.Growth;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 커리어 세이브의 성장 상태가 기존 경기 입력과 컨디션·나이에 연결되는지 검증한다.
    /// </summary>
    public sealed class PlayerGrowthIntegrationTests
    {
        [Test]
        public void PlayerState_성장한BaseAbility를경기Player로변환한다()
        {
            Player identity = CreatePlayer();
            var state = new PlayerState(
                2, identity.PlayerId, identity.Name, "대한민국", 18,
                identity.PrimaryPosition, identity.BattingHand, identity.ThrowingHand,
                identity.BatterAttributes, identity.PitcherAttributes, 10);
            PlayerGrowthState growth = new PlayerGrowthFactory(GrowthBalanceTable.CreateDefault())
                .Create(identity, 18, 90);
            state.AttachGrowthState(growth);

            growth.ApplyBaseAbilityChange(PlayerAbility.Contact, 2);
            state.InitializeSeasonStatus(80, 50);
            state.AdvanceAge();

            var block = new SkillBlockDefinition(
                "contact_bonus",
                SkillBlockRarity.Common,
                SkillBlockCategory.Contact,
                new[] { new BoardCell(0, 0) },
                false,
                new[] { new AbilityChange(PlayerAbility.Contact, 2) },
                60L);
            SkillBlockInstance instance = state.SkillBoardState.AddOwnedBlock(block.BlockId);
            var boardService = new SkillBoardService(
                SkillBoardDefinition.CreateDefault(),
                new[] { block });
            boardService.PlaceBlock(state.SkillBoardState, instance.InstanceId, 0, 0, 0);

            Assert.That(state.ToPlayer().BatterAttributes.Contact, Is.EqualTo(62));
            Assert.That(state.ToPlayer(boardService).BatterAttributes.Contact, Is.EqualTo(64));
            Assert.That(growth.Condition, Is.EqualTo(80));
            Assert.That(growth.Age, Is.EqualTo(19));
        }

        private static Player CreatePlayer()
        {
            return new Player(
                1,
                "테스트 선수",
                PlayerPosition.CenterField,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(60, 55, 65, 40, 60, 55),
                new PitcherAttributes(40, 40, 40, 40, 40, 40));
        }
    }
}
