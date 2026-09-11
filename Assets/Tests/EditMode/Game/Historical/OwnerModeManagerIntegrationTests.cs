using System;
using System.Collections.Generic;
using Baseball.Game.Data;
using Baseball.Game.Historical;
using Baseball.Game.Guide;
using Baseball.Game.Manager;
using Baseball.Game.Shop;
using Baseball.Game.Sound;
using Baseball.Game.Unity.Persistence;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Shop;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class OwnerModeManagerIntegrationTests
    {
        [TearDown]
        public void TearDown()
        {
            if (GameManager.HasInstance)
                UnityEngine.Object.DestroyImmediate(GameManager.Instance.gameObject);
        }

        [Test]
        public void NewGameDefinition_Owner초기값과StarterTactic두장을제공한다()
        {
            OwnerModeNewGameConfiguration configuration = NewGameDefinition.LoadOwnerModeConfiguration();

            Assert.That(configuration.WorldSeed, Is.GreaterThan(0UL));
            Assert.That(configuration.OriginYear, Is.GreaterThan(0));
            Assert.That(configuration.InitialMoney, Is.GreaterThanOrEqualTo(0L));
            Assert.That(configuration.InitialScoutingPoints, Is.EqualTo(10_000));
            Assert.That(configuration.InitialDevelopmentPoints, Is.EqualTo(3_000));
            Assert.That(configuration.StarterTacticCards.Count, Is.EqualTo(2));
            Assert.That(configuration.StarterTacticCards[0].CardId,
                Is.Not.EqualTo(configuration.StarterTacticCards[1].CardId));
        }

        [Test]
        public void GameBootstrap_OwnerModeManager를한번만등록한다()
        {
            GameBootstrap.EnsureRuntimeManagers();
            GameManager gameManager = GameManager.Instance;

            Assert.That(gameManager.TryGetManager(out OwnerModeManager first), Is.True);
            GameBootstrap.EnsureRuntimeManagers();
            Assert.That(gameManager.TryGetManager(out OwnerModeManager second), Is.True);
            Assert.That(second, Is.SameAs(first));
            Assert.That(first.HasActiveRuntime, Is.False);
        }

        [Test]
        public void BgmDirector_구단주관전생명주기에따라경기와로비Bgm을전환한다()
        {
            GameBootstrap.EnsureRuntimeManagers();
            BgmDirector director = BgmDirector.Instance;
            SoundManager soundManager = SoundManager.Instance;

            director.SetOwnerMatchBroadcasting(true);
            Assert.That(soundManager.CurrentSituation, Is.EqualTo(BgmSituation.MatchPlay));

            director.SetOwnerMatchBroadcasting(false);
            Assert.That(soundManager.CurrentSituation, Is.EqualTo(BgmSituation.Lobby));
        }

        [Test]
        public void BgmDirector_결과만보기관전에서는Bgm을재생하지않는다()
        {
            GameBootstrap.EnsureRuntimeManagers();
            BgmDirector director = BgmDirector.Instance;
            SoundManager soundManager = SoundManager.Instance;

            director.SetOwnerMatchBroadcasting(true, shouldPlayAudio: false);
            Assert.That(soundManager.CurrentSituation, Is.Null);

            director.SetOwnerMatchBroadcasting(false);
            Assert.That(soundManager.CurrentSituation, Is.EqualTo(BgmSituation.Lobby));
        }

        [Test]
        public void StartNewGame_초기자원과작전카드를지급하되작전카드는자동장착하지않는다()
        {
            GameBootstrap.EnsureRuntimeManagers();
            GameManager.Instance.TryGetManager(out OwnerModeManager manager);

            Assert.That(manager.StartNewGame(), Is.True, manager.LastError);
            Assert.That(manager.Runtime.Economy.ScoutingPoints, Is.EqualTo(10_000));
            Assert.That(manager.Runtime.Economy.DevelopmentPoints, Is.EqualTo(3_000));
            OwnerModeRosterStatus rosterStatus = manager.BuildRosterStatus();
            Assert.That(rosterStatus.Strength.PlayerCount, Is.EqualTo(25));
            Assert.That(rosterStatus.Strength.HitterCount, Is.EqualTo(14));
            Assert.That(rosterStatus.Strength.PitcherCount, Is.EqualTo(11));
            Assert.That(rosterStatus.Strength.Overall, Is.InRange(1d, 100d));
            Assert.That(rosterStatus.Cost.HasValue, Is.True);
            Assert.That(manager.BuildTeamStrength(manager.Runtime.PlayerTeamSeasonKey).Overall,
                Is.EqualTo(rosterStatus.Strength.Overall));
            LineupPresetState preset = manager.Runtime.ManagerMode.GetSelectedLineupPreset();
            Assert.That(preset.TeamColorIds.Count, Is.EqualTo(LineupPresetState.TeamColorSlotCount));
            Assert.That(preset.TeamColorIds[0], Is.Not.Null.And.Not.Empty);
            Assert.That(preset.TeamColorIds[1], Is.Not.Null.And.Not.Empty);
            Assert.That(preset.TeamColorIds[0], Is.Not.EqualTo(preset.TeamColorIds[1]));
            Assert.That(preset.DefaultTacticCardIds, Is.Empty);
            Assert.That(manager.GetAvailableTacticCards().Count, Is.EqualTo(2));

            ManagerPregamePreparation preparation = manager.PrepareNextGame();
            Assert.That(preparation.PresetValidation.CanStartGame, Is.True);
            Assert.That(preparation.CanStartGame, Is.True);
        }

        [Test]
        public void PurchaseShopProduct_선수획득결과는Guide알림을추가하지않는다()
        {
            GameBootstrap.EnsureRuntimeManagers();
            GameManager.Instance.TryGetManager(out OwnerModeManager manager);
            Assert.That(manager.StartNewGame(), Is.True, manager.LastError);
            GuideManager guide = GuideManager.Instance;
            Assert.That(guide, Is.Not.Null);
            Assert.That(guide.IsAvailable, Is.True, guide.LastError);

            ShopService shop = manager.CreateShopService();
            ShopProductDefinition scoutProduct = null;
            for (int index = 0; index < shop.Catalog.Products.Count; index++)
            {
                ShopProductDefinition candidate = shop.Catalog.Products[index];
                if (candidate.Kind != ShopProductKind.PlayerCardPack || !shop.GetQuote(candidate).CanPurchase)
                    continue;
                scoutProduct = candidate;
                break;
            }
            Assert.That(scoutProduct, Is.Not.Null, "구매 가능한 선수 스카우트 상품이 필요합니다.");

            int queuedBeforePurchase = guide.QueuedCount;
            ShopPurchaseResult result = manager.PurchaseShopProduct(scoutProduct.ProductId);

            Assert.That(result.IsSuccess, Is.True, result.FailureMessage);
            Assert.That(guide.QueuedCount, Is.EqualTo(queuedBeforePurchase),
                "선수별 획득 결과는 Reveal이 표시하므로 Front Manager Queue에 다시 넣지 않습니다.");
        }

        [Test]
        public void ActiveRosterPreview_두선수교체를누적해한번에저장한다()
        {
            GameBootstrap.EnsureRuntimeManagers();
            GameManager.Instance.TryGetManager(out OwnerModeManager manager);
            Assert.That(manager.StartNewGame(), Is.True, manager.LastError);

            ManagerHistoricalRuntimeState runtime = manager.Runtime;
            CurrentRosterState roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            var outgoing = new List<ActiveRosterEntry>(2);
            for (int index = 0; index < roster.Entries.Count && outgoing.Count < 2; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (entry.Role == ActiveRosterRole.BenchHitter &&
                    entry.RegistrationType == RegistrationType.Domestic)
                    outgoing.Add(entry);
            }
            Assert.That(outgoing.Count, Is.EqualTo(2));

            var rosterPersonIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < roster.Entries.Count; index++)
                rosterPersonIds.Add(roster.Entries[index].PlayerPersonId);
            var incoming = new List<PlayerCardDefinition>(2);
            for (int index = 0; index < runtime.WorldCardCatalog.Cards.Count && incoming.Count < 2; index++)
            {
                PlayerCardDefinition card = runtime.WorldCardCatalog.Cards[index];
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                if (card.Edition != PlayerCardEdition.Normal ||
                    season.PlayerType != PlayerType.Batter ||
                    season.RegistrationType != RegistrationType.Domestic ||
                    rosterPersonIds.Contains(season.PlayerPersonId))
                    continue;
                incoming.Add(card);
                rosterPersonIds.Add(season.PlayerPersonId);
            }
            Assert.That(incoming.Count, Is.EqualTo(2));
            runtime.AcquireCard(incoming[0].CardId);
            runtime.AcquireCard(incoming[1].CardId);

            LineupPresetState firstPreset = ReplacePresetCard(
                runtime.ManagerMode.GetSelectedLineupPreset(),
                outgoing[0].CardId,
                incoming[0].CardId);
            OwnerActiveRosterChangePreview first = manager.PreviewActiveRosterChange(
                outgoing[0].CardId,
                incoming[0].CardId,
                firstPreset);
            LineupPresetState secondPreset = ReplacePresetCard(
                first.Preset,
                outgoing[1].CardId,
                incoming[1].CardId);
            OwnerActiveRosterChangePreview second = manager.AppendActiveRosterChange(
                first,
                outgoing[1].CardId,
                incoming[1].CardId,
                secondPreset);

            Assert.That(second.ReplacementCount, Is.EqualTo(2));
            Assert.That(ContainsCard(second.Roster, outgoing[0].CardId), Is.False);
            Assert.That(ContainsCard(second.Roster, outgoing[1].CardId), Is.False);
            Assert.That(ContainsCard(second.Roster, incoming[0].CardId), Is.True);
            Assert.That(ContainsCard(second.Roster, incoming[1].CardId), Is.True);
            Assert.That(second.Validation.Status, Is.EqualTo(LineupPresetValidationStatus.Valid));

            manager.ApplyActiveRosterChange(second);

            CurrentRosterState saved = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            Assert.That(ContainsCard(saved, incoming[0].CardId), Is.True);
            Assert.That(ContainsCard(saved, incoming[1].CardId), Is.True);
            Assert.That(runtime.ManagerMode.PlayerContracts.Count, Is.EqualTo(saved.Entries.Count));
            Assert.That(runtime.ManagerMode.GetPlayerContract(incoming[0].CardId), Is.Not.Null);
            Assert.That(runtime.ManagerMode.GetPlayerContract(incoming[1].CardId), Is.Not.Null);
            Assert.That(runtime.ManagerMode.GetPlayerContract(incoming[0].CardId).RemainingSeasons, Is.EqualTo(1));
            Assert.That(runtime.ManagerMode.GetPlayerContract(incoming[1].CardId).RemainingSeasons, Is.EqualTo(1));
            Assert.Throws<KeyNotFoundException>(() =>
                runtime.ManagerMode.GetPlayerContract(outgoing[0].CardId));
            Assert.DoesNotThrow(() => manager.GetPlayerContracts());
        }

        [Test]
        public void DeleteSaveAndDiscardRuntime_디스크와메모리진행을함께비운다()
        {
            GameBootstrap.EnsureRuntimeManagers();
            GameManager.Instance.TryGetManager(out OwnerModeManager manager);
            Assert.That(manager.StartNewGame(), Is.True, manager.LastError);
            Assert.That(manager.PrepareNextGame(), Is.Not.Null);

            string savePath = System.IO.Path.Combine(
                Application.temporaryCachePath,
                $"owner-delete-test-{Guid.NewGuid():N}.json");
            System.IO.File.WriteAllText(savePath, "test");
            var saveStoreField = typeof(OwnerModeManager).GetField(
                "_saveStore",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(saveStoreField, Is.Not.Null);
            saveStoreField.SetValue(manager, new ManagerHistoricalSaveJsonStore(savePath));

            try
            {
                manager.DeleteSaveAndDiscardRuntime();

                Assert.That(System.IO.File.Exists(savePath), Is.False);
                Assert.That(manager.HasSave, Is.False);
                Assert.That(manager.HasActiveRuntime, Is.False);
                Assert.That(manager.Runtime, Is.Null);
                Assert.That(manager.CurrentPregame, Is.Null);
            }
            finally
            {
                if (System.IO.File.Exists(savePath))
                    System.IO.File.Delete(savePath);
            }
        }

        [Test]
        public void OwnerNewGameFlow_Production후보에서6타자4투수를고르면25인로스터까지진행한다()
        {
            GameBootstrap.EnsureRuntimeManagers();
            GameManager.Instance.TryGetManager(out OwnerModeManager manager);
            OwnerNewGameFlow flow = manager.BeginNewGameFlow();
            IReadOnlyList<OwnerNewGameTeamView> teams = flow.GetTeamCandidates();

            Assert.That(teams, Is.Not.Empty);
            flow.SelectTeam(teams[0].TeamSeasonKey);
            IReadOnlyList<OwnerNewGameCardView> candidates = flow.GetMainCardCandidates();
            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].PlayerType == PlayerType.Pitcher)
                    Assert.That(candidates[index].PitcherRole, Is.Not.Null);
            }
            SelectLowestCostCards(flow, candidates, PlayerType.Batter, flow.Rule.MainHitterCount);
            SelectLowestCostCards(flow, candidates, PlayerType.Pitcher, flow.Rule.MainPitcherCount);

            OwnerMainCardSelectionStatus status = flow.GetMainCardSelectionStatus();
            Assert.That(status.IsValid, Is.True, status.Message);
            Assert.That(status.SelectedCount, Is.EqualTo(flow.Rule.MainCardCount));
            Assert.That(status.TotalCost, Is.LessThanOrEqualTo(flow.Rule.MaximumMainCost));
            PlayerSeasonDefinition expectedStartingSeason = OwnerStartingSeasonResolver.Resolve(
                flow.SelectedMainCardIds,
                flow.CardCatalog);

            Assert.DoesNotThrow(flow.ContinueFromMainCards);
            Assert.That(flow.StartingYear, Is.EqualTo(expectedStartingSeason.OriginYear));
            Assert.That(flow.SelectedTeamSeasonKey, Is.EqualTo(expectedStartingSeason.OriginTeamSeasonKey));
            flow.SelectFrontManager(FrontManagerIds.DefaultAnalysis);
            Assert.DoesNotThrow(() => flow.SetProfile("서울 불사조", "테스트구단주"));
            Assert.That(flow.StarterRoster, Is.Not.Null);
            Assert.That(flow.StarterRoster.Roster.TeamSeasonKey,
                Is.EqualTo(expectedStartingSeason.OriginTeamSeasonKey));
            Assert.That(flow.StarterRoster.Roster.Entries.Count,
                Is.EqualTo(ActiveRosterCompositionRule.ActiveRosterSize));
            Assert.That(flow.CreateReceipt().MainCardIds.Count, Is.EqualTo(flow.Rule.MainCardCount));
            var fillerCountByCost = new Dictionary<int, int>();
            for (int index = 0; index < flow.StarterRoster.FillerCardIds.Count; index++)
            {
                string cardId = flow.StarterRoster.FillerCardIds[index];
                Assert.That(flow.CardCatalog.TryGetCard(cardId, out PlayerCardDefinition card), Is.True);
                int cost = flow.CardCatalog.GetPlayerSeason(card).Cost;
                fillerCountByCost.TryGetValue(cost, out int count);
                fillerCountByCost[cost] = count + 1;
            }
            Assert.That(fillerCountByCost[2], Is.EqualTo(10));
            Assert.That(fillerCountByCost[3], Is.EqualTo(5));

            string savePath = System.IO.Path.Combine(
                Application.temporaryCachePath,
                $"owner-starting-year-test-{Guid.NewGuid():N}.json");
            var saveStoreField = typeof(OwnerModeManager).GetField(
                "_saveStore",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(saveStoreField, Is.Not.Null);
            saveStoreField.SetValue(manager, new ManagerHistoricalSaveJsonStore(savePath));
            try
            {
                Assert.That(manager.CompleteNewGameFlow(), Is.True, manager.LastError);
                Assert.That(manager.Runtime.ManagerMode.LiveSeason.OriginYear,
                    Is.EqualTo(expectedStartingSeason.OriginYear));
                Assert.That(manager.Runtime.PlayerTeamSeasonKey,
                    Is.EqualTo(expectedStartingSeason.OriginTeamSeasonKey));
                Assert.That(manager.Runtime.OwnerProfile.ClubName, Is.EqualTo("서울 불사조"));
                Assert.That(manager.GetClubDisplayName(manager.Runtime.PlayerTeamSeasonKey),
                    Is.EqualTo("서울 불사조"));
                string opponentTeamSeasonKey = string.Empty;
                for (int index = 0; index < manager.Runtime.League.RegularTeamSeasonKeys.Count; index++)
                {
                    string candidate = manager.Runtime.League.RegularTeamSeasonKeys[index];
                    if (string.Equals(candidate, manager.Runtime.PlayerTeamSeasonKey, StringComparison.Ordinal))
                        continue;
                    opponentTeamSeasonKey = candidate;
                    break;
                }
                int opponentYear = manager.GetTeamOriginYear(opponentTeamSeasonKey) ?? 0;
                Assert.That(manager.GetClubDisplayName(opponentTeamSeasonKey),
                    Is.EqualTo(opponentYear + " " + manager.GetTeamDisplayName(opponentTeamSeasonKey)));
                manager.Load();
                Assert.That(manager.Runtime.OwnerProfile.ClubName, Is.EqualTo("서울 불사조"));
            }
            finally
            {
                if (System.IO.File.Exists(savePath))
                    System.IO.File.Delete(savePath);
            }
        }

        private static void SelectLowestCostCards(
            OwnerNewGameFlow flow,
            IReadOnlyList<OwnerNewGameCardView> candidates,
            PlayerType playerType,
            int requiredCount)
        {
            int selectedCount = 0;
            var usedPersons = new HashSet<string>(StringComparer.Ordinal);
            for (int index = candidates.Count - 1; index >= 0 && selectedCount < requiredCount; index--)
            {
                OwnerNewGameCardView candidate = candidates[index];
                if (candidate.PlayerType != playerType || !usedPersons.Add(candidate.PlayerPersonId))
                    continue;
                try
                {
                    flow.ToggleMainCard(candidate.CardId);
                    selectedCount++;
                }
                catch (InvalidOperationException)
                {
                    usedPersons.Remove(candidate.PlayerPersonId);
                }
            }
            Assert.That(selectedCount, Is.EqualTo(requiredCount),
                $"Production 후보에서 {playerType} 메인 카드 {requiredCount}장을 구성할 수 없습니다.");
        }

        private static LineupPresetState ReplacePresetCard(
            LineupPresetState source,
            string outgoingCardId,
            string incomingCardId)
        {
            var defense = new LineupPresetSlot[source.StartingLineupSlots.Count];
            for (int index = 0; index < defense.Length; index++)
            {
                LineupPresetSlot slot = source.StartingLineupSlots[index];
                defense[index] = new LineupPresetSlot(
                    ReplaceId(slot.CardId, outgoingCardId, incomingCardId),
                    slot.Position);
            }
            return new LineupPresetState(
                source.PresetId,
                source.Name,
                defense,
                ReplaceIds(source.BattingOrderCardIds, outgoingCardId, incomingCardId),
                ReplaceIds(source.BenchPriorityCardIds, outgoingCardId, incomingCardId),
                ReplaceIds(source.StarterRotationCardIds, outgoingCardId, incomingCardId),
                ReplaceIds(source.BullpenAssignmentCardIds, outgoingCardId, incomingCardId),
                ReplaceId(source.SetupPitcherCardId, outgoingCardId, incomingCardId),
                ReplaceId(source.CloserPitcherCardId, outgoingCardId, incomingCardId),
                new string[LineupPresetState.TeamColorSlotCount],
                source.DefaultTacticCardIds);
        }

        private static string[] ReplaceIds(
            IReadOnlyList<string> source,
            string outgoingCardId,
            string incomingCardId)
        {
            var result = new string[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = ReplaceId(source[index], outgoingCardId, incomingCardId);
            return result;
        }

        private static string ReplaceId(string value, string outgoingCardId, string incomingCardId)
        {
            return string.Equals(value, outgoingCardId, StringComparison.Ordinal) ? incomingCardId : value;
        }

        private static bool ContainsCard(CurrentRosterState roster, string cardId)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
                if (string.Equals(roster.Entries[index].CardId, cardId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
