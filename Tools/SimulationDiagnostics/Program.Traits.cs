using System;
using System.IO;
using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        /// <summary>실제 상세 엔진에서 동일 시드·동일 로스터의 무특성/C/S/SS/SSS를 대조한다.</summary>
        private static int RunTraitComparison(string[] args)
        {
            int count=ParseCount(args,1,1000);
            var balance=Baseball.Tools.CommonMatchBalanceInput.Load();
            var traits=JsonSerializer.Deserialize<OwnerTraitTrainingBalance>(File.ReadAllText("Assets/10.Datas/Resources/NewGame/OwnerTraitTraining.json"),new JsonSerializerOptions{IncludeFields=true});
            traits.Validate(); balance.TraitTraining=traits;
            for(int variant=0;variant<=traits.definitions.Length;variant++)
            {
                var definition=variant==0?null:traits.definitions[variant-1];
                int[] rankIndices = { 0, 3, 4, 5 };
                for(int rank=0;rank<(variant==0?1:rankIndices.Length);rank++)
                {
                    var source=CreateRoster(1,50,50,50);
                    var effect=definition==null?default:new CardTraitEffect(definition.kind,definition.effect*traits.multipliers[rankIndices[rank]]);
                    Player Copy(Player player,bool pitcher,PitcherRole role=PitcherRole.Starter)
                    {
                        bool applies=definition!=null&&(definition.playerType==PlayerType.Pitcher)==pitcher;
                        if(definition?.position==PlayerPosition.StartingPitcher) applies&=role==PitcherRole.Starter;
                        if(definition?.position==PlayerPosition.ReliefPitcher) applies&=role!=PitcherRole.Starter;
                        return new Player(player.PlayerId,player.Name,player.PrimaryPosition,player.BattingHand,player.ThrowingHand,
                            player.BatterAttributes,player.PitcherAttributes,player.SecondaryPositions,player.Nationality,player.PitchRepertoire,
                            cardTrait:applies?effect:default);
                    }
                    var slots=new LineupSlot[9];
                    for(int i=0;i<9;i++) slots[i]=new LineupSlot(Copy(source.StartingLineup[i].Player,false),source.StartingLineup[i].FieldingPosition);
                    var bullpen=new PitcherRosterEntry[source.Bullpen.Count];
                    for(int i=0;i<bullpen.Length;i++) bullpen[i]=new PitcherRosterEntry(Copy(source.Bullpen[i].Player,true,source.Bullpen[i].Role),source.Bullpen[i].Role);
                    var team=new MatchRosterSnapshot(1,"특성 검증",new Lineup(slots),new PitcherRosterEntry(Copy(source.StartingPitcher.Player,true),PitcherRole.Starter),bullpen,Array.Empty<Player>(),ManagerTacticalProfile.Balanced,RunningApproach.Balanced);
                    var opponent=CreateRoster(2,50,50,50); var totals=new AggregateStatistics(); int wins=0; ulong fingerprint=0;
                    for(int i=0;i<count;i++)
                    {
                        ulong seed=DeterministicSeed.Derive(0x7A17UL,(ulong)i);
                        var input=new MatchInput(1,i+1,seed,team,opponent,MatchRules.CreateDefault(false));
                        var result=new MatchSimulator(balance,MatchRandomStreams.Create(seed)).Simulate(input,NullMatchEventSink.Instance,MatchExecutionProfile.DetailedBackground);
                        totals.Add(result); if(result.AwayBoxScore.Runs>result.HomeBoxScore.Runs) wins++;
                        unchecked{fingerprint=fingerprint*1099511628211UL+(ulong)(result.AwayBoxScore.Runs*100+result.HomeBoxScore.Runs);}
                    }
                    Console.WriteLine($"Trait={definition?.kind.ToString()??"None"} Rank={(variant==0?"None":((CardTraitRank)(rankIndices[rank]+1)).ToString())} Games={count} Wins={wins} Fingerprint={fingerprint}");
                    Console.WriteLine(totals.Format(count));
                }
            }
            return 0;
        }
    }
}
