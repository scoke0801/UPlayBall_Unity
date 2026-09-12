using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.Encyclopedia;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>도감 필터·읽기 권한·목록 가상화가 카드 탐색 계약을 지키는지 검증한다.</summary>
    public sealed class EncyclopediaPresentationTests
    {
        [Test]
        public void Filter_표시이름만검색하며구단연도Edition을AND로적용한다()
        {
            var entry = Entry("source-hidden-id", 2012);
            var filter = new EncyclopediaScreenFilter { Search = "월드", FranchiseId = "franchise-a", OriginYear = 2012, EditionId = "Normal" };
            Assert.That(filter.Matches(entry, false), Is.True);
            filter.Search = "source-hidden-id";
            Assert.That(filter.Matches(entry, false), Is.False);
            filter.Search = "월드";
            filter.FranchiseId = "franchise-b";
            Assert.That(filter.Matches(entry, false), Is.False);
            filter.FranchiseId = "franchise-a";
            filter.EditionId = "Mvp";
            Assert.That(filter.Matches(entry, false), Is.False);
        }

        [Test]
        public void Filter_과거획득과현재보유를구별하고읽기전용은수집조건을무시한다()
        {
            var entry = Entry("card", 2012);
            entry.WasEverAcquired = true;
            var filter = new EncyclopediaScreenFilter { CollectionState = 2 };
            Assert.That(filter.Matches(entry, false), Is.True);
            entry.IsCurrentlyOwned = true;
            Assert.That(filter.Matches(entry, false), Is.False);
            Assert.That(filter.Matches(entry, true), Is.True);
        }

        [Test]
        public void Sort_위시등록순동점은정확한CardId로안정정렬한다()
        {
            var first = Entry("a", 2012);
            var second = Entry("b", 2012);
            first.AddedSequence = second.AddedSequence = 3;
            var filter = new EncyclopediaScreenFilter { Sort = 11 };
            Assert.That(filter.Compare(first, second), Is.LessThan(0));
            second.AddedSequence = 4;
            Assert.That(filter.Compare(first, second), Is.GreaterThan(0));
        }

        [Test]
        public void Metadata_실제Edition을동일프레임과한글표시로조회한다()
        {
            var metadata = CardEditionPresentationMetadataCatalog.Resolve(PlayerCardEdition.GoldenGlove);
            Assert.That(metadata.DisplayName, Is.EqualTo("골든글러브"));
            Assert.That(metadata.FrameEdition, Is.EqualTo(PlayerCardEdition.GoldenGlove));
            Assert.That(metadata.SortPriority, Is.GreaterThan(CardEditionPresentationMetadataCatalog.Resolve(PlayerCardEdition.Normal).SortPriority));
        }

        [Test]
        public void Entry_큰상세는_목록생성때계산하지않고_최초조회한번만계산한다()
        {
            var entry = Entry("lazy", 2012);
            int resolveCount = 0;
            entry.SetDetailResolver(target =>
            {
                resolveCount++;
                target.AbilityInformation = "능력치 상세";
                target.HasScoutRoute = true;
            });

            Assert.That(resolveCount, Is.Zero);
            Assert.That(entry.AbilityInformation, Is.EqualTo("능력치 상세"));
            Assert.That(entry.HasScoutRoute, Is.True);
            Assert.That(resolveCount, Is.EqualTo(1));
        }

        [TestCase(5000)]
        [TestCase(10000)]
        [TestCase(20000)]
        public void View_대형목록에서도가시영역의카드만생성한다(int cardCount)
        {
            WithView(false, (view, shell) =>
            {
                var cards = new List<EncyclopediaScreenEntry>();
                for (int i = 0; i < cardCount; i++) cards.Add(Entry("card-" + i, 2012));
                view.Bind(new EncyclopediaScreenSnapshot { Cards = cards, Seasons = cards });
                Assert.That(view.VisibleEntryCount, Is.EqualTo(cardCount));
                Assert.That(view.InstantiatedCardCount, Is.GreaterThan(0).And.LessThan(100));
                view.Filter.Search = "없는선수";
                view.Refresh();
                Assert.That(view.VisibleEntryCount, Is.Zero);
            });
        }

        [Test]
        public void View_스크롤하면기존카드Object가새StableId로재바인딩된다()
        {
            WithView(false, (view, shell) =>
            {
                var cards = new List<EncyclopediaScreenEntry>();
                for (int i = 0; i < 5000; i++) cards.Add(Entry("card-" + i.ToString("D4"), 2012));
                view.Bind(new EncyclopediaScreenSnapshot { Cards = cards, Seasons = cards });
                var rendered = shell.MainWorkspaceHost.GetComponentsInChildren<PlayerMiniCardView>(true);
                PlayerMiniCardView recycled = Array.Find(
                    rendered,
                    card => card.Model != null && card.Model.PlayerId.StartsWith("card-", StringComparison.Ordinal));
                Assert.That(recycled, Is.Not.Null);
                string firstId = recycled.Model.PlayerId;
                ScrollRect scroll = shell.MainWorkspaceHost.GetComponentInChildren<ScrollRect>(true);

                scroll.content.anchoredPosition = new Vector2(0, 3000);
                scroll.onValueChanged.Invoke(Vector2.down);

                Assert.That(recycled.Model.PlayerId, Is.Not.EqualTo(firstId));
                Assert.That(recycled.Model.PlayerId, Does.StartWith("card-"));
                Assert.That(view.InstantiatedCardCount, Is.LessThan(100));
            });
        }

        [Test]
        public void View_선수Tab은별도이름띠대신Normal카드명찰을사용한다()
        {
            WithView(false, (view, shell) =>
            {
                var playerSeason = Entry("", 2012);
                playerSeason.PlayerSeasonId = "season-default-frame";
                view.Bind(new EncyclopediaScreenSnapshot { Seasons = new[] { playerSeason } });

                PlayerMiniCardView card = Array.Find(
                    shell.MainWorkspaceHost.GetComponentsInChildren<PlayerMiniCardView>(true),
                    candidate => candidate.Model?.PlayerId == "season-default-frame");

                Assert.That(card, Is.Not.Null);
                Assert.That(card.Model.FrameEdition, Is.EqualTo(PlayerCardEdition.Normal));
                Assert.That(card.transform.Find("LineupSubFrame").gameObject.activeSelf, Is.True);
                Assert.That(card.transform.Find("LineupSubFrame").GetComponent<Image>().sprite.name,
                    Is.EqualTo("PlayerCard_Mini_Normal_v2"));
                Assert.That(card.transform.Find("NameBand").gameObject.activeSelf, Is.False);
                RectTransform name = (RectTransform)card.transform.Find("Name");
                Assert.That(name.anchorMin, Is.EqualTo(new Vector2(.23f, .19f)));
                Assert.That(name.anchorMax, Is.EqualTo(new Vector2(.77f, .265f)));
            });
        }

        [Test]
        public void View_위시전용목록은정확한카드만보이며해제명령으로상태를직접바꾸지않는다()
        {
            WithView(true, (view, shell) =>
            {
                var normal = Entry("normal", 2012);
                var mvp = Entry("mvp", 2012);
                mvp.IsWishlisted = true;
                view.Bind(new EncyclopediaScreenSnapshot { Cards = new[] { normal, mvp } });
                Assert.That(view.VisibleEntryCount, Is.EqualTo(1));
                string requested = null;
                view.WishToggleRequested += cardId => requested = cardId;
                view.SelectCard("mvp");
                FindButton(shell.ContextActionBarHost, "ToggleWish").onClick.Invoke();
                Assert.That(requested, Is.EqualTo("mvp"));
                Assert.That(mvp.IsWishlisted, Is.True);
            });
        }

        [Test]
        public void View_읽기전용은위시Scout수집현황메뉴를숨긴다()
        {
            WithView(false, (view, shell) =>
            {
                var entry = Entry("card", 2012);
                view.Bind(new EncyclopediaScreenSnapshot { IsReadOnly = true, Cards = new[] { entry }, Seasons = new[] { entry } });
                view.SelectCard("card");
                Assert.That(FindButton(shell.ContextActionBarHost, "ToggleWish").gameObject.activeSelf, Is.False);
                Assert.That(FindButton(shell.ContextActionBarHost, "FindScout").gameObject.activeSelf, Is.False);
                Assert.That(FindButton(shell.MainWorkspaceHost, "ViewTab2").gameObject.activeSelf, Is.False);
            });
        }

        [Test]
        public void View_화면을숨기면검색창선택을해제해Ime입력이잔류하지않는다()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject ownedEventSystemObject = null;
            if (eventSystem == null)
            {
                ownedEventSystemObject = new GameObject("EncyclopediaEventSystem", typeof(EventSystem));
                eventSystem = ownedEventSystemObject.GetComponent<EventSystem>();
            }
            try
            {
                WithView(false, (view, shell) =>
                {
                    view.Bind(new EncyclopediaScreenSnapshot
                    {
                        Cards = new[] { Entry("card", 2012) },
                        Seasons = new[] { Entry("card", 2012) }
                    });
                    InputField search = shell.MainWorkspaceHost.GetComponentInChildren<InputField>(true);
                    eventSystem.SetSelectedGameObject(search.gameObject);

                    view.SetVisible(false);

                    Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
                    Assert.That(search.textComponent.text, Is.EqualTo(search.text));
                });
            }
            finally
            {
                eventSystem.SetSelectedGameObject(null);
                if (ownedEventSystemObject != null)
                    UnityEngine.Object.DestroyImmediate(ownedEventSystemObject);
            }
        }

        private static EncyclopediaScreenEntry Entry(string cardId, int year) => new EncyclopediaScreenEntry
        {
            CardId = cardId, PlayerSeasonId = "season", PlayerPersonId = "person", FranchiseId = "franchise-a",
            FranchiseDisplayName = "월드구단", DisplayName = "월드선수", OriginYear = year, Cost = 5,
            Position = "유격수", EditionId = "Normal", EditionDisplayName = "일반"
        };

        private static Button FindButton(Transform root, string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true)) if (button.name == name) return button;
            Assert.Fail("버튼을 찾을 수 없습니다: " + name);
            return null;
        }

        private static void WithView(bool wishlist, Action<UI_Scene_PlayerEncyclopedia, SharedGameShellView> run)
        {
            var root = new GameObject("EncyclopediaTest", typeof(RectTransform));
            UI_Scene_PlayerEncyclopedia view = null;
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1600, 1000);
                var shell = SharedGameShellView.CreateRuntime(root.transform);
                view = UI_Scene_PlayerEncyclopedia.CreateRuntime(shell.MainWorkspaceHost, shell.RightInspectorHost, shell.ContextActionBarHost, wishlist);
                run(view, shell);
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
