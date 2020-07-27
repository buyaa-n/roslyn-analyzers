// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.PlatformCompatabilityAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public partial class PlatformCompatabilityAnalyzerTests
    {
        [Fact]
        public async Task OsDependentMethodsCalledWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        [|WindowsOnly()|];
        [|Obsoleted()|];
        [|Removed()|];
    }
    [MinimumOSPlatform(""Windows10.1.1.1"")]
    public void WindowsOnly()
    {
    }
    [ObsoletedInOSPlatform(""Linux4.1"")]
    public void Obsoleted()
    {
    }
    [RemovedInOSPlatform(""Linux4.1"")]
    public void Removed()
    {
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task WrongPlatformStringsShouldHandledGracefully()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        Windows10();
        Windows1_2_3_4_5();
        [|ObsoletedLinuxDash4_1()|];
        [|ObsoletedLinuxStar4_1()|];
        [|RemovedLinu4_1()|];
        ObsoletedWithNullString();
        RemovedWithEmptyString();
    }

    [MinimumOSPlatform(""Windows10"")] // ArgumentException
    public void Windows10()
    {
    }
    [MinimumOSPlatform(""Windows1.2.3.4.5"")] // ArgumentException
    public void Windows1_2_3_4_5()
    {
    }
    [ObsoletedInOSPlatform(""Linux-4.1"")]
    public void ObsoletedLinuxDash4_1()
    {
    }
    [ObsoletedInOSPlatform(""Linux*4.1"")]
    public void ObsoletedLinuxStar4_1()
    {
    }
    [ObsoletedInOSPlatform(null)] // ArgumentNullException (OsPlatform.Create)
    public void ObsoletedWithNullString()
    {
    }
    [RemovedInOSPlatform(""Linu4.1"")]
    public void RemovedLinu4_1()
    {
    }
    [RemovedInOSPlatform("""")] // System.ArgumentException (OsPlatform.Create)
    public void RemovedWithEmptyString()
    {
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentPropertyCalledWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [MinimumOSPlatform(""Windows10.1.1"")]
    public string WindowsStringProperty { get; set; }
    [ObsoletedInOSPlatform(""ios4.1"")]
    public int ObsoleteIntProperty { get; set; }
    [RemovedInOSPlatform(""Linux4.1"")]
    public byte RemovedProperty { get; }
    public void M1()
    {
        [|WindowsStringProperty|] = ""Hello"";
        string s = [|WindowsStringProperty|];
        M2([|WindowsStringProperty|]);
        [|ObsoleteIntProperty|] = 5;
        M3([|ObsoleteIntProperty|]);
        M3([|RemovedProperty|]);
    }
    public string M2(string option)
    {
        return option;
    }
    public int M3(int option)
    {
        return option;
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Theory]
        [MemberData(nameof(Create_AtrrbiuteProperty_WithCondtions))]
        public async Task OsDependentPropertyConditionalCheckWarns(string attribute, string property, string condition, string setter, string getter)
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [" + attribute + @"(""Windows10.1.1"")]
    public " + property + @" { get; set; }

    public void M1()
    {
        [|" + setter + @";
        var s = [|" + getter + @"|];
        bool check = s " + condition + @";
        M2([|" + getter + @"|]);
    }
    public object M2(object option)
    {
        return option;
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        public static IEnumerable<object[]> Create_AtrrbiuteProperty_WithCondtions()
        {
            yield return new object[] { "MinimumOSPlatform", "string StringProperty", " == [|StringProperty|]", @"StringProperty|] = ""Hello""", "StringProperty" };
            yield return new object[] { "ObsoletedInOSPlatform", "int IntProperty", " > [|IntProperty|]", "IntProperty|] = 5", "IntProperty" };
            yield return new object[] { "RemovedInOSPlatform", "int RemovedProperty", " <= [|RemovedProperty|]", "RemovedProperty|] = 3", "RemovedProperty" };
        }

        [Fact]
        public async Task OsDependentEnumValueCalledWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test2
{
    public void M1()
    {
        PlatformEnum val = [|PlatformEnum.Windows10|];
        M2([|PlatformEnum.Windows10|]);
        M2([|PlatformEnum.Linux48|]);
        M2(PlatformEnum.NoPlatform);
    }
    public PlatformEnum M2(PlatformEnum option)
    {
        return option;
    }
}

