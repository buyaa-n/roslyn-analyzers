// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
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
        private static readonly LocalizableString s_localizableTitle = "Annotate not supprted platform";
        private static readonly LocalizableString s_localizableMessage = "Annotate not supprted platform";
        private static readonly LocalizableString s_localizableDescription = "Annotate not supprted platform.";
        private const string SupportedOSPlatformAttribute = nameof(SupportedOSPlatformAttribute);
        private const string UnsupportedOSPlatformAttribute = nameof(UnsupportedOSPlatformAttribute);

        internal static DiagnosticDescriptor OnlySupportedCsReachable = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMessage,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(OnlySupportedCsReachable);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemPlatformNotSupportedException, out var pNSExceptionType))
                {
                    return;
                }

                context.RegisterOperationAction(context => AnalyzeOperationBlock(context, pNSExceptionType), OperationKind.ObjectCreation);
            });
        }


        private static void AnalyzeOperationBlock(OperationAnalysisContext context, INamedTypeSymbol pNSExceptionType)
        {
            if (context.Operation is IObjectCreationOperation creation &&
                creation.Type.Equals(pNSExceptionType, SymbolEqualityComparer.Default))
            {
                if (TryGetPlatformAttributes(creation.Constructor, out var attributes))
                {

                }
            }
        }

        private static bool TryGetPlatformAttributes(ISymbol symbol, out PlatformAttributes attributes)
        {
            var container = symbol.ContainingSymbol;
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
                }

                do
                {
                    container = container.ContainingSymbol;
                    // Namespaces do not have attributes
                } while (container is INamespaceSymbol);
            }

            return !attributes.SupportedAttributes.IsEmpty || !attributes.UnupportedAttributes.IsEmpty;
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
            public void AddSupportedAttrbite(string platform, Version version)
            {
                if (!SupportedAttributes.TryGetValue(platform, out var existing) || existing < version)
                {
                    SupportedAttributes[platform] = version;
                }
            }

            public void AddUnsupportedAttrbite(string platform, Version version)
            {
                if (!UnupportedAttributes.TryGetValue(platform, out var existing) || existing > version)
                {
                    UnupportedAttributes[platform] = version;
                }
            }

            public SmallDictionary<string, Version> SupportedAttributes { get; } = new(StringComparer.OrdinalIgnoreCase);
            public SmallDictionary<string, Version> UnupportedAttributes { get; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
