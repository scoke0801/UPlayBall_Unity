using Baseball.Game.Data;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Core.Historical;
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
                Object.DestroyImmediate(GameManager.Instance.gameObject);
        }

        [Test]
        public void NewGameDefinition_Owner초기값과StarterTactic두장을제공한다()
        {
            OwnerModeNewGameConfiguration configuration = NewGameDefinition.LoadOwnerModeConfiguration();

            Assert.That(configuration.WorldSeed, Is.GreaterThan(0UL));
            Assert.That(configuration.OriginYear, Is.GreaterThan(0));
            Assert.That(configuration.InitialMoney, Is.GreaterThanOrEqualTo(0L));
            Assert.That(configuration.InitialScoutingPoints, Is.GreaterThanOrEqualTo(0));
            Assert.That(configuration.InitialDevelopmentPoints, Is.GreaterThanOrEqualTo(0));
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
        public void StartNewGame_같은Catalog의TeamColor두슬롯과Tactic두장을Pregame에전달한다()
        {
            GameBootstrap.EnsureRuntimeManagers();
            GameManager.Instance.TryGetManager(out OwnerModeManager manager);

            Assert.That(manager.StartNewGame(), Is.True, manager.LastError);
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
            Assert.That(preset.DefaultTacticCardIds.Count, Is.EqualTo(2));

            ManagerPregamePreparation preparation = manager.PrepareNextGame();
            Assert.That(preparation.PresetValidation.CanStartGame, Is.True);
            Assert.That(preparation.CanStartGame, Is.True);
        }
    }
}
