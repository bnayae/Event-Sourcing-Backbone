﻿using System.Collections.Immutable;
using System.Text;
using EventSourcing.Backbone.SrcGen.Entities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static EventSourcing.Backbone.Helper;

namespace EventSourcing.Backbone
{

    internal abstract class GeneratorIncrementalBase : IIncrementalGenerator
    {
        private const string ATTRIBUTE_SUFFIX = "Attribute";
        private static readonly string[] DEFAULT_USING = new[]{
                            "System",
                            "System.Linq",
                            "System.Collections",
                            "System.Collections.Generic",
                            "System.Threading.Tasks",
                            "System.CodeDom.Compiler",
                            "EventSourcing.Backbone",
                            "EventSourcing.Backbone.Building" }.Select(u => $"using {u};").ToArray();
        private readonly ImmutableHashSet<string> _targetAttribute = ImmutableHashSet<string>.Empty;

        #region Ctor

        protected GeneratorIncrementalBase(
            string targetAttribute)
        {
            _targetAttribute = _targetAttribute.Add(targetAttribute);
            if (targetAttribute.EndsWith(ATTRIBUTE_SUFFIX))
                _targetAttribute = _targetAttribute.Add(targetAttribute.Substring(0, targetAttribute.Length - ATTRIBUTE_SUFFIX.Length));
            else
                _targetAttribute = _targetAttribute.Add($"{targetAttribute}{ATTRIBUTE_SUFFIX}");
        }

        #endregion // Ctor

        #region Initialize

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<SyntaxReceiverResult[]> classDeclarations =
                    context.SyntaxProvider
                        .CreateSyntaxProvider(
                            predicate: (node, cancellationToken) => ShouldTriggerGeneration(node, cancellationToken),
                            transform: (ctx, cancellationToken) => ToGenerationInput(ctx, cancellationToken).ToArray())
                        .Where(static m => m is not null);

            IncrementalValueProvider<(Compilation, ImmutableArray<SyntaxReceiverResult[]>)> compilationAndClasses
                = context.CompilationProvider.Combine(classDeclarations.Collect());

            // register a code generator for the triggers
            context.RegisterSourceOutput(compilationAndClasses, Generate);

            #region ShouldTriggerGeneration

            /// <summary>
            /// Indicate whether the node should trigger a source generation />
            /// </summary>
            bool ShouldTriggerGeneration(SyntaxNode node, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (node is not TypeDeclarationSyntax t) return false;

                if (node is not TypeDeclarationSyntax tds ||
                    tds.Kind() != SyntaxKind.InterfaceDeclaration)
                {
                    return false;
                }

                bool hasAttributes = t.AttributeLists.Any(m => m.Attributes.Any(m1 =>
                        AttributePredicate(m1, _targetAttribute)));

                return hasAttributes;
            }

            #endregion // ShouldTriggerGeneration        }

            #region ToGenerationInput

            /// <summary>
            /// Called for each <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> in the compilation
            /// </summary>
            /// <param name="syntaxNode">The current <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> being visited</param>
            IEnumerable<SyntaxReceiverResult> ToGenerationInput(GeneratorSyntaxContext ctx, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SyntaxNode syntaxNode = ctx.Node;
                if (syntaxNode is not TypeDeclarationSyntax tds)
                    throw new InvalidCastException("Expecting TypeDeclarationSyntax");

                var atts = from al in tds.AttributeLists
                           from a in al.Attributes
                           let n = a.Name.ToString()
                           where _targetAttribute.Contains(n)
                           select a;
                foreach (AttributeSyntax att in atts)
                {
                    var nameArg = att.ArgumentList?.Arguments.FirstOrDefault(m => m.NameEquals?.Name.Identifier.ValueText == "Name");
                    var name = nameArg?.Expression.NormalizeWhitespace().ToString().Replace("\"", "");

                    var nsArg = att.ArgumentList?.Arguments.FirstOrDefault(m => m.NameEquals?.Name.Identifier.ValueText == "Namespace");
                    var ns = nsArg?.Expression.NormalizeWhitespace().ToString().Replace("\"", "");

                    var ctorArg = att.ArgumentList?.Arguments.First().GetText().ToString();
                    string kind = ctorArg?.Substring("EventSourceGenType.".Length) ?? "NONE";

                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(tds);

                    var result = new SyntaxReceiverResult(tds, symbol, name, ns, kind, att);
                    yield return result;
                }
            }

