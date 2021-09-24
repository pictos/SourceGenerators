using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JsonSSG
{

    [Generator]
    public class SerializerGenerator : ISourceGenerator
    {
        const string ClassSerializeAttribute = @"
using System;

namespace JsonSerializeGenerator
{
    [AttributeUsage(AttributeTargets.Class)]
    sealed class SerializeAttribute : Attribute
    {
        public SerializeAttribute()
        {

        }
    }
}";
        const string jsonSerializerName = "JsonSerializeGenerator.SerializeAttribute";
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization(i => i.AddSource("SerializeAttribute.g.cs", SourceText.From(ClassSerializeAttribute, Encoding.UTF8)));
            context.RegisterForSyntaxNotifications(() => new MySyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not MySyntaxReceiver receiver)
                return;

            var compilation = context.Compilation;
            var jsonAttributeSymbol = compilation.GetTypeByMetadataName(jsonSerializerName);
            var classes = GetClassesCandidates(receiver, compilation, jsonAttributeSymbol);
            var options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            var source = ProcessClasses(classes, compilation);

            FormatText(ref source, options);

            context.AddSource("GeneratedSerialize.g.cs", source);

            static void FormatText(ref string classSource, CSharpParseOptions options)
            {
                var mysource = CSharpSyntaxTree.ParseText(SourceText.From(classSource, Encoding.UTF8), options);
                var formattedRoot = (CSharpSyntaxNode)mysource.GetRoot().NormalizeWhitespace();
                classSource = CSharpSyntaxTree.Create(formattedRoot).ToString();
            }
        }

        string ProcessClasses(IEnumerable<ClassDeclarationSyntax> classesCandidates, Compilation compilation)
        {
            var sb = new StringBuilder($@"
using System;
namespace JsonSerializeGenerator
{{
    public static class GeneratedSerializer
    {{
");
            foreach (var item in classesCandidates)
            {
                ProcessClass(item, sb, compilation);
            }


            sb.Append($@"
        }}
    }}");
            return sb.ToString();
        }

        void ProcessClass(ClassDeclarationSyntax classDeclaration, StringBuilder sb, Compilation compilation)
        {
            var model = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            var classSymbol = model.GetDeclaredSymbol(classDeclaration) as ITypeSymbol;
            var nameSpace = classSymbol.ContainingNamespace.ToDisplayString();
            
            sb.Append($@"
            public static string Serialize({nameSpace}.{classDeclaration.Identifier.Text} input)
            {{").Append("return $\" {{ ");

            var propertyDeclarations = classDeclaration.Members.OfType<PropertyDeclarationSyntax>().ToList();

            var stringSymbol = compilation.GetTypeByMetadataName("System.String");
            ProcessProperties(propertyDeclarations, model, sb, stringSymbol);
        }

        void ProcessProperties(List<PropertyDeclarationSyntax> propertyDeclarations, SemanticModel model, StringBuilder sb, INamedTypeSymbol stringSymbol)
        {
            var count = propertyDeclarations.Count;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                
                var propDeclaration = propertyDeclarations[i];
                var propSymbol = model.GetDeclaredSymbol(propDeclaration);

                if (propSymbol.Type.Equals(stringSymbol, SymbolEqualityComparer.Default))
                    sb.Append($"\\\"{propDeclaration.Identifier.Text}\\\" : \\\"{{input.{propDeclaration.Identifier.Text}}}\\\"");
                else
                    sb.Append($"\\\"{propDeclaration.Identifier.Text}\\\" : {{input.{propDeclaration.Identifier.Text}}}");
            }

            sb.Append(" }}\";");
            sb.Append($" }}");
        }

        IEnumerable<ClassDeclarationSyntax> GetClassesCandidates(MySyntaxReceiver receiver, Compilation compilation, INamedTypeSymbol jsonAttributeSymbol)
        {
            foreach (var @class in receiver.CandidateClasses)
            {
                var model = compilation.GetSemanticModel(@class.SyntaxTree);
                var classSymbol = model.GetDeclaredSymbol(@class) as ITypeSymbol;
                if (classSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(jsonAttributeSymbol, SymbolEqualityComparer.Default)))
                    yield return @class;
            }
        }

        sealed class MySyntaxReceiver : ISyntaxReceiver
        {
            public HashSet<ClassDeclarationSyntax> CandidateClasses { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax
                    && classDeclarationSyntax.AttributeLists.Count > 0)
                    CandidateClasses.Add(classDeclarationSyntax);
            }
        }

    }
}
