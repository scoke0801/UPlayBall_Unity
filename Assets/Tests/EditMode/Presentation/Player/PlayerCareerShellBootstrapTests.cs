using Baseball.Presentation.Career;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Player
{
    /// <summary>Management Scene 직접 진입 시 Player 모드 복원 우선순위를 검증한다.</summary>
    public sealed class PlayerCareerShellBootstrapTests
    {
        [Test]
        public void 선택이없고PlayerCareer만활성화되면Player를복원한다()
        {
            Assert.That(
                PlayerCareerShellBootstrap.ShouldSelectPlayerCareer(
                    null,
                    hasActiveCareer: true,
                    hasActiveOwnerRuntime: false),
                Is.True);
        }

        [TestCase(UiGameMode.PlayerCareer)]
        [TestCase(UiGameMode.OwnerCareer)]
        public void 기존모드선택은덮어쓰지않는다(UiGameMode selectedMode)
        {
            Assert.That(
                PlayerCareerShellBootstrap.ShouldSelectPlayerCareer(
                    selectedMode,
                    hasActiveCareer: true,
                    hasActiveOwnerRuntime: false),
                Is.False);
        }

        [Test]
        public void OwnerRuntime이활성화되어있으면Player로덮어쓰지않는다()
        {
            Assert.That(
                PlayerCareerShellBootstrap.ShouldSelectPlayerCareer(
                    null,
                    hasActiveCareer: true,
                    hasActiveOwnerRuntime: true),
                Is.False);
        }
    }
}
