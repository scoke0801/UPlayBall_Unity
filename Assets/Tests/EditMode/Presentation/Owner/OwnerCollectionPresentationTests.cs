using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>보유 카드 Collection의 검색·정렬·선택이 읽기 전용 계약을 지키는지 검증한다.</summary>
    public sealed class OwnerCollectionPresentationTests
    {
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
            Assert.That(byPosition.Cards[0].MiniCard.PositionLabel, Is.EqualTo("구원투수 (RP)"));
            Assert.That(byEdition.Cards[0].MiniCard.EditionLabel, Is.EqualTo("MVP"));
            Assert.That(byEdition.Cards[1].MiniCard.EditionLabel, Is.EqualTo("골든글러브"));
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
        public void View_검색결과를공용MiniCard로그리고선택Inspector와미연결Action을표시한다()
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
                    "ContextActionBar/OwnerCollectionActionBar/EnhancementDisabled");
                Button sale = FindButton(shell.transform,
                    "ContextActionBar/OwnerCollectionActionBar/SaleDisabled");
                Button activeRoster = FindButton(shell.transform,
                    "ContextActionBar/OwnerCollectionActionBar/ActiveRosterDisabled");
                Assert.That(enhancement.interactable, Is.False);
                Assert.That(enhancement.GetComponentInChildren<Text>().text, Does.Contain("미리보기·실행"));
                Assert.That(sale.interactable, Is.False);
                Assert.That(sale.GetComponentInChildren<Text>().text, Does.Contain("미리보기·실행"));
                Assert.That(activeRoster.interactable, Is.False);
                Assert.That(activeRoster.GetComponentInChildren<Text>().text, Does.Contain("변경 미제공"));
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
                    PlayerCardEdition.AllStar, 1, 1, false, false),
                new OwnerCollectionCardSnapshot(
                    "CARD-RP", "P-RP", "김마무리", 2025, PlayerPosition.ReliefPitcher, 9,
                    PlayerCardEdition.GoldenGlove, 2, 3, true, true),
                new OwnerCollectionCardSnapshot(
                    "CARD-MVP", "P-MVP", "최거포", 2022, PlayerPosition.FirstBase, 10,
                    PlayerCardEdition.Mvp, 0, 0, false, false)
            });
        }
    }
}
