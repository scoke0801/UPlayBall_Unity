using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Baseball.Core.Historical;
using Baseball.Game.Guide;
using Baseball.Game.Historical;
using Baseball.Game.Unity.Persistence;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Match;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed partial class OwnerNavigationRefreshTests
    {
        [Test]
        public void Guide_숨김캐시가같아도비활성패널과빈리포트접근을복원한다()
        {
            Navigate(OwnerNavigationRoutes.Home);
            var view = (UI_System_OwnerGuide)GetField(_coordinator, "_ownerGuide");
            view.Bind(new GuideProgressState(), "");
            view.gameObject.SetActive(false);
            SetField(_coordinator, "_guideWasSuppressed", false);

            Invoke(_coordinator, "UpdateGuideSuppression");

            Assert.That(view.gameObject.activeInHierarchy, Is.True);
            Assert.That(((UnityEngine.UI.Button)GetField(view, "_review")).gameObject.activeInHierarchy, Is.True);
            Assert.That(GetField(_coordinator, "_ownerGuide"), Is.SameAs(view));
        }

        [Test]
        public void Guide_저장복원후숨김조건이같아도새런타임에다시바인딩한다()
        {
            Navigate(OwnerNavigationRoutes.Home);
            ConfigureGuideSave(false);
            _manager.ChangeGuideProgress(progress => progress.MarkAllReportsRead());
            var store = (ManagerHistoricalSaveJsonStore)GetField(_manager, "_saveStore");
            var adapter = (ManagerHistoricalSaveAdapter)GetField(_manager, "_saveAdapter");
            var loaded = adapter.Restore(store.Load());
            typeof(OwnerModeManager).GetProperty("Runtime").SetValue(_manager, loaded);
            SetField(_coordinator, "_guideWasSuppressed", false);

            Invoke(_coordinator, "UpdateGuideSuppression");

            var view = (UI_System_OwnerGuide)GetField(_coordinator, "_ownerGuide");
            Assert.That(view.gameObject.activeInHierarchy, Is.True);
            Assert.That(GetField(_coordinator, "_guideRuntime"), Is.SameAs(loaded));
            Assert.That(GetField(view, "_progress"), Is.SameAs(loaded.GuideProgress));
        }

        [Test]
        public void Guide_미지원이동은홈으로대체하거나온보딩을완료하지않는다()
        {
            var before = _manager.Runtime.Onboarding.IsCompleted;
            bool result = (bool)_coordinator.GetType().GetMethod("HandleGuideRouteRequested", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_coordinator, new object[] { GuideCtaAction.OpenTradeInquiry, "owner-first-entry" });
            Assert.That(result, Is.False);
            Assert.That(_manager.Runtime.Onboarding.IsCompleted, Is.EqualTo(before));
        }

        [Test]
        public void Guide_실제슬롯선택은카드신원을검증하며배치를변경하지않는다()
        {
            Navigate(OwnerNavigationRoutes.RosterLineup);
            var expansion = (OwnerExpansionWorkspaceCoordinator)GetField(_coordinator, "_expansionWorkspace");
            var preset = _manager.Runtime.ManagerMode.GetSelectedLineupPreset();
            var goal = new GuideGoal("slot", GuideGoalKind.PresetIssue, GuideTargetKind.PresetSlot, true, "",
                LineupPresetAssignmentGroup.StarterRotation, 0, preset.StarterRotationCardIds[0], preset.PresetId);
            Assert.That(expansion.TrySelectGuideTarget(goal, out RectTransform target), Is.True);
            Assert.That(target.gameObject.activeInHierarchy, Is.True);
            Assert.That(_manager.Runtime.ManagerMode.GetSelectedLineupPreset(), Is.SameAs(preset));
            var stale = new GuideGoal("slot", GuideGoalKind.PresetIssue, GuideTargetKind.PresetSlot, true, "",
                LineupPresetAssignmentGroup.StarterRotation, 0, "different-season-card", preset.PresetId);
            Assert.That(expansion.TrySelectGuideTarget(stale, out _), Is.False);
        }

        [Test]
        public void Guide_변경안이있으면이동을멈추고초안을보존한다()
        {
            Navigate(OwnerNavigationRoutes.RosterLineup);
            var preset = _manager.Runtime.ManagerMode.GetSelectedLineupPreset();
            SetField(_coordinator, "_pendingLineupPreset", preset);
            bool result = (bool)_coordinator.GetType().GetMethod("NavigateGuideRoute", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_coordinator, new object[] { OwnerNavigationRoutes.MatchCenterAnalysis });
            Assert.That(result, Is.False);
            Assert.That(GetField(_coordinator, "_pendingLineupPreset"), Is.SameAs(preset));
            Assert.That(_coordinator.LastGuideArrival, Is.EqualTo(GuideArrivalStatus.Cancelled));
        }

        [Test]
        public void Guide_실제저장복원으로보류상태를유지한다()
        {
            ConfigureGuideSave(false);
            var state = _manager.RefreshGuideProgress();
            _manager.ChangeGuideProgress(progress => progress.Snooze("preparation", 99));
            var store = (ManagerHistoricalSaveJsonStore)GetField(_manager, "_saveStore");
            var adapter = (ManagerHistoricalSaveAdapter)GetField(_manager, "_saveAdapter");
            var loaded = adapter.Restore(store.Load());
            Assert.That(loaded.GuideProgress.GetStatus("preparation"), Is.EqualTo(GuideGoalStatus.Snoozed));
            Assert.That(loaded.GuideProgress.Scope, Is.EqualTo(state.Scope));
        }

        [Test]
        public void Guide_저장실패는유지선택과보류를성공으로남기지않는다()
        {
            ConfigureGuideSave(true);
            _manager.RefreshGuideProgress();
            var before = _manager.Runtime.GuideProgress.GetStatus("preparation");
            Assert.Catch(() => _manager.ChangeGuideProgress(progress => progress.Snooze("preparation", 99)));
            Assert.That(_manager.Runtime.GuideProgress.GetStatus("preparation"), Is.EqualTo(before));
        }

        [Test]
        public void Guide_선수모드에는구단운영이동권한을제공하지않는다()
        {
            UiGameModeSession.Select(UiGameMode.PlayerCareer);
            bool result = (bool)_coordinator.GetType().GetMethod("CanRouteGuideAction", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_coordinator, new object[] { GuideCtaAction.OpenRoster });
            Assert.That(result, Is.False);
        }

        private void ConfigureGuideSave(bool fail)
        {
            var provider = (IHistoricalContentProvider)GetField(_manager, "_contentProvider");
            SetField(_manager, "_saveAdapter", new ManagerHistoricalSaveAdapter(provider, CardEditionBalanceTable.CreateInitial()));
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../GuideSaveTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            SetField(_manager, "_saveStore", new ManagerHistoricalSaveJsonStore(fail ? directory : Path.Combine(directory, "save.json")));
        }

        [Test]
        public void Guide_빠른화면전환으로늦어진도착은완료하지않는다()
        {
            ConfigureGuideSave(false);
            _manager.RefreshGuideProgress();
            var goal = new GuideGoal("preparation", GuideGoalKind.Preparation, GuideTargetKind.Analysis, false, "");
            Invoke(_coordinator, "OpenMatchCenter", OwnerNavigationRoutes.MatchCenterAnalysis);
            var request = (int)GetField(_coordinator, "_guideRequest");
            var arrival = (IEnumerator)_coordinator.GetType().GetMethod("CompleteGuideArrival", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_coordinator, new object[] { goal, request, OwnerNavigationRoutes.MatchCenterAnalysis, _manager.Runtime });
            Assert.That(arrival.MoveNext(), Is.True);
            Navigate(OwnerNavigationRoutes.Home);
            Assert.That(arrival.MoveNext(), Is.False);
            Assert.That(_coordinator.LastGuideArrival, Is.EqualTo(GuideArrivalStatus.Cancelled));
            Assert.That(_manager.Runtime.GuideProgress.GetStatus("preparation"), Is.Not.EqualTo(GuideGoalStatus.Resolved));
        }

        [Test]
        public void Guide_리포트도착은실제본문준비후저장된다()
        {
            ConfigureGuideSave(false);
            _manager.RefreshGuideProgress();
            var goal = new GuideGoal("preparation", GuideGoalKind.Preparation, GuideTargetKind.Analysis, false, "");
            Invoke(_coordinator, "OpenMatchCenter", OwnerNavigationRoutes.MatchCenterAnalysis);
            var arrival = (IEnumerator)_coordinator.GetType().GetMethod("CompleteGuideArrival", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_coordinator, new object[] { goal, (int)GetField(_coordinator, "_guideRequest"),
                    OwnerNavigationRoutes.MatchCenterAnalysis, _manager.Runtime });
            Assert.That(arrival.MoveNext(), Is.True);
            Assert.That(arrival.MoveNext(), Is.False);
            Assert.That(_coordinator.LastGuideArrival, Is.EqualTo(GuideArrivalStatus.TargetReady));
            Assert.That(_manager.Runtime.GuideProgress.GetStatus("preparation"), Is.EqualTo(GuideGoalStatus.Resolved));
            var store = (ManagerHistoricalSaveJsonStore)GetField(_manager, "_saveStore");
            Assert.That(GuideProgressState.Restore(store.Load().guideProgress).GetStatus("preparation"), Is.EqualTo(GuideGoalStatus.Resolved));
        }

        [Test]
        public void Guide_같은입력의실제세경기는안내유무와관계없이모든이벤트가같다()
        {
            var baseline = CreateRuntime(out IHistoricalContentProvider provider);
            var service = new ManagerModeMatchService(provider, _manager.Balance);
            var currentService = (ManagerModeMatchService)GetField(_manager, "_matchService");
            for (int game = 0; game < 3; game++)
            {
                var progress = _manager.RefreshGuideProgress();
                progress.Track("preparation"); progress.Snooze("preparation", 99);
                var first = new MatchEventBuffer(); var second = new MatchEventBuffer();
                var profile = new MatchExecutionProfile(SimulationEngineKind.Detailed, MatchDecisionMode.InternalAiOnly,
                    MatchEventMode.Full, MatchDecisionTraceMode.None, MatchStatisticsMode.FullBoxScore);
                var guided = currentService.PlayNextGame(_manager.Runtime, first, profile);
                var plain = service.PlayNextGame(baseline, second, profile);
                Assert.That(first.Count, Is.GreaterThan(0));
                CollectionAssert.AreEqual(second.ToArray(), first.ToArray());
                Assert.That(guided.Match.HomeBoxScore.Runs, Is.EqualTo(plain.Match.HomeBoxScore.Runs));
                Assert.That(guided.Match.AwayBoxScore.Runs, Is.EqualTo(plain.Match.AwayBoxScore.Runs));
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        public void Guide_홈에서만표시하고선수단에서는공간을반환한다(int width, int height)
        {
            var canvas = _root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)_root.transform).sizeDelta = new Vector2(width, height);
            Navigate(OwnerNavigationRoutes.Home);
            var view = (UI_System_OwnerGuide)GetField(_coordinator, "_ownerGuide");
            view.SetOpen(true);
            Assert.That(view.IsOpen, Is.True);
            Navigate(OwnerNavigationRoutes.RosterLineup);
            Invoke(_coordinator, "UpdateGuideSuppression");
            Canvas.ForceUpdateCanvases();
            var cameraObject = new GameObject("GuideRosterCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                camera.orthographicSize = height / 2f; camera.transform.position = new Vector3(0, 0, -100);
                camera.nearClipPlane = .1f; camera.farClipPlane = 500; camera.targetTexture = target;
                canvas.worldCamera = camera; Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../GuideScreenshots"));
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, $"roster-guide-{width}x{height}.png"), pixels.EncodeToPNG());
                Assert.That(view.IsOpen, Is.False);
                Assert.That(_shell.transform.Find("FrontManagerHost").gameObject.activeSelf, Is.False);
                Assert.That(_shell.MainWorkspaceHost.rect.height, Is.GreaterThan(0));
                Navigate(OwnerNavigationRoutes.Home);
                Invoke(_coordinator, "UpdateGuideSuppression");
                Assert.That(_shell.transform.Find("FrontManagerHost").gameObject.activeSelf, Is.True);
                view.SetOpen(true);
                Canvas.ForceUpdateCanvases(); camera.Render();
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, $"home-guide-{width}x{height}.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
