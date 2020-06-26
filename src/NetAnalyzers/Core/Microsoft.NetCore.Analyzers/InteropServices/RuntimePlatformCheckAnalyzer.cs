// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
#pragma warning disable RS1001 // Missing diagnostic analyzer attribute - TODO: fix and enable analyzer.
    public sealed class RuntimePlatformCheckAnalyzer : DiagnosticAnalyzer
#pragma warning restore RS1001 // Missing diagnostic analyzer attribute.
    {
        internal const string RuleId = "CA1416";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatCheckTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableAddedMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatCheckAddedMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableObsoleteMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatCheckObsoleteMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableRemovedMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatCheckRemovedMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatCheckDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private const char SeparatorDash = '-';
        private const char SeparatorSemicolon = ';';
        private const char SeparatorDot = '.';
        private const string AddedAttributeName = nameof(AddedInOSPlatformVersionAttribute);
        private const string ObsoleteAttributeName = nameof(ObsoletedInOSPlatformVersionAttribute);
        private const string RemovedAttributeName = nameof(RemovedInOSPlatformVersionAttribute);
        private const string Windows = nameof(Windows);
        private static readonly Regex s_neutralTfmRegex = new Regex(@"^net([5-9]|standard\d|coreapp\d)\.\d$", RegexOptions.IgnoreCase);
        private static readonly Regex s_osParseRegex = new Regex(@"([a-z]{3,7})((\d{1,2})\.?(\d)?\.?(\d)?\.?(\d)?)*", RegexOptions.IgnoreCase);

        internal static DiagnosticDescriptor AddedRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableAddedMessage,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.IdeSuggestion,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);
        internal static DiagnosticDescriptor ObsoleteRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableAddedMessage,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.IdeSuggestion,
                                                                                      description: s_localizableObsoleteMessage,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);
        internal static DiagnosticDescriptor RemovedRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableAddedMessage,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.IdeSuggestion,
                                                                                      description: s_localizableRemovedMessage,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(AddedRule, ObsoleteRule, RemovedRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            bool suppressed = false;

            context.RegisterCompilationStartAction(context =>
            { 
                context.RegisterOperationAction(context =>
                {
                    var osAttribute = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeVersioningOSPlatformVersionAttribute);
                    if (osAttribute == null)
                    {
                        return;
                    }

                    suppressed = AnalyzeInvocationOperation((IInvocationOperation)context.Operation, osAttribute, context);
                }
                , OperationKind.Invocation);

                /*                                
                                // TODO: Remove the below temporary hack once new APIs are available.
                                var typeName = WellKnownTypeNames.SystemRuntimeInteropServicesRuntimeInformation + "Helper";

                                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(typeName, out var runtimeInformationType) ||
                                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesOSPlatform, out var osPlatformType))
                                {
                                    return;
                                }

                                var platformCheckMethods = GetPlatformCheckMethods(runtimeInformationType, osPlatformType);
                                if (platformCheckMethods.IsEmpty)
                                {
                                    return;
                                }

                                context.RegisterOperationBlockStartAction(context => AnalyzerOperationBlock(context, platformCheckMethods, osPlatformType));
                                return;

                                static ImmutableArray<IMethodSymbol> GetPlatformCheckMethods(INamedTypeSymbol runtimeInformationType, INamedTypeSymbol osPlatformType)
                                {
                                    using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
                                    var methods = runtimeInformationType.GetMembers().OfType<IMethodSymbol>();
                                    foreach (var method in methods)
                                    {
                                        if (s_platformCheckMethods.Contains(method.Name) &&
                                            method.Parameters.Length >= 1 &&
                                            method.Parameters[0].Type.Equals(osPlatformType) &&
                                            method.Parameters.Skip(1).All(p => p.Type.SpecialType == SpecialType.System_Int32))
                                        {
                                            builder.Add(method);
                                        }
                                    }

                                    return builder.ToImmutable();
                                }*/
            });
        }

        private static bool AnalyzeInvocationOperation(IInvocationOperation operation, INamedTypeSymbol osAttribute, OperationAnalysisContext context)
        {
            List<PlatformInfo>? parsedTfms = ParseTfm(context, context.ContainingSymbol);

            var attributes = GetApplicableAttributes(operation.TargetMethod.GetAttributes(), operation.TargetMethod.ContainingType, osAttribute);

            foreach (AttributeData attribute in attributes)
            {
                PlatformInfo parsedAttribute = PlatformInfo.ParseAttributeData(attribute);
                if (parsedTfms != null)
                {
                    foreach (PlatformInfo tfm in parsedTfms)
                    {
                        if (tfm.OsPlatform != null && tfm.OsPlatform.Equals(parsedAttribute.OsPlatform, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (AttributeVersionsMatch(parsedAttribute, tfm))
                            {
                                return true;
                            }
                        }
                    }
                }

                if (Suppress(parsedAttribute, context.ContainingSymbol))
                {
                    return true;
                }
                else
                {
                    context.ReportDiagnostic(context.Operation.Syntax.CreateDiagnostic(SwitchRule(parsedAttribute.AttributeType), operation.TargetMethod.Name,
                       parsedAttribute.OsPlatform!, $"{parsedAttribute.Version[0]}.{parsedAttribute.Version[1]}.{parsedAttribute.Version[2]}.{parsedAttribute.Version[3]}"));
                }
            }
            return false;
        }

        private static DiagnosticDescriptor SwitchRule(OsAttrbiteType attributeType)
        {
            if (attributeType == OsAttrbiteType.AddedInOSPlatformVersionAttribute)
                return AddedRule;
            if (attributeType == OsAttrbiteType.ObsoletedInOSPlatformVersionAttribute)
                return ObsoleteRule;
            return RemovedRule;
        }

        private static List<PlatformInfo>? ParseTfm(OperationAnalysisContext context, ISymbol containingSymbol)
        { // ((net[5-9]|netstandard\d|netcoreapp\d)\.\d(-([a-z]{3,7})(\d{1,2}\.?\d?\.?\d?\.?\d?)*)?)+
            string? tfmString = context.Options.GetMSBuildPropertyValue(MSBuildPropertyOptionNames.TargetFramework, AddedRule, containingSymbol, context.Compilation, context.CancellationToken);
            if (tfmString != null)
            {
                List<PlatformInfo> platformInfos = new List<PlatformInfo>() { };
                var tfms = tfmString.Split(SeparatorSemicolon);

                foreach (var tfm in tfms)
                {
                    var tokens = tfm.Split(SeparatorDash);
                    PlatformInfo platformInfo = new PlatformInfo();
                    platformInfo.Version = new int[4];
                    if (tokens.Length == 1)
                    {
                        if (!s_neutralTfmRegex.IsMatch(tokens[0]))
                        {
                            platformInfo.OsPlatform = Windows;
                        }
                    }
                    else
                    {
                        Debug.Assert(tokens.Length == 2);
                        Match match = s_osParseRegex.Match(tokens[1]);
                        if (match.Success)
                        {
                            platformInfo.OsPlatform = match.Groups[1].Value;
                            for (int i = 3; i < 7; i++)
                            {
                                if (!string.IsNullOrEmpty(match.Groups[i].Value))
                                {
                                    platformInfo.Version[i - 3] = int.Parse(match.Groups[i].Value, CultureInfo.InvariantCulture);
                                }
                            }
                        }
                        var tpmv = context.Options.GetMSBuildPropertyValue(MSBuildPropertyOptionNames.TargetPlatformMinVersion, AddedRule, containingSymbol, context.Compilation, context.CancellationToken);
                        if (tpmv != null)
                        {
                            var splitted = tpmv.Split(SeparatorDot);
                            int i = 0;
                            foreach (var token in splitted)
                            {
                                platformInfo.Version[i] = int.Parse(token, CultureInfo.InvariantCulture);
                            }
                        }
                    }
                    platformInfos.Add(platformInfo);
                }
                return platformInfos;
            }
            return null;
        }

        private static List<AttributeData> GetApplicableAttributes(ImmutableArray<AttributeData> immediateAttributes, INamedTypeSymbol type, INamedTypeSymbol osAttribute)
        {
            var attributes = new List<AttributeData>();
            foreach (AttributeData attribute in immediateAttributes)
            {
                if (attribute.AttributeClass.DerivesFromOrImplementsAnyConstructionOf(osAttribute))
                {
                    attributes.Add(attribute);
                }
            }

            while (type != null)
            {
                var current = type.GetAttributes();
                foreach (var attribute in current)
                {
                    if (attribute.AttributeClass.DerivesFromOrImplementsAnyConstructionOf(osAttribute))
                    {
                        attributes.Add(attribute);
                    }
                }
                type = type.BaseType;
            }
            return attributes;
        }

        private static bool Suppress(PlatformInfo diagnosingAttribute, ISymbol containingSymbol)
        {
            while (containingSymbol != null)
            {
                var attributes = containingSymbol.GetAttributes();
                if (attributes != null)
                {
                    foreach (AttributeData attribute in attributes)
                    {
                        if (diagnosingAttribute.AttributeType.ToString().Equals(attribute.AttributeClass.Name, StringComparison.InvariantCulture)
                            && diagnosingAttribute.OsPlatform!.Equals(attribute.ConstructorArguments[0].Value.ToString(), StringComparison.InvariantCultureIgnoreCase)
                            && AttributeVersionsMatch(diagnosingAttribute, attribute))
                        {
                            return true;
                        }
                    }
                }

                containingSymbol = containingSymbol.ContainingSymbol;
            }
            return false;
        }

        private static bool AttributeVersionsMatch(PlatformInfo diagnosingAttribute, AttributeData suppressingAttribute)
        {
            if (diagnosingAttribute.AttributeType == OsAttrbiteType.AddedInOSPlatformVersionAttribute)
            {
                for (int i = 1; i < 5; i++)
                {
                    if (diagnosingAttribute.Version[i - 1] < (int)suppressingAttribute.ConstructorArguments[i].Value)
                    {
                        return true;
                    }
                    else if (diagnosingAttribute.Version[i - 1] > (int)suppressingAttribute.ConstructorArguments[i].Value)
                    {
                        return false;
                    }
                }
                return true;
            }
            else
            {
                for (int i = 1; i < 5; i++)
                {
                    if (diagnosingAttribute.Version[i - 1] > (int)suppressingAttribute.ConstructorArguments[i].Value)
                    {
                        return true;
                    }
                    else if (diagnosingAttribute.Version[i - 1] < (int)suppressingAttribute.ConstructorArguments[i].Value)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private static bool AttributeVersionsMatch(PlatformInfo diagnosingAttribute, PlatformInfo tfm)
        {
            if (diagnosingAttribute.AttributeType == OsAttrbiteType.AddedInOSPlatformVersionAttribute)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (diagnosingAttribute.Version[i] < tfm.Version[i])
                    {
                        return true;
                    }
                    else if (diagnosingAttribute.Version[i] > tfm.Version[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    if (diagnosingAttribute.Version[i] > tfm.Version[i])
                    {
                        return true;
                    }
                    else if (diagnosingAttribute.Version[i] < tfm.Version[i])
                    {
                        return false;
                    }
                }
                return true;
            };
        }

        private static void AnalyzerOperationBlock(
            OperationBlockStartAnalysisContext context,
            ImmutableArray<IMethodSymbol> platformCheckMethods,
            INamedTypeSymbol osPlatformType)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope - disposed in OperationBlockEndAction.
            var platformSpecificOperations = PooledConcurrentSet<IInvocationOperation>.GetInstance();
#pragma warning restore CA2000 // Dispose objects before losing scope
            var needsValueContentAnalysis = false;

            context.RegisterOperationAction(context =>
            {
                var invocation = (IInvocationOperation)context.Operation;
                if (platformCheckMethods.Contains(invocation.TargetMethod))
                {
                    needsValueContentAnalysis = needsValueContentAnalysis || ComputeNeedsValueContentAnalysis(invocation);
                }
                else
                {
                    // TODO: Add real platform specific operations that need runtime OS platform validation.
                    platformSpecificOperations.Add(invocation);
                }
            }, OperationKind.Invocation);

            context.RegisterOperationBlockEndAction(context =>
            {
                try
                {
                    if (platformSpecificOperations.IsEmpty ||
                        !(context.OperationBlocks.GetControlFlowGraph() is { } cfg))
                    {
                        return;
                    }

                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                    var analysisResult = FlightEnabledAnalysis.TryGetOrComputeResult(
                        cfg, context.OwningSymbol, platformCheckMethods, c => GetValueForFlightEnablingMethodInvocation(c, osPlatformType),
                        wellKnownTypeProvider, context.Options, AddedRule, performPointsToAnalysis: needsValueContentAnalysis,
                        performValueContentAnalysis: needsValueContentAnalysis, context.CancellationToken,
                        out var pointsToAnalysisResult, out var valueContentAnalysisResult);
                    if (analysisResult == null)
                    {
                        return;
                    }

                    Debug.Assert(valueContentAnalysisResult == null || needsValueContentAnalysis);
                    Debug.Assert(pointsToAnalysisResult == null || needsValueContentAnalysis);

                    foreach (var platformSpecificOperation in platformSpecificOperations)
                    {
                        var value = analysisResult[platformSpecificOperation.Kind, platformSpecificOperation.Syntax];
                        if (value.Kind == FlightEnabledAbstractValueKind.Unknown)
                        {
                            continue;
                        }

                        // TODO: Add real checks.

                        // TODO Platform checks:'{0}'
                        context.ReportDiagnostic(platformSpecificOperation.CreateDiagnostic(AddedRule, value));
                    }
                }
                finally
                {
                    platformSpecificOperations.Free();
                }

                return;

                // local functions
                static FlightEnabledAbstractValue GetValueForFlightEnablingMethodInvocation(FlightEnabledAnalysisCallbackContext context, INamedTypeSymbol osPlatformType)
                {
                    Debug.Assert(context.Arguments.Length > 0);

                    if (!TryDecodeOSPlatform(context.Arguments, osPlatformType, out var osPlatformProperty) ||
                        !TryDecodeOSVersion(context, out var osVersion))
                    {
                        // Bail out
                        return FlightEnabledAbstractValue.Unknown;
                    }

                    var enabledFlight = $"{context.InvokedMethod.Name};{osPlatformProperty.Name};{osVersion}";
                    return new FlightEnabledAbstractValue(enabledFlight);
                }
            });
        }

        private static bool TryDecodeOSPlatform(
            ImmutableArray<IArgumentOperation> arguments,
            INamedTypeSymbol osPlatformType,
            [NotNullWhen(returnValue: true)] out IPropertySymbol? osPlatformProperty)
        {
            Debug.Assert(!arguments.IsEmpty);
            return TryDecodeOSPlatform(arguments[0].Value, osPlatformType, out osPlatformProperty);
        }

        private static bool TryDecodeOSPlatform(
            IOperation argumentValue,
            INamedTypeSymbol osPlatformType,
            [NotNullWhen(returnValue: true)] out IPropertySymbol? osPlatformProperty)
        {
            if ((argumentValue is IPropertyReferenceOperation propertyReference) &&
                propertyReference.Property.ContainingType.Equals(osPlatformType))
            {
                osPlatformProperty = propertyReference.Property;
                return true;
            }

            osPlatformProperty = null;
            return false;
        }

        private static bool TryDecodeOSVersion(FlightEnabledAnalysisCallbackContext context, [NotNullWhen(returnValue: true)] out string? osVersion)
        {
            var builder = new StringBuilder();
            var first = true;
            foreach (var argument in context.Arguments.Skip(1))
            {
                if (!TryDecodeOSVersionPart(argument, context, out var osVersionPart))
                {
                    osVersion = null;
                    return false;
                }

                if (!first)
                {
                    builder.Append(".");
                }

                builder.Append(osVersionPart);
                first = false;
            }

            osVersion = builder.ToString();
            return osVersion.Length > 0;

            static bool TryDecodeOSVersionPart(IArgumentOperation argument, FlightEnabledAnalysisCallbackContext context, out int osVersionPart)
            {
                if (argument.Value.ConstantValue.HasValue &&
                    argument.Value.ConstantValue.Value is int versionPart)
                {
                    osVersionPart = versionPart;
                    return true;
                }

                if (context.ValueContentAnalysisResult != null)
                {
                    var valueContentValue = context.ValueContentAnalysisResult[argument.Value];
                    if (valueContentValue.IsLiteralState &&
                        valueContentValue.LiteralValues.Count == 1 &&
                        valueContentValue.LiteralValues.Single() is int part)
                    {
                        osVersionPart = part;
                        return true;
                    }
                }

                osVersionPart = default;
                return false;
            }
        }

        private static bool ComputeNeedsValueContentAnalysis(IInvocationOperation invocation)
        {
            Debug.Assert(invocation.Arguments.Length > 0);
            foreach (var argument in invocation.Arguments.Skip(1))
            {
                if (!argument.Value.ConstantValue.HasValue)
                {
                    return true;
                }
            }

            return false;
        }

        private enum OsAttrbiteType
        {
            None, AddedInOSPlatformVersionAttribute, ObsoletedInOSPlatformVersionAttribute, RemovedInOSPlatformVersionAttribute
        }

        private class PlatformInfo
        {
            public OsAttrbiteType AttributeType { get; set; }
            public string? OsPlatform { get; set; }
            public int[] Version { get; set; }

            public static PlatformInfo ParseAttributeData(AttributeData osAttibute)
            {
                PlatformInfo platformInfo = new PlatformInfo();
                switch (osAttibute.AttributeClass.Name)
                {
                    case AddedAttributeName:
                        platformInfo.AttributeType = OsAttrbiteType.AddedInOSPlatformVersionAttribute; break;
                    case ObsoleteAttributeName:
                        platformInfo.AttributeType = OsAttrbiteType.ObsoletedInOSPlatformVersionAttribute; break;
                    case RemovedAttributeName:
                        platformInfo.AttributeType = OsAttrbiteType.RemovedInOSPlatformVersionAttribute; break;
                    default:
                        platformInfo.AttributeType = OsAttrbiteType.None; break;
                }

                platformInfo.OsPlatform = osAttibute.ConstructorArguments[0].Value.ToString();
                platformInfo.Version = new int[4];
                for (int i = 1; i < osAttibute.ConstructorArguments.Length; i++)
                {
                    platformInfo.Version[i - 1] = (int)osAttibute.ConstructorArguments[i].Value;
                }
                return platformInfo;
            }
        }
    }


}