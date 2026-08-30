using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Match;
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
                SkillBlockRarity.Normal,
                SkillBlockCategory.Contact,
                TetrominoShapeCatalog.CreateCells(TetrominoShape.O),
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
            growth.ApplyPeakBonusChange(PlayerAbility.Contact, 2);
            Assert.That(state.ToRosterPlayer(boardService).BatterAttributes.Contact, Is.EqualTo(64));
            Assert.That(state.ToPlayer(boardService).BatterAttributes.Contact, Is.EqualTo(66));
            Assert.That(growth.Condition, Is.EqualTo(80));
            Assert.That(growth.Age, Is.EqualTo(19));
        }

        [Test]
        public void CareerGameRunner_장착블록보너스를감독판단과경기입력에사용한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            NewGameFlow flow = CreateSignedCareer(configuration, 8181UL);
            PlayerGrowthState growth = flow.Career.MyPlayer.GrowthState;
            int baseContact = growth.BaseAbilities.Get(PlayerAbility.Contact);
            SkillBlockDefinition block = FindBlock(
                configuration.Balance.Growth.SkillBlocks,
                SkillBlockCategory.Contact,
                SkillBlockRarity.Unique);
            SkillBlockInstance instance = flow.Career.MyPlayer.SkillBoardState.AddOwnedBlock(block.BlockId);
            var boardService = new SkillBoardService(
                configuration.Balance.Growth.SkillBoard,
                configuration.Balance.Growth.SkillBlocks);
            boardService.PlaceBlock(flow.Career.MyPlayer.SkillBoardState, instance.InstanceId, 0, 0, 0);
            flow.StartRookieSeason();

            ScheduledGameState game = flow.Career.CurrentLeague.CurrentSeason.Schedule.GetNextGameForTeam(
                flow.Career.MyPlayer.CurrentTeamId);
            var runner = new CareerGameRunner(flow.Career, configuration.Balance);
            MatchInput input = runner.CreateMatchInput(
                game,
                Baseball.Core.Teams.PlayerGameRole.StartingBatter,
                flow.Career.CurrentLeague.CurrentSeason.SeasonId);
            Baseball.Core.Teams.Team playerTeam = input.AwayTeam.TeamId == flow.Career.MyPlayer.CurrentTeamId
                ? input.AwayTeam
                : input.HomeTeam;
            Player lockedPlayer = FindLineupPlayer(playerTeam, flow.Career.MyPlayer.PlayerId);

            Assert.That(lockedPlayer, Is.Not.Null);
            Assert.That(
                lockedPlayer.BatterAttributes.Contact,
                Is.EqualTo(baseContact + GetAbilityBonus(block, PlayerAbility.Contact)));
            Assert.That(growth.BaseAbilities.Get(PlayerAbility.Contact), Is.EqualTo(baseContact),
                "성장판 보너스가 영구 Base Ability를 바꾸면 안 됩니다.");
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

        private static NewGameFlow CreateSignedCareer(
            NewGameConfiguration configuration,
            ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("성장판 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(63, 58, 60, 53, 66, 60));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            return flow;
        }

        /// <summary>블록 보너스 수치는 밸런스 데이터라 테스트에 상수로 박지 않고 정의에서 읽는다.</summary>
        private static int GetAbilityBonus(SkillBlockDefinition block, PlayerAbility ability)
        {
            int amount = 0;
            for (int index = 0; index < block.AbilityBonuses.Length; index++)
            {
                if (block.AbilityBonuses[index].Ability == ability)
                    amount += block.AbilityBonuses[index].Amount;
            }
            return amount;
        }

        private static SkillBlockDefinition FindBlock(
            SkillBlockDefinition[] blocks,
            SkillBlockCategory category,
            SkillBlockRarity rarity)
        {
            for (int index = 0; index < blocks.Length; index++)
            {
                if (blocks[index].Category == category && blocks[index].Rarity == rarity)
                    return blocks[index];
            }
            throw new System.InvalidOperationException("테스트할 기본 블록을 찾지 못했습니다.");
        }

        private static Player FindLineupPlayer(Baseball.Core.Teams.Team team, int playerId)
        {
            for (int index = 0; index < team.Lineup.Count; index++)
            {
                if (team.Lineup[index].Player.PlayerId == playerId)
                    return team.Lineup[index].Player;
            }
            return null;
        }
    }
}
