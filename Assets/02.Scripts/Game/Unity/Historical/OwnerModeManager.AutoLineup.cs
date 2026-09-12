using System;
using System.Collections.Generic;
using System.Threading;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        /// <summary>사용 불가·유학·특수 영입 예약 카드를 제외한 자동 배치 입력을 동결한다.</summary>
        public OwnerAutoLineupBuilder CreateAutoLineupBuilder(int year, string franchiseId, CancellationToken cancellation)
        {
            var runtime = RequireRuntime();
            var unavailablePersons = new HashSet<string>(StringComparer.Ordinal);
            foreach (var player in runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey).Players)
                if (player.Availability == PlayerAvailabilityStatus.Unavailable) unavailablePersons.Add(player.PlayerPersonId);
            var studying = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in runtime.PlayerGrowth.StudyProjects) studying.Add(project.CardId);
            var available = new List<string>();
            foreach (var owned in runtime.OwnedCards)
            {
                if (runtime.IsCardReserved(owned.CardId) || studying.Contains(owned.CardId)) continue;
                if (!runtime.WorldCardCatalog.TryGetCard(owned.CardId, out var card)) continue;
                if (unavailablePersons.Contains(runtime.WorldCardCatalog.GetPlayerSeason(card).PlayerPersonId)) continue;
                available.Add(owned.CardId);
            }
            return new OwnerAutoLineupBuilder(runtime.WorldCardCatalog, available, year, franchiseId, cancellation);
        }

        /// <summary>자동 편성의 전체 등록 차이를 하나의 기존 미리보기·저장 트랜잭션으로 검증한다.</summary>
        public OwnerActiveRosterChangePreview PreviewAutoLineup(LineupPresetState preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            var runtime = RequireRuntime();
            var roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            var requested = new List<string>();
            foreach (var slot in preset.StartingLineupSlots) requested.Add(slot.CardId);
            requested.AddRange(preset.BenchPriorityCardIds);
            requested.AddRange(preset.StarterRotationCardIds);
            requested.AddRange(preset.BullpenAssignmentCardIds);
            requested.Add(preset.SetupPitcherCardId);
            requested.Add(preset.CloserPitcherCardId);
            var current = new HashSet<string>(StringComparer.Ordinal);
            var outgoing = new List<ActiveRosterEntry>();
            foreach (var entry in roster.Entries)
            {
                current.Add(entry.CardId);
                if (!requested.Contains(entry.CardId)) outgoing.Add(entry);
            }
            var incoming = new List<PlayerCardDefinition>();
            foreach (string id in requested)
            {
                if (current.Contains(id)) continue;
                if (!runtime.TryGetOwnedCard(id, out _) || runtime.IsCardReserved(id) ||
                    !runtime.WorldCardCatalog.TryGetCard(id, out var card))
                    throw new InvalidOperationException("보유 카드 상태가 바뀌었습니다. 자동 배치를 다시 실행해 주세요.");
                incoming.Add(card);
            }
            if (incoming.Count == 0) return null;
            var replacements = new List<OwnerActiveRosterReplacement>();
            // 같은 인물의 등급·연도 교체를 먼저 처리해야 기존 상태 보존과 중간 중복 검사가 모두 성립한다.
            for (int i = incoming.Count - 1; i >= 0; i--)
            {
                string person = runtime.WorldCardCatalog.GetPlayerSeason(incoming[i]).PlayerPersonId;
                int index = outgoing.FindIndex(entry => entry.PlayerPersonId == person);
                if (index < 0) continue;
                replacements.Add(new OwnerActiveRosterReplacement(outgoing[index].CardId, incoming[i].CardId));
                outgoing.RemoveAt(index); incoming.RemoveAt(i);
            }
            foreach (var card in incoming)
            {
                bool hitter = runtime.WorldCardCatalog.GetPlayerSeason(card).PlayerType == PlayerType.Batter;
                int index = outgoing.FindIndex(entry => ActiveRosterCompositionRule.Standard.IsHitterRole(entry.Role) == hitter);
                if (index < 0) throw new InvalidOperationException("야수 14명·투수 11명 구성에 맞지 않습니다.");
                replacements.Add(new OwnerActiveRosterReplacement(outgoing[index].CardId, card.CardId));
                outgoing.RemoveAt(index);
            }
            return BuildActiveRosterChangePreview(replacements, preset);
        }
    }
}
