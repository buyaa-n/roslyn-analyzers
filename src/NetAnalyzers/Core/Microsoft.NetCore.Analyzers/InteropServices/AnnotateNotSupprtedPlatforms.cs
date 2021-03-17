// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AnnotateNotSupprtedPlatforms : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1419";
        private static readonly LocalizableString s_localizableTitle = "Annotate not supported platform";
        private static readonly LocalizableString s_localizableOneLinerThrow = "'{0}' only throws PNSE and not annotated accordingly";
        private static readonly LocalizableString s_localizableThrowWithinObsolete = "'{0}' throws PNSE and annotated with Obsolete";
        private static readonly LocalizableString s_localizableMultiLinerThrow = "'{0}' throws PNSE and has no annotation";
        private static readonly LocalizableString s_localizableDescription = "Annotate not supported platform.";
        private const string SupportedOSPlatformAttribute = nameof(SupportedOSPlatformAttribute);
        private const string UnsupportedOSPlatformAttribute = nameof(UnsupportedOSPlatformAttribute);

        internal static DiagnosticDescriptor OneLinerThrow = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableOneLinerThrow,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor ThrownWithinObsolete = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableThrowWithinObsolete,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor MultiLinerThrow = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMultiLinerThrow,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(OneLinerThrow, MultiLinerThrow, ThrownWithinObsolete);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemPlatformNotSupportedException, out var pNSException) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute, out var obsoleteAttribute))
                {
                    return;
                }

                if (IsLowerThanNet5(context.Options, context.Compilation, context.CancellationToken))
                {
                    return;
                }

                context.RegisterOperationAction(context => AnalyzeOperationBlock(context, pNSException, obsoleteAttribute), OperationKind.Throw);
            });
        }

        private static bool IsLowerThanNet5(AnalyzerOptions options, Compilation compilation, CancellationToken token)
        {
            var tfmString = options.GetMSBuildPropertyValue(MSBuildPropertyOptionNames.TargetFramework, compilation, token);

            if (tfmString?.Length >= 4 &&
                tfmString.StartsWith("net", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(tfmString[3].ToString(), out var major) &&
                major >= 5)
            {
                return false;
            }

            return true;
        }

        private static void AnalyzeOperationBlock(OperationAnalysisContext context, INamedTypeSymbol pNSException, INamedTypeSymbol obsoleteAttribute)
        {
            var containingSymbol = context.ContainingSymbol;
            if (containingSymbol.ToDisplayString().Contains("System.Runtime.Intrinsics", StringComparison.Ordinal))
            {
                return;
            }

            var path = context.ContainingSymbol.Locations[0].SourceTree.FilePath;

            if (path != null && Path.GetFileNameWithoutExtension(path).Contains("notsupported.cs", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (context.Operation is IThrowOperation throwOperation &&
                throwOperation.GetThrownExceptionType() is ITypeSymbol createdException &&
                createdException.Equals(pNSException, SymbolEqualityComparer.Default))
            {
                if (containingSymbol is IMethodSymbol method &&
                    method.IsVirtual)
                {
                    return;
                }

                if (TryGetPlatformAttributes(context.ContainingSymbol, out var attributes, obsoleteAttribute))
                {
                    if (attributes.Obsolete && attributes.SupportedList.IsEmpty && attributes.UnupportedList.IsEmpty)
                    {
                        // context.ReportDiagnostic(throwOperation.CreateDiagnostic(ThrownWithinObsolete, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation))));
                    }
                    else // Maybe report what attributes it has?
                    {
                        //context.ReportDiagnostic(throwOperation.CreateDiagnostic(ThrownWithinObsolete, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation))));
                    }
                }
                else
                {
                    var containingBlock = throwOperation.GetTopmostParentBlock();
                    if (containingBlock != null && IsSingleStatementBody(containingBlock))
                    {
                        context.ReportDiagnostic(throwOperation.CreateDiagnostic(OneLinerThrow, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation))));
                    }
                    else
                    {
                        context.ReportDiagnostic(throwOperation.CreateDiagnostic(MultiLinerThrow, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation))));
                    }
                }
            }

            static bool IsSingleStatementBody(IBlockOperation body)
            {
                return body.Operations.Length == 1 ||
                    (body.Operations.Length == 3 && body.Syntax.Language == LanguageNames.VisualBasic &&
                     body.Operations[1] is ILabeledOperation labeledOp && labeledOp.IsImplicit &&
                     body.Operations[2] is IReturnOperation returnOp && returnOp.IsImplicit);
            }

            static SymbolDisplayFormat GetLanguageSpecificFormat(IOperation operation) => operation.Language == LanguageNames.CSharp ?
                SymbolDisplayFormat.CSharpShortErrorMessageFormat : SymbolDisplayFormat.VisualBasicShortErrorMessageFormat;
        }

        private static bool TryGetPlatformAttributes(ISymbol symbol, out PlatformAttributes attributes, INamedTypeSymbol obsoleteAttribute)
        {
            var container = symbol;
            attributes = new PlatformAttributes();

            while (container != null)
            {
                foreach (AttributeData attribute in container.GetAttributes())
                {
                    if (SupportedOSPlatformAttribute == attribute.AttributeClass.Name &&
                        !attribute.ConstructorArguments.IsEmpty &&
                        attribute.ConstructorArguments[0] is { } argument &&
                        argument.Type.SpecialType == SpecialType.System_String &&
                        TryParsePlatformNameAndVersion(argument.Value.ToString(), out string platformName, out Version? version))
                    {
                        attributes.AddSupportedAttrbite(platformName, version);
                    }
                    else if (UnsupportedOSPlatformAttribute == attribute.AttributeClass.Name &&
                            !attribute.ConstructorArguments.IsEmpty &&
                            attribute.ConstructorArguments[0] is { } arg &&
                            arg.Type.SpecialType == SpecialType.System_String &&
                            TryParsePlatformNameAndVersion(arg.Value.ToString(), out string platform, out Version? version2))
                    {
                        attributes.AddUnsupportedAttrbite(platform, version2);
                    }

                    if (attribute.AttributeClass.Equals(obsoleteAttribute))
                    {
                        attributes.Obsolete = true;
                    }
                }

                do
                {
                    container = container.ContainingSymbol;
                    // Namespaces do not have attributes
                } while (container is INamespaceSymbol);
            }

            return attributes.Obsolete || !attributes.SupportedList.IsEmpty || !attributes.UnupportedList.IsEmpty;
        }

        private static bool TryParsePlatformNameAndVersion(string osString, out string osPlatformName, [NotNullWhen(true)] out Version? version)
        {
            version = null;
            osPlatformName = string.Empty;
            for (int i = 0; i < osString.Length; i++)
            {
                if (char.IsDigit(osString[i]))
                {
                    if (i > 0 && Version.TryParse(osString[i..], out Version? parsedVersion))
                    {
                        osPlatformName = osString.Substring(0, i);
                        version = parsedVersion;
                        return true;
                    }

                    return false;
                }
            }

            osPlatformName = osString;
            version = new Version(0, 0);
            return true;
        }

        private sealed class PlatformAttributes
        {
            public PlatformAttributes()
            {

            }
            public void AddSupportedAttrbite(string platform, Version version)
            {
                if (!SupportedList.TryGetValue(platform, out var existing) || existing < version)
                {
                    SupportedList[platform] = version;
                }
            }

            public void AddUnsupportedAttrbite(string platform, Version version)
            {
                if (!UnupportedList.TryGetValue(platform, out var existing) || existing > version)
                {
                    UnupportedList[platform] = version;
                }
            }

            public SmallDictionary<string, Version> SupportedList { get; } = new(StringComparer.OrdinalIgnoreCase);
            public SmallDictionary<string, Version> UnupportedList { get; } = new(StringComparer.OrdinalIgnoreCase);
            public bool Obsolete { get; set; }
        }
    }
}
