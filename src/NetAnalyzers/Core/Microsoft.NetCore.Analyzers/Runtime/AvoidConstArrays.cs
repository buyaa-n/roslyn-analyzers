// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1861: Avoid constant arrays as arguments. Replace with static readonly arrays.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidConstArraysAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1861";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
            CreateLocalizableResourceString(nameof(AvoidConstArraysTitle)),
            CreateLocalizableResourceString(nameof(AvoidConstArraysMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(AvoidConstArraysDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                INamedTypeSymbol? readonlySpanType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1);
                INamedTypeSymbol? functionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemFunc2);

                // Analyzes an argument operation
                context.RegisterOperationAction(context =>
                {
                    IArgumentOperation? argumentOperation;

                    if (context.Operation is IArrayCreationOperation arrayCreationOperation) // For arrays passed as arguments
                    {
                        argumentOperation = arrayCreationOperation.GetAncestor<IArgumentOperation>(OperationKind.Argument);

                        // If no argument, return
                        // If argument is passed as a params array but isn't itself an array, return
                        if (argumentOperation is null || (argumentOperation.Parameter.IsParams && arrayCreationOperation.IsImplicit))
                        {
                            return;
                        }
                    }
                    else if (context.Operation is IInvocationOperation invocationOperation) // For arrays passed in extension methods, like in LINQ
                    {
                        IEnumerable<IOperation> invocationDescendants = invocationOperation.Descendants();
                        if (invocationDescendants.Any(x => x is IArrayCreationOperation)
                            && invocationDescendants.Any(x => x is IArgumentOperation))
                        {
                            // This is an invocation that contains an array as an argument
                            // This will get caught by the first case in another cycle
                            return;
                        }

                        argumentOperation = invocationOperation.Arguments.FirstOrDefault();
                        if (argumentOperation is not null)
                        {
                            if (argumentOperation.Children.First() is not IConversionOperation conversionOperation
                                || conversionOperation.Operand is not IArrayCreationOperation arrayCreation)
                            {
                                return;
                            }

                            arrayCreationOperation = arrayCreation;
                        }
                        else // An invocation, extension or regular, has an argument, unless it's a VB extension method call
                        {
                            // For VB extension method invocations, find a matching child
                            arrayCreationOperation = (IArrayCreationOperation)invocationOperation.Descendants()
                                .FirstOrDefault(x => x is IArrayCreationOperation);
                            if (arrayCreationOperation is null)
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        return;
                    }

                    // Must be literal array
                    if (arrayCreationOperation.Initializer.ElementValues.Any(x => x is not ILiteralOperation))
                    {
                        return;
                    }

                    string? paramName = null;
                    if (argumentOperation is not null)
                    {
                        ITypeSymbol originalDefinition = argumentOperation.Parameter.Type.OriginalDefinition;

                        // Can't be a ReadOnlySpan, as those are already optimized
                        if (SymbolEqualityComparer.Default.Equals(readonlySpanType, originalDefinition))
                        {
                            return;
                        }

                        // Check if the parameter is a function so the name can be set to null
                        // Otherwise, the parameter name doesn't reflect the array creation as well
                        bool isDirectlyInsideLambda = originalDefinition.Equals(functionType);

                        // Parameter shouldn't have same containing type as the context, to prevent naming ambiguity
                        // Ignore parameter name if we're inside a lambda function
                        if (!isDirectlyInsideLambda && !argumentOperation.Parameter.ContainingType.Equals(context.ContainingSymbol.ContainingType))
                        {
                            paramName = argumentOperation.Parameter.Name;
                        }
                    }

                    Dictionary<string, string?> properties = new()
                    {
                        { "paramName", paramName }
                    };

                    context.ReportDiagnostic(arrayCreationOperation.CreateDiagnostic(Rule, properties.ToImmutableDictionary()));
                },
                OperationKind.ArrayCreation,
                OperationKind.Invocation);
            });
        }
    }
}