public enum PlatformEnum
{
    [MinimumOSPlatform(""windows10.0"")]
    Windows10,
    [MinimumOSPlatform(""linux4.8"")]
    Linux48,
    NoPlatform
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentEnumConditionalCheckNotWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test2
{
    public void M1()
    {
        PlatformEnum val = [|PlatformEnum.Windows10|];
        if (val == PlatformEnum.Windows10)
            return;
        M2([|PlatformEnum.Windows10|]);
        M2([|PlatformEnum.Linux48|]);
        M2(PlatformEnum.NoPlatform);
    }
    public PlatformEnum M2(PlatformEnum option)
    {
        return option;
    }
}

public enum PlatformEnum
{
    [MinimumOSPlatform(""Windows10.0"")]
    Windows10,
    [MinimumOSPlatform(""Linux4.8"")]
    Linux48,
    NoPlatform
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentFieldCalledWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [MinimumOSPlatform(""Windows10.1.1.1"")]
    string WindowsStringField;
    [MinimumOSPlatform(""Windows10.1.1.1"")]
    public int WindowsIntField { get; set; }
    public void M1()
    {
        Test test = new Test();
        [|WindowsStringField|] = ""Hello"";
        string s = [|WindowsStringField|];
        M2([|test.WindowsStringField|]);
        M2([|WindowsStringField|]);
        M3([|WindowsIntField|]);
    }
    public string M2(string option)
    {
        return option;
    }
    public int M3(int option)
    {
        return option;
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        /*[Fact] TODO: test with assembly level attribute
        public async Task MethodWithTargetPlatrofrmAttributeDoesNotWarn()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        M2();
    }
    [TargetPlatform(""Windows10.1.1.1"")]
    public void M2()
    {
    }
}
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }*/

        [Fact]
        public async Task OsDependentMethodCalledFromInstanceWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    private B field = new B();
    public void M1()
    {
        [|field.M2()|];
    }
}
public class B
{
    [MinimumOSPlatform(""Windows10.1.1.1"")]
    public void M2()
    {
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentMethodCalledFromOtherNsInstanceWarns()
        {
            var source = @"
using System.Runtime.Versioning;
using Ns;

public class Test
{
    private B field = new B();
    public void M1()
    {
        [|field.M2()|];
    }
}

namespace Ns
{
    public class B
    {
        [MinimumOSPlatform(""Windows10.1.1.1"")]
        public void M2()
        {
        }
    }
}
" + MockAttributesSource;

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentConstructorOfClassUsedCalledWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        C instance = [|new C()|];
        instance.M2();
    }
}

public class C
{
    [MinimumOSPlatform(""Windows10.1.2.3"")]
    public C()
    {
    }
    public void M2()
    {
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task ConstructorAndMethodOfOsDependentClassCalledWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        OsDependentClass odc = [|new OsDependentClass()|];
        [|odc.M2()|];
    }
}
[MinimumOSPlatform(""Windows10.1.2.3"")]
public class OsDependentClass
{
    public void M2()
    {
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionCallsOsDependentMemberWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        void Test()
        {
            [|M2()|];
        }
        Test();
    }

