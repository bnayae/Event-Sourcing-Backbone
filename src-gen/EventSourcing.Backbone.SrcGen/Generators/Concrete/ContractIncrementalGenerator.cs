using System.Text;

using EventSourcing.Backbone.SrcGen.Entities;
using EventSourcing.Backbone.SrcGen.Generators.Entities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static EventSourcing.Backbone.Helper;

namespace EventSourcing.Backbone;

[Generator]
internal class ContractIncrementalGenerator : GeneratorIncrementalBase
{
    private const string TARGET_ATTRIBUTE = "EventsContract";
    private readonly BridgeIncrementalGenerator _bridge = new();

    public ContractIncrementalGenerator() : base(TARGET_ATTRIBUTE)
    {

    }

    /// <summary>
    /// Called when [execute].
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="compilation">The compilation.</param>
    /// <param name="info">The information.</param>
    /// <param name="usingStatements">The using statements.</param>
    /// <param name="versionOfSubInterface">The version of sub interface.</param>
    /// <returns>
    /// File name
    /// </returns>
    protected override GenInstruction[] OnGenerate(
                        SourceProductionContext context,
                        Compilation compilation,
                        SyntaxReceiverResult info,
                        string[] usingStatements,
                        int? versionOfSubInterface = null)
    {
#pragma warning disable S1481 // Unused local variables should be removed
        var (type, att, symbol, kind, ns, isProducer, @using) = info;
#pragma warning restore S1481 // Unused local variables should be removed
        string interfaceName = info.FormatName();
        var versionInfo = att.GetVersionInfo(compilation, kind);

        var builder = new StringBuilder();
        CopyDocumentation(builder, kind, type, "\t");
        var asm = GetType().Assembly.GetName();
        string interfaceNameWithVersion = versionOfSubInterface == null
                    ? string.Empty
                    : versionInfo.FormatNameWithVersion(interfaceName, versionOfSubInterface ?? -1);
        builder.AppendLine($"\t[GeneratedCode(\"{asm.Name}\",\"{asm.Version}\")]");
        builder.Append($"\tpublic interface {interfaceNameWithVersion}");
        var baseTypes = symbol.Interfaces.Select(m => info.FormatName(m.Name));
        string inheritance = string.Join(", ", baseTypes);
        if (string.IsNullOrEmpty(inheritance))
            builder.AppendLine();
        else
            builder.AppendLine($" : {inheritance}");

        IList<GenInstruction>? results = null;
        if (versionOfSubInterface == null)
        {
            var versions = type.Members.Select(m =>
                                        {
                                            if (m is MethodDeclarationSyntax mds)
                                            {
                                                var opVersionInfo = mds.GetOperationVersionInfo(compilation);
                                                var v = opVersionInfo.Version;
                                                if (versionInfo.MinVersion > v || versionInfo.IgnoreVersion.Contains(v))
                                                    return -1;

                                                return v;
                                            }
                                            return -1;
                                        })
                                        .Where(m => m != -1);
            builder.AppendLine(" :");
            var versionInterfaces = versions.Select(v => $"\t\t\t\t{versionInfo.FormatNameWithVersion(interfaceName, v)}");
            builder.AppendLine(string.Join(", \r\n", versionInterfaces));
            builder.AppendLine("{");
            builder.AppendLine("}");
            results = versions.Select(v => OnGenerate(context, compilation, info, usingStatements, v))
                               .SelectMany(m => m)
                               .ToList();

        }
        else
        {
            builder.AppendLine("\t{");

            foreach (var method in type.Members)
            {
                if (method is MethodDeclarationSyntax mds)
                {
                    var opVersionInfo = mds.GetOperationVersionInfo(compilation);
                    var v = opVersionInfo.Version;
                    if (versionInfo.MinVersion > v || versionInfo.IgnoreVersion.Contains(v))
                        continue;
                    if (versionOfSubInterface != v)
                        continue;

                    versionInfo = GenMethod(kind, isProducer, versionInfo, builder, mds, opVersionInfo);
                }
            }
            builder.AppendLine("\t}");
        }

        var contractOnlyArg = att.ArgumentList?.Arguments.FirstOrDefault(m => m.NameEquals?.Name.Identifier.ValueText == "ContractOnly");
        var contractOnly = contractOnlyArg?.Expression.NormalizeWhitespace().ToString() == "true";

        if (!contractOnly)
            _bridge.GenerateSingle(context, compilation, info, versionOfSubInterface);

        var result = new GenInstruction(interfaceNameWithVersion, builder.ToString());
        if (results == null)
            return new[] { result };
        results.Add(result);
        return results.ToArray();

        #region GetParameter

        string GetParameter(ParameterSyntax p)
        {
            var mod = p.Modifiers.FirstOrDefault();
            string modifier = mod == null ? string.Empty : $" {mod} ";
            var result = $"\r\n\t\t\t{modifier}{p.Type} {p.Identifier.ValueText}";
            return result;
        }

        #endregion // GetParameter

        #region GenMethod

        SrcGen.Entities.VersionInfo GenMethod(string kind, bool isProducer, SrcGen.Entities.VersionInfo versionInfo, StringBuilder builder, MethodDeclarationSyntax mds, SrcGen.Entities.OperationVersionInfo opVersionInfo)
        {
            var sb = new StringBuilder();
            CopyDocumentation(sb, kind, mds, opVersionInfo);
            var ps = mds.ParameterList.Parameters.Select(GetParameter);
            if (sb.Length != 0 && !isProducer && ps.Any())
            {
                string summaryEnds = "/// </summary>";
                int idxRet = sb.ToString().IndexOf(summaryEnds);
                if (idxRet != -1)
                    sb.Insert(idxRet + summaryEnds.Length, "\r\n\t\t/// <param name=\"consumerMetadata\">The consumer metadata.</param>");
            }
            builder.Append(sb);

            builder.Append("\t\tValueTask");
            if (isProducer)
                builder.Append("<EventKeys>");
            var mtdName = mds.ToNameConvention();
            string nameVersion = versionInfo.FormatNameWithVersion(mtdName, opVersionInfo.Version);
            builder.AppendLine($" {nameVersion}(");

            if (!isProducer)
            {
                builder.Append("\t\t\t");
                builder.Append("ConsumerMetadata consumerMetadata");
                if (ps.Any())
                    builder.Append(',');
            }
            builder.Append("\t\t\t");
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
            builder.Append(string.Join(",", ps));
#pragma warning restore RS1035 // Do not use APIs banned for analyzers
            builder.AppendLine(");");
            builder.AppendLine();
            return versionInfo;
        }

        #endregion // GenMethod
    }
}
