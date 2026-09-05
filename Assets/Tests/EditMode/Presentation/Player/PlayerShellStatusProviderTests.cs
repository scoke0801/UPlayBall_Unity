using System;
using System.Linq;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.Player;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Player
{
    /// <summary>
    /// 선수 상태 Provider가 개인 정보만 Shell에 공급하는지 검증한다.
    /// </summary>
    public sealed class PlayerShellStatusProviderTests
    {
        [Test]
        public void GetCurrentStatus_개인자금Condition역할만모드Slot에노출한다()
        {
            var provider = new PlayerShellStatusProvider(CreateModel(88));

            ShellStatusModel status = provider.GetCurrentStatus();

            Assert.That(status.TeamName, Is.EqualTo("서울 웨이브"));
            Assert.That(status.RankText, Is.EqualTo("3위"));
            Assert.That(status.NextMatchText, Is.EqualTo("부산 앵커스 · 홈"));
            Assert.That(status.ModeSlots.Select(slot => slot.SlotId), Is.EquivalentTo(new[]
            {
                "player.condition",
                "player.role",
                "player.money"
            }));
            Assert.That(status.ModeSlots.Any(slot => slot.SlotId.Contains("sp")), Is.False);
            Assert.That(status.ModeSlots.Any(slot => slot.SlotId.Contains("dp")), Is.False);
            Assert.That(status.ModeSlots.Single(slot => slot.SlotId == "player.condition").Emphasis,
                Is.EqualTo(ShellStatusEmphasis.Positive));
        }

        [Test]
        public void Update_Shell구독자에게한번알린다()
        {
            var provider = new PlayerShellStatusProvider(CreateModel(70));
            int changedCount = 0;
            provider.StatusChanged += () => changedCount++;

            provider.Update(CreateModel(28));

            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(provider.GetCurrentStatus().ModeSlots[0].Emphasis,
                Is.EqualTo(ShellStatusEmphasis.Critical));
        }

        private static PlayerHomePresentationModel CreateModel(int condition)
        {
            return new PlayerHomePresentationModel(
                new PlayerHomeIdentityModel(
                    "김가람",
                    23,
                    PlayerPosition.Shortstop,
                    72,
                    "서울 웨이브",
                    12,
                    2028,
                    LeagueLevel.Major,
                    SeasonPhase.RegularSeason),
                new PlayerUsageModel(
                    ExpectedRole.StartingCompetition,
                    81,
                    PlayerGameRole.StartingBatter,
                    5,
                    null),
                new PlayerNextMatchModel(new NextCareerGameView(
                    44,
                    new DateTime(2028, 5, 14),
                    "부산 앵커스",
                    "서울 웨이브",
                    "부산 앵커스",
                    true,
                    PlayerGameRole.StartingBatter,
                    5)),
                default,
                Array.Empty<PlayerRecentGameModel>(),
                condition,
                123_000_000L,
                3,
                18,
                12,
                1,
                PlayerGrowthStatusModel.Unavailable,
                PlayerContractStatusModel.Unavailable);
        }
    }
}
