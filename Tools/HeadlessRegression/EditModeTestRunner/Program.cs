using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;

namespace Baseball.Tools.EditModeTests
{
    /// <summary>
    /// Unity 테스트 러너 밖에서 EditMode NUnit 스위트를 Release로 실행한다.
    /// </summary>
    /// <remarks>
    /// 어셈블리를 반사로 훑어 <see cref="TestAttribute"/>·<see cref="TestCaseAttribute"/>를 모두
    /// 실행하므로, 새 테스트가 추가돼도 러너에 목록을 손으로 등록할 필요가 없다.
    /// Unity 타입에 직접 의존하는 스위트는 Headless 테스트 프로젝트의 컴파일 대상에서 빠져 있다.
    /// </remarks>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string typeFilter = args.Length > 0 ? args[0] : null;
            string methodFilter = args.Length > 1 ? args[1] : null;
            bool includeExplicit = typeFilter != null;

            Assembly[] assemblies =
            {
                typeof(Baseball.Tests.EditMode.Core.PlayerTeamModelTests).Assembly,
                typeof(Baseball.Tests.EditMode.Simulation.MatchSimulatorTests).Assembly,
                typeof(Baseball.Tests.EditMode.Game.MultiLeagueWorldTests).Assembly
            };

            int passed = 0;
            int failed = 0;
            int skipped = 0;
            var failures = new List<string>();
            var stopwatch = Stopwatch.StartNew();

            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Assembly assembly = assemblies[assemblyIndex];
                Console.WriteLine($"### {assembly.GetName().Name}");
                Type[] types = assembly.GetTypes();
                Array.Sort(types, (left, right) => string.CompareOrdinal(left.FullName, right.FullName));
                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                        continue;
                    if (typeFilter != null && !string.Equals(type.Name, typeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    RunType(type, methodFilter, includeExplicit, failures, ref passed, ref failed, ref skipped);
                }
            }

            stopwatch.Stop();
            Console.WriteLine();
            for (int index = 0; index < failures.Count; index++)
                Console.Error.WriteLine(failures[index]);
            Console.WriteLine($"Passed={passed} Failed={failed} Skipped={skipped} ElapsedSeconds={stopwatch.Elapsed.TotalSeconds:F1}");
            return failed == 0 ? 0 : 1;
        }

        private static void RunType(
            Type type,
            string methodFilter,
            bool includeExplicit,
            List<string> failures,
            ref int passed,
            ref int failed,
            ref int skipped)
        {
            MethodInfo setUp = FindAttributedMethod<SetUpAttribute>(type);
            MethodInfo tearDown = FindAttributedMethod<TearDownAttribute>(type);
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(methods, (left, right) => string.CompareOrdinal(left.Name, right.Name));

            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (methodFilter != null && !string.Equals(method.Name, methodFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool hasTest = method.GetCustomAttribute<TestAttribute>() != null;
                var testCases = (TestCaseAttribute[])method.GetCustomAttributes<TestCaseAttribute>();
                if (!hasTest && testCases.Length == 0)
                    continue;
                if (method.GetCustomAttribute<ExplicitAttribute>() != null && !includeExplicit)
                {
                    skipped++;
                    continue;
                }
                if (method.GetCustomAttribute<IgnoreAttribute>() != null)
                {
                    skipped++;
                    continue;
                }

                if (hasTest && method.GetParameters().Length == 0)
                    Invoke(type, method, null, setUp, tearDown, failures, ref passed, ref failed);
                for (int caseIndex = 0; caseIndex < testCases.Length; caseIndex++)
                    Invoke(type, method, testCases[caseIndex].Arguments, setUp, tearDown, failures, ref passed, ref failed);
            }
        }

        private static void Invoke(
            Type type,
            MethodInfo method,
            object[] arguments,
            MethodInfo setUp,
            MethodInfo tearDown,
            List<string> failures,
            ref int passed,
            ref int failed)
        {
            object instance = Activator.CreateInstance(type);
            try
            {
                setUp?.Invoke(instance, null);
                method.Invoke(instance, arguments);
                passed++;
            }
            catch (Exception exception)
            {
                Exception cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;
                failed++;
                failures.Add($"FAIL {type.Name}.{method.Name}: {cause}");
                Console.WriteLine($"FAIL {type.Name}.{method.Name}");
            }
            finally
            {
                try
                {
                    tearDown?.Invoke(instance, null);
                }
                catch (Exception)
                {
                    // TearDown 실패는 테스트 결과를 덮어쓰지 않도록 무시한다.
                }
            }
        }

        private static MethodInfo FindAttributedMethod<TAttribute>(Type type)
            where TAttribute : Attribute
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int index = 0; index < methods.Length; index++)
            {
                if (methods[index].GetCustomAttribute<TAttribute>() != null)
                    return methods[index];
            }
            return null;
        }
    }
}
