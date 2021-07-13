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
    public void MethodWithMultilineConditional(int a)
    {
        if (a == 0)
        {
            a=1;
            [|throw new PlatformNotSupportedException();|] // 'Test.MethodWithMultilineConditional(int)' conditionally throws PNSE in non platform check
        }
    }    

    public void MethodWithConditional()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            [|throw new PlatformNotSupportedException();|] // 'Test.MethodWithConditional()' conditionally throws PNSE in non platform check
        }
    }

    public void MethodWithOtherStatement()
    {
        string message = ""Hello world"";
        [|throw new PlatformNotSupportedException(message);|] // '{0}' throws PNSE and has no annotation, reachable on {1}
    }

    public void MethodWithMoreStatements(int state, string msg)
    {
        var a = state - 1;
        DoSomething(a);
        if (a > 0)
            DoSomething(a);
        else
            [|throw new PlatformNotSupportedException();|] // '{0}' throws PNSE and has no annotation, reachable on {1}
    }

    private void DoSomething(int a)
    {
        a--;
        if (a < 0)
            [|throw new PlatformNotSupportedException();|] // '{0}' throws PNSE and has no annotation, reachable on {1}
        Console.WriteLine(a);
    }
}";

            await VerifyAnalyzerAsyncCs(csSource);
        }

        [Fact]
        public async Task IgnoreDefaultCase()
        {
            var csSource = @"
using System;
using System.Runtime.InteropServices;

public class Test
{
    public void MethodThrowsOnDefaultCase(int a)
    {
        switch (a)
        {
            case 1 : a++; break;
            case 2: a+=2; break;
            default: throw new PlatformNotSupportedException(); 
        }
    }    

    public void MethodThrowsInOneCase(int a)
    {
        switch (a)
        {
            case 1 : a++; break;
            case 2 : a+=2; break;
            case 3 : [|throw new PlatformNotSupportedException(""message"")|]; break;
            default : a=0; break; 
        }
    }
}";

            await VerifyAnalyzerAsyncCs(csSource);
        }

        [Fact]
        public async Task ThrowsPNSEWithAttributeAndConditional()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

public class Test
{
    [SupportedOSPlatform(""Linux"")]
    public void MethodWithConditionalAndSupportedAttribute()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) 
            throw new PlatformNotSupportedException();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) 
            [|throw new PlatformNotSupportedException();|]
    }

    [UnsupportedOSPlatform(""Linux"")]
    public void MethodWithConditionalAndUnsupportedAttribute()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) 
            [|throw new PlatformNotSupportedException();|]

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) 
            throw new PlatformNotSupportedException();
    }
}";
            await VerifyAnalyzerAsyncCsNet50(csSource);
        }

        [Fact]
        public async Task ThrowsPNSEWithAttributeOneLiner()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

public class Test
{
    [SupportedOSPlatform(""Linux"")]
    public void OneLinerThrowWithSupprtedAttribute() 
    {
        [|throw new PlatformNotSupportedException();|] // 'Test.OneLinerThrowWithSupprtedAttribute()' unconditionally throws PNSE and not annotated accordingly, reachable on Analyzer.Utilities.SmallDictionary`2[System.String,System.Version]
    }

    [UnsupportedOSPlatform(""Linux"")]
    public void OneLinerThrowWithUnsupprtedAttribute() 
    {
        throw new PlatformNotSupportedException(); // 'Test.OneLinerThrowWithUnsupprtedAttribute()' unconditionally throws PNSE and not annotated accordingly, unreachable on Analyzer.Utilities.SmallDictionary`2[System.String,System.Version]
    }

    [SupportedOSPlatform(""Linux"")]
    [Obsolete(""Does not work in .Net Core"")]
    public void SupportedObsoleteOneLinerThrow() 
    {
        throw new PlatformNotSupportedException();
    }

    [UnsupportedOSPlatform(""Linux"")]
    [Obsolete(""Does not work in .Net Core"")]
    public void UnsupportedObsoleteOneLinerThrow() 
    {
        throw new PlatformNotSupportedException();
    }
}";

            await VerifyAnalyzerAsyncCsNet50(csSource);
        }

        [Fact]
        public async Task ThrowsPNSEWithinIntrinsicsIgnored()
        {
            var csSource = @"
using System;

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

        [Fact]
        public async Task ThrowsPNSEWithinISerializableImplementation()
        {
            var csSource = @"
using System;
using System.Runtime.Serialization;

namespace ns
{
    public class Test : ISerializable, IDeserializationCallback, IObjectReference
    {
        public static int M1(int value) { [|throw new PlatformNotSupportedException();|] }
        void IDeserializationCallback.OnDeserialization(object sender) =>
            throw new PlatformNotSupportedException();
        public void GetObjectData(SerializationInfo info, StreamingContext context) =>
            throw new PlatformNotSupportedException();
        public object GetRealObject(StreamingContext context) =>
            throw new PlatformNotSupportedException();
    }
}";

            await VerifyAnalyzerAsyncCsNet50(csSource);
        }

        [Fact]
        public async Task ThrowsPNSEWithinParentImplementedISerializable()
        {
            var csSource = @"
using System;
using System.Runtime.Serialization;
using System.Collections;

namespace ns
{
    class Program
    {
        private sealed class SyncHashtable : Hashtable
        {
            internal SyncHashtable(SerializationInfo info, StreamingContext context) : base(info, context)
            {
                throw new PlatformNotSupportedException();
            }

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                throw new PlatformNotSupportedException();
            }
        }
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
build_property.TargetFramework = net6.0
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
build_property.TargetFramework = net5.0
"));
            return test;
        }
    }
}
