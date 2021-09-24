using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;


namespace StructGenerator
{
    partial class StructGen
    {
        sealed class SyntaxReceiver : ISyntaxReceiver
        {
            public HashSet<StructDeclarationSyntax> StructCandidates { get; } = new();
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is StructDeclarationSyntax structDeclarationSyntax
                    && structDeclarationSyntax.BaseList is not null
                    && structDeclarationSyntax.BaseList.Types.Count > 0)
                    StructCandidates.Add(structDeclarationSyntax);
            }
        }
    }
}
