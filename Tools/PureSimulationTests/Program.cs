using System;
using System.Reflection;
using NUnit.Framework;

namespace Baseball.Tools.PureSimulationTests
{
    /// <summary>Unity 사전 빌드 훅과 무관하게 순수 Simulation NUnit 테스트를 실행한다.</summary>
    internal static class Program
    {
        private static int Main()
        {
            Type[] testTypes =
            {
                typeof(Baseball.Tests.EditMode.Simulation.MatchSimulatorTests),
                typeof(Baseball.Tests.EditMode.Simulation.DetailedMatchSimulationV2Tests),
                typeof(Baseball.Tests.EditMode.Simulation.MatchDecisionTests),
                typeof(Baseball.Tests.EditMode.Simulation.MatchSimulationStatisticsTests),
                typeof(Baseball.Tests.EditMode.Simulation.PlateAppearanceSimulatorTests)
            };
            int passed = 0;
            int failed = 0;
            for (int typeIndex = 0; typeIndex < testTypes.Length; typeIndex++)
            {
                Type type = testTypes[typeIndex];
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
                        Console.Error.WriteLine($"FAIL {type.Name}.{method.Name}: {cause.Message}");
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
