// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.RuntimePlatformCheckAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class RuntimePlatformCheckAnalyzerTests
    {
        [Fact]
        public async Task OsDependentMethodCalledWithoutSuppressionWarns()
        {
            var source = @"
        using System.Runtime.Versioning;

        public class Test
        {
            public void M1()
            {
                [|M2()|];
            }

            [AddedInOSPlatformVersion(""Windows"", 10, 1, 1, 1)]
            public void M2()
            {
            }
        }
" + MockPlatformApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task OsDependentMethodCalledFromIntanceWithoutSuppressionWarns()
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
            [AddedInOSPlatformVersion(""Windows"", 10, 1, 1, 1)]
            public void M2()
            {
            }
        }
" + MockPlatformApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task OsDependentMethodCalledFromOtherNsIntanceWarns()
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
                [AddedInOSPlatformVersion(""Windows"", 10, 1, 1, 1)]
                public void M2()
                {
                }
            }
        }
" + MockPlatformApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MethodOfOsDependentClassCalledWithoutSuppressionWarns()
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

            [AddedInOSPlatformVersion(""Windows"", 10, 1, 2, 3)]
            public class OsDependentClass
            {
                public void M2()
                {
                }
            }
" + MockPlatformApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        /*        [Fact] TODO find out how to pass 2 sources
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

                    [assembly:AddedInOSPlatformVersion(""Windows"", 10, 1, 2, 3)]
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
                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithSpan(10, 21, 10, 29).WithArguments("M2", "Windows", "10.1.2.3"));
                }*/

        [Theory]
        [InlineData("10, 1, 2, 3", "10, 1, 2, 3", false)]
        [InlineData("10, 1, 2, 3", "10, 1, 3, 3", false)]
        [InlineData("10, 1, 2, 3", "10, 1, 3, 1", false)]
        [InlineData("10, 1, 2, 3", "11, 1, 2, 3", false)]
        [InlineData("10, 1, 2, 3", "10, 2, 2, 0", false)]
        [InlineData("10, 1, 2, 3", "10, 1, 1, 3", true)]
        [InlineData("10, 1, 2, 3", "10, 1, 1, 4", true)]
        [InlineData("10, 1, 2, 3", "10, 0, 1, 9", true)]
        [InlineData("10, 1, 2, 3", "8, 2, 3, 3", true)]
        public async Task MethodOfOsDependentClassSuppressedWithAddedAttribute(string dependentVersion, string suppressingVersion, bool warn)
        {
            var source = @"
            using System.Runtime.Versioning;
            public class Test
            {
                [AddedInOSPlatformVersion(""Windows""," + suppressingVersion + @")]
                public void M1()
                {
                    OsDependentClass odc = new OsDependentClass();
                    odc.M2();
                }
            }

            [AddedInOSPlatformVersion(""Windows""," + dependentVersion + @")]
            public class OsDependentClass
            {
                public void M2()
                {
                }
            }
" + MockPlatformApiSource;

            if (warn)
                await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.AddedRule).WithSpan(9, 21, 9, 29).WithArguments("M2", "Windows", "10.1.2.3"));
            else
                await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Theory]
        [InlineData("10, 1, 2, 3", "10, 1, 2, 3", false)]
        [InlineData("10, 1, 2, 3", "10, 1, 3, 3", true)]
        [InlineData("10, 1, 2, 3", "10, 1, 3, 1", true)]
        [InlineData("10, 1, 2, 3", "11, 1, 2, 3", true)]
        [InlineData("10, 1, 2, 3", "10, 2, 2, 0", true)]
        [InlineData("10, 1, 2, 3", "10, 1, 1, 3", false)]
        [InlineData("10, 1, 2, 3", "10, 1, 1, 4", false)]
        [InlineData("10, 1, 2, 3", "10, 1, 0, 1", false)]
        [InlineData("10, 1, 2, 3", "8, 2, 3, 4", false)]
        public async Task MethodOfOsDependentClassSuppressedWithObsoleteAttribute(string dependentVersion, string suppressingVersion, bool warn)
        {
            var source = @"
            using System.Runtime.Versioning;
            public class Test
            {
                [ObsoletedInOSPlatformVersion(""Windows""," + suppressingVersion + @")]
                public void M1()
                {
                    OsDependentClass odc = new OsDependentClass();
                    odc.M2();
                }
            }

            [ObsoletedInOSPlatformVersion(""Windows""," + dependentVersion + @")]
            public class OsDependentClass
            {
                public void M2()
                {
                }
            }
" + MockPlatformApiSource;

            if (warn)
                await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.ObsoleteRule).WithSpan(9, 21, 9, 29).WithArguments("M2", "Windows", "10.1.2.3"));
            else
                await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Theory]
        [InlineData("10, 1, 2, 3", "10, 1, 2, 3", false)]
        [InlineData("10, 1, 2, 3", "10, 1, 3, 3", true)]
        [InlineData("10, 1, 2, 3", "10, 1, 3, 1", true)]
        [InlineData("10, 1, 2, 3", "11, 1, 2, 3", true)]
        [InlineData("10, 1, 2, 3", "10, 2, 2, 0", true)]
        [InlineData("10, 1, 2, 3", "10, 1, 1, 3", false)]
        [InlineData("10, 1, 2, 3", "10, 1, 1, 4", false)]
        [InlineData("10, 1, 2, 3", "10, 1, 0, 1", false)]
        [InlineData("10, 1, 2, 3", "8, 2, 3, 4", false)]
        public async Task MethodOfOsDependentClassSuppressedWithRemovedAttribute(string dependentVersion, string suppressingVersion, bool warn)
        {
            var source = @"
            using System.Runtime.Versioning;
            public class Test
            {
                [RemovedInOSPlatformVersionAttribute(""Windows""," + suppressingVersion + @")]
                public void M1()
                {
                    OsDependentClass odc = new OsDependentClass();
                    odc.M2();
                }
            }

            [RemovedInOSPlatformVersionAttribute(""Windows""," + dependentVersion + @")]
            public class OsDependentClass
            {
                public void M2()
                {
                }
            }
" + MockPlatformApiSource;

            if (warn)
                await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.RemovedRule).WithSpan(9, 21, 9, 29).WithArguments("M2", "Windows", "10.1.2.3"));
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
        [InlineData("build_property.TargetFramework=net5.0-windows11", false)]
        [InlineData("build_property.TargetFramework=net5.0-windows10", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows11\nbuild_property.TargetPlatformMinVersion=10;", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.2\nbuild_property.TargetPlatformMinVersion=10.1;", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.1.1.2\nbuild_property.TargetPlatformMinVersion=10.0.0.1;", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.2.1\nbuild_property.TargetPlatformMinVersion=9.1.1.1;", true)]
        public async Task TfmAndTargetPlatformMinVersionWithAddedAttribute(string editorConfigText, bool expectDiagnostic)
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

            [AddedInOSPlatformVersion(""Windows"", 10, 1, 1, 1)]
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
        [InlineData("build_property.TargetFramework=net472", false)] // TODO because no version is set to 0.0.0.0
        [InlineData("build_property.TargetFramework=net5.0-linux", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows", false)] // Same here
        [InlineData("build_property.TargetFramework=net5.0-windows10.1.1.1", false)]
        [InlineData("build_property.TargetFramework=net5.0-windows10.2", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows11", true)]
        [InlineData("build_property.TargetFramework=net5.0-windows10", false)]
        [InlineData("build_property.TargetFramework=net5.0-windows11\nbuild_property.TargetPlatformMinVersion=10;", false)]
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

            [ObsoletedInOSPlatformVersionAttribute(""Windows"", 10, 1, 1, 1)]
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
    public abstract class OSPlatformVersionAttribute : Attribute
    {
        protected OSPlatformVersionAttribute(string osPlatform,
                                              int major,
                                              int minor,
                                              int build,
                                              int revision)
        {
            PlatformIdentifier = osPlatform;
            Major = major;
            Minor = minor;
            Build = build;
            Revision = revision;
        }

        public string PlatformIdentifier { get; }
        public int Major { get; }
        public int Minor { get; }
        public int Build { get; }
        public int Revision { get; }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Struct,
                    AllowMultiple = false, Inherited = true)]
    public sealed class AddedInOSPlatformVersionAttribute : OSPlatformVersionAttribute
    {
        public AddedInOSPlatformVersionAttribute(string osPlatform,
                                          int major,
                                          int minor,
                                          int build,
                                          int revision) : base(osPlatform, major, minor, build, revision)
        { }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Struct,
                    AllowMultiple = false, Inherited = true)]
    public sealed class RemovedInOSPlatformVersionAttribute : OSPlatformVersionAttribute
    {
        public RemovedInOSPlatformVersionAttribute(string osPlatform,
                                            int major,
                                            int minor,
                                            int build,
                                            int revision) : base(osPlatform, major, minor, build, revision)
        { }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Struct,
                    AllowMultiple = false, Inherited = true)]
    public sealed class ObsoletedInOSPlatformVersionAttribute : OSPlatformVersionAttribute
    {
        public ObsoletedInOSPlatformVersionAttribute(string osPlatform,
                                              int major,
                                              int minor,
                                              int build,
                                              int revision) : base(osPlatform, major, minor, build, revision)
        { }
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
            return RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor)
        {
            return RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor, int build)
        {
            return RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor, int build, int revision)
        {
            return RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major)
        {
            return !RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor)
        {
            return !RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor, int build)
        {
            return !RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor, int build, int revision)
        {
            return !RuntimeInformation.IsOSPlatform(osPlatform);
        }
#pragma warning restore CA1801, IDE0060 // Review unused parameters
    }
}";

        /*        private readonly string PlatformCheckApiSource = @"
                namespace System.Runtime.InteropServices
                {
                    public class RuntimeInformationHelper
                    {
                        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major) => true;
                        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor) => true;
                        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor, int build) => true;
                        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor, int build, int revision) => true;

                        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major) => true;
                        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor) => true;
                        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor, int build) => true;
                        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor, int build, int revision) => true;
                    }   
                }";

                      [Fact]
                        public async Task SimpleIfReturnTest()
                        {
                        var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(!RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:''
                                    return;
                                }

                                {|#2:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformOrLater;Windows;1"));
                                }

                                [Fact]
                                public async Task SimpleIfTest()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
                                }

                                if(RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Windows, 1, 1))
                                {
                                    {|#2:M2()|};        // Platform checks:'IsOSPlatformEarlierThan;Windows;1.1'
                                }

                                {|#3:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformEarlierThan;Windows;1.1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfElseTest()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
                                }
                                else
                                {
                                    {|#2:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Windows;1'
                                }

                                {|#3:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("!IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfElseIfElseTest()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
                                }
                                else if(RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Linux, 1, 1))
                                {
                                    {|#2:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Windows;1 && IsOSPlatformEarlierThan;Linux;1.1'
                                }
                                else
                                {
                                    {|#3:M2()|};        // Platform checks:'!IsOSPlatformEarlierThan;Linux;1.1 && !IsOSPlatformOrLater;Windows;1'
                                }

                                {|#4:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("!IsOSPlatformOrLater;Windows;1 && IsOSPlatformEarlierThan;Linux;1.1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("!IsOSPlatformEarlierThan;Linux;1.1 && !IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(4).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfTestWithNegation()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(!RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Windows;1'
                                }

                                if(!RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Windows, 1, 1))
                                {
                                    {|#2:M2()|};        // Platform checks:'!IsOSPlatformEarlierThan;Windows;1.1'
                                }

                                {|#3:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("!IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("!IsOSPlatformEarlierThan;Windows;1.1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfElseTestWithNegation()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(!RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Windows;1'
                                }
                                else
                                {
                                    {|#2:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
                                }

                                {|#3:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("!IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfElseIfElseTestWithNegation()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(!RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Windows;1'
                                }
                                else if(RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Linux, 1, 1))
                                {
                                    {|#2:M2()|};        // Platform checks:'IsOSPlatformEarlierThan;Linux;1.1 && IsOSPlatformOrLater;Windows;1'
                                }
                                else
                                {
                                    {|#3:M2()|};        // Platform checks:'!IsOSPlatformEarlierThan;Linux;1.1 && IsOSPlatformOrLater;Windows;1'
                                }

                                {|#4:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("!IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformEarlierThan;Linux;1.1 && IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("!IsOSPlatformEarlierThan;Linux;1.1 && IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(4).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfTestWithLogicalAnd()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) &&
                                   RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Windows, 2))
                                {
                                    {|#1:M2()|};        // Platform checks:'IsOSPlatformEarlierThan;Windows;2 && IsOSPlatformOrLater;Windows;1'
                                }

                                {|#2:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformEarlierThan;Windows;2 && IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfElseTestWithLogicalAnd()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) &&
                                   RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Linux;1 && IsOSPlatformOrLater;Windows;1'
                                }
                                else
                                {
                                    {|#2:M2()|};        // Platform checks:''
                                }

                                {|#3:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Linux;1 && IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfTestWithLogicalOr()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) ||
                                   RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)'
                                }

                                {|#2:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfElseTestWithLogicalOr()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) ||
                                   RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)'
                                }
                                else
                                {
                                    {|#2:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1'
                                }

                                {|#3:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfElseIfElseTestWithLogicalOr()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) ||
                                   RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)'
                                }
                                else if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 3))
                                {
                                    {|#2:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Windows;3'
                                }
                                else
                                {
                                    {|#3:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1 && !IsOSPlatformOrLater;Windows;3'
                                }

                                {|#4:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Windows;3"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1 && !IsOSPlatformOrLater;Windows;3"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(4).WithArguments(""));
                                }

                                [Fact]
                                public async Task SimpleIfElseIfTestWithLogicalOr_02()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if((RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) ||
                                    RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1)) &&
                                    (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 2) ||
                                    RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 2)))
                                {
                                    {|#1:M2()|};        // Platform checks:'((!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1) && !IsOSPlatformOrLater;Windows;2 && IsOSPlatformOrLater;Linux;2 || (!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1) && IsOSPlatformOrLater;Windows;2)'
                                }
                                else if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 3) ||
                                         RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 3) ||
                                         RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 4))
                                {
                                    {|#2:M2()|};        // Platform checks:'(!IsOSPlatformOrLater;Linux;3 && !IsOSPlatformOrLater;Windows;3 && IsOSPlatformOrLater;Linux;4 || (!IsOSPlatformOrLater;Windows;3 && IsOSPlatformOrLater;Linux;3 || IsOSPlatformOrLater;Windows;3))'
                                }

                                {|#3:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("((!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1) && !IsOSPlatformOrLater;Windows;2 && IsOSPlatformOrLater;Linux;2 || (!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1) && IsOSPlatformOrLater;Windows;2)"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("(!IsOSPlatformOrLater;Linux;3 && !IsOSPlatformOrLater;Windows;3 && IsOSPlatformOrLater;Linux;4 || (!IsOSPlatformOrLater;Windows;3 && IsOSPlatformOrLater;Linux;3 || IsOSPlatformOrLater;Windows;3))"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
                                }

                                [Fact]
                                public async Task ControlFlowAndMultipleChecks()
                                {
                                    var source = @"
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1))
                                {
                                    {|#1:M2()|};      // Platform checks:'IsOSPlatformOrLater;Linux;1'

                                    if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 2, 0))
                                    {
                                        {|#2:M2()|};    // Platform checks:'IsOSPlatformOrLater;Linux;1 && IsOSPlatformOrLater;Linux;2.0'
                                    }
                                    else if (!RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Windows, 3, 1, 1))
                                    {
                                        {|#3:M2()|};    // Platform checks:'!IsOSPlatformEarlierThan;Windows;3.1.1 && !IsOSPlatformOrLater;Linux;2.0 && IsOSPlatformOrLater;Linux;1'
                                    }

                                    {|#4:M2()|};    // Platform checks:'IsOSPlatformOrLater;Linux;1'
                                }
                                else
                                {
                                    {|#5:M2()|};    // Platform checks:'!IsOSPlatformOrLater;Linux;1'
                                }

                                {|#6:M2()|};        // Platform checks:''

                                if ({|#7:IsWindows3OrLater()|})    // Platform checks:''
                                {
                                    {|#8:M2()|};    // Platform checks:''
                                }

                                {|#9:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }

                            bool IsWindows3OrLater()
                            {
                                return RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 3, 0, 0, 0);
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Linux;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformOrLater;Linux;1 && IsOSPlatformOrLater;Linux;2.0"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("!IsOSPlatformEarlierThan;Windows;3.1.1 && !IsOSPlatformOrLater;Linux;2.0 && IsOSPlatformOrLater;Linux;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(4).WithArguments("IsOSPlatformOrLater;Linux;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(5).WithArguments("!IsOSPlatformOrLater;Linux;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(6).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(7).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(8).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(9).WithArguments(""));
                                }

                                [Fact]
                                public async Task DebugAssertAnalysisTest()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                {|#1:Debug.Assert(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 3, 0, 0, 0))|};  // Platform checks:'IsOSPlatformOrLater;Windows;3.0.0.0'

                                {|#2:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;3.0.0.0'
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;3.0.0.0"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformOrLater;Windows;3.0.0.0"));
                                }

                                [Fact]
                                public async Task ResultSavedInLocal()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                var x1 = RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1);
                                var x2 = RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1);
                                if (x1 || x2)
                                {
                                    {|#1:M2()|};        // Platform checks:'(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)'
                                }

                                {|#2:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
                                }

                                [Fact]
                                public async Task VersionSavedInLocal()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                var v1 = 1;
                                if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, v1))
                                {
                                    {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
                                }

                                {|#2:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
                                }

                                [Fact]
                                public async Task PlatformSavedInLocal_NotYetSupported()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                var platform = OSPlatform.Windows;
                                if (RuntimeInformationHelper.IsOSPlatformOrLater(platform, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:''
                                }

                                {|#2:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
                                }

                                [Fact]
                                public async Task UnrelatedConditionCheckDoesNotInvalidateState()
                                {
                                    var source = @"
                        using System.Diagnostics;
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1(bool flag1, bool flag2)
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
                                {
                                    {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'

                                    if (flag1 || flag2)
                                    {
                                        {|#2:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
                                    }
                                    else
                                    {
                                        {|#3:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
                                    }

                                    {|#4:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
                                }

                                {|#5:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }
                        }" + PlatformCheckApiSource;

                                    await VerifyCS.VerifyAnalyzerAsync(source,
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(4).WithArguments("IsOSPlatformOrLater;Windows;1"),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(5).WithArguments(""));
                                }

                                [Theory]
                                [InlineData("")]
                                [InlineData("dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive")]
                                public async Task InterproceduralAnalysisTest(string editorconfig)
                                {
                                    var source = @"
                        using System.Runtime.InteropServices;

                        class Test
                        {
                            void M1()
                            {
                                {|#0:M2()|};    // Platform checks:''

                                if ({|#1:IsWindows3OrLater()|})    // Platform checks:''
                                {
                                    {|#2:M2()|};    // Platform checks:'IsOSPlatformOrLater;Windows;3.0.0.0'
                                }

                                {|#3:M2()|};        // Platform checks:''
                            }

                            void M2()
                            {
                            }

                            bool IsWindows3OrLater()
                            {
                                return RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 3, 0, 0, 0);
                            }
                        }" + PlatformCheckApiSource;

                                    var test = new VerifyCS.Test
                                    {
                                        TestState =
                                        {
                                            Sources = { source },
                                            AdditionalFiles = { (".editorconfig", editorconfig) }
                                        }
                                    };

                                    var argForInterprocDiagnostics = editorconfig.Length == 0 ? "" : "IsOSPlatformOrLater;Windows;3.0.0.0";
                                    test.ExpectedDiagnostics.AddRange(new[]
                                    {
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments(argForInterprocDiagnostics),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(argForInterprocDiagnostics),
                                        VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("")
                                    });

                                    await test.RunAsync();
                                }*/
    }
}
