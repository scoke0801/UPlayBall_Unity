using System;
using Baseball.Core.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using UnityEngine;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        public const int PracticePlayerIdBase = 20000000;
        private LegendaryPracticeCatalog _practiceCatalog;
        private bool _isPracticeSaving;
        private LegendaryPracticeRosterBuilder _practiceRosterBuilder;
        public bool IsPracticeSaving => _isPracticeSaving;
        /// <summary>공개 편성도 실제 연습경기에 사용한 원 시즌 수상 카드를 조회한다.</summary>
        public WorldCardCatalog PracticeCardCatalog => EnsurePracticeRosterBuilder().CardCatalog;
        public MatchRosterSnapshot GetPracticePlayerRoster() => _matchService.CreatePracticePlayerRoster(RequireRuntime());

        public TeamSeasonDefinition GetPracticeTeam(string teamId)
        {
            if (!_contentProvider.Load().TryGetTeamSeason(GetPracticeCatalog().Find(teamId).teamSeasonKey, out var team))
                throw new InvalidOperationException("역사 팀 원본을 찾을 수 없습니다.");
            return team;
        }

        public PlayerCardDefinition[] GetPracticeCards(string teamId)
        {
            EnsurePracticeRosterBuilder();
            return _practiceRosterBuilder.SelectCards(GetPracticeTeam(teamId));
        }

        public PlayerSeasonDefinition GetPracticePlayerSeason(PlayerCardDefinition card)
        {
            if (!_contentProvider.Load().TryGetPlayerSeason(card.PlayerSeasonId, out var season))
                throw new InvalidOperationException("역사 선수 원본이 없습니다.");
            return season;
        }

        /// <summary>연습경기 양 팀의 경기 번호를 현재 실제·가상 선수 표시명에 연결한다.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<int, string> CreatePracticeParticipantNames(string challengeId)
        {
            var runtime = RequireRuntime();
            var names = new System.Collections.Generic.Dictionary<int, string>();
            var cards = GetPracticeCards(challengeId);
            for (int i = 0; i < cards.Length; i++)
                names.Add(PracticePlayerIdBase + i + 1,
                    runtime.IdentityRegistry.GetPresentationPlayerName(GetPracticePlayerSeason(cards[i]).PlayerPersonId));
            var ids = ManagerModeMatchService.PlayerIdMap.Create(runtime);
            foreach (var entry in runtime.GetRoster(runtime.PlayerTeamSeasonKey).Entries)
                names[ids.Get(runtime.PlayerTeamSeasonKey, entry.PlayerSeasonId)] =
                    runtime.IdentityRegistry.GetPresentationPlayerName(entry.PlayerPersonId);
            return names;
        }

        /// <summary>플레이어 빌드에 포함된 검증된 순위만 로드한다.</summary>
        public LegendaryPracticeCatalog GetPracticeCatalog()
        {
            if (_practiceCatalog != null) return _practiceCatalog;
            var asset = Resources.Load<TextAsset>("NewGame/LegendaryPracticeCatalog");
            if (asset == null) throw new InvalidOperationException("역대 강팀 정보를 불러올 수 없습니다. 게임 데이터를 확인해 주세요.");
            var catalog = JsonUtility.FromJson<LegendaryPracticeCatalog>(asset.text);
            catalog.ValidateCoverage(_contentProvider.Load().TeamSeasons);
            if (catalog.contentHash != _contentProvider.Load().Manifest.ContentHash)
                throw new InvalidOperationException("역대 강팀 정보가 현재 선수 데이터와 맞지 않습니다.");
            if (catalog.simulationVersion != LegendaryPracticeCatalog.CreateSimulationVersion(
                Resources.Load<TextAsset>("NewGame/MiniGameBalance").text,
                Resources.Load<TextAsset>("NewGame/MatchRatingCurve").text))
                throw new InvalidOperationException("현재 경기 규칙에 맞는 역대 강팀 정보를 다시 준비해야 합니다.");
            _practiceCatalog = catalog; return catalog;
        }

        public MatchRosterSnapshot[] GetPracticeOpponent(string teamId, out TeamColorDefinition[] colors)
        {
            var team = GetPracticeCatalog().Find(teamId);
            var content = _contentProvider.Load();
            if (!content.TryGetTeamSeason(team.teamSeasonKey, out var definition))
                throw new InvalidOperationException("역사 팀의 선수 정보를 찾을 수 없습니다.");
            var builder = EnsurePracticeRosterBuilder();
            if (builder.GetRosterHash(definition) != team.rosterHash)
                throw new InvalidOperationException("역사 팀 편성이 변경되어 경기할 수 없습니다.");
            return builder.Build(definition, RequireRuntime().IdentityRegistry, 2000000, PracticePlayerIdBase, out colors, team.rank);
        }

        private LegendaryPracticeRosterBuilder EnsurePracticeRosterBuilder()
        {
            if (_practiceRosterBuilder != null) return _practiceRosterBuilder;
            var asset = Resources.Load<TextAsset>("NewGame/LegendaryPracticeDevelopment");
            if (asset == null) throw new InvalidOperationException("연습경기 성장 정보를 불러올 수 없습니다.");
            var development = JsonUtility.FromJson<LegendaryPracticeDevelopmentBalance>(asset.text);
            return _practiceRosterBuilder = new LegendaryPracticeRosterBuilder(_contentProvider.Load(), _balance, development);
        }

        /// <summary>상대 선수 카드의 표시에도 실제 경기와 같은 순위별 성장 상태를 제공한다.</summary>
        public OwnedPlayerCardState GetPracticeCardDevelopment(string teamId, PlayerCardDefinition card) =>
            EnsurePracticeRosterBuilder().CreateDevelopment(card, GetPracticeCatalog().Find(teamId).rank);

        /// <summary>결과와 시도 번호를 한 번 저장한 뒤 관전에 전달한다. 실패하면 기존 진행으로 복구한다.</summary>
        public ManagerModeMatchResult PlayPractice(string teamId, IMatchEventSink events)
        {
            EnsureRegularSeasonSimulationIsNotRunning();
            if (_isPracticeSaving) throw new InvalidOperationException("연습경기 결과를 저장하고 있습니다.");
            var runtime = RequireRuntime(); var catalog = GetPracticeCatalog(); var team = catalog.Find(teamId);
            if (!runtime.LegendaryPractice.CanPlay(catalog, team))
                throw new InvalidOperationException("앞선 역사 팀에 먼저 3승을 달성해 주세요.");
            var progress = runtime.LegendaryPractice.Get(teamId);
            int attempt = checked(progress.attempts + 1);
            var opponent = GetPracticeOpponent(teamId, out _)[progress.NextStarterIndex];
            ulong identity = 14695981039346656037UL;
            foreach (char c in teamId) identity = unchecked((identity ^ c) * 1099511628211UL);
            ulong seed = DeterministicSeed.Derive(runtime.WorldHistory.WorldHistorySeed,
                DeterministicSeed.Derive(identity, (ulong)attempt));
            var before = runtime.LegendaryPractice.Capture();
            _isPracticeSaving = true;
            try
            {
                var result = _matchService.PlayPractice(runtime, opponent, attempt, seed, events);
                runtime.LegendaryPractice.Commit(catalog, teamId, attempt,
                    result.Match.HomeBoxScore.Runs, result.Match.AwayBoxScore.Runs, DateTime.UtcNow.Ticks);
                _saveStore.Save(_saveAdapter.CreateSaveData(runtime));
                return result;
            }
            catch { runtime.RestoreLegendaryPractice(before); throw; }
            finally { _isPracticeSaving = false; }
        }

        /// <summary>재도전 시작점을 저장하고 저장 실패 시 기존 진행을 복구한다.</summary>
        public bool RestartPractice(string teamId)
        {
            EnsureRegularSeasonSimulationIsNotRunning();
            if (_isPracticeSaving) throw new InvalidOperationException("연습경기 진행을 저장하고 있습니다.");
            var runtime = RequireRuntime();
            var before = runtime.LegendaryPractice.Capture();
            _isPracticeSaving = true;
            try
            {
                if (!runtime.LegendaryPractice.Restart(GetPracticeCatalog(), teamId)) return false;
                _saveStore.Save(_saveAdapter.CreateSaveData(runtime));
                return true;
            }
            catch { runtime.RestoreLegendaryPractice(before); throw; }
            finally { _isPracticeSaving = false; }
        }

        /// <summary>지갑과 수령 원장을 같은 파일에 원자 저장하며 실패 시 둘 다 복구한다.</summary>
        public int ClaimPracticeRewards(string teamId = null)
        {
            EnsureRegularSeasonSimulationIsNotRunning();
            if (_isPracticeSaving) throw new InvalidOperationException("보상을 저장하고 있습니다.");
            var runtime = RequireRuntime(); var catalog = GetPracticeCatalog();
            var before = _saveAdapter.CreateSaveData(runtime);
            _isPracticeSaving = true;
            try
            {
                int claimed = 0;
                foreach (var team in catalog.teams)
                    if ((teamId == null || team.challengeTeamId == teamId) &&
                        runtime.LegendaryPractice.Claim(catalog, team.challengeTeamId, runtime.Economy)) claimed++;
                if (claimed > 0) _saveStore.Save(_saveAdapter.CreateSaveData(runtime));
                return claimed;
            }
            catch { Runtime = _saveAdapter.Restore(before); throw; }
            finally { _isPracticeSaving = false; }
        }
    }
}
