using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>한 번의 선수단 편집에서 누적할 1군 카드 교체 한 건을 나타낸다.</summary>
    public sealed class OwnerActiveRosterReplacement
    {
        public OwnerActiveRosterReplacement(string outgoingCardId, string incomingCardId)
        {
            OutgoingCardId = RequireId(outgoingCardId, nameof(outgoingCardId));
            IncomingCardId = RequireId(incomingCardId, nameof(incomingCardId));
            if (string.Equals(OutgoingCardId, IncomingCardId, StringComparison.Ordinal))
                throw new ArgumentException("교체 전후 CardId는 달라야 합니다.", nameof(incomingCardId));
        }

        public string OutgoingCardId { get; }
        public string IncomingCardId { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>저장 전 1군 카드 교체 후보와 프리셋 검증 결과를 한 단위로 보관한다.</summary>
    public sealed class OwnerActiveRosterChangePreview
    {
        public OwnerActiveRosterChangePreview(
            string outgoingCardId,
            string incomingCardId,
            CurrentRosterState roster,
            TeamSeasonPlayerStatusState playerStatus,
            LineupPresetState preset,
            LineupPresetValidationResult validation)
            : this(
                new[] { new OwnerActiveRosterReplacement(outgoingCardId, incomingCardId) },
                roster,
                playerStatus,
                preset,
                validation)
        {
        }

        public OwnerActiveRosterChangePreview(
            IReadOnlyList<OwnerActiveRosterReplacement> replacements,
            CurrentRosterState roster,
            TeamSeasonPlayerStatusState playerStatus,
            LineupPresetState preset,
            LineupPresetValidationResult validation,
            int clearedTeamColorCount = 0)
        {
            if (replacements == null) throw new ArgumentNullException(nameof(replacements));
            if (replacements.Count == 0)
                throw new ArgumentException("1군 교체 후보가 한 건 이상 필요합니다.", nameof(replacements));
            if (clearedTeamColorCount < 0 || clearedTeamColorCount > LineupPresetState.TeamColorSlotCount)
                throw new ArgumentOutOfRangeException(nameof(clearedTeamColorCount));
            var copiedReplacements = new OwnerActiveRosterReplacement[replacements.Count];
            for (int index = 0; index < copiedReplacements.Length; index++)
                copiedReplacements[index] = replacements[index] ??
                    throw new ArgumentException("1군 교체 후보는 null일 수 없습니다.", nameof(replacements));

            Replacements = copiedReplacements;
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            PlayerStatus = playerStatus ?? throw new ArgumentNullException(nameof(playerStatus));
            Preset = preset ?? throw new ArgumentNullException(nameof(preset));
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));
            ClearedTeamColorCount = clearedTeamColorCount;
        }

        public IReadOnlyList<OwnerActiveRosterReplacement> Replacements { get; }
        public int ReplacementCount => Replacements.Count;
        public CurrentRosterState Roster { get; }
        public TeamSeasonPlayerStatusState PlayerStatus { get; }
        public LineupPresetState Preset { get; }
        public LineupPresetValidationResult Validation { get; }
        public int ClearedTeamColorCount { get; }
    }

    /// <summary>보유 카드를 같은 1군 역할 슬롯에 넣는 불변 후보를 만든다.</summary>
    public static class OwnerActiveRosterChangeBuilder
    {
        public static CurrentRosterState ReplaceCard(
            CurrentRosterState source,
            string outgoingCardId,
            PlayerCardDefinition incomingCard,
            PlayerSeasonDefinition incomingSeason)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (incomingCard == null) throw new ArgumentNullException(nameof(incomingCard));
            if (incomingSeason == null) throw new ArgumentNullException(nameof(incomingSeason));
            string outgoingId = RequireId(outgoingCardId, nameof(outgoingCardId));
            if (!string.Equals(incomingCard.PlayerSeasonId, incomingSeason.PlayerSeasonId, StringComparison.Ordinal))
                throw new ArgumentException("카드와 선수 시즌 원본이 일치하지 않습니다.", nameof(incomingSeason));

            var entries = new ActiveRosterEntry[source.Entries.Count];
            bool replaced = false;
            for (int index = 0; index < entries.Length; index++)
            {
                ActiveRosterEntry entry = source.Entries[index];
                if (string.Equals(entry.CardId, incomingCard.CardId, StringComparison.Ordinal))
                    throw new InvalidOperationException("이미 1군에 등록된 카드는 다시 등록할 수 없습니다.");
                if (!string.Equals(entry.CardId, outgoingId, StringComparison.Ordinal))
                {
                    entries[index] = entry;
                    continue;
                }
                entries[index] = new ActiveRosterEntry(
                    incomingCard.CardId,
                    incomingCard.PlayerSeasonId,
                    incomingSeason.PlayerPersonId,
                    incomingSeason.RegistrationType,
                    entry.Role);
                replaced = true;
            }
            if (!replaced) throw new InvalidOperationException("교체할 1군 카드를 찾을 수 없습니다.");
            return new CurrentRosterState(source.TeamSeasonKey, entries);
        }

        public static TeamSeasonPlayerStatusState ReplacePlayerStatus(
            TeamSeasonPlayerStatusState source,
            string outgoingPersonId,
            string incomingPersonId,
            int neutralCondition)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            string outgoingId = RequireId(outgoingPersonId, nameof(outgoingPersonId));
            string incomingId = RequireId(incomingPersonId, nameof(incomingPersonId));
            if (string.Equals(outgoingId, incomingId, StringComparison.Ordinal)) return source;

            var players = new TeamSeasonPlayerStatus[source.Players.Count];
            bool replaced = false;
            for (int index = 0; index < players.Length; index++)
            {
                TeamSeasonPlayerStatus player = source.Players[index];
                if (string.Equals(player.PlayerPersonId, incomingId, StringComparison.Ordinal))
                    throw new InvalidOperationException("같은 선수를 다른 카드로 중복 등록할 수 없습니다.");
                if (!string.Equals(player.PlayerPersonId, outgoingId, StringComparison.Ordinal))
                {
                    players[index] = player;
                    continue;
                }
                players[index] = new TeamSeasonPlayerStatus(incomingId, neutralCondition);
                replaced = true;
            }
            if (!replaced) throw new InvalidOperationException("교체할 선수 상태를 찾을 수 없습니다.");
            return new TeamSeasonPlayerStatusState(source.TeamSeasonKey, players);
        }

        /// <summary>1군 변경으로 발동 후보에서 빠진 TeamColor만 해제한 프리셋 후보를 만든다.</summary>
        public static LineupPresetState ClearUnavailableTeamColors(
            LineupPresetState source,
            IReadOnlyList<string> availableTeamColorIds,
            out int clearedCount)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (availableTeamColorIds == null) throw new ArgumentNullException(nameof(availableTeamColorIds));

            clearedCount = 0;
            var teamColorIds = new string[LineupPresetState.TeamColorSlotCount];
            for (int slotIndex = 0; slotIndex < teamColorIds.Length; slotIndex++)
            {
                string selectedId = source.TeamColorIds[slotIndex];
                if (string.IsNullOrWhiteSpace(selectedId)) continue;
                if (ContainsId(availableTeamColorIds, selectedId))
                {
                    teamColorIds[slotIndex] = selectedId;
                    continue;
                }
                clearedCount++;
            }
            if (clearedCount == 0) return source;

            return new LineupPresetState(
                source.PresetId,
                source.Name,
                source.StartingLineupSlots,
                source.BattingOrderCardIds,
                source.BenchPriorityCardIds,
                source.StarterRotationCardIds,
                source.BullpenAssignmentCardIds,
                source.SetupPitcherCardId,
                source.CloserPitcherCardId,
                teamColorIds,
                source.DefaultTacticCardIds);
        }

        private static bool ContainsId(IReadOnlyList<string> values, string expected)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(values[index], expected, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    public sealed partial class ManagerModeRuntimeState
    {
        internal void ReplacePlayerStatusState(TeamSeasonPlayerStatusState replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            int index = FindStatusIndex(replacement.TeamSeasonKey);
            _playerStatuses[index] = replacement;
        }
    }

    public sealed partial class ManagerHistoricalRuntimeState
    {
        internal void ApplyPlayerActiveRosterChange(
            CurrentRosterState replacement,
            TeamSeasonPlayerStatusState playerStatus)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (playerStatus == null) throw new ArgumentNullException(nameof(playerStatus));
            if (!string.Equals(replacement.TeamSeasonKey, PlayerTeamSeasonKey, StringComparison.Ordinal) ||
                !string.Equals(playerStatus.TeamSeasonKey, PlayerTeamSeasonKey, StringComparison.Ordinal))
                throw new InvalidOperationException("플레이어 구단의 1군만 이 경로에서 변경할 수 있습니다.");

            RosterValidationResult validation = new ActiveRosterValidator().Validate(replacement);
            if (!validation.IsValid)
                throw new InvalidOperationException($"1군 등록 규칙을 위반했습니다: {validation.Issues[0].Code}");
            ValidateRosterCards(replacement, WorldCardCatalog);
            for (int index = 0; index < replacement.Entries.Count; index++)
            {
                ActiveRosterEntry entry = replacement.Entries[index];
                if (!TryGetOwnedCard(entry.CardId, out _))
                    throw new InvalidOperationException("보유하지 않은 카드는 1군에 등록할 수 없습니다.");
                playerStatus.GetRequiredPlayer(entry.PlayerPersonId);
            }

            int rosterIndex = FindRosterIndex(PlayerTeamSeasonKey);
            _rosters[rosterIndex] = replacement;
            _rostersByTeamSeasonKey[PlayerTeamSeasonKey] = replacement;
            ManagerMode.ReplacePlayerStatusState(playerStatus);
        }
    }
}
