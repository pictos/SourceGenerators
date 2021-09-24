using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace StructGenerator
{
    [Generator]
    partial class StructGen : ISourceGenerator
    {
        const string IEquatableSymbol = "System.IEquatable`1";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;

            var compilation = context.Compilation;
            var structs = ProcessStrucCandidates(compilation, receiver).ToList();
            var x = ProcessTypleDeclarationCandidates<StructDeclarationSyntax>(compilation, receiver.StructCandidates).ToList();
            var options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;

            foreach (var @struct in structs)
            {
                var source = ProccessStruct(@struct, compilation);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    FormatText(ref source, options);
                    context.AddSource($"{@struct.structSymbol.Name}.g.cs", source); 
                }
            }


            static void FormatText(ref string classSource, CSharpParseOptions options)
            {
                var mysource = CSharpSyntaxTree.ParseText(SourceText.From(classSource, Encoding.UTF8), options);
                var formattedRoot = (CSharpSyntaxNode)mysource.GetRoot().NormalizeWhitespace();
                classSource = CSharpSyntaxTree.Create(formattedRoot).ToString();
            }
        }

        string ProccessStruct((StructDeclarationSyntax structDeclaration, ITypeSymbol structSymbol) @struct, Compilation compilation)
        {
            var (structDeclaration, structSymbol) = @struct;
            var namespaceName = structSymbol.ContainingNamespace.ToDisplayString();
            var sb = new StringBuilder(@$"
///<generated>
namespace {namespaceName}
{{
    partial struct {structSymbol.Name}
    {{
");

            var props = GetProperties(structDeclaration);
            var implementedMethods = GetImplementedMethods(structDeclaration, structSymbol, compilation);

            if (props.Count == 0)
            {
                //TODO reportar error 
                return null;
            }

            if (implementedMethods[MethodsToGenerate.Equals])
            {
                sb.Append($"\t public bool Equals({structSymbol.Name} other) => ");

                GenerateEquals(sb, props, null);
                GenerateEquals(sb, props, "other.");
            }
            if (implementedMethods[MethodsToGenerate.OverrideHashCode])
            {
                sb.Append("public override int GetHashCode() => ");
                GenerateHashCode(sb, props);
            }
            if (implementedMethods[MethodsToGenerate.OverrideEquals])
            {
                sb.Append($"public override bool Equals(object obj) => ");
                sb.AppendLine($"(obj is {structSymbol.Name} {structSymbol.Name.ToLower()}) && Equals({structSymbol.Name.ToLower()});");
            }

            if (implementedMethods[MethodsToGenerate.EqualOperator])
                GenerateOperator(sb, structSymbol.Name, "==");
            if (implementedMethods[MethodsToGenerate.NotEqualOperator])
                GenerateOperator(sb, structSymbol.Name, "!=");

            sb.AppendLine("}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        void GenerateOperator(StringBuilder sb, in string structName, in string operatorSymbol) =>
            sb.AppendLine($"public static bool operator {operatorSymbol}({structName} left, {structName} right) => left.Equals(right);");

        void GenerateHashCode(StringBuilder sb, List<string> props)
        {
            sb.Append('(');
            for (int i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                sb.Append(prop);

                if (i < props.Count - 1)
                    sb.Append(',');
                else
                    sb.Append(')');
            }
            sb.AppendLine(".GetHashCode();");
        }

        void GenerateEquals(StringBuilder sb, List<string> props, string prefix)
        {
            var hasPrefix = !string.IsNullOrWhiteSpace(prefix);
            if (!hasPrefix)
                sb.Append('(');

            for (int i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                sb.Append($"{prefix}{prop}");
                if (i < props.Count - 1)
                    sb.Append(',');
                else
                    sb.Append(')');
            }

            if (!hasPrefix)
                sb.Append(" == (");
            else
                sb.AppendLine(";");
        }

        enum MethodsToGenerate
        {
            Equals,
            OverrideEquals,
            OverrideHashCode,
            EqualOperator,
            NotEqualOperator
        }

        List<string> GetProperties(StructDeclarationSyntax structDeclaration) =>
            structDeclaration.DescendantNodesAndSelf().OfType<PropertyDeclarationSyntax>().Select(x => x.Identifier.ValueText).ToList();

        Dictionary<MethodsToGenerate, bool> GetImplementedMethods(StructDeclarationSyntax structDeclaration, ITypeSymbol structSymbol, Compilation compilation)
        {
            var dic = new Dictionary<MethodsToGenerate, bool>(5)
            {
                [MethodsToGenerate.Equals] = true,
                [MethodsToGenerate.OverrideEquals] = true,
                [MethodsToGenerate.OverrideHashCode] = true,
                [MethodsToGenerate.EqualOperator] = true,
                [MethodsToGenerate.NotEqualOperator] = true
            };

            var descendantNodesAndSelf = structDeclaration.DescendantNodesAndSelf();
            var methods = descendantNodesAndSelf.OfType<MethodDeclarationSyntax>().ToList();
            var operators = descendantNodesAndSelf.OfType<OperatorDeclarationSyntax>().ToList();
            var model = compilation.GetSemanticModel(structDeclaration.SyntaxTree);

            foreach (var method in methods)
            {
                var methodSymbol = model.GetDeclaredSymbol(method);
                var isInterfaceImplementation = IsInterfaceImplementation(methodSymbol);

                if (isInterfaceImplementation && methodSymbol.Name.Equals("Equals"))
                {
                    dic[MethodsToGenerate.Equals] = false;
                }
                else if (!isInterfaceImplementation && methodSymbol.IsOverride && methodSymbol.Name.Equals("GetHashCode"))
                {
                    dic[MethodsToGenerate.OverrideHashCode] = false;
                }
                else if (!isInterfaceImplementation && methodSymbol.IsOverride && methodSymbol.Name.Equals("Equals"))
                {
                    dic[MethodsToGenerate.OverrideEquals] = false;
                }

            }

            foreach (var @operator in operators)
            {
                var operatorSymbol = model.GetDeclaredSymbol(@operator);
                var isFromStruct = operatorSymbol.Parameters.Any(x => x.Type.Equals(structSymbol, SymbolEqualityComparer.Default));

                if (isFromStruct && operatorSymbol.Name.Equals("op_Equality"))
                {
                    dic[MethodsToGenerate.EqualOperator] = false;
                }
                else if (isFromStruct && operatorSymbol.Name.Equals("op_Inequality"))
                {
                    dic[MethodsToGenerate.NotEqualOperator] = false;
                }
            }

            return dic;
        }

        IEnumerable<(StructDeclarationSyntax structDeclaration, ITypeSymbol structSymbol)> ProcessStrucCandidates(Compilation compilation, SyntaxReceiver receiver)
        {
            var iEquitableSymbol = compilation.GetTypeByMetadataName(IEquatableSymbol);

            foreach (var structDeclaration in receiver.StructCandidates)
            {
                var model = compilation.GetSemanticModel(structDeclaration.SyntaxTree);
                var structSymbol = model.GetDeclaredSymbol(structDeclaration) as ITypeSymbol;
                if (structSymbol.AllInterfaces.Any(i => i.OriginalDefinition.Equals(iEquitableSymbol, SymbolEqualityComparer.Default)))
                    yield return (structDeclaration, structSymbol);
            }
        }

        IEnumerable<(TDeclarationSyntax, ITypeSymbol)> ProcessTypleDeclarationCandidates<TDeclarationSyntax>(Compilation compilation, IEnumerable<TDeclarationSyntax> candidates)
            where TDeclarationSyntax : TypeDeclarationSyntax
        {
            var iEquitableSymbol = compilation.GetTypeByMetadataName(IEquatableSymbol);
            foreach (var structDeclaration in candidates)
            {
                var model = compilation.GetSemanticModel(structDeclaration.SyntaxTree);
                var symbol = GetSymbol<ITypeSymbol>(compilation, structDeclaration);

                if(symbol.AllInterfaces.Any( i => i.OriginalDefinition.Equals(iEquitableSymbol, SymbolEqualityComparer.Default)))
                    yield return (structDeclaration, symbol);
            }
        }


        TSymbol GetSymbol<TSymbol>(Compilation compilation, BaseTypeDeclarationSyntax declarationSyntax)
            where TSymbol : ISymbol
        {
            var model = compilation.GetSemanticModel(declarationSyntax.SyntaxTree);
            return (TSymbol)model.GetDeclaredSymbol(declarationSyntax);
        }

        static bool IsInterfaceImplementation(IMethodSymbol method)
        {
            return method.ContainingType.AllInterfaces.SelectMany(@interface => @interface
            .GetMembers()
            .OfType<IMethodSymbol>())
                .Any(interfaceMethod =>
                method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod)
                .Equals(method, SymbolEqualityComparer.Default));
        }
    }
}
