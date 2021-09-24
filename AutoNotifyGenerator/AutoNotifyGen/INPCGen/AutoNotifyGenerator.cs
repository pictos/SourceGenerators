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

namespace INPCGen
{
    [Generator]
    public class AutoNotifyGenerator : ISourceGenerator
    {

        const string attributeText = @"
using System;
namespace AutoNotify
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    sealed class AutoNotifyAttribute : Attribute
    {
        public AutoNotifyAttribute()
        {
        }
        public string PropertyName { get; set; }
    }
}
";

        const string commandAttribute = @"
using System;
namespace AutoNotify
{
	[AttributeUsage(AttributeTargets.Method)]
	sealed class CommandAttribute : Attribute
    {
        public string CommandName { get; set; }

        public CommandAttribute()
        {
        }
    }
}
";
        const string autoNotifyName = "AutoNotify.AutoNotifyAttribute";
        const string notifyName = "System.ComponentModel.INotifyPropertyChanged";
        const string commandAttributeName = "AutoNotify.CommandAttribute";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization(i =>
            {
                i.AddSource("AutoNotifyAttribute.g.cs", attributeText);
                i.AddSource("CommandAttribute.g.cs", commandAttribute);
            });
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            //context.AddSource("AutoNotifyAttribute", attributeText);

            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;

            var options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;

