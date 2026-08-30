using System;
using System.Reflection;
using NUnit.Framework;

namespace Baseball.Tools.PureSimulationTests
{
    /// <summary>Unity 사전 빌드 훅과 무관하게 순수 Simulation NUnit 테스트를 실행한다.</summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Type[] testTypes =
            {
                typeof(Baseball.Tests.EditMode.Core.AttributeAllocationTests),
                typeof(Baseball.Tests.EditMode.Game.NewGameFlowTests),
                typeof(Baseball.Tests.EditMode.Game.MultiLeagueWorldTests),
                typeof(Baseball.Tests.EditMode.Game.CareerLeagueMovementTests),
                typeof(Baseball.Tests.EditMode.Game.News.CareerNewsServiceTests),
                // 아래 Game 스위트는 UnityEngine API를 쓰지 않아 콘솔 러너에서도 그대로 돈다.
                // 나머지 Career*Service 계열은 UnityEngine에 의존하므로 Unity EditMode 러너에서만 검증한다.
                typeof(Baseball.Tests.EditMode.Game.CareerGrowthServiceTests),
                typeof(Baseball.Tests.EditMode.Game.CareerNarrativeContinuationTests),
                typeof(Baseball.Tests.EditMode.Game.CareerSeasonTransitionServiceTests),
                typeof(Baseball.Tests.EditMode.Game.MatchNarrativeServiceTests),
                typeof(Baseball.Tests.EditMode.Game.PlayerGrowthIntegrationTests),
                typeof(Baseball.Tests.EditMode.Simulation.MatchSimulatorTests),
                typeof(Baseball.Tests.EditMode.Simulation.DetailedMatchSimulationV2Tests),
                typeof(Baseball.Tests.EditMode.Simulation.MatchDecisionTests),
                typeof(Baseball.Tests.EditMode.Simulation.MiniGameSimulationTests),
                typeof(Baseball.Tests.EditMode.Simulation.MatchSimulationStatisticsTests),
                typeof(Baseball.Tests.EditMode.Simulation.PlateAppearanceSimulatorTests),
                typeof(Baseball.Tests.EditMode.Simulation.Growth.SkillBoardAndGachaTests),
                typeof(Baseball.Tests.EditMode.Simulation.Growth.GrowthBoardWorkspaceRulesTests),
                typeof(Baseball.Tests.EditMode.Simulation.Career.NewGameSetupTests),
                typeof(Baseball.Tests.EditMode.Simulation.Career.TeamGeneratorTests),
                typeof(Baseball.Tests.EditMode.Simulation.Career.PlayerRetirementResolverTests)
            };
            // TestContext.WriteLine은 실행 컨텍스트의 OutWriter가 없으면 NullReference로 죽는다.
            // Unity 테스트 러너 밖에서도 통계 테스트가 로그를 남길 수 있도록 콘솔로 연결한다.
            int passed = 0;
            int failed = 0;
            for (int typeIndex = 0; typeIndex < testTypes.Length; typeIndex++)
            {
                Type type = testTypes[typeIndex];
                if (args.Length > 0 &&
                    !string.Equals(type.Name, args[0], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!type.IsClass)
                {
                    continue;
                }
                MethodInfo setup = FindAttributedMethod<SetUpAttribute>(type);
                MethodInfo tearDown = FindAttributedMethod<TearDownAttribute>(type);
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (method.GetCustomAttribute<TestAttribute>() == null || method.GetParameters().Length != 0)
                        continue;
                    if (method.GetCustomAttribute<ExplicitAttribute>() != null && args.Length <= 1)
                        continue;
                    if (args.Length > 1 &&
                        !string.Equals(method.Name, args[1], StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    object instance = Activator.CreateInstance(type);
                    try
                    {
                        setup?.Invoke(instance, null);
                        method.Invoke(instance, null);
                        Console.WriteLine($"PASS {type.Name}.{method.Name}");
                        passed++;
                    }
                    catch (TargetInvocationException exception)
                    {
                        Exception cause = exception.InnerException ?? exception;
                        Console.Error.WriteLine($"FAIL {type.Name}.{method.Name}: {cause}");
                        failed++;
                    }
                    finally
                    {
                        tearDown?.Invoke(instance, null);
                    }
                }
            }
            Console.WriteLine($"Passed={passed}, Failed={failed}");
            return failed == 0 ? 0 : 1;
        }

        private static MethodInfo FindAttributedMethod<TAttribute>(Type type)
            where TAttribute : Attribute
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.GetCustomAttribute<TAttribute>() != null)
                    return method;
            }
            return null;
        }
    }
}
