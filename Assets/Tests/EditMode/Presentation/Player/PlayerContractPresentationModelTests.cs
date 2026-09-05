using System;
using System.Linq;
using System.Reflection;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.Player;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Player
{
    /// <summary>선수 계약 표시 모델의 실제 Career 매핑과 구단주 상태 비유출을 검증한다.</summary>
    public sealed class PlayerContractPresentationModelTests
    {
        [Test]
        public void Build_실제계약이력상여오퍼를공용표로변환한다()
        {
            CareerContractView view = CreateView();

            PlayerContractPresentationModel model =
                new PlayerContractPresentationModelBuilder().Build(view);

            Assert.That(model.ContractHistoryState.Kind, Is.EqualTo(UiContentStateKind.Ready));
            Assert.That(model.ContractHistory.Rows.Count, Is.EqualTo(2));
            Assert.That(model.ContractHistory.Rows[0].IsHighlighted, Is.True);
            Assert.That(model.BonusProgressState.Kind, Is.EqualTo(UiContentStateKind.Ready));
            Assert.That(model.BonusProgress.Rows.Single().FindCell("Progress").DisplayValue, Is.EqualTo("82/100"));
            Assert.That(model.OffersState.Kind, Is.EqualTo(UiContentStateKind.Ready));
            Assert.That(model.Offers.Rows.Single().IsHighlighted, Is.True);
            Assert.That(
                PlayerContractPresentationModel.TryGetOfferTeamId(
                    model.Offers.Rows.Single().RowId,
                    out int teamId),
                Is.True);
            Assert.That(teamId, Is.EqualTo(101));
        }

        [Test]
        public void Build_제안없음과오류를공용콘텐츠상태로구분한다()
        {
            CareerContractView view = CreateView();
            Set(view, nameof(CareerContractView.RenewalOffers), Array.Empty<RenewalContractOfferView>());

            PlayerContractPresentationModel empty =
                new PlayerContractPresentationModelBuilder().Build(view);
            Set(view, nameof(CareerContractView.LastError), "협상 단계가 아닙니다.");
            PlayerContractPresentationModel error =
                new PlayerContractPresentationModelBuilder().Build(view);

            Assert.That(empty.OffersState.Kind, Is.EqualTo(UiContentStateKind.Empty));
            Assert.That(error.OffersState.Kind, Is.EqualTo(UiContentStateKind.Error));
            Assert.That(error.OffersState.Message, Is.EqualTo("협상 단계가 아닙니다."));
        }

        [Test]
        public void Player계약표시Api_구단주전용State를노출하지않는다()
        {
            string[] forbidden = { "Owned", "Enhancement", "Scout", "TeamColorEquip", "OwnerFinance" };
            Type[] types =
            {
                typeof(PlayerContractPresentationModel),
                typeof(PlayerContractPresentationModelBuilder)
            };

            foreach (Type type in types)
            {
                MemberInfo[] members = type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
                foreach (MemberInfo member in members)
                {
                    Assert.That(
                        forbidden.Any(term => member.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0),
                        Is.False,
                        $"{type.FullName}.{member.Name}");
                }
            }
        }

        private static CareerContractView CreateView()
        {
            var view = new CareerContractView();
            Set(view, nameof(CareerContractView.ContractHistory), new[]
            {
                new ContractHistoryView(
                    "현재 팀", 2026, 2028, 3, 300_000_000L, 900_000_000L,
                    ExpectedRole.StartingCompetition, true),
                new ContractHistoryView(
                    "이전 팀", 2024, 2025, 2, 100_000_000L, 200_000_000L,
                    ExpectedRole.RosterCompetition, false)
            });
            var clause = new ContractBonusClause(
                "games_played",
                ContractBonusMetric.GamesPlayed,
                100d,
                30_000_000L);
            Set(view, nameof(CareerContractView.BonusProgress), new[]
            {
                new ContractBonusProgressView(new ContractBonusProgress(clause, 82d, 0.82d, false, true))
            });
            Set(view, nameof(CareerContractView.RenewalOffers), new[]
            {
                new RenewalContractOfferView(
                    101,
                    "새 구단",
                    default,
                    default,
                    84,
                    77,
                    50_000_000L,
                    400_000_000L,
                    3,
                    ExpectedRole.StartingCompetition,
                    ContractOfferChannel.OpenMarket,
                    0.72d,
                    "낮음",
                    true,
                    30_000_000L,
                    false,
                    true)
            });
            Set(view, nameof(CareerContractView.LastError), string.Empty);
            return view;
        }

        private static void Set<T>(CareerContractView view, string propertyName, T value)
        {
            PropertyInfo property = typeof(CareerContractView).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            property.SetValue(view, value);
        }
    }
}
