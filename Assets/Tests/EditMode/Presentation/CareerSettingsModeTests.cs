using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Game.Unity.Persistence;
using Baseball.Presentation.Career;
using Baseball.Presentation.Match;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>공용 설정 Popup이 현재 게임 모드의 종료 권한만 노출하는지 검증한다.</summary>
    public sealed class CareerSettingsModeTests
    {
        private OwnerMatchPresentationOptions _originalOwnerMatchSettings;

        [SetUp]
        public void SetUp()
        {
            _originalOwnerMatchSettings = OwnerMatchPresentationSettings.Load();
            OwnerMatchPresentationSettings.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            OwnerMatchPresentationSettings.SetPlaybackSpeed(_originalOwnerMatchSettings.PlaybackSpeed);
            OwnerMatchPresentationSettings.SetViewingMode(_originalOwnerMatchSettings.ViewingMode);
            UiGameModeSession.Clear();
            if (GameManager.HasInstance)
                Object.DestroyImmediate(GameManager.Instance.gameObject);
        }

        [Test]
        public void 타이틀_게임종료탭은게임종료버튼만노출한다()
        {
            UI_Popup_CareerSettings popup = OpenExitSettings();
            Transform body = popup.transform.Find("Content/SettingsPanel/Body");

            Assert.That(body.childCount, Is.EqualTo(1));
            Assert.That(body.Find("QuitGame"), Is.Not.Null);
            Assert.That(body.Find("Retirement"), Is.Null);
            Assert.That(body.Find("ReturnToTitle"), Is.Null);
        }

        [Test]
        public void 타이틀_구단주저장삭제는메모리진행도함께폐기한다()
        {
            GameBootstrap.EnsureRuntimeManagers();
            GameManager.Instance.TryGetManager(out OwnerModeManager manager);
            Assert.That(manager.StartNewGame(), Is.True, manager.LastError);

            string savePath = System.IO.Path.Combine(
                Application.temporaryCachePath,
                $"owner-title-delete-test-{System.Guid.NewGuid():N}.json");
            System.IO.File.WriteAllText(savePath, "test");
            var saveStoreField = typeof(OwnerModeManager).GetField(
                "_saveStore",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(saveStoreField, Is.Not.Null);
            saveStoreField.SetValue(manager, new ManagerHistoricalSaveJsonStore(savePath));

            try
            {
                UI_Popup_CareerSettings popup = UI_Popup_CareerSettings.ShowSaveLoadRuntime();
                Transform body = popup.transform.Find("Content/SettingsPanel/Body");
                body.Find("OwnerSaveSlotTab").GetComponent<Button>().onClick.Invoke();
                body = popup.transform.Find("Content/SettingsPanel/Body");
                body.Find("DeleteOwnerSave").GetComponent<Button>().onClick.Invoke();
                popup.transform.Find("Content/SettingsPanel/PersistenceConfirmation/Confirm")
                    .GetComponent<Button>().onClick.Invoke();

                Assert.That(manager.HasSave, Is.False);
                Assert.That(manager.HasActiveRuntime, Is.False);
            }
            finally
            {
                if (System.IO.File.Exists(savePath))
                    System.IO.File.Delete(savePath);
            }
        }

        [Test]
        public void 구단주모드_게임종료탭은선수커리어마무리를노출하지않는다()
        {
            UiGameModeSession.Select(UiGameMode.OwnerCareer);

            UI_Popup_CareerSettings popup = OpenExitSettings();
            Transform body = popup.transform.Find("Content/SettingsPanel/Body");

            Assert.That(body.Find("Retirement"), Is.Null);
            Assert.That(body.Find("RetirementGuide"), Is.Null);
            Assert.That(body.Find("Title").GetComponent<Text>().text, Is.EqualTo("구단주 모드 종료"));
            Assert.That(body.Find("ReturnToTitle"), Is.Not.Null);
        }

        [Test]
        public void 구단주모드_경기탭에서관전방식과배속을설정한다()
        {
            UiGameModeSession.Select(UiGameMode.OwnerCareer);
            UI_Popup_CareerSettings popup = UI_Popup_CareerSettings.ShowRuntime();
            Transform body = popup.transform.Find("Content/SettingsPanel/Body");

            Assert.That(body.Find("Unavailable"), Is.Null);
            Assert.That(body.Find("OwnerMode_EveryMoment"), Is.Not.Null);
            Assert.That(body.Find("OwnerMode_KeyMoments"), Is.Not.Null);
            Assert.That(body.Find("OwnerMode_ResultOnly"), Is.Not.Null);
            Assert.That(body.Find("OwnerSpeed_1"), Is.Not.Null);
            Assert.That(body.Find("OwnerSpeed_2"), Is.Not.Null);
            Assert.That(body.Find("OwnerSpeed_5"), Is.Not.Null);

            body.Find("OwnerSpeed_5").GetComponent<Button>().onClick.Invoke();
            body = popup.transform.Find("Content/SettingsPanel/Body");
            body.Find("OwnerMode_ResultOnly").GetComponent<Button>().onClick.Invoke();
            body = popup.transform.Find("Content/SettingsPanel/Body");

            OwnerMatchPresentationOptions settings = OwnerMatchPresentationSettings.Load();
            Assert.That(settings.PlaybackSpeed, Is.EqualTo(OwnerMatchPlaybackSpeed.VeryFast));
            Assert.That(settings.ViewingMode, Is.EqualTo(OwnerMatchViewingMode.ResultOnly));
            Assert.That(settings.ShouldPlayMatchAudio, Is.False);
            Assert.That(body.Find("OwnerSpeed_1").GetComponent<Button>().interactable, Is.False);
            Assert.That(body.Find("OwnerSpeed_2").GetComponent<Button>().interactable, Is.False);
            Assert.That(body.Find("OwnerSpeed_5").GetComponent<Button>().interactable, Is.False);
            Assert.That(body.Find("OwnerMatchGuide").GetComponent<Text>().text, Does.Contain("사운드"));
        }

        [Test]
        public void 선수모드_게임종료탭은커리어마무리를유지한다()
        {
            UiGameModeSession.Select(UiGameMode.PlayerCareer);

            UI_Popup_CareerSettings popup = OpenExitSettings();
            Transform body = popup.transform.Find("Content/SettingsPanel/Body");

            Assert.That(body.Find("Retirement"), Is.Not.Null);
            Assert.That(body.Find("RetirementGuide"), Is.Not.Null);
            Assert.That(body.Find("Title").GetComponent<Text>().text, Is.EqualTo("선수 커리어 마무리"));
        }

        private static UI_Popup_CareerSettings OpenExitSettings()
        {
            UI_Popup_CareerSettings popup = UI_Popup_CareerSettings.ShowRuntime();
            Transform tab = popup.transform.Find("Content/SettingsPanel/Tab_5");
            Assert.That(tab, Is.Not.Null);
            tab.GetComponent<Button>().onClick.Invoke();
            return popup;
        }
    }
}
