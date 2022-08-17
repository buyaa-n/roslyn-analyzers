﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpUseCuriouslyRecurringTemplatePatternCorrectly : UseCuriouslyRecurringTemplatePatternCorrectly
    {
        protected override SyntaxNode? FindTheTypeArgumentOfTheInterfaceFromTypeDeclaration(ISymbol typeSymbol, ISymbol anInterfaceSymbol, int argumentLocation)
        {
            foreach (SyntaxReference syntaxReference in typeSymbol.DeclaringSyntaxReferences)
            {
                SyntaxNode typeDefinition = syntaxReference.GetSyntax();
                if (typeDefinition is ClassDeclarationSyntax classDeclaration)
                {
                    return FindTypeArgumentFromBaseInterfaceList(classDeclaration.BaseList.Types, anInterfaceSymbol, argumentLocation);
                }
                else if (typeDefinition is StructDeclarationSyntax structDeclaration)
                {
                    return FindTypeArgumentFromBaseInterfaceList(structDeclaration.BaseList.Types, anInterfaceSymbol, argumentLocation);
                }
            }

            return null;
        }

        private static SyntaxNode? FindTypeArgumentFromBaseInterfaceList(SeparatedSyntaxList<BaseTypeSyntax> baseListTypes, ISymbol anInterfaceSymbol, int argumentLocation)
        {
            foreach (BaseTypeSyntax baseType in baseListTypes)
            {
                if (baseType is SimpleBaseTypeSyntax simpleBaseType &&
                    simpleBaseType.Type is GenericNameSyntax genericName &&
                    genericName.Identifier.ValueText == anInterfaceSymbol.Name)
                {
                    return genericName.TypeArgumentList.Arguments[argumentLocation];
                }
            }

            return null;
        }
    }
}