    [MinimumOSPlatform(""Windows10.1.2.3"")]
    public void M2()
    {
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalGuardedFunctionCallsOsDependentMemberNotWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [MinimumOSPlatform(""Windows10.2"")]
    public void M1()
    {
        void Test()
        {
            M2();
        }
        Test();
    }

    [MinimumOSPlatform(""Windows10.1.2.3"")]
    public void M2()
    {
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaCallsOsDependentMemberWarns()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        void Test() => [|M2()|];
        Test();

        Action action = () =>
        {
            [|M2()|];
        };
    }

    [MinimumOSPlatform(""Windows10.1.2.3"")]
    public void M2()
    {
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task AttributedLambdaCallsOsDependentMemberNotWarn()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    [MinimumOSPlatform(""Windows10.13"")]
    public void M1()
    {
        void Test() => M2();
        Test();

        Action action = () =>
        {
            M2();
        };
    }

    [MinimumOSPlatform(""Windows10.1.2.3"")]
    public void M2()
    {
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentEventAccessedWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public delegate void Del();

    [MinimumOSPlatform(""Windows10.1.2.3"")]
    public event Del SampleEvent;

    public void M1()
    {
        [|SampleEvent|] += M3;
        M2();
    }

    public void M2()
    {
        [|SampleEvent|]?.Invoke();
    }

    public void M3()
    {
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentMethodAssignedToDelegateWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public delegate void Del(); // The attribute not supported on delegates, so no tets for that

    [MinimumOSPlatform(""Windows10.1.2.3"")]
    public void DelegateMethod()
    {
    }
    public void M1()
    {
        Del handler = [|DelegateMethod|];
        handler(); // assume it shouldn't warn here
    }
}
" + MockAttributesSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        /*[Fact] TODO wait until assembly level APIs merged
        public async Task MethodOfOsDependentAssemblyCalledWithoutSuppressionWarns()
        {
            var source = @"
            using System.Runtime.Versioning;
            using ns;
            public class Test
            {
                public void M1()
                {
                    OsDependentClass odc = new OsDependentClass();
                    odc.M2();
                }
            }
            [assembly:MinimumOSPlatform(""Windows10.1.2.3"")]
            namespace ns
            {
                public class OsDependentClass
                {
                    public void M2()
                    {
                    }
                }
            }
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer2.Rule).WithSpan(10, 21, 10, 29).WithArguments("M2", "Windows", "10.1.2.3"));
        }*/

        public static IEnumerable<object[]> MinimumOsAttributeTestData()
        {
            yield return new object[] { "Windows10.1.2.3", "Windows10.1.2.3", false };
            yield return new object[] { "Windows10.1.2.3", "Windows10.1.3.3", false };
            yield return new object[] { "WINDOWS10.1.2.3", "Windows10.1.3.1", false };
            yield return new object[] { "Windows10.1.2.3", "Windows11.1.2.3", false };
            yield return new object[] { "Windows10.1.2.3", "windows10.2.2.0", false };
            yield return new object[] { "Windows10.1.2.3", "Windows10.1.1.3", true };
            yield return new object[] { "Windows10.1.2.3", "WINDOWS11.1.1.3", false };
            yield return new object[] { "Windows10.1.2.3", "Windows10.1.1.4", true };
            yield return new object[] { "osx10.1.2.3", "macos10.2.2.0", false };
            yield return new object[] { "macOs10.1.2.3", "Osx11.1.1.0", false };
            yield return new object[] { "MACOS10.1.2.3", "osx10.2.2.0", false };
            yield return new object[] { "Windows10.1.2.3", "Osx11.1.1.4", true };
            yield return new object[] { "Windows10.1.2.3", "Windows10.0.1.9", true };
            yield return new object[] { "Windows10.1.2.3", "Windows10.1.1.4", true };
            yield return new object[] { "Windows10.1.2.3", "Windows8.2.3.3", true };
        }

        [Theory]
        [MemberData(nameof(MinimumOsAttributeTestData))]
        public async Task MethodOfOsDependentClassSuppressedWithMinimumOsAttribute(string dependentVersion, string suppressingVersion, bool warn)
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [MinimumOSPlatform(""" + suppressingVersion + @""")]
    public void M1()
    {
        OsDependentClass odc = new OsDependentClass();
        odc.M2();
    }
}

[MinimumOSPlatform(""" + dependentVersion + @""")]
public class OsDependentClass
{
    public void M2()
    {
    }
}
" + MockAttributesSource;

            if (warn)
            {
                await VerifyAnalyzerAsyncCs(source,
                    VerifyCS.Diagnostic(PlatformCompatabilityAnalyzer.MinimumOsRule).WithSpan(9, 32, 9, 54).WithArguments(".ctor", "Windows", "10.1.2.3"),
                    VerifyCS.Diagnostic(PlatformCompatabilityAnalyzer.MinimumOsRule).WithSpan(10, 9, 10, 17).WithArguments("M2", "Windows", "10.1.2.3"));
            }
            else
            {
                await VerifyAnalyzerAsyncCs(source);
            }
        }

        [Theory]
        [MemberData(nameof(ObsoletedRemovedAttributeTestData))]
        public async Task MethodOfOsDependentClassSuppressedWithObsoleteAttribute(string dependentVersion, string suppressingVersion, bool warn)
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [ObsoletedInOSPlatform(""" + suppressingVersion + @""")]
    public void M1()
    {
        OsDependentClass odc = new OsDependentClass();
        odc.M2();
    }
 }
 
[ObsoletedInOSPlatform(""" + dependentVersion + @""")]
public class OsDependentClass
{
     public void M2()
    {
    }
}
" + MockAttributesSource;

            if (warn)
            {
                await VerifyAnalyzerAsyncCs(source,
                    VerifyCS.Diagnostic(PlatformCompatabilityAnalyzer.MinimumOsRule).WithSpan(9, 32, 9, 54).WithArguments(".ctor", "Windows", "10.1.2.3"),
                    VerifyCS.Diagnostic(PlatformCompatabilityAnalyzer.ObsoleteRule).WithSpan(10, 9, 10, 17).WithArguments("M2", "Windows", "10.1.2.3"));
            }
            else
            {
                await VerifyAnalyzerAsyncCs(source);
            }
        }

