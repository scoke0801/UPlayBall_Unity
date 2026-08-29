using System;
using System.Reflection;
using NUnit.Framework;

namespace Baseball.Tools.ContractFlowRetirementTests
{
    /// <summary>Unity 에디터 없이 계약 플로우 은퇴 Game EditMode 테스트를 실행한다.</summary>
    internal static class Program
    {
        private static int Main()
        {
            Type type = typeof(Baseball.Tests.EditMode.Game.ContractFlowRetirementTests);
            int passed = 0;
            int failed = 0;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.GetCustomAttribute<TestAttribute>() == null || method.GetParameters().Length != 0)
                    continue;
                object instance = Activator.CreateInstance(type);
                try
                {
                    method.Invoke(instance, null);
                    Console.WriteLine($"PASS {method.Name}");
                    passed++;
                }
                catch (TargetInvocationException exception)
                {
                    Exception cause = exception.InnerException ?? exception;
                    Console.Error.WriteLine($"FAIL {method.Name}: {cause}");
                    failed++;
                }
            }
            Console.WriteLine($"Passed={passed}, Failed={failed}");
            return failed == 0 ? 0 : 1;
        }
    }
}
