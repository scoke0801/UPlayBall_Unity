using System;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>구단주 진행의 사용자 구단명과 비플레이어 구단 연도 표시 계약을 검증한다.</summary>
    public sealed class OwnerClubDisplayNameFormatterTests
    {
        [Test]
        public void Format_내구단은입력한구단명만표시한다()
        {
            string result = OwnerClubDisplayNameFormatter.Format(
                "LG 트윈스",
                2008,
                true,
                " 서울 불사조 ");

            Assert.That(result, Is.EqualTo("서울 불사조"));
        }

        [Test]
        public void Format_상대구단은Identity이름앞에원본연도를표시한다()
        {
            Assert.That(
                OwnerClubDisplayNameFormatter.Format("서울 타이드", 2024, false, "서울 불사조"),
                Is.EqualTo("2024 서울 타이드"));
            Assert.That(
                OwnerClubDisplayNameFormatter.Format("두산 베어스", 2024, false, "서울 불사조"),
                Is.EqualTo("2024 두산 베어스"));
        }

        [Test]
        public void Format_이미연도가있는특수구단에는연도를중복하지않는다()
        {
            string result = OwnerClubDisplayNameFormatter.Format(
                "2024 올스타",
                2024,
                false,
                "서울 불사조");

            Assert.That(result, Is.EqualTo("2024 올스타"));
        }

        [Test]
        public void OwnerProfileState_구단명길이를검증하고공백을제거한다()
        {
            var profile = new OwnerProfileState(" 구단주 ", FrontManagerIds.DefaultAnalysis, " 서울 불사조 ");

            Assert.That(profile.ClubName, Is.EqualTo("서울 불사조"));
            Assert.That(profile.Nickname, Is.EqualTo("구단주"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new OwnerProfileState("구단주", FrontManagerIds.DefaultAnalysis, "서"));
        }
    }
}
