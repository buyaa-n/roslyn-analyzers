﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source);
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
    [MinimumOSPlatform(""Windows10"")]
    public void Windows10()
    {
    }
    [MinimumOSPlatform(""Windows1.2.3.4.5"")]
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
    [ObsoletedInOSPlatform(null)]
    public void ObsoletedWithNullString()
    {
    }
    [RemovedInOSPlatform(""Linu4.1"")]
    public void RemovedLinu4_1()
    {
    }
    [RemovedInOSPlatform("""")]
    public void RemovedWithEmptyString()
    {
    }
}
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source);
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
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Theory]
        [InlineData("MinimumOSPlatform", "string StringProperty", " == StringProperty", @"StringProperty|] = ""Hello""", "StringProperty")]
        [InlineData("ObsoletedInOSPlatform", "int IntProperty", " > IntProperty", "IntProperty|] = 5", "IntProperty")]
        [InlineData("RemovedInOSPlatform", "int RemovedProperty", " <= RemovedProperty", "RemovedProperty|] = 3", "RemovedProperty")]
        public async Task OsDependentPropertyConditionalCheckNotWarns(string attribute, string property, string condition, string setter, string getter)
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
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source);
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
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source);
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
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source);
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
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        /*[Fact] TODO: Add the attribute assembly level and test
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
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source);
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
" + MockPlatformApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MethodOfOsDependentClassCalledWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        OsDependentClass odc = new OsDependentClass();
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
" + MockPlatformApiSource;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        /*        [Fact] TODO find out how to pass 2 sources or wait until assembly level APIs merged
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

        [Theory]
        [InlineData("10.1.2.3", "10.1.2.3", false)]
        [InlineData("10.1.2.3", "10.1.3.3", false)]
        [InlineData("10.1.2.3", "10.1.3.1", false)]
        [InlineData("10.1.2.3", "11.1.2.3", false)]
        [InlineData("10.1.2.3", "10.2.2.0", false)]
        [InlineData("10.1.2.3", "10.1.1.3", true)]
        [InlineData("10.1.2.3", "10.1.1.4", true)]
        [InlineData("10.1.2.3", "10.0.1.9", true)]
        [InlineData("10.1.2.3", "8.2.3.3", true)]
        public async Task MethodOfOsDependentClassSuppressedWithMinimumOsAttribute(string dependentVersion, string suppressingVersion, bool warn)
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [MinimumOSPlatform(""Windows" + suppressingVersion + @""")]
    public void M1()
    {
        OsDependentClass odc = new OsDependentClass();
        odc.M2();
    }
}

[MinimumOSPlatform(""Windows" + dependentVersion + @""")]
public class OsDependentClass
{
    public void M2()
    {
    }
}
" + MockPlatformApiSource;

            if (warn)
                await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic(PlatformCompatabilityAnalyzer.MinimumOsRule).WithSpan(10, 9, 10, 17).WithArguments("M2", "Windows", "10.1.2.3"));
            else
                await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Theory]
        [InlineData("10.1.2.3", "10.1.2.3", true)]
        [InlineData("10.1.2.3", "10.1.3.3", true)]
        [InlineData("10.1.2.3", "10.1.3.1", true)]
        [InlineData("10.1.2.3", "11.1.2.3", true)]
        [InlineData("10.1.2.3", "10.2.2.0", true)]
        [InlineData("10.1.2.3", "10.1.1.3", false)]
        [InlineData("10.1.2.3", "10.1.1.4", false)]
        [InlineData("10.1.2.3", "10.1.0.1", false)]
        [InlineData("10.1.2.3", "8.2.3.4", false)]
        public async Task MethodOfOsDependentClassSuppressedWithObsoleteAttribute(string dependentVersion, string suppressingVersion, bool warn)
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [ObsoletedInOSPlatform(""Windows" + suppressingVersion + @""")]
    public void M1()
    {
        OsDependentClass odc = new OsDependentClass();
        odc.M2();
    }
 }
 
[ObsoletedInOSPlatform(""Windows" + dependentVersion + @""")]
public class OsDependentClass
{
     public void M2()
    {
    }
}
" + MockPlatformApiSource;

            if (warn)
                await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic(PlatformCompatabilityAnalyzer.ObsoleteRule).WithSpan(10, 9, 10, 17).WithArguments("M2", "Windows", "10.1.2.3"));
            else
                await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Theory]
        [InlineData("10.1.2.3", "10.1.2.3", true)]
        [InlineData("10.1.2.3", "10.1.3.3", true)]
        [InlineData("10.1.2.3", "10.1.3.1", true)]
        [InlineData("10.1.2.3", "11.1.2.3", true)]
        [InlineData("10.1.2.3", "10.2.2.0", true)]
        [InlineData("10.1.2.3", "10.1.1.3", false)]
        [InlineData("10.1.2.3", "10.1.1.4", false)]
        [InlineData("10.1.2.3", "10.1.0.1", false)]
        [InlineData("10.1.2.3", "8.2.3.4", false)]
        public async Task MethodOfOsDependentClassSuppressedWithRemovedAttribute(string dependentVersion, string suppressingVersion, bool warn)
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{
    [RemovedInOSPlatform(""Windows" + suppressingVersion + @""")]
    public void M1()
    {
        OsDependentClass odc = new OsDependentClass();
        odc.M2();
    }
}

[RemovedInOSPlatform(""Windows" + dependentVersion + @""")]
public class OsDependentClass
{
    public void M2()
    {
    }
}
" + MockPlatformApiSource;

            if (warn)
                await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic(PlatformCompatabilityAnalyzer.RemovedRule).WithSpan(10, 9, 10, 17).WithArguments("M2", "Windows", "10.1.2.3"));
            else
                await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Theory]
        [InlineData("", true)]
        [InlineData("build_property.TargetFramework=net5.0", true)]
        [InlineData("build_property.TargetFramework=net472", true)]
        [InlineData("build_property.TargetFramework=net5.0-linux", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.1.1.1", false)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.2", false)]
        [InlineData("build_property.TargetFramework=net5.0-windows11.0", false)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.0", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows11.0\nbuild_property.TargetPlatformMinVersion=10.0;", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.2\nbuild_property.TargetPlatformMinVersion=10.1;", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.1.1.2\nbuild_property.TargetPlatformMinVersion=10.0.0.1;", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.2.1\nbuild_property.TargetPlatformMinVersion=9.1.1.1;", true)]
        public async Task TfmAndTargetPlatformMinVersionWithMinimumOsAttribute(string editorConfigText, bool expectDiagnostic)
        {
            var invocation = expectDiagnostic ? @"[|M2()|]" : "M2()";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
using System.Runtime.Versioning;

public class Test
{{
    public void M1()
    {{
        {invocation};
    }}
    [MinimumOSPlatform(""Windows10.1.1.1"")]
    public void M2()
    {{
    }}
}}
" + MockPlatformApiSource
                    },
                    AdditionalFiles = { (".editorconfig", editorConfigText) }
                },
                MarkupOptions = MarkupOptions.UseFirstDescriptor
            }.RunAsync();
        }

        [Theory]
        [InlineData("", true)]
        [InlineData("build_property.TargetFramework=net5.0", true)]
        [InlineData("build_property.TargetFramework=net472", false)] // TODO: because no version part it is 0.0.0.0
        [InlineData("build_property.TargetFramework=net5.0-linux", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.1.1.1", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.2", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows11.0", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.0", false)]
        [InlineData("build_property.TargetFramework=net5.0-windows11.0\nbuild_property.TargetPlatformMinVersion=10.0;", false)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.2\nbuild_property.TargetPlatformMinVersion=10.1;", false)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.1.1.2\nbuild_property.TargetPlatformMinVersion=10.0.0.1;", false)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.2.1\nbuild_property.TargetPlatformMinVersion=9.1.1.1;", false)]
        public async Task TfmAndTargetPlatformMinVersionWithObsoleteAttribute(string editorConfigText, bool expectDiagnostic)
        {
            var invocation = expectDiagnostic ? @"[|M2()|]" : "M2()";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
$@"
using System.Runtime.Versioning;

public class Test
{{
    public void M1()
    {{
        {invocation};
    }}
    [ObsoletedInOSPlatform(""Windows10.1.1.1"")]
    public void M2()
    {{
    }}
}}
" + MockPlatformApiSource
                    },
                    AdditionalFiles = { (".editorconfig", editorConfigText) }
                },
                MarkupOptions = MarkupOptions.UseFirstDescriptor
            }.RunAsync();
        }

        private readonly string MockPlatformApiSource = @"
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

namespace System.Runtime.InteropServices
{
    public static class RuntimeInformationHelper
    {
#pragma warning disable CA1801, IDE0060 // Review unused parameters
        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major)
        {
            return true;
        }
        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor)
        {
            return true;
        }
        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor, int build)
        {
            return true;
        }
        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor, int build, int revision)
        {
            return true;
        }
        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major)
        {
            return false;
        }
        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor)
        {
            return false;
        }
        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor, int build)
        {
            return false;
        }
        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor, int build, int revision)
        {
            return false;
        }
        public static bool IsOSPlatformOrLater(string platformName) => true;
        public static bool IsOSPlatformEarlierThan(string platformName) => true;
#pragma warning restore CA1801, IDE0060 // Review unused parameters
    }
}";
    }
}