            var compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(attributeText, Encoding.UTF8), options));

            var attributeSymbol = compilation.GetTypeByMetadataName(autoNotifyName);
            var commandattributeSymbol = compilation.GetTypeByMetadataName(commandAttributeName);
            var notifySymbol = compilation.GetTypeByMetadataName(notifyName);

            var fieldSymbols = GetFieldsCandidates(receiver, compilation, attributeSymbol);
            var methodSymbols = GetMethodsCandidates(receiver, compilation, commandattributeSymbol);
            var holder = new TypesHolder
            {
                Methods = new(methodSymbols),
                Fields = new(fieldSymbols)
            };
            var group = holder.Fields.Count > 0 ? holder.Fields[0].ContainingType : holder.Methods[0].ContainingType;
            var classSource = ProcessClass(holder, attributeSymbol, notifySymbol, commandattributeSymbol, context);

            //foreach (IGrouping<INamedTypeSymbol, TypesHolder> group in fieldSymbols.GroupBy(f => f.ContainingType))
            //{
            //    //var classSource = ProcessClass(group.Key, group.ToList(), attributeSymbol, notifySymbol, context);
            //}

            FormatText(ref classSource, options);

            context.AddSource($"{group.Name}_autoNotify.cs", classSource);

            static void FormatText(ref string classSource, CSharpParseOptions options)
            {
                var mysource = CSharpSyntaxTree.ParseText(SourceText.From(classSource, Encoding.UTF8), options);
                var formattedRoot = (CSharpSyntaxNode)mysource.GetRoot().NormalizeWhitespace();
                classSource = CSharpSyntaxTree.Create(formattedRoot).ToString();
            }
        }

        string ProcessClass(TypesHolder holder, ISymbol autoNotifyAttributeSymbol, ISymbol notifySymbol, ISymbol commandAttributeSymbol, GeneratorExecutionContext context)
        {
            var classSymbol = holder.Fields.Count > 0 ? holder.Fields[0].ContainingType : holder.Methods[0].ContainingType;

            if (!classSymbol.ContainingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default))
            {
                return null;
            }

            var nameSpaceName = classSymbol.ContainingNamespace.ToDisplayString();
            var source = new StringBuilder(@$"
using System.Windows.Input;
namespace {nameSpaceName}
{{
	public partial class {classSymbol.Name} : {notifySymbol.ToDisplayString()}
	{{
");
            if (!classSymbol.Interfaces.Contains(notifySymbol, SymbolEqualityComparer.Default))
                source.AppendLine("public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");

            GenerateCommands(holder.Methods, commandAttributeSymbol, source);
            GenerateProperties(holder.Fields, autoNotifyAttributeSymbol, source);


            source.Append("} }");

            return source.ToString();
        }

        static IEnumerable<IMethodSymbol> GetMethodsCandidates(SyntaxReceiver receiver, Compilation compilation, INamedTypeSymbol attributeSymbol)
        {
            foreach (var method in receiver.CandidateMethods)
            {
                var model = compilation.GetSemanticModel(method.SyntaxTree);
                var methodSymbol = model.GetDeclaredSymbol(method);

                if (!methodSymbol.ReturnsVoid)
                {
                    //add diagnostic
                    continue;
                }

                if (methodSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
                    yield return methodSymbol;
            }
        }

        static IEnumerable<IFieldSymbol> GetFieldsCandidates(SyntaxReceiver receiver, Compilation compilation, INamedTypeSymbol attributeSymbol)
        {
            foreach (var field in receiver.CandidateFields)
            {
                var model = compilation.GetSemanticModel(field.SyntaxTree);
                foreach (var variable in field.Declaration.Variables)
                {
                    var fieldSymbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;
                    var name = fieldSymbol.ToDisplayString();
                    if (fieldSymbol.GetAttributes().Any(a => a.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
                        yield return fieldSymbol;
                }
            }
        }

        void GenerateCommands(List<IMethodSymbol> methods, ISymbol attributeSymbol, StringBuilder source)
        {
            foreach (var methodSymbol in methods)
            {
                ProcessMethod(source, methodSymbol, attributeSymbol);
            }
        }

        void GenerateProperties(List<IFieldSymbol> fields, ISymbol attributeSymbol, StringBuilder source)
        {
            foreach (var fieldSymbol in fields)
                ProcessField(source, fieldSymbol, attributeSymbol);
        }

        void ProcessMethod(StringBuilder source, IMethodSymbol methodSymbol, ISymbol attributeSymbol)
        {
            var methodName = methodSymbol.Name;
            var attributeData = methodSymbol.GetAttributes().Single(a => a.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default));
            var overridenNameOpt = attributeData.NamedArguments.SingleOrDefault(kvp => kvp.Key == "CommandName").Value;

            var commandName = overridenNameOpt.IsNull ? methodName : overridenNameOpt.Value.ToString();
            commandName = ProcessPropertyName(commandName);

            if (string.IsNullOrEmpty(commandName))
                return;

            var fieldName = ProcessFieldName(commandName);
            source.AppendLine($@"
ICommand {fieldName};
public ICommand {commandName} => {fieldName} ??= new Command({methodName});");


            static string ProcessFieldName(string commandName) =>
                commandName.Substring(0, 1).ToLower() + commandName.Substring(1);

            static string ProcessPropertyName(string methodName)
            {
                return methodName.Replace("Execute", "");
            }
        }

        void ProcessField(StringBuilder source, IFieldSymbol fieldSymbol, ISymbol attributeSymbol)
        {
            var fieldName = fieldSymbol.Name;
            var fieldType = fieldSymbol.Type;

            var attributeData = fieldSymbol.GetAttributes().Single(a => a.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default));
            var overridenNameOpt = attributeData.NamedArguments.SingleOrDefault(kvp => kvp.Key == "PropertyName").Value;

            var propName = ChooseName(fieldName, overridenNameOpt);

            if (propName.Length == 0 || propName == fieldName)
            {
                return;
            }

            source.Append($@"
public {fieldType} {propName}
{{
	get
	{{
		return this.{fieldName};
	}}

	set
	{{
		if (this.{fieldName} == value)
			return;

			this.{fieldName} = value;
			this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof({propName})));
	}}
}}
");


            static string ChooseName(string fieldName, TypedConstant overridenNameOpt)
            {
                if (!overridenNameOpt.IsNull)
                    return overridenNameOpt.Value.ToString();

                fieldName = fieldName.TrimStart('_');

                if (fieldName.Length == 0)
                    return string.Empty;
                if (fieldName.Length == 1)
                    return fieldName.ToUpper();

                return fieldName.Substring(0, 1).ToUpper() + fieldName.Substring(1);
            }
        }

        string ProcessClass(INamedTypeSymbol classSymbol, List<IFieldSymbol> fields, ISymbol attributeSymbol, ISymbol notifySymbol, GeneratorExecutionContext context)
        {
            if (!classSymbol.ContainingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default))
            {
                return null;
            }

            var nameSpaceName = classSymbol.ContainingNamespace.ToDisplayString();

            var source = new StringBuilder(@$"
using System.Windows.Input;
namespace {nameSpaceName}
{{
	public partial class {classSymbol.Name} : {notifySymbol.ToDisplayString()}
	{{
");
            if (!classSymbol.Interfaces.Contains(notifySymbol, SymbolEqualityComparer.Default))
                source.Append("public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");

            GenerateProperties(fields, attributeSymbol, source);
            return source.ToString();
        }
    }

    class TypesHolder
    {
        public List<IMethodSymbol> Methods { get; init; } = new();

        public List<IFieldSymbol> Fields { get; init; } = new();
    }

    class SyntaxReceiver : ISyntaxReceiver
    {
        public HashSet<FieldDeclarationSyntax> CandidateFields { get; } = new();
        public HashSet<MethodDeclarationSyntax> CandidateMethods { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is FieldDeclarationSyntax fieldDeclarationSyntax
                && fieldDeclarationSyntax.AttributeLists.Count > 0)
                CandidateFields.Add(fieldDeclarationSyntax);

            if (syntaxNode is MethodDeclarationSyntax methodDeclarationSyntax
                && methodDeclarationSyntax.AttributeLists.Count > 0)
                CandidateMethods.Add(methodDeclarationSyntax);
        }
    }
}
