using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    /// <summary>
    /// HasAward는 카드 카탈로그 생성의 핫패스다. 선형 탐색을 색인 조회로 바꿨으므로
    /// 조회 결과가 이전과 동일한지, 중복 검사와 Entries 순서가 그대로인지 고정한다.
    /// </summary>
    public sealed class WorldAwardRecordTests
    {
        private static WorldAwardEntry Entry(
            int seasonYear,
            WorldAwardType awardType,
            string playerSeasonId,
            PlayerPosition position = PlayerPosition.Catcher)
        {
            return new WorldAwardEntry(seasonYear, awardType, playerSeasonId, position);
        }

        [Test]
        public void HasAward_수상한선수시즌만true를돌려준다()
        {
            var record = new WorldAwardRecord(new List<WorldAwardEntry>
            {
                Entry(2024, WorldAwardType.AllStar, "PS-1"),
                Entry(2024, WorldAwardType.GoldenGlove, "PS-2"),
                Entry(2024, WorldAwardType.RegularSeasonMvp, "PS-1")
            });

            Assert.That(record.HasAward("PS-1", WorldAwardType.AllStar), Is.True);
            Assert.That(record.HasAward("PS-1", WorldAwardType.RegularSeasonMvp), Is.True);
            Assert.That(record.HasAward("PS-1", WorldAwardType.GoldenGlove), Is.False);
            Assert.That(record.HasAward("PS-2", WorldAwardType.GoldenGlove), Is.True);
            Assert.That(record.HasAward("PS-2", WorldAwardType.AllStar), Is.False);
        }

        [Test]
        public void HasAward_없는선수시즌과null은false다()
        {
            var record = new WorldAwardRecord(new List<WorldAwardEntry>
            {
                Entry(2024, WorldAwardType.AllStar, "PS-1")
            });

            Assert.That(record.HasAward("PS-없음", WorldAwardType.AllStar), Is.False);
            Assert.That(record.HasAward(null, WorldAwardType.AllStar), Is.False);
        }

        [Test]
        public void HasAward_같은선수가여러해같은상을받아도누적된다()
        {
            var record = new WorldAwardRecord(new List<WorldAwardEntry>
            {
                Entry(2023, WorldAwardType.AllStar, "PS-1"),
                Entry(2024, WorldAwardType.AllStar, "PS-1")
            });

            Assert.That(record.HasAward("PS-1", WorldAwardType.AllStar), Is.True);
            Assert.That(record.Entries.Count, Is.EqualTo(2));
        }

        [Test]
        public void 생성자_같은수상을중복저장하면거부한다()
        {
            Assert.Throws<System.ArgumentException>(() => new WorldAwardRecord(new List<WorldAwardEntry>
            {
                Entry(2024, WorldAwardType.AllStar, "PS-1"),
                Entry(2024, WorldAwardType.AllStar, "PS-1")
            }));
        }

        [Test]
        public void Entries_입력순서를그대로보존한다()
        {
            var record = new WorldAwardRecord(new List<WorldAwardEntry>
            {
                Entry(2024, WorldAwardType.AllStar, "PS-3"),
                Entry(2024, WorldAwardType.AllStar, "PS-1"),
                Entry(2024, WorldAwardType.AllStar, "PS-2")
            });

            Assert.That(record.Entries[0].PlayerSeasonId, Is.EqualTo("PS-3"));
            Assert.That(record.Entries[1].PlayerSeasonId, Is.EqualTo("PS-1"));
            Assert.That(record.Entries[2].PlayerSeasonId, Is.EqualTo("PS-2"));
        }
    }
}
