using System;
using System.Collections.Generic;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerModeShellCoordinator
    {
        private readonly HashSet<string> _boundSnapshotRoutes = new HashSet<string>(StringComparer.Ordinal);

        // 조회는 RNG와 저장 상태를 바꾸지 않는다. RuntimeChanged/명시적 Refresh에서 무효화하고,
        // 같은 상태의 화면 왕복에서는 기존 View와 Snapshot을 재사용한다.
        private void EnsureRouteSnapshots(string routeId)
        {
            string snapshotRouteId = ResolveSnapshotRoute(ResolveWorkspaceRoute(routeId));
            if (_boundSnapshotRoutes.Contains(snapshotRouteId))
                return;

            BindRouteSnapshot(snapshotRouteId);
            _boundSnapshotRoutes.Add(snapshotRouteId);
        }

        private static string ResolveSnapshotRoute(string routeId)
        {
            switch (routeId)
            {
                case OwnerNavigationRoutes.RosterPitching:
                    return OwnerExpansionWorkspaceCoordinator.RosterLineupRouteId;
                case OwnerNavigationRoutes.DugoutManagerPolicy:
                    return OwnerNavigationRoutes.DugoutLineupNotes;
                case OwnerNavigationRoutes.PowerUpTraining:
                case OwnerNavigationRoutes.PowerUpEnhancementSale:
                    return OwnerNavigationRoutes.PowerUpScout;
                case OwnerNavigationRoutes.PowerUpStudy:
                    return OwnerNavigationRoutes.PowerUpSkills;
                case OwnerManagementRoutes.ClubFacility:
                    return OwnerManagementRoutes.ClubFinance;
                case OwnerNavigationRoutes.ClubOwner:
                    return OwnerNavigationRoutes.ClubInformation;
                case OwnerNavigationRoutes.LeagueStandings:
                case OwnerNavigationRoutes.LeagueTeamResults:
                case OwnerNavigationRoutes.LeagueMatchups:
                case OwnerNavigationRoutes.LeagueRankHistory:
                    return OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId;
                default:
                    return routeId;
            }
        }

        private void BindRouteSnapshot(string routeId)
        {
            switch (routeId)
            {
                case OwnerNavigationRoutes.DugoutLineupNotes:
                    _expansionWorkspace.BindDugout(_snapshotFactory.CreateDugout(_manager));
                    break;
                case OwnerNavigationRoutes.RosterTeamColor:
                case OwnerNavigationRoutes.DugoutTeamColor:
                    _expansionWorkspace.BindTeamColor(_snapshotFactory.CreateTeamColor(_manager));
                    break;
                case OwnerNavigationRoutes.RosterTacticCards:
                case OwnerNavigationRoutes.DugoutTactics:
                    _expansionWorkspace.BindTactics(_snapshotFactory.CreateTactics(_manager));
                    break;
                case OwnerExpansionWorkspaceCoordinator.RosterLineupRouteId:
                    _expansionWorkspace.BindRosterLineup(_snapshotFactory.CreateRosterLineup(_manager));
                    break;
                case OwnerExpansionWorkspaceCoordinator.CollectionRouteId:
                    _expansionWorkspace.BindCollection(_snapshotFactory.CreateCollection(_manager));
                    break;
                case OwnerNavigationRoutes.PowerUpSkills:
                    _expansionWorkspace.BindGrowth(OwnerGrowthPresentationBuilder.Build(
                        _manager, _snapshotFactory.CreateCollection(_manager)));
                    break;
                case OwnerExpansionWorkspaceCoordinator.ShopRouteId:
                    BindShopSnapshot();
                    break;
                case OwnerNavigationRoutes.PowerUpScout:
                    _shopService = _manager.CreateShopService();
                    _expansionWorkspace.BindPowerUp(OwnerPowerUpPresentationBuilder.Build(
                        _manager, _shopService, _snapshotFactory.CreateCollection(_manager)));
                    break;
                case OwnerManagementRoutes.ClubFinance:
                    _expansionWorkspace.BindClubOperation(_snapshotFactory.CreateClubOperation(_manager));
                    break;
                case OwnerExpansionWorkspaceCoordinator.StaffOfficeRouteId:
                    _expansionWorkspace.BindStaffOffice(_snapshotFactory.CreateStaffOffice(_manager));
                    break;
                case OwnerNavigationRoutes.ClubContract:
                    _expansionWorkspace.BindPlayerContracts(_snapshotFactory.CreatePlayerContracts(
                        _manager, _selectedContractCardId, _selectedContractTerm));
                    break;
                case OwnerNavigationRoutes.ClubTrade:
                    _expansionWorkspace.BindPlayerTrade(_snapshotFactory.CreatePlayerTrade(
                        _manager, _selectedTradePartnerId, _selectedTradeOutgoingCardId, _selectedTradeIncomingCardId));
                    break;
                case OwnerExpansionWorkspaceCoordinator.PregameRouteId:
                case OwnerNavigationRoutes.MatchCenterOpponentLineup:
                    if (_manager.Runtime.ManagerMode.LiveSeason.NextPlayerGame == null)
                        break;
                    _expansionWorkspace.BindPregame(_snapshotFactory.CreatePregame(_manager));
                    if (routeId == OwnerNavigationRoutes.MatchCenterOpponentLineup)
                        _expansionWorkspace.BindOpponentLineup(_snapshotFactory.CreateTeamLineup(
                            _manager, _manager.CurrentPregame.OpponentTeamSeasonKey));
                    break;
                case OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId:
                    _sharedInformationWorkspace.BindSchedule(
                        _sharedInformationSnapshotFactory.CreateSchedule(_manager), _profile.Capabilities);
                    break;
                case OwnerSharedInformationWorkspaceCoordinator.RecordsRouteId:
                    _sharedInformationWorkspace.BindClubSeasonHistoryRecords(
                        _sharedInformationSnapshotFactory.CreateClubSeasonHistoryRecords(_manager), _profile.Capabilities);
                    break;
                case OwnerSharedInformationWorkspaceCoordinator.SeasonRecordsRouteId:
                    _sharedInformationWorkspace.BindSeasonRecords(
                        _sharedInformationSnapshotFactory.CreateSeasonRecords(_manager, _selectedRecordsSeason),
                        HandleRecordsSeasonSelected);
                    break;
                case OwnerNavigationRoutes.ClubInformation:
                    _sharedInformationWorkspace.BindClubInformation(new OwnerClubInformationPresentationModel(
                        _snapshotFactory.CreateHome(_manager), _snapshotFactory.CreateCollection(_manager),
                        _snapshotFactory.CreateClubOperation(_manager),
                        _sharedInformationSnapshotFactory.CreateSchedule(_manager),
                        _manager.Runtime.OwnerProfile.Nickname, _manager.Runtime.OwnerProfile.FrontManagerId));
                    break;
            }
        }
    }
}
