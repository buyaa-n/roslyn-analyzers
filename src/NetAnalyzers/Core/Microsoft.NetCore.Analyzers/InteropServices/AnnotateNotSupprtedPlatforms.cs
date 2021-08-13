// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AnnotateNotSupprtedPlatforms : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1420";
        private static readonly LocalizableString s_localizableTitle = "Annotate not supported platform";
        private static readonly LocalizableString s_localizableOneLinerThrow = "'{0}' unconditionally throws PNSE and not annotated accordingly, reachable on {1}";
        private static readonly LocalizableString s_localizableOneLinerThrowUnsupported = "'{0}' unconditionally throws PNSE unreachable on {1} and reachable on {2}";
        private static readonly LocalizableString s_localizableConditionallyThrowsFromSupported = "'{0}' throws PNSE on '{1}' and has no unsupported annotation and reachable on {2}";
        private static readonly LocalizableString s_localizableConditionallyThrowsFromSupportedUnsupported = "'{0}' throws PNSE on '{1}' and reachable on {2} and unreachable {3}";
        private static readonly LocalizableString s_localizableConditionallyThrowsFromUnsupported = "'{0}' throws PNSE on '{1}' and unreachable on {2}";
        //private static readonly LocalizableString s_localizablePlatformCheckThrowsFromUnsupported = "'{0}' throws PNSE on '{1}' and has {2} platform check, unreachable on {3}";
        private static readonly LocalizableString s_localizableConditionallyThrowsNotPlatformCheck = "'{0}' conditionally throws PNSE in non platform check";
        private static readonly LocalizableString s_localizableMultiLinerThrowReachable = "'{0}' throws PNSE and has no annotation, reachable on {1}";
        private static readonly LocalizableString s_localizableMultiLinerThrowUnreachable = "'{0}' throws PNSE, unreachable on {1}, reachable on {1}";
        private static readonly LocalizableString s_localizableDescription = "Annotate not supported platform.";
        private const string SupportedOSPlatformAttribute = nameof(SupportedOSPlatformAttribute);
        private const string UnsupportedOSPlatformAttribute = nameof(UnsupportedOSPlatformAttribute);
        private const string AllPlatforms = "all platforms";

        internal static DiagnosticDescriptor OneLinerThrowReachable = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableOneLinerThrow,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor OneLinerThrowUnreachable = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableOneLinerThrowUnsupported,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor ConditionallyThrowsFromSupported = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableConditionallyThrowsFromSupported,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor ConditionallyThrowsFromUnsupported = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableConditionallyThrowsFromUnsupported,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor ConditionallyThrowsFromSupportedUnsupported = DiagnosticDescriptorHelper.Create(RuleId,
                                                                      s_localizableTitle,
                                                                      s_localizableConditionallyThrowsFromSupportedUnsupported,
                                                                      DiagnosticCategory.Interoperability,
                                                                      RuleLevel.BuildWarning,
                                                                      description: s_localizableDescription,
                                                                      isPortedFxCopRule: false,
                                                                      isDataflowRule: false);

        /*internal static DiagnosticDescriptor PlatformCheckThrowsFromUnsupported = DiagnosticDescriptorHelper.Create(RuleId,
                                                                              s_localizableTitle,
                                                                              s_localizablePlatformCheckThrowsFromUnsupported,
                                                                              DiagnosticCategory.Interoperability,
                                                                              RuleLevel.BuildWarning,
                                                                              description: s_localizableDescription,
                                                                              isPortedFxCopRule: false,
                                                                              isDataflowRule: false);*/

        internal static DiagnosticDescriptor ConditionallyThrowsNonPlatformCheck = DiagnosticDescriptorHelper.Create(RuleId,
                                                                              s_localizableTitle,
                                                                              s_localizableConditionallyThrowsNotPlatformCheck,
                                                                              DiagnosticCategory.Interoperability,
                                                                              RuleLevel.BuildWarning,
                                                                              description: s_localizableDescription,
                                                                              isPortedFxCopRule: false,
                                                                              isDataflowRule: false);

        internal static DiagnosticDescriptor MultiLinerThrowReachable = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMultiLinerThrowReachable,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);
        internal static DiagnosticDescriptor MultiLinerThrowUnreachable = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMultiLinerThrowUnreachable,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(OneLinerThrowReachable, MultiLinerThrowReachable, ConditionallyThrowsFromSupported, ConditionallyThrowsFromUnsupported);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemPlatformNotSupportedException, out var pNSException) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute, out var obsoleteAttribute) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemOperatingSystem, out var operatingSystemType))
                {
                    return;
                }

                if (IsLowerThanNet5(context.Options, context.Compilation))
                {
                    return;
                }

                var getObjectData = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationISerializable)?.
                        GetMembers().OfType<IMethodSymbol>().FirstOrDefault();
                var onDeserialization = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationIDeserializationCallback)?.
                        GetMembers().OfType<IMethodSymbol>().FirstOrDefault();
                var getRealObject = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationIObjectReference)?.
                        GetMembers().OfType<IMethodSymbol>().FirstOrDefault();

                var serializationInfoType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationSerializationInfo);
                var streamingContextType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationStreamingContext);
                var runtimeInformationType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesRuntimeInformation);
                var osPlatformType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesOSPlatform);

                var runtimeIsOSPlatformMethod = runtimeInformationType?.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m =>
                        "IsOSPlatform" == m.Name &&
                        m.IsStatic &&
                        m.ReturnType.SpecialType == SpecialType.System_Boolean &&
                        m.Parameters.Length == 1);

                var guardMethods = GetOperatingSystemGuardMethods(runtimeIsOSPlatformMethod, operatingSystemType);

                context.RegisterOperationAction(context => AnalyzeOperationBlock(context, pNSException, obsoleteAttribute, getObjectData,
                    onDeserialization, getRealObject, serializationInfoType, streamingContextType, guardMethods, osPlatformType), OperationKind.Throw);
            });

            static ImmutableArray<IMethodSymbol> GetOperatingSystemGuardMethods(IMethodSymbol? runtimeIsOSPlatformMethod, INamedTypeSymbol operatingSystemType)
            {
                var methods = operatingSystemType.GetMembers().OfType<IMethodSymbol>().Where(m =>
                    m.IsStatic &&
                    m.ReturnType.SpecialType == SpecialType.System_Boolean &&
                    ("IsOSPlatform" == m.Name) || NameAndParametersValid(m)).
                    ToImmutableArray();

                if (runtimeIsOSPlatformMethod != null)
                {
                    return methods.Add(runtimeIsOSPlatformMethod);
                }

                return methods;
            }

            static bool NameAndParametersValid(IMethodSymbol method) => method.Name.StartsWith("Is", StringComparison.Ordinal) &&
                    (method.Parameters.Length == 0 || method.Name.EndsWith("VersionAtLeast", StringComparison.Ordinal));
        }

        private static bool IsLowerThanNet5(AnalyzerOptions options, Compilation compilation)
        {
            var tfmString = options.GetMSBuildPropertyValue(MSBuildPropertyOptionNames.TargetFramework, compilation);

            if (tfmString?.Length >= 4 &&
                tfmString.StartsWith("net", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(tfmString[3].ToString(), out var major) &&
                major >= 5)
            {
                return false;
            }

            return true;
        }

        private static void AnalyzeOperationBlock(OperationAnalysisContext context, INamedTypeSymbol pNSException, INamedTypeSymbol obsoleteAttribute,
            IMethodSymbol? getObjectData, IMethodSymbol? onDeserialization, IMethodSymbol? getRealObject, INamedTypeSymbol? serializationInfoType,
            INamedTypeSymbol? streamingContextType, ImmutableArray<IMethodSymbol> guardMethods, INamedTypeSymbol? osPlatformType)
        {
            var containingSymbol = context.ContainingSymbol;
            if (containingSymbol.ToDisplayString().Contains("System.Runtime.Intrinsics", StringComparison.Ordinal))
            {
                return;
            }

            /*var path = context.ContainingSymbol.Locations[0].SourceTree.FilePath;

            if (path != null && Path.GetFileNameWithoutExtension(path).Contains("notsupported.cs", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }*/

            if (context.Operation is IThrowOperation throwOperation &&
                throwOperation.GetThrownExceptionType() is ITypeSymbol createdException &&
                createdException.Equals(pNSException, SymbolEqualityComparer.Default))
            {
                if (containingSymbol is IMethodSymbol method)
                {
                    if (method.IsVirtual ||
                        method.IsOverrideOrImplementationOfInterfaceMember(getObjectData) ||
                        method.IsOverrideOrImplementationOfInterfaceMember(onDeserialization) ||
                        method.IsOverrideOrImplementationOfInterfaceMember(getRealObject) ||
                        method.IsSerializationConstructor(serializationInfoType, streamingContextType))
                    {
                        return;
                    }
                }

                var containingBlock = throwOperation.GetTopmostParentBlock();
                if (TryGetPlatformAttributes(containingSymbol, out var attributes, obsoleteAttribute))
                {
                    if (!attributes.Obsolete)
                    {
                        if (!attributes.SupportedList.IsEmpty)
                        {
                            if (attributes.UnupportedList.IsEmpty)
                            {
                                if (containingBlock != null)
                                {
                                    if (IsSingleStatement(containingBlock))
                                    {
                                        if (IsConditional(containingBlock, out var conditional))
                                        {
                                            if (IsPlatformCheck(conditional.Condition, guardMethods, osPlatformType, out var platform, out var _))
                                            {
                                                if (attributes.SupportedList.ContainsKey(platform))
                                                {
                                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromSupported, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                        platform, string.Join(",", attributes.SupportedList.Keys.AsEnumerable())));
                                                }
                                            } // Do not flag non platform check conditions
                                        }
                                        else if (!IsSwitchCase(containingBlock))
                                        {
                                            // "'{0}' unconditionally throws PNSE and not annotated accordingly, reachable on {1}"
                                            context.ReportDiagnostic(throwOperation.CreateDiagnostic(OneLinerThrowReachable, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                string.Join(",", attributes.SupportedList.Keys.AsEnumerable())));
                                        }
                                    }
                                    else // multi line
                                    {
                                        if (throwOperation.Parent is IConditionalOperation conditional)
                                        {
                                            if (IsPlatformCheck(conditional.Condition, guardMethods, osPlatformType, out var platform, out var _))
                                            {
                                                if (attributes.SupportedList.ContainsKey(platform))
                                                {
                                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromSupported, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                        platform, string.Join(",", attributes.SupportedList.Keys.AsEnumerable())));
                                                }
                                            } // Do not flag non platform check conditions
                                        }
                                        else if (throwOperation.Parent is not ISwitchCaseOperation) // do not flag throws within switch case
                                        {
                                            // "'{0}' unconditionally throws PNSE and not annotated accordingly, reachable on {1}"
                                            context.ReportDiagnostic(throwOperation.CreateDiagnostic(MultiLinerThrowReachable, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                string.Join(",", attributes.SupportedList.Keys.AsEnumerable())));
                                        }
                                    }
                                }
                                else
                                {
                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(OneLinerThrowReachable,
                                        context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)), attributes.SupportedList));
                                }
                            }
                            else // has both supported and unsupported
                            {
                                if (containingBlock != null)
                                {
                                    if (IsSingleStatement(containingBlock))
                                    {
                                        if (IsConditional(containingBlock, out var conditional))
                                        {
                                            if (IsPlatformCheck(conditional.Condition, guardMethods, osPlatformType, out var platform, out var _))
                                            {
                                                if (attributes.SupportedList.ContainsKey(platform))
                                                {
                                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromSupported, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                        platform, string.Join(",", attributes.SupportedList.Keys.AsEnumerable())));
                                                }
                                                else if (!attributes.UnupportedList.ContainsKey(platform))
                                                {
                                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromSupportedUnsupported, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                         platform, string.Join(",", attributes.SupportedList.Keys.AsEnumerable()), string.Join(",", attributes.UnupportedList.Keys.AsEnumerable())));
                                                }
                                            } // Do not flag non platform check conditions
                                        }
                                        else if (throwOperation.Parent is not ISwitchCaseOperation)
                                        {
                                            // "'{0}' unconditionally throws PNSE and not annotated accordingly, reachable on {1}"
                                            context.ReportDiagnostic(throwOperation.CreateDiagnostic(OneLinerThrowReachable, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                string.Join(",", attributes.SupportedList.Keys.AsEnumerable()) + ", unreachable: " + string.Join(",", attributes.UnupportedList.Keys.AsEnumerable())));
                                        }
                                    }
                                    else // multi line
                                    {
                                        if (throwOperation.Parent is IConditionalOperation conditional)
                                        {
                                            if (IsPlatformCheck(conditional.Condition, guardMethods, osPlatformType, out var platform, out var negated))
                                            {
                                                if (negated)
                                                {
                                                    if (attributes.UnupportedList.ContainsKey(platform))
                                                    {
                                                        context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromSupportedUnsupported, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                             platform, string.Join(",", attributes.SupportedList.Keys.AsEnumerable()), string.Join(",", attributes.UnupportedList.Keys.AsEnumerable())));
                                                    }
                                                }
                                                else
                                                {
                                                    if (!attributes.UnupportedList.ContainsKey(platform))
                                                    {
                                                        context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromSupported, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                            platform, string.Join(",", attributes.SupportedList.Keys.AsEnumerable())));
                                                    }
                                                    else if (attributes.SupportedList.ContainsKey(platform))
                                                    {
                                                        context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromSupportedUnsupported, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                             platform, string.Join(",", attributes.SupportedList.Keys.AsEnumerable()), string.Join(",", attributes.UnupportedList.Keys.AsEnumerable())));
                                                    }
                                                }
                                            } // Do not flag non platform check conditions
                                        }
                                        else if (throwOperation.Parent is not ISwitchCaseOperation) // do not flag throws within switch case
                                        {
                                            // "'{0}' unconditionally throws PNSE and not annotated accordingly, reachable on {1}"
                                            context.ReportDiagnostic(throwOperation.CreateDiagnostic(MultiLinerThrowReachable, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                                string.Join(",", attributes.SupportedList.Keys.AsEnumerable()) + ", unreachable: " + string.Join(",", attributes.UnupportedList.Keys.AsEnumerable())));
                                        }
                                    }
                                }
                                else
                                {
                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(MultiLinerThrowReachable, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)), attributes.SupportedList));
                                }
                            }
                        }
                        else if (!attributes.UnupportedList.IsEmpty) // has only unsupported attributes
                        {
                            if (containingBlock != null)
                            {
                                if (IsSingleStatement(containingBlock))
                                {
                                    if (IsConditional(containingBlock, out var conditional))
                                    {
                                        if (IsPlatformCheck(conditional.Condition, guardMethods, osPlatformType, out var platform, out var negated))
                                        { // '{0}' throws PNSE on '{1}' and unreachable on {2}
                                            if (negated)
                                            {   // probably should flag for any case, with better messaging
                                                if (attributes.UnupportedList.ContainsKey(platform))
                                                {
                                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromUnsupported,
                                                        context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)), platform, string.Join(",", attributes.UnupportedList.Keys.AsEnumerable())));
                                                }
                                            }
                                            else
                                            {
                                                if (!attributes.UnupportedList.ContainsKey(platform))
                                                {
                                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromUnsupported,
                                                        context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)), platform, string.Join(",", attributes.UnupportedList.Keys.AsEnumerable())));
                                                }
                                            }
                                        } // Do not flag non platform check conditions
                                    }
                                    else if (!IsSwitchCase(containingBlock))
                                    {
                                        //'{0}' unconditionally throws PNSE unreachable on {1} and reachable on {2}
                                        context.ReportDiagnostic(throwOperation.CreateDiagnostic(OneLinerThrowUnreachable, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                            string.Join(",", attributes.UnupportedList.Keys.AsEnumerable()), AllPlatforms));
                                    }
                                }
                                else
                                {
                                    if (throwOperation.Parent is IConditionalOperation conditional)
                                    {
                                        if (IsPlatformCheck(conditional.Condition, guardMethods, osPlatformType, out var platform, out var negated))
                                        { //'{0}' throws PNSE on '{1}' and unreachable on {2}
                                            if (negated)
                                            {   // probably should flag for any case, with better messaging
                                                if (attributes.UnupportedList.ContainsKey(platform))
                                                {
                                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromUnsupported,
                                                        context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)), platform, string.Join(",", attributes.UnupportedList.Keys.AsEnumerable())));
                                                }
                                            }
                                            else if (!attributes.UnupportedList.ContainsKey(platform))
                                            {
                                                context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromUnsupported,
                                                    context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)), platform, string.Join(",", attributes.UnupportedList.Keys.AsEnumerable())));
                                            }
                                        }
                                    }
                                    else if (throwOperation.Parent is not ISwitchCaseOperation)
                                    { // "'{0}' throws PNSE, unreachable on {1}, reachable on {1}"
                                        context.ReportDiagnostic(throwOperation.CreateDiagnostic(MultiLinerThrowUnreachable, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)),
                                            string.Join(",", attributes.UnupportedList.Keys.AsEnumerable()), AllPlatforms));
                                    }
                                }
                            }
                        }
                    }
                }
                else // cross platform and has not obsoleted
                {
                    if (containingBlock != null)
                    {
                        if (IsSingleStatement(containingBlock))
                        {
                            if (IsConditional(containingBlock, out var conditional))
                            {
                                if (IsPlatformCheck(conditional.Condition, guardMethods, osPlatformType, out var platform, out var _))
                                { // "'{0}' throws PNSE on '{1}' and has no unsupported annotation and reachable on {2}";
                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromSupported,
                                      context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)), platform, AllPlatforms));
                                }
                                else
                                {
                                    // non cross platformcheck is usually false positive
                                    //context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsNonPlatformCheck, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation))));
                                }
                            }
                            else if (!IsSwitchCase(containingBlock))
                            {
                                context.ReportDiagnostic(throwOperation.CreateDiagnostic(OneLinerThrowReachable, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)), AllPlatforms));
                            }
                        }
                        else
                        {
                            if (throwOperation.Parent is IConditionalOperation conditional)
                            {
                                if (IsPlatformCheck(conditional.Condition, guardMethods, osPlatformType, out var platform, out var _))
                                { // "'{0}' throws PNSE on '{1}' and has no unsupported annotation and reachable on {2}";
                                    context.ReportDiagnostic(throwOperation.CreateDiagnostic(ConditionallyThrowsFromSupported,
                                      context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)), platform, AllPlatforms));
                                }
                            }
                            else if (throwOperation.Parent is not ISwitchCaseOperation)
                            {
                                context.ReportDiagnostic(throwOperation.CreateDiagnostic(MultiLinerThrowReachable, context.ContainingSymbol.ToDisplayString(GetLanguageSpecificFormat(throwOperation)), "cross platform"));
                            }
                        }
                    }
                }
            }

            static bool IsPlatformCheck(IOperation condition, ImmutableArray<IMethodSymbol> guardMethods,
                INamedTypeSymbol? osPlatformType, [NotNullWhen(true)] out string? platform, out bool negated)
            {
                platform = null;
                negated = false;
                if (condition is IUnaryOperation unary)
                {
                    condition = unary.Operand;
                    negated = true;
                }

                if (condition is IInvocationOperation invocation && guardMethods.Contains(invocation.TargetMethod.OriginalDefinition))
                {
                    using var infosBuilder = ArrayBuilder<PlatformCompatibilityAnalyzer.PlatformMethodValue>.GetInstance();
                    if (PlatformCompatibilityAnalyzer.PlatformMethodValue.TryDecode(invocation.TargetMethod, invocation.Arguments, null, osPlatformType, infosBuilder))
                    {
                        platform = infosBuilder[0].PlatformName;
                        return true;
                    }
                }
                return false;
            }

            static bool IsSingleStatement(IBlockOperation body)
            {
                return body.Operations.Length == 1 || (body.Operations.Length == 3 && body.Syntax.Language == LanguageNames.VisualBasic &&
                     body.Operations[1] is ILabeledOperation labeledOp && labeledOp.IsImplicit &&
                     body.Operations[2] is IReturnOperation returnOp && returnOp.IsImplicit);
            }

            static bool IsConditional(IBlockOperation body, [NotNullWhen(true)] out IConditionalOperation? conditional)
            {
                conditional = null;
                if (body.Operations[0] is IConditionalOperation cOperations)
                {
                    conditional = cOperations;
                    return true;
                }

                return false;
            }

            static SymbolDisplayFormat GetLanguageSpecificFormat(IOperation operation) => operation.Language == LanguageNames.CSharp ?
                SymbolDisplayFormat.CSharpShortErrorMessageFormat : SymbolDisplayFormat.VisualBasicShortErrorMessageFormat;
        }

        private static bool IsSwitchCase(IBlockOperation containingBlock)
        {
            return containingBlock.Operations[0] is ISwitchOperation;
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
