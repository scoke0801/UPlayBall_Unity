using System;
using System.Collections;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Presentation.Match;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Match;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerModeShellCoordinator
    {
        private UI_Scene_LegendaryPractice _practiceView;
        private UI_Scene_OwnerMatchSpectator _practiceSpectator;
        private bool _isPracticeMatchActive, _isPreparingPractice;
        private UnityEngine.Coroutine _practicePreparation;
        private string _activePracticeTeamId;

        private bool TryShowPractice(string route)
        {
            if (route != OwnerNavigationRoutes.LegendaryPractice)
            { _practiceView?.gameObject.SetActive(false); return false; }
            _homeView?.SetVisible(false); _expansionWorkspace.HideAll(); _sharedInformationWorkspace.HideAll();
            _shell.SetInspectorVisible(false); _shell.SetActionBarVisible(false);
            _presenter.ShowContext(new ShellContextModel(route, "역대 강팀", "3승으로 다음 역사에 도전하세요", "연습경기", true, "홈으로"));
            bool isEntering = _practiceView == null || !_practiceView.gameObject.activeSelf;
            if (_practiceView == null)
            {
                _practiceView = UI_Scene_LegendaryPractice.CreateRuntime(_shell.MainWorkspaceHost);
                _practiceView.SelectionRequested += SelectPracticeOpponent;
                _practiceView.StartRequested += BeginPractice;
                _practiceView.ClaimAllRequested += ClaimAllPractice;
                _practiceView.RetryRequested += Refresh;
                _practiceView.LineupRequested += () => HandleNavigationRequested(OwnerNavigationRoutes.RosterLineup);
            }
            _practiceView.gameObject.SetActive(!_isPracticeMatchActive);
            try
            {
                _practiceView.Bind(_manager.GetPracticeCatalog(), _manager.Runtime.LegendaryPractice, PracticeTeamName);
                if (isEntering) _practiceView.FocusAction();
            }
            catch (Exception error) { ReportPracticeError(error, "역대 강팀 정보를 불러오지 못했습니다."); }
            return true;
        }

        private string PracticeTeamName(LegendaryPracticeTeam team)
        {
            var definition = _manager.GetPracticeTeam(team.challengeTeamId);
            return team.year + " " + _manager.Runtime.IdentityRegistry.GetPresentationTeamSeasonName(definition.TeamSeasonKey, definition.FranchiseId);
        }

        private void SelectPracticeOpponent(string id)
        {
            try
            {
                var roster = _manager.GetPracticeOpponent(id, out var colors);
                var lineup = _snapshotFactory.CreatePracticeLineup(_manager, id, roster, colors);
                _practiceView.BindOpponent(roster, lineup);
                var source = new List<PlayerCardDefinition>();
                foreach (var candidate in _manager.GetPracticeCards(id))
                    if (candidate.Edition != PlayerCardEdition.CareerHigh && candidate.Edition != PlayerCardEdition.Legend)
                        source.Add(candidate);
                // 편성 슬롯 순서가 아닌 원 시즌 코스트로 대표 선수를 고른다. 동점은 카드 ID로 고정한다.
                source.Sort((a, b) =>
                {
                    int cost = _manager.GetPracticePlayerSeason(b).Cost.CompareTo(_manager.GetPracticePlayerSeason(a).Cost);
                    return cost != 0 ? cost : string.CompareOrdinal(a.CardId, b.CardId);
                });
                var cards = new PlayerMiniCardModel[Math.Min(3, source.Count)];
                for (int i = 0; i < cards.Length; i++)
                {
                    var card = source[i]; var season = _manager.GetPracticePlayerSeason(card);
                    var development = _manager.GetPracticeCardDevelopment(id, card);
                    var ratings = new Baseball.Simulation.Historical.OwnerCardAbilityResolver(_manager.Balance.Growth)
                        .ResolvePermanent(season, card, development);
                    bool pitcher = season.PlayerType == PlayerType.Pitcher;
                    string[] labels = pitcher ? new[] { "체력", "구속", "구위", "변화", "제구", "정신" }
                        : new[] { "교타", "장타", "주력", "번트", "수비", "정신" };
                    var stats = new PlayerMiniCardStatModel[6];
                    for (int a = 0; a < 6; a++)
                    {
                        var ability = (PlayerAbility)(a + (pitcher ? (int)PlayerAbility.Stamina : 0));
                        stats[a] = new PlayerMiniCardStatModel(labels[a], ratings.Get(ability), AttributeRating.Maximum);
                    }
                    cards[i] = new PlayerMiniCardModel(card.CardId, _manager.Runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId),
                        OwnerCollectionPresentationBuilder.FormatPlayerRole(season.Position, pitcher ? season.PitcherRole : null, false),
                        season.OriginYear.ToString(), "", PlayerCardEditionText.Get(card.Edition) + " +" + development.EnhancementLevel,
                        portraitAssetKey: season.PlayerSeasonId, stats: stats, frameEdition: card.Edition, cost: season.Cost,
                        growthBadges: OwnerCardGrowthBadgeBuilder.Build(development, null, _manager.Balance.Growth, _manager.TraitBalance));
                }
                _practiceView.BindFeaturedCards(cards);
                try { _practiceView.BindPlayerRoster(_manager.GetPracticePlayerRoster()); }
                catch (InvalidOperationException) { _practiceView.BindPlayerRoster(null); }
            }
            catch (Exception error) { ReportPracticeError(error, "선수 편성을 확인해 주세요. 선수 오더에서 변경할 수 있습니다."); }
        }

        private void BeginPractice(string id)
        {
            if (_isPreparingPractice || _isPracticeMatchActive) return;
            var progress = _manager.Runtime.LegendaryPractice.Get(id);
            if (progress.wins == 3 && !progress.rewardClaimed)
            {
                try { _manager.ClaimPracticeRewards(id); Refresh(); }
                catch (Exception error) { Refresh(); ReportPracticeError(error, "보상을 저장하지 못했습니다. 다시 시도해 주세요."); }
                return;
            }
            _isPreparingPractice = true; _practiceView.SetBusy(true); _practicePreparation = StartCoroutine(PreparePractice(id));
        }

        private IEnumerator PreparePractice(string id)
        {
            yield return null;
            if (!UiGameModeSession.IsSelected(UiGameMode.OwnerCareer))
            { _isPreparingPractice = false; _practiceView.SetBusy(false); yield break; }
            try
            {
                var buffer = new MatchEventBuffer(); var result = _manager.PlayPractice(id, buffer);
                if (_practiceSpectator == null)
                {
                    _practiceSpectator = UI_Scene_OwnerMatchSpectator.CreateRuntime(_shell.MainWorkspaceHost);
                    _practiceSpectator.HomeRequested += ReturnFromPractice;
                    _practiceSpectator.NextGameRequested += ContinuePractice;
                    _practiceSpectator.PresentationCompleted += FocusPracticeResult;
                }
                _activePracticeTeamId = id;
                _isPracticeMatchActive = true; _practiceView.gameObject.SetActive(false);
                _practiceSpectator.PlayPractice(result, buffer.ToArray(),
                    _manager.GetTeamUniformFranchiseId(_manager.GetPracticeTeam(id).TeamSeasonKey),
                    _manager.GetTeamUniformFranchiseId(_manager.Runtime.PlayerTeamSeasonKey),
                    teamId => teamId == result.Match.Input.HomeRoster.TeamId
                        ? _manager.GetClubDisplayName(_manager.Runtime.PlayerTeamSeasonKey)
                        : PracticeTeamName(_manager.GetPracticeCatalog().Find(id)),
                    _manager.CreatePracticeParticipantNames(id));
                _practiceSpectator.SetNextGameAvailability(CanContinuePractice());
                if (_practiceSpectator.IsComplete) _practiceSpectator.FocusCompletedResult();
            }
            catch (Exception error)
            {
                _isPracticeMatchActive = false; _isPreparingPractice = false;
                _practiceSpectator?.EndPresentation(); _practiceView.SetBusy(false); Refresh();
                ReportPracticeError(error, "경기 화면을 열지 못했습니다. 저장된 진행을 확인해 주세요.");
            }
            finally { _isPreparingPractice = false; _practicePreparation = null; }
        }

        private void ReturnFromPractice()
        {
            if (_isPreparingPractice) return;
            _practiceSpectator.EndPresentation(); _isPracticeMatchActive = false;
            Refresh(); _practiceView.FocusAction();
        }

        private void FocusPracticeResult()
        {
            _practiceSpectator.SetNextGameAvailability(CanContinuePractice());
            _practiceSpectator.FocusCompletedResult();
        }

        private bool CanContinuePractice()
        {
            if (string.IsNullOrEmpty(_activePracticeTeamId) || _manager.IsPracticeSaving) return false;
            var catalog = _manager.GetPracticeCatalog();
            var progress = _manager.Runtime.LegendaryPractice.Get(_activePracticeTeamId);
            // 3승 직후에는 목록의 최초 보상 행동으로 안내하고, 보상 수령 후 재도전은 계속 허용한다.
            return _manager.Runtime.LegendaryPractice.CanPlay(catalog, catalog.Find(_activePracticeTeamId))
                && (progress.wins < 3 || progress.rewardClaimed);
        }

        private void ContinuePractice()
        {
            if (_isPreparingPractice || !_isPracticeMatchActive || !_practiceSpectator.IsComplete || !CanContinuePractice()) return;
            _isPreparingPractice = true;
            _practiceSpectator.SetNextGameAvailability(true, true);
            _practicePreparation = StartCoroutine(PreparePractice(_activePracticeTeamId));
        }

        private void ClaimAllPractice()
        {
            try { _manager.ClaimPracticeRewards(); Refresh(); }
            catch (Exception error) { Refresh(); ReportPracticeError(error, "보상을 저장하지 못했습니다. 다시 시도해 주세요."); }
        }

        private void ReportPracticeError(Exception error, string message)
        { UnityEngine.Debug.LogException(error); _practiceView.ShowError(message); }

        private void CancelPracticePreparation()
        {
            if (_practicePreparation != null) StopCoroutine(_practicePreparation);
            _practicePreparation = null; _isPreparingPractice = false;
            _practiceView?.SetBusy(false);
        }
    }
}