            #endregion // ToGenerationInput
        }

        #endregion // Initialize

        #region Generate

        /// <summary>
        /// Source generates loop.
        /// </summary>
        /// <param name="context">The SPC.</param>
        /// <param name="source">The source.</param>
        private void Generate(
            SourceProductionContext context,
            (Compilation compilation, ImmutableArray<SyntaxReceiverResult[]> items) source)
        {
            var (compilation, items) = source;
            var flatten = items.SelectMany(m => m).ToArray();
            foreach (SyntaxReceiverResult item in flatten)
            {
                GenerateSingle(context, compilation, item);
            }
        }

        #endregion // Generate

        #region GenerateSingle

        public void GenerateSingle(
                            SourceProductionContext context,
                            Compilation compilation,
                            SyntaxReceiverResult info)
        {
            var (typeDeclaration, symbol, kind, ns, usingStatements) = info;

            #region Validation

            if (kind == "NONE")
            {
                context.AddSource($"ERROR.cs", $"// Invalid source input: kind = [{kind}], {typeDeclaration}");
                return;
            }

            #endregion // Validation

            GenInstruction[] codes = OnGenerate(context, compilation, info, usingStatements);

            foreach (var (fileName, content, dynamicNs, usn) in codes)
            {
                var builder = new StringBuilder();
                var overrideNS = dynamicNs ?? ns ?? symbol.ContainingNamespace.ToDisplayString() ?? "EventSourcing.Backbone";
                var usingSet = new HashSet<string>(DEFAULT_USING);
                builder.AppendLine();
                builder.AppendLine("#nullable enable");
                builder.AppendLine("#pragma warning disable CS1573 // Parameter 'parameter' has no matching param tag in the XML comment for 'parameter' (but other parameters do)");
                builder.AppendLine("#pragma warning disable CS1529 // duplicate using");
                builder.AppendLine("#pragma warning disable IDE0005 // Remove unnecessary using directives");
                builder.AppendLine("#pragma warning disable CS0105 // The using directive for 'namespace' appeared previously in this namespace.");

                foreach (var u in usingStatements.Concat(usn))
                {
                    if (!usingSet.Contains(u))
                        usingSet.Add(u);
                }

                foreach (var u in usingSet.OrderBy(m => m))
                {
                    builder.AppendLine(u);
                }
                builder.AppendLine($"namespace {overrideNS}");
                builder.AppendLine("{");

                var fileScope = typeDeclaration.Parent! as FileScopedNamespaceDeclarationSyntax;
                if (fileScope != null)
                {
                    foreach (var u in fileScope.Usings)
                    {
                        builder.AppendLine($"\t{u}");
                    }
                    builder.AppendLine();
                }

                builder.AppendLine(content);
                builder.AppendLine("}");

                context.AddSource($"{fileName}.{kind}.cs", builder.ToString());
            }
        }

        #endregion // GenerateSingle

        #region OnGenerate

        /// <summary>
        /// Called when [execute].
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="compilation">The compilation.</param>
        /// <param name="info">The information.</param>
        /// <param name="usingStatements">The using statements.</param>
        /// <returns>
        /// File name
        /// </returns>
        protected abstract GenInstruction[] OnGenerate(
                            SourceProductionContext context,
                            Compilation compilation,
                            SyntaxReceiverResult info,
                            string[] usingStatements);

        #endregion // OnGenerate

        #region AttributePredicate

        /// <summary>
        /// The predicate whether match to the target attribute
        /// </summary>
        /// <param name="syntax">The syntax.</param>
        /// <param name="targetAttribute">The target attribute.</param>
        /// <param name="kindFilter">The kind filter.</param>
        /// <returns></returns>
        private static bool AttributePredicate(AttributeSyntax syntax, ImmutableHashSet<string> targetAttribute)
        {
            string candidate = syntax.Name.ToString();
            int len = candidate.LastIndexOf(".");
            if (len != -1)
                candidate = candidate.Substring(len + 1);

            return targetAttribute.Contains(candidate);
        }

        #endregion // AttributePredicate
    }
}
