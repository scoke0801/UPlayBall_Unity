using System;

namespace Baseball.Game.Career.News
{
    /// <summary>부상 시스템의 확정 진단과 복귀 단계만 뉴스 사건으로 변환한다.</summary>
    public sealed class InjuryNewsEvaluator
    {
        public NewsEvent EvaluateConfirmedInjury(
            string eventId,
            CareerDate occurredAt,
            PlayerState player,
            TeamState team,
            int expectedAbsenceGames,
            bool isSeasonEnding)
        {
            if (expectedAbsenceGames <= 1 && !isSeasonEnding)
                return null;
            int importance = isSeasonEnding
                ? 55
                : expectedAbsenceGames >= 22 ? 45 : expectedAbsenceGames >= 6 ? 30 : 20;
            var newsEvent = new NewsEvent(
                eventId,
                NewsEventType.PlayerInjuryConfirmed,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(player.PlayerId, player.Name),
                $"injury_{player.PlayerId}_{occurredAt.Cycle.SeasonId}",
                importance)
            {
                CareerImpact = isSeasonEnding ? 35 : expectedAbsenceGames >= 6 ? 20 : 5,
                Rarity = isSeasonEnding ? 20 : 0,
                IsCareerArchive = expectedAbsenceGames >= 6
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            newsEvent.FactSet.SetInteger(NewsFactKey.ExpectedAbsenceGames, expectedAbsenceGames);
            return newsEvent;
        }

        public NewsEvent EvaluateReturn(
            string eventId,
            CareerDate occurredAt,
            PlayerState player,
            TeamState team)
        {
            var newsEvent = new NewsEvent(
                eventId,
                NewsEventType.PlayerReturnedFromInjury,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(player.PlayerId, player.Name),
                $"injury_{player.PlayerId}_{occurredAt.Cycle.SeasonId}",
                baseImportance: 30)
            {
                CareerImpact = 20,
                IsCareerArchive = true
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            return newsEvent;
        }
    }

    /// <summary>감독이 확정한 장기 역할 변화만 뉴스 사건으로 변환한다.</summary>
    public sealed class PlayerRoleNewsEvaluator
    {
        public NewsEvent Evaluate(
            string eventId,
            CareerDate occurredAt,
            PlayerState player,
            TeamState team,
            string previousRole,
            string newRole)
        {
            if (string.Equals(previousRole, newRole, StringComparison.Ordinal))
                return null;
            var newsEvent = new NewsEvent(
                eventId,
                NewsEventType.PlayerRoleChanged,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(player.PlayerId, player.Name),
                $"role_{player.PlayerId}_{occurredAt.Cycle.SeasonId}",
                baseImportance: 30)
            {
                CareerImpact = 20,
                IsCareerArchive = true,
                CooldownGroup = $"role_change_{player.PlayerId}"
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            newsEvent.FactSet.SetText(NewsFactKey.PreviousRole, previousRole);
            newsEvent.FactSet.SetText(NewsFactKey.NewRole, newRole);
            return newsEvent;
        }
    }

    /// <summary>서명이 끝난 공개 계약만 기사 사건으로 만들며 제안 단계는 받지 않는다.</summary>
    public sealed class ContractNewsEvaluator
    {
        public NewsEvent EvaluateSignedContract(
            string eventId,
            CareerDate occurredAt,
            PlayerState player,
            TeamState team,
            int contractYears,
            long annualSalary)
        {
            var newsEvent = new NewsEvent(
                eventId,
                NewsEventType.ContractSigned,
                occurredAt,
                NewsReleaseGate.AfterContractConfirmation,
                NewsSubject.Player(player.PlayerId, player.Name),
                eventId,
                baseImportance: 40)
            {
                CareerImpact = 25,
                IsCareerArchive = true
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            newsEvent.FactSet.SetInteger(NewsFactKey.ContractYears, contractYears);
            newsEvent.FactSet.SetInteger(NewsFactKey.ContractSalary, annualSalary);
            return newsEvent;
        }
    }

    /// <summary>외부에 알려질 가치가 있다고 성장 시스템이 판정한 오프시즌 활동만 기사화한다.</summary>
    public sealed class OffseasonNewsEvaluator
    {
        public NewsEvent EvaluateCompletedActivity(
            string eventId,
            CareerDate occurredAt,
            PlayerState player,
            TeamState team,
            string publicActivityName,
            bool isMajorChange)
        {
            if (string.IsNullOrWhiteSpace(publicActivityName))
                return null;
            var newsEvent = new NewsEvent(
                eventId,
                NewsEventType.OffseasonActivityCompleted,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(player.PlayerId, player.Name),
                eventId,
                baseImportance: isMajorChange ? 35 : 22)
            {
                CareerImpact = isMajorChange ? 20 : 5,
                IsCareerArchive = isMajorChange
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            newsEvent.FactSet.SetText(NewsFactKey.OffseasonActivityName, publicActivityName);
            return newsEvent;
        }
    }

    /// <summary>월드 저널의 승강·계약·은퇴 확정 사실을 뉴스 입력으로 변환한다.</summary>
    public sealed class WorldDomainNewsEvaluator
    {
        public NewsEvent Evaluate(CareerState career, WorldDomainEvent domainEvent, CareerDate publicationDate)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (!TryGetNewsType(domainEvent.EventType, out NewsEventType newsType))
                return null;

            CareerDate occurredAt = new CareerDate(publicationDate.Cycle, domainEvent.WorldDate);
            bool isTeamEvent = newsType is
                NewsEventType.PromotionRaceEntered or
                NewsEventType.PromotionClinched or
                NewsEventType.RelegationRiskEntered or
                NewsEventType.RelegationConfirmed or
                NewsEventType.TeamLeagueChanged;
            NewsSubject primary;
            TeamState team = null;
            PlayerState player = null;
            if (isTeamEvent)
            {
                team = career.World.GetTeam(domainEvent.PrimaryEntityId);
                primary = NewsSubject.Team(team.TeamId, team.Name);
            }
            else
            {
                player = career.World.GetPlayer(domainEvent.PrimaryEntityId);
                primary = NewsSubject.Player(player.PlayerId, player.Name);
                int teamId = ResolveRelatedTeamId(newsType, domainEvent, player);
                if (teamId > 0)
                    team = career.World.GetTeam(teamId);
            }

            bool isMyCareer = player?.PlayerId == career.MyPlayerId;
            var newsEvent = new NewsEvent(
                domainEvent.EventId,
                newsType,
                occurredAt,
                ResolveGate(newsType),
                primary,
                domainEvent.EventId,
                ResolveImportance(newsType, isMyCareer))
            {
                CareerImpact = isMyCareer ? 35 : 0,
                IsCareerArchive = isMyCareer || newsType is
                    NewsEventType.PromotionClinched or NewsEventType.RelegationConfirmed
            };
            if (player != null)
                newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            if (team != null)
            {
                newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
                newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            }
            if (newsType is NewsEventType.PromotionRaceEntered or NewsEventType.RelegationRiskEntered)
                newsEvent.FactSet.SetInteger(NewsFactKey.TeamRank, domainEvent.SecondaryEntityId);
            string leagueName = ResolveLeagueName(career, domainEvent, newsType, team);
            if (!string.IsNullOrEmpty(leagueName))
                newsEvent.FactSet.SetText(NewsFactKey.LeagueName, leagueName);
            return newsEvent;
        }

        private static int ResolveRelatedTeamId(
            NewsEventType newsType,
            WorldDomainEvent domainEvent,
            PlayerState player)
        {
            if (newsType is NewsEventType.UpperLeagueInterestConfirmed or
                NewsEventType.CrossLeagueContractSigned or
                NewsEventType.PlayerRetired)
                return domainEvent.SecondaryEntityId;
            return player.CurrentTeamId;
        }

        private static string ResolveLeagueName(
            CareerState career,
            WorldDomainEvent domainEvent,
            NewsEventType newsType,
            TeamState team)
        {
            if (newsType is NewsEventType.PromotionClinched or
                NewsEventType.RelegationConfirmed or
                NewsEventType.TeamLeagueChanged)
            {
                LeagueLevel tier = (LeagueLevel)domainEvent.SecondaryEntityId;
                return LeagueLevelRules.IsValid(tier)
                    ? WorldGenerationConfiguration.GetDefaultDefinition(tier).DisplayName
                    : string.Empty;
            }
            if (newsType == NewsEventType.UpperLeagueInterestConfirmed)
            {
                LeagueLevel tier = (LeagueLevel)domainEvent.TertiaryEntityId;
                return LeagueLevelRules.IsValid(tier)
                    ? WorldGenerationConfiguration.GetDefaultDefinition(tier).DisplayName
                    : string.Empty;
            }
            if (newsType == NewsEventType.GalaxyLeagueDebut)
                return WorldGenerationConfiguration.GetDefaultDefinition(LeagueLevel.Galaxy).DisplayName;
            if (newsType == NewsEventType.FirstLeagueReached)
            {
                LeagueLevel tier = (LeagueLevel)domainEvent.SecondaryEntityId;
                return LeagueLevelRules.IsValid(tier)
                    ? WorldGenerationConfiguration.GetDefaultDefinition(tier).DisplayName
                    : string.Empty;
            }
            return team == null
                ? string.Empty
                : WorldGenerationConfiguration
                    .GetDefaultDefinition(career.World.GetLeague(team.LeagueId).LeagueLevel)
                    .DisplayName;
        }

        private static NewsReleaseGate ResolveGate(NewsEventType newsType)
        {
            return newsType is NewsEventType.CrossLeagueContractSigned or
                NewsEventType.UpperLeagueInterestConfirmed
                ? NewsReleaseGate.AfterContractConfirmation
                : NewsReleaseGate.EndOfScheduleDate;
        }

        private static int ResolveImportance(NewsEventType newsType, bool isMyCareer)
        {
            int result = newsType switch
            {
                NewsEventType.PromotionClinched => 55,
                NewsEventType.RelegationConfirmed => 55,
                NewsEventType.CrossLeagueContractSigned => 50,
                NewsEventType.FirstLeagueReached => 55,
                NewsEventType.GalaxyLeagueDebut => 65,
                NewsEventType.FinalSeasonAnnounced => 65,
                NewsEventType.PlayerRetired => 55,
                _ => 30
            };
            return isMyCareer ? result + 20 : result;
        }

        private static bool TryGetNewsType(string eventType, out NewsEventType newsType)
        {
            newsType = eventType switch
            {
                "PromotionRaceEntered" => NewsEventType.PromotionRaceEntered,
                "PromotionClinched" => NewsEventType.PromotionClinched,
                "RelegationRiskEntered" => NewsEventType.RelegationRiskEntered,
                "RelegationConfirmed" => NewsEventType.RelegationConfirmed,
                "TeamLeagueChanged" => NewsEventType.TeamLeagueChanged,
                "UpperLeagueInterestConfirmed" => NewsEventType.UpperLeagueInterestConfirmed,
                "CrossLeagueContractSigned" => NewsEventType.CrossLeagueContractSigned,
                "FirstLeagueReached" => NewsEventType.FirstLeagueReached,
                "GalaxyLeagueDebut" => NewsEventType.GalaxyLeagueDebut,
                "FinalSeasonAnnounced" => NewsEventType.FinalSeasonAnnounced,
                "PlayerRetired" => NewsEventType.PlayerRetired,
                _ => default
            };
            return eventType is
                "PromotionRaceEntered" or
                "PromotionClinched" or
                "RelegationRiskEntered" or
                "RelegationConfirmed" or
                "TeamLeagueChanged" or
                "UpperLeagueInterestConfirmed" or
                "CrossLeagueContractSigned" or
                "FirstLeagueReached" or
                "GalaxyLeagueDebut" or
                "FinalSeasonAnnounced" or
                "PlayerRetired";
        }
    }
}
