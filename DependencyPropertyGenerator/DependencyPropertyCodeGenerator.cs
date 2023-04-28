using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DependencyPropertyGenerator
{
    [Generator]
    public class DependencyPropertyCodeGenerator : ISourceGenerator
    {
        private const string attributeNamespace = "DependencyPropertyGenerator";
        private const string attributeClass = "DependencyPropertyAttribute";
        private const string attributeText = @"
using System;

namespace DependencyPropertyGenerator
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class DependencyPropertyAttribute<T> : Attribute
    {
        public DependencyPropertyAttribute(string name, string defaultCode, bool isNullable = false)
        {
        }
    }
}";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization((i) =>
            {
                i.AddSource($"{attributeClass}.g.cs", attributeText);
            });

            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is SyntaxReceiver receiver)
            {
                foreach (ITypeSymbol item in receiver.Classes)
                {
                    context.AddSource($"{item.ContainingNamespace.ToDisplayString()}_{item.Name}.DependencyProperty.g.cs", ProcessClass(item));
                }
            }
        }

        private string ProcessClass(ITypeSymbol classSymbol)
        {
            string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            bool isPartial = false;
            foreach (SyntaxNode item in classSymbol.DeclaringSyntaxReferences.Select(r => r.GetSyntax()))
            {
                if (item is ClassDeclarationSyntax syntax && syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    isPartial = true;

                    break;
                }
            }

            if (!isPartial)
            {
                throw new Exception($"{classSymbol} must be set to Partial.");
            }

            bool isDependencyObject = false;
            string namespaceDependencyObject = string.Empty;

            INamedTypeSymbol baseType = classSymbol.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == "DependencyObject")
                {
                    namespaceDependencyObject = baseType.ContainingNamespace.ToDisplayString();

                    if (namespaceDependencyObject == "System.Windows" || namespaceDependencyObject == "Microsoft.UI.Xaml")
                    {
                        isDependencyObject = true;

                        break;
                    }
                }
                baseType = baseType.BaseType;
            }

            if (!isDependencyObject)
            {
                throw new Exception($"{classSymbol} must be a derived class of DependencyObject.");
            }

            StringBuilder source = new StringBuilder();
            source.AppendLine($@"namespace {namespaceName}");
            source.AppendLine($@"{{");
            source.AppendLine($@"   using {namespaceDependencyObject};");
            source.AppendLine($@"   public partial class {classSymbol.Name}");
            source.AppendLine($@"   {{");

            List<AttributeData> attributes = classSymbol.GetAttributes().Where(item => item.AttributeClass.ToDisplayString().Contains($"{attributeNamespace}.{attributeClass}")).ToList();
            foreach (AttributeData item in attributes)
            {
                ITypeSymbol typeSymbol = item.AttributeClass.TypeArguments.Single();
                TypedConstant nameConstant = item.ConstructorArguments[0];
                TypedConstant defaultCodeConstant = item.ConstructorArguments[1];
                TypedConstant isNullableConstant = item.ConstructorArguments[2];

                ProcessProperty(source, classSymbol, typeSymbol, (string)nameConstant.Value, (string)defaultCodeConstant.Value, (bool)isNullableConstant.Value);

                if (item != attributes.Last())
                {
                    source.AppendLine();
                }
            }

            source.AppendLine($@"   }}");
            source.AppendLine($@"}}");

            return source.ToString();
        }

        private void ProcessProperty(StringBuilder source, ITypeSymbol classSymbol, ITypeSymbol typeSymbol, string name, string defaultCode, bool isNullable)
        {
            string type = typeSymbol.ToString();
            if (isNullable)
            {
                type += "?";
            }

            source.AppendLine($@"       public {type} {name}");
            source.AppendLine($@"       {{");
            source.AppendLine($@"           get {{ return ({type})GetValue({name}Property); }}");
            source.AppendLine($@"           set {{ SetValue({name}Property, value); }}");
            source.AppendLine($@"       }}");

            source.AppendLine();

            source.AppendLine($@"       public static readonly DependencyProperty {name}Property = DependencyProperty.Register(nameof({name}), typeof({type}), typeof({classSymbol}), new PropertyMetadata({defaultCode}, (a, b) =>");
            source.AppendLine($@"       {{");
            source.AppendLine($@"           (({classSymbol})a).On{name}Changed(({type})b.OldValue, ({type})b.NewValue);");
            source.AppendLine($@"       }}));");

            source.AppendLine();

            source.AppendLine($@"       partial void On{name}Changed({type} oldValue, {type} newValue);");
        }

        class SyntaxReceiver : ISyntaxContextReceiver
        {
            public List<ITypeSymbol> Classes { get; } = new List<ITypeSymbol>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is ClassDeclarationSyntax classDeclarationSyntax && classDeclarationSyntax.AttributeLists.Count > 0)
                {
                    ITypeSymbol typeSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) as ITypeSymbol;
                    if (typeSymbol.GetAttributes().Any(ad => ad.AttributeClass.ToDisplayString().Contains($"{attributeNamespace}.{attributeClass}")))
                    {
                        Classes.Add(typeSymbol);
                    }
                }
            }
        }
    }
}
