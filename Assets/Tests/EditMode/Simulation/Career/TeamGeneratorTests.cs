using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    /// <summary>
    /// 새 게임 구단 생성이 결정론적이고 요청한 개수만큼 만들어지는지 확인한다.
    /// </summary>
    public sealed class TeamGeneratorTests
    {
        private static readonly string[] NamePool =
        {
            "서울 블루윙스", "부산 마리너스", "인천 웨이브", "광주 레드폭스",
            "수원 스타즈", "대전 호크스", "대구 크라운", "창원 블레이즈"
        };

        [Test]
        public void GenerateLeague_요청한개수만큼구단을만든다()
        {
            var generator = new TeamGenerator(new Pcg32Random(1234UL));
            TeamArchetypeProfile[] pool = TeamArchetypeLibrary.CreateDefaultPool();

            GeneratedTeam[] teams = generator.GenerateLeague(8, pool, NamePool);

            Assert.That(teams, Has.Length.EqualTo(8));
        }

        [Test]
        public void GenerateLeague_같은Seed는같은구단목록을만든다()
        {
            TeamArchetypeProfile[] pool = TeamArchetypeLibrary.CreateDefaultPool();

            GeneratedTeam[] first = new TeamGenerator(new Pcg32Random(777UL))
                .GenerateLeague(8, pool, NamePool);
            GeneratedTeam[] second = new TeamGenerator(new Pcg32Random(777UL))
                .GenerateLeague(8, pool, NamePool);

            for (int index = 0; index < first.Length; index++)
            {
                Assert.That(second[index].Name, Is.EqualTo(first[index].Name));
                Assert.That(second[index].Archetype.Archetype, Is.EqualTo(first[index].Archetype.Archetype));
                Assert.That(
                    second[index].GetPositionNeed(PlayerPositionForAssertion),
                    Is.EqualTo(first[index].GetPositionNeed(PlayerPositionForAssertion)));
            }
        }

        [Test]
        public void GenerateLeague_구단이름은중복되지않는다()
        {
            var generator = new TeamGenerator(new Pcg32Random(42UL));
            TeamArchetypeProfile[] pool = TeamArchetypeLibrary.CreateDefaultPool();

            GeneratedTeam[] teams = generator.GenerateLeague(8, pool, NamePool);

            var seenNames = new System.Collections.Generic.HashSet<string>();
            foreach (GeneratedTeam team in teams)
                Assert.That(seenNames.Add(team.Name), Is.True, $"{team.Name}이 중복되었습니다.");
        }

        private const Baseball.Core.Players.PlayerPosition PlayerPositionForAssertion =
            Baseball.Core.Players.PlayerPosition.Shortstop;
    }
}