        public static IEnumerable<object[]> ObsoletedRemovedAttributeTestData()
        {
            yield return new object[] { "Windows10.1.2.3", "Windows10.1.2.3", true };
            yield return new object[] { "Windows10.1.2.3", "Windows10.1.3.3", true };
            yield return new object[] { "Windows10.1.2.3", "Windows10.1.3.1", true };
            yield return new object[] { "Windows10.1.2.3", "Windows11.1.2.3", true };
            yield return new object[] { "Windows10.1.2.3", "Windows10.2.2.0", true };
            yield return new object[] { "Windows10.1.2.3", "Windows10.1.1.3", false };
            yield return new object[] { "Windows10.1.2.3", "WINDOWS10.1.1.3", false };
            yield return new object[] { "Windows10.1.2.3", "Windows10.1.1.4", false };
            yield return new object[] { "osx10.1.2.3", "MacOs10.1.1.4", false };
            yield return new object[] { "macOs10.1.2.3", "Osx10.1.1.4", false };
            yield return new object[] { "Windows10.1.2.3", "Osx10.1.1.4", true };
            yield return new object[] { "windows10.1.2.3", "Windows10.1.0.1", false };
            yield return new object[] { "Windows10.1.2.3", "Windows8.2.3.4", false };
        }

        [Theory]
        [MemberData(nameof(ObsoletedRemovedAttributeTestData))]
        public async Task MethodOfOsDependentClassSuppressedWithRemovedAttribute(string dependentVersion, string suppressingVersion, bool warn)
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{
    [RemovedInOSPlatform(""" + suppressingVersion + @""")]
    public void M1()
    {
        OsDependentClass odc = new OsDependentClass();
        odc.M2();
    }
}

[RemovedInOSPlatform(""" + dependentVersion + @""")]
public class OsDependentClass
{
    public void M2()
    {
    }
}
" + MockAttributesSource;

            if (warn)
            {
                await VerifyAnalyzerAsyncCs(source,
                    VerifyCS.Diagnostic(PlatformCompatabilityAnalyzer.MinimumOsRule).WithSpan(9, 32, 9, 54).WithArguments(".ctor", "Windows", "10.1.2.3"),
                    VerifyCS.Diagnostic(PlatformCompatabilityAnalyzer.RemovedRule).WithSpan(10, 9, 10, 17).WithArguments("M2", "Windows", "10.1.2.3"));
            }
            else
            {
                await VerifyAnalyzerAsyncCs(source);
            }
        }

        private static VerifyCS.Test PopulateTestCs(string sourceCode)
        {
            return new VerifyCS.Test
            {
                TestCode = sourceCode,
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp50,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                TestState = { }
            };
        }

        private static async Task VerifyAnalyzerAsyncCs(string sourceCode) => await PopulateTestCs(sourceCode).RunAsync();

        private static async Task VerifyAnalyzerAsyncCs(string sourceCode, params DiagnosticResult[] expectedDiagnostics)
        {
            var test = PopulateTestCs(sourceCode);
            foreach (DiagnosticResult expectedDiagnostic in expectedDiagnostics)
            {
                test.ExpectedDiagnostics.Add(expectedDiagnostic);
            }
            await test.RunAsync();
        }

        private static async Task VerifyAnalyzerAsyncCs(string sourceCode, string additionalFiles)
        {
            var test = PopulateTestCs(sourceCode);
            test.TestState.AdditionalFiles.Add((".editorconfig", additionalFiles));
            await test.RunAsync();
        }

        private readonly string MockAttributesSource = @"
namespace System.Runtime.Versioning
{
    public abstract class OSPlatformAttribute : Attribute
    {
        private protected OSPlatformAttribute(string platformName)
        {
            PlatformName = platformName;
        }

        public string PlatformName { get; }
    }

    [AttributeUsage(AttributeTargets.Assembly,
                    AllowMultiple = false, Inherited = false)]
    public sealed class TargetPlatformAttribute : OSPlatformAttribute
    {
        public TargetPlatformAttribute(string platformName) : base(platformName)
        { }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Field |
                    AttributeTargets.Struct,
                    AllowMultiple = true, Inherited = false)]
    public sealed class MinimumOSPlatformAttribute : OSPlatformAttribute
    {
        public MinimumOSPlatformAttribute(string platformName) : base(platformName)
        { }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Field |
                    AttributeTargets.Struct,
                    AllowMultiple = true, Inherited = false)]
    public sealed class RemovedInOSPlatformAttribute : OSPlatformAttribute
    {
        public RemovedInOSPlatformAttribute(string platformName) : base(platformName)
        { }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Field |
                    AttributeTargets.Struct,
                    AllowMultiple = true, Inherited = false)]
    public sealed class ObsoletedInOSPlatformAttribute: OSPlatformAttribute
    {
        public ObsoletedInOSPlatformAttribute(string platformName) : base(platformName)
        { }
        public ObsoletedInOSPlatformAttribute(string platformName, string message) : base(platformName)
        {
            Message = message;
        }
        public string Message { get; }
        public string Url { get; set; }
    }
}
";
    }
}
