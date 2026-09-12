using System;
using System.Reflection;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>보유 카드 Collection의 검색·정렬·선택이 읽기 전용 계약을 지키는지 검증한다.</summary>
    public sealed class OwnerCollectionPresentationTests
    {
        [Test]
        public void Builder_결측포지션도지명타자로표시하고검색한다()
        {
            var snapshot = new OwnerCollectionSnapshot(new[]
            {
                new OwnerCollectionCardSnapshot("UNKNOWN", "PERSON-1", "가상미확인", 1994,
                    PlayerPosition.DesignatedHitter, 3, PlayerCardEdition.Normal, 0, 0, false, false,
                    isPositionEvidenceMissing: true),
                new OwnerCollectionCardSnapshot("DH", "PERSON-2", "가상지명", 1994,
                    PlayerPosition.DesignatedHitter, 3, PlayerCardEdition.Normal, 0, 0, false, false)
            });

            var unknown = OwnerCollectionPresentationBuilder.Build(snapshot, "미확인");
            var designated = OwnerCollectionPresentationBuilder.Build(snapshot, "지명타자");

            Assert.That(unknown.Cards.Count, Is.EqualTo(1));
            Assert.That(unknown.Cards[0].Snapshot.CardId, Is.EqualTo("UNKNOWN"));
            Assert.That(unknown.Cards[0].MiniCard.PositionLabel, Is.EqualTo("지명타자"));
            Assert.That(designated.Cards.Count, Is.EqualTo(2));
            Assert.That(designated.Cards, Has.All.Matches<OwnerCollectionCardModel>(
                card => card.MiniCard.PositionLabel == "지명타자"));
        }

        [Test]
        public void Builder_이름포지션CostEdition을검색하고정렬한다()
        {
            OwnerCollectionSnapshot snapshot = CreateSnapshot();

            OwnerCollectionPresentationModel byPosition = OwnerCollectionPresentationBuilder.Build(
                snapshot, "투수", OwnerCollectionSort.Cost);
            OwnerCollectionPresentationModel byEdition = OwnerCollectionPresentationBuilder.Build(
                snapshot, string.Empty, OwnerCollectionSort.Edition);

            Assert.That(byPosition.CountText, Is.EqualTo("검색 결과 2/4장"));
            Assert.That(byPosition.Cards[0].Snapshot.DisplayName, Is.EqualTo("김마무리"));
            Assert.That(byPosition.Cards[0].MiniCard.PositionLabel, Is.EqualTo("마무리"));
            Assert.That(byEdition.Cards[0].MiniCard.EditionLabel, Is.EqualTo("MVP"));
            Assert.That(byEdition.Cards[1].MiniCard.EditionLabel, Is.EqualTo("골든글러브"));
        }

        [TestCase(PitcherRole.Setup, "셋업")]
        [TestCase(PitcherRole.Closer, "마무리")]
        public void Builder_투수NaturalRole을카드와검색에노출한다(PitcherRole role, string label)
        {
            var snapshot = new OwnerCollectionSnapshot(new[]
            {
                new OwnerCollectionCardSnapshot(
                    "CARD", "PERSON", "가상투수", 2025, PlayerPosition.ReliefPitcher, 7,
                    PlayerCardEdition.Normal, 0, 0, false, false, pitcherRole: role)
            });

            OwnerCollectionPresentationModel model = OwnerCollectionPresentationBuilder.Build(snapshot, label);

            Assert.That(model.Cards, Has.Count.EqualTo(1));
            Assert.That(model.Cards[0].MiniCard.PositionLabel, Is.EqualTo(label));
        }

        [Test]
        public void Builder_실제소유상태를공용MiniCard상태로표시한다()
        {
            OwnerCollectionPresentationModel model = OwnerCollectionPresentationBuilder.Build(CreateSnapshot());
            OwnerCollectionCardModel favorite = FindCard(model, "CARD-RP");

            Assert.That(favorite.MiniCard, Is.TypeOf<PlayerMiniCardModel>());
            Assert.That(favorite.MiniCard.StatusLabel, Does.Contain("즐겨찾기"));
            Assert.That(favorite.MiniCard.StatusLabel, Does.Contain("잠금"));
            Assert.That(favorite.MiniCard.StatusLabel, Does.Contain("+2"));
            Assert.That(favorite.MiniCard.StatusLabel, Does.Contain("중복 3"));
            Assert.That(favorite.MiniCard.VisualState, Is.EqualTo(PlayerMiniCardVisualState.Highlighted));
        }

        [Test]
        public void OwnerCardFilters_같은Franchise의연도별구단을하나의옵션으로묶는다()
        {
            var root = new GameObject("OwnerCardFiltersTestRoot", typeof(RectTransform));
            try
            {
                var cards = new[]
                {
                    new OwnerCollectionCardSnapshot(
                        "MBC-1982", "PERSON-1", "가상선수1", 1982, PlayerPosition.Catcher, 3,
                        PlayerCardEdition.Normal, 0, 0, false, false,
                        teamDisplayName: "1982 MBC 청룡",
                        originFranchiseId: "FRANCHISE-LG",
                        franchiseHistoryDisplayName: "MBC 청룡 & LG"),
                    new OwnerCollectionCardSnapshot(
                        "LG-2025", "PERSON-2", "가상선수2", 2025, PlayerPosition.FirstBase, 4,
                        PlayerCardEdition.Normal, 0, 0, false, false,
                        teamDisplayName: "2025 LG 트윈스",
                        originFranchiseId: "FRANCHISE-LG",
                        franchiseHistoryDisplayName: "MBC 청룡 & LG"),
                    new OwnerCollectionCardSnapshot(
                        "OB-1982", "PERSON-3", "가상선수3", 1982, PlayerPosition.SecondBase, 4,
                        PlayerCardEdition.Normal, 0, 0, false, false,
                        teamDisplayName: "1982 OB 베어스",
                        originFranchiseId: "FRANCHISE-DOOSAN",
                        franchiseHistoryDisplayName: "OB & 두산")
                };
                Type filterType = Assembly.Load("Baseball.Presentation")
                    .GetType("Baseball.Presentation.Owner.OwnerCardFilters", true);
                object filters = Activator.CreateInstance(filterType, true);
                int changeCount = 0;
                filterType.GetMethod("Build").Invoke(
                    filters,
                    new object[] { root.transform, cards, (Action)(() => changeCount++) });
                Dropdown dropdown = root.transform.Find("TeamFilter").GetComponent<Dropdown>();

                Assert.That(dropdown.options, Has.Count.EqualTo(3));
                Assert.That(dropdown.options.Exists(option => option.text == "MBC 청룡 & LG"), Is.True);
                Assert.That(dropdown.options.Exists(option => option.text.Contains("1982")), Is.False);
                int lgOption = dropdown.options.FindIndex(option => option.text == "MBC 청룡 & LG");
                dropdown.value = lgOption;

                MethodInfo matches = filterType.GetMethod("Matches");
                Assert.That(matches.Invoke(filters, new object[] { cards[0] }), Is.True);
                Assert.That(matches.Invoke(filters, new object[] { cards[1] }), Is.True);
                Assert.That(matches.Invoke(filters, new object[] { cards[2] }), Is.False);
                Assert.That(changeCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void View_검색결과를공용MiniCard로그리고선택카드Action을전달한다()
        {
            var root = new GameObject("OwnerCollectionTestRoot", typeof(RectTransform));
            UI_Scene_OwnerCollection view = null;
            try
            {
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                view = UI_Scene_OwnerCollection.CreateRuntime(
                    shell.MainWorkspaceHost, shell.RightInspectorHost, shell.ContextActionBarHost);
                view.Bind(CreateSnapshot());

                InputField search = shell.transform.Find(
                    "MainWorkspaceHost/OwnerCollectionWorkspace/CollectionPanel/ContentSafeRect/FilterBar/SearchField")
                    .GetComponent<InputField>();
                search.text = "김마무리";
                PlayerMiniCardView[] cards = shell.MainWorkspaceHost.GetComponentsInChildren<PlayerMiniCardView>();
                Assert.That(cards, Has.Length.EqualTo(1));

                cards[0].GetComponent<Button>().onClick.Invoke();
                Text inspector = shell.transform.Find(
                    "OptionalRightInspector/OwnerCollectionInspector/SelectedCardPanel/ContentSafeRect/SelectedCardDetails")
                    .GetComponent<Text>();
                Assert.That(inspector.text, Does.Contain("김마무리"));
                Assert.That(inspector.text, Does.Contain("골든글러브"));
                Button enhancement = FindButton(shell.transform,
                    "ContextActionBar/OwnerCollectionActionBar/Enhancement");
                Button sale = FindButton(shell.transform,
                    "ContextActionBar/OwnerCollectionActionBar/Sale");
                string enhancedId = null;
                string soldId = null;
                view.EnhancementRequested += id => enhancedId = id;
                view.DuplicateSaleRequested += id => soldId = id;
                enhancement.onClick.Invoke();
                Assert.That(enhancedId, Is.Null, "첫 클릭은 미리보기다.");
                enhancement.onClick.Invoke();
                sale.onClick.Invoke();
                Assert.That(enhancedId, Is.EqualTo("CARD-RP"));
                Assert.That(soldId, Is.EqualTo("CARD-RP"));
            }
            finally
            {
                if (view != null) Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(root);
            }
        }

        private static OwnerCollectionCardModel FindCard(OwnerCollectionPresentationModel model, string cardId)
        {
            for (int index = 0; index < model.Cards.Count; index++)
                if (model.Cards[index].Snapshot.CardId == cardId)
                    return model.Cards[index];
            Assert.Fail($"{cardId} 카드가 없습니다.");
            return null;
        }

        private static Button FindButton(Transform root, string path) => root.Find(path).GetComponent<Button>();

        private static OwnerCollectionSnapshot CreateSnapshot()
        {
            return new OwnerCollectionSnapshot(new[]
            {
                new OwnerCollectionCardSnapshot(
                    "CARD-C", "P-C", "박포수", 2024, PlayerPosition.Catcher, 4,
                    PlayerCardEdition.Normal, 0, 0, false, false),
                new OwnerCollectionCardSnapshot(
                    "CARD-SP", "P-SP", "이선발", 2023, PlayerPosition.StartingPitcher, 6,
                    PlayerCardEdition.AllStar, 1, 1, false, false, pitcherRole: PitcherRole.Starter),
                new OwnerCollectionCardSnapshot(
                    "CARD-RP", "P-RP", "김마무리", 2025, PlayerPosition.ReliefPitcher, 9,
                    PlayerCardEdition.GoldenGlove, 2, 3, true, true, pitcherRole: PitcherRole.Closer),
                new OwnerCollectionCardSnapshot(
                    "CARD-MVP", "P-MVP", "최거포", 2022, PlayerPosition.FirstBase, 10,
                    PlayerCardEdition.Mvp, 0, 0, false, false)
            });
        }
    }
}
