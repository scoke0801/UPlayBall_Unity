using System;
using System.Collections;
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
                var roster = _manager.GetPracticeOpponent(id, out var colors); _practiceView.BindOpponent(roster, colors);
                var source = _manager.GetPracticeCards(id); var cards = new PlayerMiniCardModel[3];
                string[] labels = { "교타", "장타", "주력", "번트", "수비", "정신" };
                for (int i = 0; i < 3; i++)
                {
                    var card = source[i]; var season = _manager.GetPracticePlayerSeason(card); var ratings = season.CreateBaseAttributes();
                    var stats = new PlayerMiniCardStatModel[6];
                    for (int a = 0; a < 6; a++) stats[a] = new PlayerMiniCardStatModel(labels[a],
                        ratings.Get((Baseball.Core.Growth.PlayerAbility)a) + card.GetModifier((Baseball.Core.Growth.PlayerAbility)a), Baseball.Core.Players.AttributeRating.Maximum);
                    cards[i] = new PlayerMiniCardModel(card.CardId, _manager.Runtime.IdentityRegistry.GetPlayerDisplayName(season.PlayerPersonId),
                        "주전 야수", season.OriginYear.ToString(), "", PlayerCardEditionText.Get(card.Edition),
                        portraitAssetKey: season.PlayerSeasonId, stats: stats, frameEdition: card.Edition, cost: season.Cost);
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
                }
                _isPracticeMatchActive = true; _practiceView.gameObject.SetActive(false);
                _practiceSpectator.PlayPractice(result, buffer.ToArray(),
                    _manager.GetTeamUniformFranchiseId(_manager.GetPracticeTeam(id).TeamSeasonKey),
                    _manager.GetTeamUniformFranchiseId(_manager.Runtime.PlayerTeamSeasonKey));
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
            _practiceSpectator.EndPresentation(); _isPracticeMatchActive = false;
            Refresh(); _practiceView.FocusAction();
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
