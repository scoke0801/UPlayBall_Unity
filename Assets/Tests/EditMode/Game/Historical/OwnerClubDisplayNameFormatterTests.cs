using System;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>실제·가상 구단 Identity가 연도와 함께 한 쌍으로 표시되는 계약을 검증한다.</summary>
    public sealed class OwnerClubDisplayNameFormatterTests
    {
        [Test]
        public void Format_내구단은입력한구단명을그대로표시한다()
        {
            string result = OwnerClubDisplayNameFormatter.Format(
                "LG 트윈스",
                2008,
                true,
                " 서울 불사조 ");

            Assert.That(result, Is.EqualTo("서울 불사조"));
        }

        [Test]
        public void Format_구단명을입력하지않은내구단은Identity와원본연도를표시한다()
        {
            Assert.That(
                OwnerClubDisplayNameFormatter.Format("LG 트윈스", 2008, true, " "),
                Is.EqualTo("2008 LG 트윈스"));
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
        public void Format_간소화호출도같은연도계약을사용한다()
        {
            Assert.That(
                OwnerClubDisplayNameFormatter.Format("LG 트윈스", 2024),
                Is.EqualTo("2024 LG 트윈스"));
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
