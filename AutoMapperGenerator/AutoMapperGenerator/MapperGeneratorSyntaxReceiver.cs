using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace AutoMapperGenerator
{
    sealed class MapperGeneratorSyntaxReceiver : ISyntaxReceiver
    {
        public HashSet<ClassDeclarationSyntax> ClassesCandidates { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax &&
                classDeclarationSyntax.AttributeLists.Count > 0)
                ClassesCandidates.Add(classDeclarationSyntax);
        }
    }
}
