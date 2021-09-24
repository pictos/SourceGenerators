using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace AutoMapperGenerator
{
    [Generator]
    public class MapperGenerator : ISourceGenerator
    {
        const string MapperClassAttribute = "AutoMapperGenerator.MapperToAttribute";
        const string MapperPropertyAttribute = "AutoMapperGenerator.PropertyNameAttribute";
        const string MapperAttribute = @"
using System;

namespace AutoMapperGenerator
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class MapperToAttribute : System.Attribute
    {
        public Type MapperTo { get; }
        public MapperToAttribute(Type type)
        {
            MapperTo = type;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class PropertyNameAttribute : System.Attribute
    {
        public string PropertyName { get; }
        public Type MapperTo { get; }
        public PropertyNameAttribute(string name, Type type)
        {
            PropertyName = name;
            MapperTo = type;
        }
    }
}";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization
                (i => i.AddSource("MapperAttributes.g.cs", SourceText.From(MapperAttribute, Encoding.UTF8)));
            context.RegisterForSyntaxNotifications(() => new MapperGeneratorSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not MapperGeneratorSyntaxReceiver receiver)
                return;

            var compilation = context.Compilation;
            var options = (compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            var mapperClassAttributeSymbol = compilation.GetTypeByMetadataName(MapperClassAttribute);
            var mapperPropertyAttributeSymbol = compilation.GetTypeByMetadataName(MapperPropertyAttribute);

            var classes = GetClassesCandidates(receiver, mapperClassAttributeSymbol, compilation).ToList();

            var source = ProcessClass(classes, mapperPropertyAttributeSymbol, mapperClassAttributeSymbol, compilation);

            FormatText(ref source, options);

            context.AddSource("MapperClass.g.cs", source);

            static void FormatText(ref string classSource, CSharpParseOptions options)
            {
                var mysource = CSharpSyntaxTree.ParseText(SourceText.From(classSource, Encoding.UTF8), options);
                var formattedRoot = (CSharpSyntaxNode)mysource.GetRoot().NormalizeWhitespace();
                classSource = CSharpSyntaxTree.Create(formattedRoot).ToString();
            }
        }

        string ProcessClass(List<(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol)> classes, INamedTypeSymbol mapperPropertyAttributeSymbol, INamedTypeSymbol mapperClassAttributeSymbol, Compilation compilation)
        {
            var sb = new StringBuilder();
            sb.Append(@"
namespace AutoMapperGenerator
{
    public static class MapperClass
    {
");
            foreach (var methodToGenerate in classes)
            {
                GenerateMethod(methodToGenerate, compilation, mapperPropertyAttributeSymbol, mapperClassAttributeSymbol, sb);
            }
            sb.Append("}  }");

            return sb.ToString();
        }

        void GenerateMethod((ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol) methodToGenerate, Compilation compilation, INamedTypeSymbol mapperPropertyAttributeSymbol, INamedTypeSymbol mapperClassAttributeSymbol, StringBuilder sb)
        {
            var fromType = methodToGenerate.classSymbol.OriginalDefinition.ToDisplayString();
            var attributeData = methodToGenerate.classSymbol.GetAttributes().Where(x => x.AttributeClass.Equals(mapperClassAttributeSymbol, SymbolEqualityComparer.Default));

            foreach (var attribute in attributeData)
            {
                var toType = attribute.ConstructorArguments[0].Value;
                var toTypeSymbol = compilation.GetTypeByMetadataName(toType.ToString());
                //public static PessoaDto Mapper(Pessoa from)
                sb.Append(@$"public static {toType} MapperTo{toTypeSymbol.Name}({fromType} from) =>")
                    //return new PessoaDto
                    //{
                    //    PessoaNome = from.Nome,
                    //    Idade = from.Idade
                    //};
                    .Append($@" new {toType} {{");
                AssingProperties(toTypeSymbol, methodToGenerate.classSymbol, sb, mapperPropertyAttributeSymbol, compilation);
                sb.Append("};");
            }
            var z = sb.ToString();
        }

        void AssingProperties(INamedTypeSymbol toTypeSymbol, INamedTypeSymbol fromTypeSymbol, StringBuilder sb, INamedTypeSymbol mapperPropertyAttributeSymbol, Compilation compilation)
        {
            var toTypeMembers = toTypeSymbol.GetMembers().Where(x => x.Kind == SymbolKind.Property).ToList();
            var fromTypeMembers = fromTypeSymbol.GetMembers().Where(x => x.Kind == SymbolKind.Property).ToList();

            foreach (var member in fromTypeMembers)
            {
                var key = member.Name;


                var attributeData = member.GetAttributes().Where(x => x.AttributeClass.Equals(mapperPropertyAttributeSymbol, SymbolEqualityComparer.Default));

                //foreach (var item in attributeData)
                //{
                //    var x = item.ConstructorArguments;
                //    var y = x[1];
                //    var v = compilation.GetTypeByMetadataName(y.Value.ToString());

                //    var isSameType = toTypeSymbol.ToDisplayString() == v.ToDisplayString();
                //}

                string value;
                var myType = attributeData?.SingleOrDefault(x => x.ConstructorArguments[1].Value.ToString() == toTypeSymbol.ToDisplayString());

                if (myType?.ConstructorArguments[0].IsNull ?? true)
                    value = toTypeMembers.FirstOrDefault(x => x.Name == key).Name;
                else
                    value = myType.ConstructorArguments[0].Value.ToString();
                sb.Append($"{value} = from.{key},");
            }
        }

        IEnumerable<(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol)> GetClassesCandidates(MapperGeneratorSyntaxReceiver receiver, INamedTypeSymbol mapperClassAttributeSymbol, Compilation compilation)
        {
            foreach (var classDeclaration in receiver.ClassesCandidates)
            {
                var model = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = model.GetDeclaredSymbol(classDeclaration);

                if (classSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(mapperClassAttributeSymbol, SymbolEqualityComparer.Default)))
                    yield return (classDeclaration, classSymbol);
            }
        }
    }
}
