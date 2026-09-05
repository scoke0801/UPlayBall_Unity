using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 공용 UI Profile, Capability, Navigation, 상태 공급 계약을 검증한다.
    /// </summary>
    public sealed class SharedUiContractsTests
    {
        [Test]
        public void NavigationManifest_현재Capability에맞는항목만노출한다()
        {
            var manifest = new NavigationManifest(new[]
            {
                new NavigationEntry("Shared.League", "리그", UiCapability.CanViewLeagueInformation),
                new NavigationEntry("Owner.Scout", "스카우트", UiCapability.CanUseScout),
                new NavigationEntry("Player.Growth", "성장", UiCapability.CanViewCareerPlayerGrowth)
            });
            var capabilities = new UiCapabilitySet(
                UiCapability.CanViewLeagueInformation | UiCapability.CanViewCareerPlayerGrowth);

            IReadOnlyList<NavigationEntry> visible = manifest.GetVisibleEntries(capabilities);

            Assert.That(visible, Has.Count.EqualTo(2));
            Assert.That(visible[0].RouteId, Is.EqualTo("Shared.League"));
            Assert.That(visible[1].RouteId, Is.EqualTo("Player.Growth"));
        }

        [Test]
        public void NavigationManifest_하위Route를포함한중복을거부한다()
        {
            var child = new NavigationEntry("Shared.Records", "기록");

            Assert.Throws<ArgumentException>(() => new NavigationManifest(new[]
            {
                new NavigationEntry("Shared.League", "리그", children: new[] { child }),
                new NavigationEntry("Shared.Records", "중복 기록")
            }));
        }

        [Test]
        public void NavigationManifest_Depth3Route를거부한다()
        {
            var depthThree = new NavigationEntry(
                "Career.Records.Season",
                "정규시즌",
                children: new[] { new NavigationEntry("Career.Records.Season.All", "전체지표") });

            Assert.Throws<ArgumentException>(() => new NavigationManifest(new[]
            {
                new NavigationEntry("Career", "커리어", children: new[] { depthThree })
            }));
        }

        [Test]
        public void GameModeUiProfile_OldRoute를새LocalTarget으로변환한다()
        {
            var migrations = new NavigationRouteMigrationMap(new Dictionary<string, string>
            {
                ["Player.Growth"] = "Player.Profile.Growth"
            });
            var profile = new GameModeUiProfile(
                UiGameMode.PlayerCareer,
                "선수 모드",
                new NavigationManifest(new[]
                {
                    new NavigationEntry("Player.Profile", "선수", children: new[]
                    {
                        new NavigationEntry("Player.Profile.Growth", "성장")
                    })
                }),
                new UiCapabilitySet(UiCapability.None),
                routeMigrations: migrations);

            Assert.That(profile.ResolveRouteId("Player.Growth"), Is.EqualTo("Player.Profile.Growth"));
            Assert.That(profile.FindNavigationGroup("Player.Growth").RouteId, Is.EqualTo("Player.Profile"));
        }

        [Test]
        public void GameModeNavigationState_ContextBack이진입원점과Local선택을복원한다()
        {
            GameModeUiProfile profile = CreateNavigationStateProfile();
            var state = new GameModeNavigationState(profile, "Owner.Home");

            Assert.That(state.Navigate("Shared.League.Schedule"), Is.EqualTo("Shared.League.Schedule"));
            Assert.That(state.OpenContext("Owner.MatchCenter"), Is.EqualTo("Owner.MatchCenter.Analysis"));
            Assert.That(state.NavigateContext("Owner.MatchCenter.Lineup"), Is.EqualTo("Owner.MatchCenter.Lineup"));

            Assert.That(state.TryBack(out string returnRoute), Is.True);
            Assert.That(returnRoute, Is.EqualTo("Shared.League.Schedule"));
            Assert.That(state.Navigate("Shared.League"), Is.EqualTo("Shared.League.Schedule"));
        }

        [Test]
        public void SharedGameShellPresenter_모드이름분기없이Profile과상태를바인딩한다()
        {
            var view = new FakeShellView();
            var provider = new FakeStatusProvider(CreateStatus("서울 웨이브스"));
            GameModeUiProfile profile = CreatePlayerProfile();

            using (var presenter = new SharedGameShellPresenter(view, profile, provider))
            {
                Assert.That(view.BoundProfile, Is.SameAs(profile));
                Assert.That(view.BoundStatus.TeamName, Is.EqualTo("서울 웨이브스"));

                provider.SetStatus(CreateStatus("부산 마리너스"));

                Assert.That(view.BoundStatus.TeamName, Is.EqualTo("부산 마리너스"));
            }
        }

        [Test]
        public void SharedGameShellPresenter_비활성또는권한없는Route요청을차단한다()
        {
            var view = new FakeShellView();
            var provider = new FakeStatusProvider(CreateStatus("서울 웨이브스"));
            GameModeUiProfile profile = CreatePlayerProfile();
            var requestedRoutes = new List<string>();

            using (var presenter = new SharedGameShellPresenter(view, profile, provider))
            {
                presenter.NavigationRequested += requestedRoutes.Add;

                view.RequestNavigation("Player.Home");
                view.RequestNavigation("Owner.Scout");
                view.RequestNavigation("Player.Locked");
            }

            Assert.That(requestedRoutes, Is.EqualTo(new[] { "Player.Home" }));
        }

        [Test]
        public void SharedGameShellPresenter_Back요청을Router에전달한다()
        {
            var view = new FakeShellView();
            var statusProvider = new FakeStatusProvider(CreateStatus("서울 코멧츠"));
            using var presenter = new SharedGameShellPresenter(view, CreatePlayerProfile(), statusProvider);
            int requestCount = 0;
            presenter.BackRequested += () => requestCount++;

            view.RequestBack();

            Assert.That(requestCount, Is.EqualTo(1));
        }

        [Test]
        public void UiContentStateModel_ActionId와Label을쌍으로요구한다()
        {
            Assert.Throws<ArgumentException>(() =>
                UiContentStateModel.CreateError("불러오기 실패", "네트워크 오류", "Retry"));

            UiContentStateModel state = UiContentStateModel.CreateEmpty(
                "선수가 없습니다", "필터를 변경하세요.", "ClearFilter", "필터 초기화");

            Assert.That(state.Kind, Is.EqualTo(UiContentStateKind.Empty));
            Assert.That(state.ActionId, Is.EqualTo("ClearFilter"));
            Assert.That(state.ActionLabel, Is.EqualTo("필터 초기화"));
        }

        [Test]
        public void UiGameModeSession_동시에하나의모드만선택하고변경을알린다()
        {
            var changes = new List<UiGameMode?>();
            UiGameModeSession.Clear();
            UiGameModeSession.ModeChanged += changes.Add;
            try
            {
                UiGameModeSession.Select(UiGameMode.PlayerCareer);
                UiGameModeSession.Select(UiGameMode.PlayerCareer);
                UiGameModeSession.Select(UiGameMode.OwnerCareer);

                Assert.That(UiGameModeSession.IsSelected(UiGameMode.PlayerCareer), Is.False);
                Assert.That(UiGameModeSession.IsSelected(UiGameMode.OwnerCareer), Is.True);
                Assert.That(changes, Is.EqualTo(new UiGameMode?[]
                {
                    UiGameMode.PlayerCareer,
                    UiGameMode.OwnerCareer
                }));
            }
            finally
            {
                UiGameModeSession.ModeChanged -= changes.Add;
                UiGameModeSession.Clear();
            }
        }

        [Test]
        public void UiGameModeSession_정의되지않은모드를거부한다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                UiGameModeSession.Select((UiGameMode)999));
        }

        [Test]
        public void UiGameModeSession_두Runtime이공존하면임의선택하지않는다()
        {
            UiGameMode? inferred = UiGameModeSession.InferInitialMode(
                null,
                hasActivePlayerCareer: true,
                hasActiveOwnerRuntime: true);

            Assert.That(inferred, Is.Null);
        }

        [TestCase(true, false, UiGameMode.PlayerCareer)]
        [TestCase(false, true, UiGameMode.OwnerCareer)]
        public void UiGameModeSession_단일활성Runtime만복원한다(
            bool hasPlayer,
            bool hasOwner,
            UiGameMode expected)
        {
            Assert.That(
                UiGameModeSession.InferInitialMode(null, hasPlayer, hasOwner),
                Is.EqualTo(expected));
        }

        [Test]
        public void UiGameModeSession_선택한Runtime이사라지면다른모드를임의선택하지않는다()
        {
            Assert.That(
                UiGameModeSession.InferInitialMode(
                    UiGameMode.PlayerCareer,
                    hasActivePlayerCareer: false,
                    hasActiveOwnerRuntime: true),
                Is.Null);
        }

        [Test]
        public void UiGameModeSession_선택한Runtime이사라지면세션을비운다()
        {
            UiGameModeSession.Clear();
            UiGameModeSession.Select(UiGameMode.PlayerCareer);
            try
            {
                UiGameMode? resolved = UiGameModeSession.ResolveInitialMode(
                    hasActivePlayerCareer: false,
                    hasActiveOwnerRuntime: true);

                Assert.That(resolved, Is.Null);
                Assert.That(UiGameModeSession.CurrentMode, Is.Null);
            }
            finally
            {
                UiGameModeSession.Clear();
            }
        }

        private static GameModeUiProfile CreatePlayerProfile()
        {
            var manifest = new NavigationManifest(new[]
            {
                new NavigationEntry("Player.Home", "홈"),
                new NavigationEntry("Owner.Scout", "스카우트", UiCapability.CanUseScout),
                new NavigationEntry("Player.Locked", "준비 중", isEnabled: false, disabledReason: "백엔드 미연결")
            });
            return new GameModeUiProfile(
                UiGameMode.PlayerCareer,
                "선수 모드",
                manifest,
                new UiCapabilitySet(UiCapability.CanViewCareerPlayerGrowth));
        }

        private static GameModeUiProfile CreateNavigationStateProfile()
        {
            return new GameModeUiProfile(
                UiGameMode.OwnerCareer,
                "구단주 모드",
                new NavigationManifest(new[]
                {
                    new NavigationEntry("Owner.Home", "홈"),
                    new NavigationEntry("Shared.League", "리그", children: new[]
                    {
                        new NavigationEntry("Shared.League.Schedule", "일정"),
                        new NavigationEntry("Shared.League.Records", "기록")
                    })
                }),
                new UiCapabilitySet(UiCapability.None),
                new NavigationManifest(new[]
                {
                    new NavigationEntry("Owner.MatchCenter", "Match Center", children: new[]
                    {
                        new NavigationEntry("Owner.MatchCenter.Analysis", "상대 분석"),
                        new NavigationEntry("Owner.MatchCenter.Lineup", "우리 라인업")
                    })
                }));
        }

        private static ShellStatusModel CreateStatus(string teamName)
        {
            return new ShellStatusModel("2028", "4월 3주", "Premier", teamName, "3위", "다음 경기 D-1");
        }

        private sealed class FakeShellView : ISharedGameShellView
        {
            public event Action<string> NavigationRequested;
            public event Action BackRequested;

            public GameModeUiProfile BoundProfile { get; private set; }
            public ShellStatusModel BoundStatus { get; private set; }
            public ShellContextModel BoundContext { get; private set; }

            public void BindProfile(GameModeUiProfile profile)
            {
                BoundProfile = profile;
            }

            public void BindStatus(ShellStatusModel status)
            {
                BoundStatus = status;
            }

            public void BindContext(ShellContextModel context)
            {
                BoundContext = context;
            }

            public void RequestNavigation(string routeId)
            {
                NavigationRequested?.Invoke(routeId);
            }

            public void RequestBack()
            {
                BackRequested?.Invoke();
            }
        }

        private sealed class FakeStatusProvider : UiShellStatusProviderBase
        {
            private ShellStatusModel _status;

            public FakeStatusProvider(ShellStatusModel status)
            {
                _status = status;
            }

            public override ShellStatusModel GetCurrentStatus()
            {
                return _status;
            }

            public void SetStatus(ShellStatusModel status)
            {
                _status = status;
                NotifyStatusChanged();
            }
        }
    }
}
