using System;
using System.IO;
using System.Reflection;
using System.Linq;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Career.Persistence;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Game.Guide;
using Baseball.Game.Unity.Persistence;
using Baseball.Presentation.Career;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>슬롯 파일 격리, 실제 Manager 연결과 설정의 선택·확인·레이아웃을 검증한다.</summary>
    public sealed class SaveSlotTests
    {
        private string _directory;
        private OwnerModeManager _owner;
        private CareerManager _career;
        private ManagerHistoricalSaveJsonStore _ownerStore;
        private CareerSaveJsonStore _playerStore;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "UPlayBall-SaveSlots", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            GameBootstrap.EnsureRuntimeManagers();
            Assert.That(GameManager.Instance.TryGetManager(out _owner), Is.True);
            Assert.That(GameManager.Instance.TryGetManager(out _career), Is.True);
            _ownerStore = new ManagerHistoricalSaveJsonStore(Path.Combine(_directory, "owner.json"));
            _playerStore = new CareerSaveJsonStore(Path.Combine(_directory, "player.json"));
            SetField(_owner, "_saveStore", _ownerStore);
            SetField(_career, "_careerSaveStore", _playerStore);
            UiGameModeSession.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            UiGameModeSession.Clear();
            if (GameManager.HasInstance) Object.DestroyImmediate(GameManager.Instance.gameObject);
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void 다섯슬롯의저장과백업복구삭제는다른모드와슬롯을보존한다()
        {
            for (int slot = 1; slot <= SaveSlotPaths.SlotCount; slot++)
            {
                _ownerStore.ForSlot(slot).Save(OwnerData(slot));
                _playerStore.ForSlot(slot).SaveAtomic(PlayerData(slot));
            }
            var third = _playerStore.ForSlot(3);
            third.SaveAtomic(PlayerData(33));
            Assert.That(third.LoadBackup().summary.year, Is.EqualTo(3));
            third.PromoteBackupToPrimaryAtomic();
            Assert.That(third.LoadPrimary().summary.year, Is.EqualTo(3));
            third.DeleteAll();
            _ownerStore.ForSlot(4).ForSlot(3).Delete();
            for (int slot = 1; slot <= SaveSlotPaths.SlotCount; slot++)
            {
                if (slot == 3)
                {
                    Assert.That(_ownerStore.ForSlot(slot).Exists, Is.False);
                    Assert.That(_playerStore.ForSlot(slot).Exists, Is.False);
                    continue;
                }
                Assert.That(_ownerStore.ForSlot(slot).Load().managerMode.liveSeason.originYear, Is.EqualTo(slot));
                Assert.That(_playerStore.ForSlot(slot).LoadPrimary().summary.year, Is.EqualTo(slot));
                Assert.That(_playerStore.ForSlot(slot).BackupExists, Is.False);
            }
            Assert.That(_ownerStore.ForSlot(1).FilePath, Is.EqualTo(Path.Combine(_directory, "owner.json")));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ownerStore.ForSlot(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _playerStore.ForSlot(6));
        }

        [Test]
        public void 실제선수커리어도슬롯별로다른선수를복원하고백업을격리한다()
        {
            var configuration = NewGameConfiguration.CreateDefault();
            _career.BeginCareer(CreateCareer(configuration, "첫번째선수", 52001UL), configuration.Balance);
            AssertSuccess(_career.SaveCareer(2));
            _career.BeginCareer(CreateCareer(configuration, "두번째선수", 52002UL), configuration.Balance);
            AssertSuccess(_career.SaveCareer(5));
            AssertSuccess(_career.LoadCareer(2));
            Assert.That(_career.CurrentCareer.MyPlayer.Name, Is.EqualTo("첫번째선수"));
            Assert.That(_career.ActiveSaveSlot, Is.EqualTo(2));
            AssertSuccess(_career.SaveCareer(5));
            AssertSuccess(_career.RecoverCareerSaveBackup(5));
            Assert.That(_career.CurrentCareer.MyPlayer.Name, Is.EqualTo("두번째선수"));
            _career.InspectCareerSave(2);
            Assert.That(_career.ActiveSaveSlot, Is.EqualTo(5));
            AssertSuccess(_career.DeleteCareerSave(5));
            Assert.That(_career.HasCareerSaveInSlot(5), Is.False);
            AssertSuccess(_career.LoadCareer(2));
            Assert.That(_career.CurrentCareer.MyPlayer.Name, Is.EqualTo("첫번째선수"));
        }

        [Test]
        public void 실제구단주저장과불러오기만후속저장대상을바꾸고실패와조회는유지한다()
        {
            var flow = _owner.BeginNewGameFlow(2);
            flow.SelectTeam(flow.GetTeamCandidates()[0].TeamSeasonKey);
            var candidates = flow.GetMainCardCandidates();
            foreach (var card in candidates.Where(card => card.PlayerType == PlayerType.Batter)
                         .OrderBy(card => card.Cost).GroupBy(card => card.PlayerPersonId).Select(group => group.First()).Take(flow.Rule.MainHitterCount))
                flow.ToggleMainCard(card.CardId);
            foreach (var card in candidates.Where(card => card.PlayerType == PlayerType.Pitcher)
                         .OrderBy(card => card.Cost).GroupBy(card => card.PlayerPersonId).Select(group => group.First()).Take(flow.Rule.MainPitcherCount))
                flow.ToggleMainCard(card.CardId);
            flow.ContinueFromMainCards();
            flow.SelectFrontManager(FrontManagerIds.DefaultAnalysis);
            flow.SetProfile("저장검증구단", "슬롯검증구단주");
            Assert.That(_owner.CompleteNewGameFlow(), Is.True, _owner.LastError);
            Assert.That(_owner.ActiveSaveSlot, Is.EqualTo(2));
            Assert.That(_owner.HasSaveInSlot(1), Is.False);
            Assert.That(_owner.InspectSave(2).Summary.teamName, Is.EqualTo("저장검증구단"));
            _owner.Save(2);
            string second = File.ReadAllText(_ownerStore.ForSlot(2).FilePath);
            _owner.Save(4);
            _owner.InspectSave(2);
            Assert.That(_owner.ActiveSaveSlot, Is.EqualTo(4));
            _owner.Save();
            Assert.That(File.ReadAllText(_ownerStore.ForSlot(2).FilePath), Is.EqualTo(second));
            _owner.Load(2);
            Assert.That(_owner.ActiveSaveSlot, Is.EqualTo(2));
            var runtime = _owner.Runtime;
            File.WriteAllText(_ownerStore.ForSlot(3).FilePath, "broken");
            Assert.That(_owner.InspectSave(3).Status, Is.EqualTo(CareerSaveSlotStatus.Damaged));
            Assert.Throws<InvalidDataException>(() => _owner.Load(3));
            Assert.That(_owner.Runtime, Is.SameAs(runtime));
            Assert.That(_owner.ActiveSaveSlot, Is.EqualTo(2));
            Assert.That(_owner.HasAnySave, Is.True);
        }

        [Test]
        public void 선택한구단주슬롯만확인후삭제하고취소는포커스를복원한다()
        {
            _ownerStore.ForSlot(2).Save(OwnerData(2024));
            _ownerStore.ForSlot(5).Save(OwnerData(2025));
            var popup = UI_Popup_CareerSettings.ShowSaveLoadRuntime(UiGameMode.OwnerCareer);
            Click(popup, "Body/SaveSlot_5");
            Assert.That(_owner.ActiveSaveSlot, Is.EqualTo(1));
            Click(popup, "Body/DeleteOwnerSave");
            Assert.That(popup.TryHandleCancel(), Is.True);
            Assert.That(_owner.HasSaveInSlot(5), Is.True);
            if (EventSystem.current != null)
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("SaveSlot_5"));
            Click(popup, "Body/DeleteOwnerSave");
            string confirmation = popup.transform.Find("Content/SettingsPanel/PersistenceConfirmation/Message").GetComponent<Text>().text;
            Assert.That(confirmation, Does.Contain("슬롯 5"));
            Click(popup, "PersistenceConfirmation/Confirm");
            Assert.That(_owner.HasSaveInSlot(5), Is.False);
            Assert.That(_owner.HasSaveInSlot(2), Is.True);
            Assert.That(popup.transform.Find("Content/SettingsPanel/Body/NewOwnerCareer"), Is.Not.Null);
        }

        [TestCase(1280, 720, false)]
        [TestCase(1920, 1080, false)]
        [TestCase(2560, 1440, false)]
        [TestCase(3440, 1440, false)]
        [TestCase(1280, 720, true)]
        [TestCase(1920, 1080, true)]
        [TestCase(2560, 1440, true)]
        [TestCase(3440, 1440, true)]
        public void 슬롯화면은각해상도에서텍스트와선택영역이겹치지않는다(int width, int height, bool owner)
        {
            if (owner) _ownerStore.Save(OwnerData(2024));
            var host = new GameObject("SaveCanvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("SaveCamera", typeof(Camera));
            var texture = new RenderTexture(width, height, 24);
            RenderTexture previous = RenderTexture.active;
            try
            {
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                float scale = Mathf.Sqrt(width / 1920f * height / 1080f);
                var hostRect = host.GetComponent<RectTransform>();
                hostRect.sizeDelta = new Vector2(width / scale, height / scale);
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = hostRect.rect.height / 2;
                camera.targetTexture = texture;
                canvas.worldCamera = camera;
                var popup = UI_Popup_CareerSettings.ShowSaveLoadRuntime(owner ? UiGameMode.OwnerCareer : UiGameMode.PlayerCareer);
                popup.transform.SetParent(hostRect, false);
                CareerUiSkin.Apply(popup.transform);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = texture;
                var png = new Texture2D(width, height, TextureFormat.RGB24, false);
                png.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                png.Apply();
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../SaveSlotScreenshots"));
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, $"{(owner ? "owner" : "player")}-{width}x{height}.png"), png.EncodeToPNG());
                Object.DestroyImmediate(png);
                var panel = (RectTransform)popup.transform.Find("Content/SettingsPanel");
                var body = (RectTransform)panel.Find("Body");
                AssertContained(hostRect, panel);
                foreach (RectTransform child in body)
                    AssertContained(body, child);
                var texts = body.GetComponentsInChildren<Text>();
                foreach (Text text in texts)
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f), text.name + ": " + text.text);
                for (int a = 0; a < body.childCount; a++)
                for (int b = a + 1; b < body.childCount; b++)
                {
                    var first = (RectTransform)body.GetChild(a);
                    var second = (RectTransform)body.GetChild(b);
                    Assert.That(BoundsIn(body, first).Overlaps(BoundsIn(body, second)), Is.False, first.name + " / " + second.name);
                }
                foreach (Button button in body.GetComponentsInChildren<Button>())
                    Assert.That((button.targetGraphic as Image)?.sprite, Is.Not.Null, button.name);

            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3 min = parent.InverseTransformPoint(corners[0]);
            Vector3 max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertContained(RectTransform parent, RectTransform child)
        {
            Rect bounds = BoundsIn(parent, child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - 1), child.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + 1), child.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - 1), child.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + 1), child.name);
        }

        private static void Click(UI_Popup_CareerSettings popup, string path) =>
            popup.transform.Find("Content/SettingsPanel/" + path).GetComponent<Button>().onClick.Invoke();

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void AssertSuccess(CareerSaveCommandResult result) =>
            Assert.That(result.IsSuccess, Is.True, result.Message);

        private static CareerState CreateCareer(NewGameConfiguration configuration, string name, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity(name, "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(60, 55, 58, 50, 62, 54));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }

        private static CareerSaveData PlayerData(int year) => new CareerSaveData
        {
            summary = new CareerSaveSummaryData { playerName = "김선수", year = year }
        };

        private static ManagerHistoricalSaveData OwnerData(int year) => new ManagerHistoricalSaveData
        {
            saveVersion = ManagerHistoricalSaveAdapter.CurrentSaveVersion,
            playerTeamSeasonKey = "TEST",
            ownerProfile = new OwnerProfileSaveData { nickname = "서울구단주", clubName = "서울 챔피언스 베이스볼" },
            managerMode = new ManagerModeSaveData { liveSeason = new ManagerLiveSeasonSaveData
                { originYear = year, seasonNumber = 20, currentWeekIndex = 143 } }
        };
    }
}
