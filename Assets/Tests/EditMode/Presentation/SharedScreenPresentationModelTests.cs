using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 공용 정보 화면이 같은 Snapshot에 모드별 Action만 합성하는지 검증한다.
    /// </summary>
    public sealed class SharedScreenPresentationModelTests
    {
        [Test]
        public void SameSnapshot_모드별Provider만다른Action을공급한다()
        {
            var snapshot = new RecordsScreenSnapshot(
                "2028", "Premier", "전체", "타격", CreateRecordTable());
            var profile = new SharedScreenProfile(
                "Records.Season", "기록", SharedScreenKind.SeasonRecords,
                UiCapability.CanViewSeasonRecords);
            var context = new SharedScreenContext("Records.Season", focusedEntityId: "player-17");

            var ownerModel = new SharedScreenPresentationModel<RecordsScreenSnapshot>(
                profile,
                context,
                snapshot,
                UiContentStateModel.Ready,
                new UiCapabilitySet(UiCapability.CanViewSeasonRecords | UiCapability.CanEditLineup),
                new StubActionProvider(new SharedScreenActionModel(
                    "OpenRoster", "선수단 배치", requiredCapability: UiCapability.CanEditLineup)));
            var playerModel = new SharedScreenPresentationModel<RecordsScreenSnapshot>(
                profile,
                context,
                snapshot,
                UiContentStateModel.Ready,
                new UiCapabilitySet(UiCapability.CanViewSeasonRecords | UiCapability.CanViewManagerDecisionReason),
                new StubActionProvider(new SharedScreenActionModel(
                    "ViewRole", "기용 이유", requiredCapability: UiCapability.CanViewManagerDecisionReason)));

            Assert.That(ownerModel.Snapshot, Is.SameAs(playerModel.Snapshot));
            Assert.That(ownerModel.Actions[0].ActionId, Is.EqualTo("OpenRoster"));
            Assert.That(playerModel.Actions[0].ActionId, Is.EqualTo("ViewRole"));
        }

        [Test]
        public void PresentationModel_Capability가없는Action을노출하지않는다()
        {
            var profile = new SharedScreenProfile("Records.Season", "기록", SharedScreenKind.SeasonRecords);
            var context = new SharedScreenContext("Records.Season");
            var provider = new StubActionProvider(
                new SharedScreenActionModel("View", "보기"),
                new SharedScreenActionModel("Edit", "편집", requiredCapability: UiCapability.CanEditLineup));

            var model = new SharedScreenPresentationModel<RecordsScreenSnapshot>(
                profile,
                context,
                new RecordsScreenSnapshot("2028", "Premier", "전체", "타격", CreateRecordTable()),
                UiContentStateModel.Ready,
                UiCapabilitySet.None,
                provider);

            Assert.That(model.Actions, Has.Count.EqualTo(1));
            Assert.That(model.Actions[0].ActionId, Is.EqualTo("View"));
        }

        [Test]
        public void PresentationModel_Loading은Snapshot없이생성하고Ready는거부한다()
        {
            var profile = new SharedScreenProfile("Team.Roster", "선수단", SharedScreenKind.TeamRoster);
            var context = new SharedScreenContext("Team.Roster");
            var provider = new StubActionProvider();

            var loading = new SharedScreenPresentationModel<ReadOnlyRosterModel>(
                profile,
                context,
                null,
                UiContentStateModel.CreateLoading("선수단 불러오는 중"),
                UiCapabilitySet.None,
                provider);

            Assert.That(loading.ContentState.Kind, Is.EqualTo(UiContentStateKind.Loading));
            Assert.Throws<ArgumentNullException>(() => new SharedScreenPresentationModel<ReadOnlyRosterModel>(
                profile, context, null, UiContentStateModel.Ready, UiCapabilitySet.None, provider));
        }

        [Test]
        public void ReadOnlyRoster_입력목록변경과무관한불변Snapshot을유지한다()
        {
            var sourcePlayers = new List<ReadOnlyRosterPlayerModel>
            {
                new ReadOnlyRosterPlayerModel(
                    "player-17", "김하늘", "SS", "주전", "82", "좋음", ".312",
                    visualState: RosterPlayerVisualState.Highlighted,
                    highlightReason: "오늘 5번 타자")
            };
            var group = new ReadOnlyRosterGroupModel("StartingLineup", "주전 타순", sourcePlayers);
            var roster = new ReadOnlyRosterModel("team-1", "서울 웨이브스", "2028", "25명", new[] { group });

            sourcePlayers.Clear();

            Assert.That(roster.IsReadOnly, Is.True);
            Assert.That(roster.Groups[0].Players, Has.Count.EqualTo(1));
            Assert.That(roster.Groups[0].Players[0].HighlightReason, Is.EqualTo("오늘 5번 타자"));
        }

        private static RecordTableModel CreateRecordTable()
        {
            return new RecordTableModel(
                new[] { new RecordTableColumnModel("AVG", "타율", RecordSortValueKind.Number) },
                new[]
                {
                    new RecordTableRowModel("player-17", new[]
                    {
                        new RecordTableCellModel("AVG", ".312", RecordSortValue.FromNumber(0.312d))
                    }, true, "내 선수")
                });
        }

        private sealed class StubActionProvider : ISharedScreenActionProvider
        {
            private readonly SharedScreenActionModel[] _actions;

            public StubActionProvider(params SharedScreenActionModel[] actions)
            {
                _actions = actions ?? Array.Empty<SharedScreenActionModel>();
            }

            public IReadOnlyList<SharedScreenActionModel> GetActions(SharedScreenContext context)
            {
                return _actions;
            }

            public bool TryExecute(string actionId, SharedScreenContext context)
            {
                return true;
            }
        }
    }
}
