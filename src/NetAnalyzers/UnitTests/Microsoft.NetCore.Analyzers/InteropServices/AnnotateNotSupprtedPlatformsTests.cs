// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.AnnotateNotSupprtedPlatforms,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.AnnotateNotSupprtedPlatforms,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public partial class AnnotateNotSupprtedPlatformsTests
    {
        [Fact]
        public async Task OneLinerThrows()
        {
            var csSource = @"
using System;
public class Test
{
    public void MethodJustThrows()
    {
        [|throw new PlatformNotSupportedException();|] //'Test.MethodJustThrows()' only throws PNSE and not annotated accordingly
    }

    public void MethodJustThrows(string message)
    {
        [|throw new PlatformNotSupportedException(message);|]
    }

    public void OneLinerThrow() => [|throw new PlatformNotSupportedException()|];
    public void OneLinerThrow(string message) => [|throw new PlatformNotSupportedException(message)|];

    public void MethodThrowsUsingHelper()
    {
        throw ExceptionHelper(); // Helper might not that popular, maybe not need to cover
    }

    private Exception ExceptionHelper()
    {
        return new PlatformNotSupportedException();
    }
}";

            await VerifyAnalyzerAsyncCs(csSource);
        }

        [Fact]
        public async Task MultiLineThrows()
        {
            var csSource = @"
using System;
using System.Runtime.InteropServices;

public class Test
{
    public void MethodWithConditional()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) // Somehow this accounted as one liner (Almost one liner though)
            [|throw new PlatformNotSupportedException();|] // 'Test.MethodWithConditional()' only throws PNSE and not annotated accordingly
    }

    public void MethodWithOtherStatement()
    {
        string message = ""Hello world"";
        [|throw new PlatformNotSupportedException(message);|] // 'Test.MethodWithOtherStatement()' throws PNSE and has no annotation
    }

    public void MethodWithMoreStatements(int state, string msg)
    {
        var a = state - 1;
        DoSomething(a);
        if (a > 0)
            DoSomething(a);
        else
            [|throw new PlatformNotSupportedException();|] // 'Test.MethodWithMoreStatements(int, string)' throws PNSE and has no annotation
    }

    private void DoSomething(int a)
    {
        a--;
        if (a < 0)
            [|throw new PlatformNotSupportedException();|] // 'Test.DoSomething(int)' throws PNSE and has no annotation
        Console.WriteLine(a);
    }
}";

            await VerifyAnalyzerAsyncCs(csSource);
        }

        [Fact]
        public async Task ThrowsPNSEWithAttribute()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

public class Test
{
    [SupportedOSPlatform(""Linux"")]
    public void MethodWithConditional()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) 
            throw new PlatformNotSupportedException(); // 'Test.MethodWithConditional()' only throws PNSE and not annotated accordingly
    }

    [UnsupportedOSPlatform(""Linux"")]
    public void OneLinerThrow() 
    {
        throw new PlatformNotSupportedException();
    }

    [Obsolete(""Does not work in .Net Core"")]
    public void ObsoleteOneLinerThrow() 
    {
        throw new PlatformNotSupportedException(); // Should we report here? 'Test.ObsoleteOneLinerThrow()' throws PNSE and annotated with Obsolete
    }
}";

            await VerifyAnalyzerAsyncCsNet50(csSource);
        }

        [Fact]
        public async Task ThrowsPNSEWithinIntrinsics()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

namespace System.Runtime.Intrinsics.Arm
{
    public class Test
    {
        public static int LeadingSignCount(int value) { throw new PlatformNotSupportedException(); }
        public static ulong MultiplyHigh(ulong left, ulong right) { throw new PlatformNotSupportedException(); }
    }
}";

            await VerifyAnalyzerAsyncCsNet50(csSource);
        }

        private static async Task VerifyAnalyzerAsyncCs(string sourceCode, params DiagnosticResult[] expectedDiagnostics)
        {
            var test = PopulateTestCs(sourceCode, expectedDiagnostics);
            await test.RunAsync();
        }

        private static async Task VerifyAnalyzerAsyncCsNet50(string sourceCode, params DiagnosticResult[] expectedDiagnostics)
        {
            var test = PopulateTestCsNet50(sourceCode, expectedDiagnostics);
            await test.RunAsync();
        }

        private static VerifyCS.Test PopulateTestCs(string sourceCode, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = sourceCode,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
            };
            test.ExpectedDiagnostics.AddRange(expected);
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $@"root = true

[*]
build_property.TargetFramework = net5
"));
            return test;
        }

        private static VerifyCS.Test PopulateTestCsNet50(string sourceCode, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = sourceCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
            };
            test.ExpectedDiagnostics.AddRange(expected);
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $@"root = true

[*]
build_property.TargetFramework = net5
"));
            return test;
        }
    }
}
