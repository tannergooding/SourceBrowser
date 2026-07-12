using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectFinalizer
    {
        public void CreateReferencesFiles()
        {
            BackpatchUnreferencedDeclarations(referencesFolder);
            Markup.WriteRedirectFile(ProjectDestinationFolder);
            GenerateFinalReferencesFiles(referencesFolder);
        }

        public void GenerateFinalReferencesFiles(string referencesFolder)
        {
            var shardFiles = Directory.Exists(referencesFolder)
                ? Directory.GetFiles(
                    referencesFolder,
                    ProjectGenerator.ReferenceShardPrefix + "*" + ProjectGenerator.ReferenceShardExtension)
                : Array.Empty<string>();

            // Symbols that only have a base member or implemented interface member link (and no actual
            // references) still need a references file so those links render. They are no longer tracked
            // via per-symbol marker files -- the base/interface member maps loaded in Pass2 already hold
            // exactly that set -- so track which symbols the shards produced and backfill the rest below.
            var writtenSymbols = new HashSet<string>(StringComparer.Ordinal);

            if (shardFiles.Length != 0)
            {
                Log.Write("Creating references files for " + this.AssemblyId);

                // Process one shard at a time so the per-symbol grouping only ever holds a single shard's
                // references in memory, then write that shard's HTML files in parallel. A symbol maps to
                // exactly one shard, so its full reference set is always grouped from a single file.
                foreach (var shardFile in shardFiles)
                {
                    try
                    {
                        GenerateReferencesFilesFromShard(shardFile, referencesFolder, writtenSymbols);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "Failed to generate references files for shard: " + shardFile);
                    }
                }
            }

            GenerateBaseAndInterfaceOnlyReferencesFiles(referencesFolder, writtenSymbols);
        }

        private void GenerateBaseAndInterfaceOnlyReferencesFiles(string referencesFolder, HashSet<string> writtenSymbols)
        {
            if (this.AssemblyId == Constants.MSBuildItemsAssembly ||
                this.AssemblyId == Constants.MSBuildPropertiesAssembly ||
                this.AssemblyId == Constants.MSBuildTargetsAssembly ||
                this.AssemblyId == Constants.MSBuildTasksAssembly ||
                this.AssemblyId == Constants.GuidAssembly)
            {
                return;
            }

            var pending = new HashSet<ulong>();
            foreach (var id in BaseMembers.Keys)
            {
                pending.Add(id);
            }
            foreach (var id in ImplementedInterfaceMembers.Keys)
            {
                pending.Add(id);
            }

            if (pending.Count == 0)
            {
                return;
            }

            Directory.CreateDirectory(referencesFolder);

            Parallel.ForEach(
                pending,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                id =>
                {
                    var symbolId = Serialization.ULongToHexString(id);
                    if (writtenSymbols.Contains(symbolId))
                    {
                        return;
                    }

                    try
                    {
                        WriteReferencesFile(symbolId, Array.Empty<string>(), referencesFolder);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "Failed to generate base/interface references file for symbol: " + symbolId);
                    }
                });
        }

        private void GenerateReferencesFilesFromShard(string shardFile, string referencesFolder, HashSet<string> writtenSymbols)
        {
            var referencesBySymbol = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            // Read the shard and mark it delete-on-close so it is removed once consumed. Each record is
            // three lines: the symbol id followed by the two lines Reference.WriteTo emits.
            using (var stream = new FileStream(shardFile, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, bufferSize: 65536, FileOptions.SequentialScan | FileOptions.DeleteOnClose))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string symbolId;
                while ((symbolId = reader.ReadLine()) != null)
                {
                    string separatedLine = reader.ReadLine();
                    string sourceLine = reader.ReadLine();
                    if (separatedLine == null || sourceLine == null)
                    {
                        break;
                    }

                    if (!referencesBySymbol.TryGetValue(symbolId, out var lines))
                    {
                        lines = new List<string>();
                        referencesBySymbol.Add(symbolId, lines);
                    }

                    lines.Add(separatedLine);
                    lines.Add(sourceLine);
                }
            }

            // Record which symbols this shard produced so the base/interface-only backfill can skip them.
            // A symbol maps to exactly one shard, so this runs single-threaded across shards without racing.
            foreach (var symbolId in referencesBySymbol.Keys)
            {
                writtenSymbols.Add(symbolId);
            }

            Parallel.ForEach(
                referencesBySymbol,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                symbol =>
                {
                    try
                    {
                        WriteReferencesFile(symbol.Key, symbol.Value.ToArray(), referencesFolder);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "Failed to generate references file for symbol: " + symbol.Key);
                    }
                });
        }

        private void WriteReferencesFile(string symbolId, string[] referencesLines, string referencesFolder)
        {
            string referencesFile = Path.Combine(referencesFolder, symbolId + ".html");

            var referenceKindGroups = CreateReferences(referencesLines, out string symbolName);

            using (var writer = new StreamWriter(referencesFile, append: false, Encoding.UTF8, bufferSize: 65536))
            {
                Markup.WriteReferencesFileHeader(writer, symbolName);

                if (this.AssemblyId != Constants.MSBuildItemsAssembly &&
                    this.AssemblyId != Constants.MSBuildPropertiesAssembly &&
                    this.AssemblyId != Constants.MSBuildTargetsAssembly &&
                    this.AssemblyId != Constants.MSBuildTasksAssembly &&
                    this.AssemblyId != Constants.GuidAssembly)
                {
                    var id = Serialization.HexStringToULong(symbolId);
                    WriteBaseMember(id, writer);
                    WriteImplementedInterfaceMembers(id, writer);
                }

                foreach (var referenceKind in referenceKindGroups.OrderBy(k => (int)k.Kind))
                {
                    string formatString = "";

                    switch (referenceKind.Kind)
                    {
                        case ReferenceKind.Reference:
                            formatString = "{0} reference{1} to {2}";
                            break;
                        case ReferenceKind.DerivedType:
                            formatString = "{0} type{1} derived from {2}";
                            break;
                        case ReferenceKind.InterfaceInheritance:
                            formatString = "{0} interface{1} inheriting from {2}";
                            break;
                        case ReferenceKind.InterfaceImplementation:
                            formatString = "{0} implementation{1} of {2}";
                            break;
                        case ReferenceKind.Read:
                            formatString = "{0} read{1} of {2}";
                            break;
                        case ReferenceKind.Write:
                            formatString = "{0} write{1} to {2}";
                            break;
                        case ReferenceKind.Instantiation:
                            formatString = "{0} instantiation{1} of {2}";
                            break;
                        case ReferenceKind.Override:
                            formatString = "{0} override{1} of {2}";
                            break;
                        case ReferenceKind.InterfaceMemberImplementation:
                            formatString = "{0} implementation{1} of {2}";
                            break;
                        case ReferenceKind.GuidUsage:
                            formatString = "{0} usage{1} of Guid {2}";
                            break;
                        case ReferenceKind.EmptyArrayAllocation:
                            formatString = "{0} allocation{1} of empty arrays";
                            break;
                        case ReferenceKind.MSBuildPropertyAssignment:
                            formatString = "{0} assignment{1} to MSBuild property {2}";
                            break;
                        case ReferenceKind.MSBuildPropertyUsage:
                            formatString = "{0} usage{1} of MSBuild property {2}";
                            break;
                        case ReferenceKind.MSBuildItemAssignment:
                            formatString = "{0} assignment{1} to MSBuild item {2}";
                            break;
                        case ReferenceKind.MSBuildItemUsage:
                            formatString = "{0} usage{1} of MSBuild item {2}";
                            break;
                        case ReferenceKind.MSBuildTargetDeclaration:
                            formatString = "{0} declaration{1} of MSBuild target {2}";
                            break;
                        case ReferenceKind.MSBuildTargetUsage:
                            formatString = "{0} usage{1} of MSBuild target {2}";
                            break;
                        case ReferenceKind.MSBuildTaskDeclaration:
                            formatString = "{0} import{1} of MSBuild task {2}";
                            break;
                        case ReferenceKind.MSBuildTaskUsage:
                            formatString = "{0} call{1} to MSBuild task {2}";
                            break;
                        default:
                            throw new NotImplementedException("Missing case for " + referenceKind.Kind);
                    }

                    int totalReferenceCount = referenceKind.Count;
                    string headerText = string.Format(
                        formatString,
                        totalReferenceCount,
                        totalReferenceCount == 1 ? "" : "s",
                        symbolName);

                    writer.Write(@"<div class=""rH"">");
                    writer.Write(headerText);
                    writer.Write("</div>");

                    foreach (var sameAssemblyReferencesGroup in referenceKind.Assemblies.OrderBy(a => a.AssemblyName))
                    {
                        string assemblyName = sameAssemblyReferencesGroup.AssemblyName;
                        writer.Write("<div class=\"rA\">");
                        writer.Write(assemblyName);
                        writer.Write(" (");
                        writer.Write(sameAssemblyReferencesGroup.Count);
                        writer.Write(")</div>");

                        writer.Write("<div class=\"rG\" id=\"");
                        writer.Write(assemblyName);
                        writer.Write("\">");

                        foreach (var sameFileReferencesGroup in sameAssemblyReferencesGroup.Files.OrderBy(f => f.FilePath))
                        {
                            writer.Write("<div class=\"rF\">");
                            writer.Write("<div class=\"rN\">");
                            writer.Write(sameFileReferencesGroup.FilePath);
                            writer.Write(" (");
                            writer.Write(sameFileReferencesGroup.Count);
                            writer.Write(")</div>");
                            writer.WriteLine();

                            foreach (var sameLineReferencesGroup in sameFileReferencesGroup.Lines)
                            {
                                var references = sameLineReferencesGroup.References;
                                var url = references[0].Url;
                                writer.Write("<a href=\"");
                                writer.Write(url);
                                writer.Write("\">");

                                writer.Write("<b>");
                                writer.Write(sameLineReferencesGroup.LineNumber);
                                writer.Write("</b>");
                                MergeOccurrences(writer, references);
                                writer.Write("</a>");
                                writer.WriteLine();
                            }

                            writer.Write("</div>");
                            writer.WriteLine();
                        }

                        writer.Write("</div>");
                        writer.WriteLine();
                    }
                }

                Write(writer, "</body></html>");
            }
        }

        private void WriteImplementedInterfaceMembers(ulong symbolId, StreamWriter writer)
        {
            if (!ImplementedInterfaceMembers.TryGetValue(symbolId, out HashSet<Tuple<string, ulong>> implementedInterfaceMembers))
            {
                return;
            }

            Write(writer, string.Format(@"<div class=""rH"">Implemented interface member{0}:</div>", implementedInterfaceMembers.Count > 1 ? "s" : ""));

            foreach (var implementedInterfaceMember in implementedInterfaceMembers)
            {
                var assemblyName = implementedInterfaceMember.Item1;
                var interfaceSymbolId = implementedInterfaceMember.Item2;

                if (!this.SolutionFinalizer.assemblyNameToProjectMap.TryGetValue(assemblyName, out ProjectFinalizer baseProject))
                {
                    return;
                }

                if (baseProject.DeclaredSymbols.TryGetValue(interfaceSymbolId, out DeclaredSymbolInfo symbol))
                {
                    var sb = new StringBuilder();
                    Markup.WriteSymbol(symbol, sb);
                    writer.Write(sb.ToString());
                }
            }
        }

        private void WriteBaseMember(ulong symbolId, StreamWriter writer)
        {
            if (!BaseMembers.TryGetValue(symbolId, out Tuple<string, ulong> baseMemberLink))
            {
                return;
            }

            Write(writer, @"<div class=""rH"">Base:</div>");

            var assemblyName = baseMemberLink.Item1;
            var baseSymbolId = baseMemberLink.Item2;

            if (!this.SolutionFinalizer.assemblyNameToProjectMap.TryGetValue(assemblyName, out ProjectFinalizer baseProject))
            {
                return;
            }

            if (baseProject.DeclaredSymbols.TryGetValue(baseSymbolId, out DeclaredSymbolInfo symbol))
            {
                var sb = new StringBuilder();
                Markup.WriteSymbol(symbol, sb);
                writer.Write(sb.ToString());
            }
        }

        // Materialized reference tree: built once in a single pass over the raw reference
        // lines with per-level counts precomputed. This replaces the previous lazy nested
        // GroupBy chain, which re-executed the grouping on every re-enumeration and required
        // separate CountItems passes that fully re-walked each subtree.
        private sealed class ReferenceKindGroup
        {
            public ReferenceKind Kind;
            public int Count;
            public readonly List<ReferenceAssemblyGroup> Assemblies = new List<ReferenceAssemblyGroup>();
            public readonly Dictionary<string, ReferenceAssemblyGroup> AssemblyMap = new Dictionary<string, ReferenceAssemblyGroup>();
        }

        private sealed class ReferenceAssemblyGroup
        {
            public string AssemblyName;
            public int Count;
            public readonly List<ReferenceFileGroup> Files = new List<ReferenceFileGroup>();
            public readonly Dictionary<string, ReferenceFileGroup> FileMap = new Dictionary<string, ReferenceFileGroup>();
        }

        private sealed class ReferenceFileGroup
        {
            public string FilePath;
            public int Count;
            public readonly List<ReferenceLineGroup> Lines = new List<ReferenceLineGroup>();
            public readonly Dictionary<int, ReferenceLineGroup> LineMap = new Dictionary<int, ReferenceLineGroup>();
        }

        private sealed class ReferenceLineGroup
        {
            public int LineNumber;
            public readonly List<Reference> References = new List<Reference>();
        }

        private static List<ReferenceKindGroup> CreateReferences(
            string[] referencesLines,
            out string referencedSymbolName)
        {
            referencedSymbolName = null;

            var kindGroups = new List<ReferenceKindGroup>();
            var kindMap = new Dictionary<ReferenceKind, ReferenceKindGroup>();

            for (int i = 0; i < referencesLines.Length; i += 2)
            {
                var reference = new Reference(referencesLines[i], referencesLines[i + 1]);

                if (referencedSymbolName == null &&
                    reference.ToSymbolName != "this" &&
                    reference.ToSymbolName != "base" &&
                    reference.ToSymbolName != "var" &&
                    reference.ToSymbolName != "UsingTask" &&
                    reference.ToSymbolName != "[")
                {
                    referencedSymbolName = reference.ToSymbolName;
                }

                if (!kindMap.TryGetValue(reference.Kind, out var kindGroup))
                {
                    kindGroup = new ReferenceKindGroup { Kind = reference.Kind };
                    kindMap.Add(reference.Kind, kindGroup);
                    kindGroups.Add(kindGroup);
                }

                kindGroup.Count++;

                if (!kindGroup.AssemblyMap.TryGetValue(reference.FromAssemblyId, out var assemblyGroup))
                {
                    assemblyGroup = new ReferenceAssemblyGroup { AssemblyName = reference.FromAssemblyId };
                    kindGroup.AssemblyMap.Add(reference.FromAssemblyId, assemblyGroup);
                    kindGroup.Assemblies.Add(assemblyGroup);
                }

                assemblyGroup.Count++;

                if (!assemblyGroup.FileMap.TryGetValue(reference.FromLocalPath, out var fileGroup))
                {
                    fileGroup = new ReferenceFileGroup { FilePath = reference.FromLocalPath };
                    assemblyGroup.FileMap.Add(reference.FromLocalPath, fileGroup);
                    assemblyGroup.Files.Add(fileGroup);
                }

                fileGroup.Count++;

                if (!fileGroup.LineMap.TryGetValue(reference.ReferenceLineNumber, out var lineGroup))
                {
                    lineGroup = new ReferenceLineGroup { LineNumber = reference.ReferenceLineNumber };
                    fileGroup.LineMap.Add(reference.ReferenceLineNumber, lineGroup);
                    fileGroup.Lines.Add(lineGroup);
                }

                lineGroup.References.Add(reference);
            }

            return kindGroups;
        }

        private static void MergeOccurrences(StreamWriter writer, IEnumerable<Reference> referencesOnTheSameLine)
        {
            var text = referencesOnTheSameLine.First().ReferenceLineText;
            referencesOnTheSameLine = referencesOnTheSameLine.OrderBy(r => r.ReferenceColumnStart);
            int current = 0;
            foreach (var occurrence in referencesOnTheSameLine)
            {
                if (occurrence.ReferenceColumnStart < 0 ||
                    occurrence.ReferenceColumnStart >= text.Length ||
                    occurrence.ReferenceColumnEnd <= occurrence.ReferenceColumnStart)
                {
                    string message = "occurrence.ReferenceColumnStart = " + occurrence.ReferenceColumnStart;
                    message += "\r\noccurrence.ReferenceColumnEnd = " + occurrence.ReferenceColumnEnd;
                    message += "\r\ntext = " + text;
                    Log.Exception("MergeOccurrences1: " + message);
                }

                if (occurrence.ReferenceColumnStart > current)
                {
                    if (current < 0 ||
                        current >= text.Length ||
                        occurrence.ReferenceColumnStart < current ||
                        occurrence.ReferenceColumnStart >= text.Length)
                    {
                        string message = "occurrence.ReferenceColumnStart = " + occurrence.ReferenceColumnStart;
                        message += "\r\noccurrence.ReferenceColumnEnd = " + occurrence.ReferenceColumnEnd;
                        message += "\r\ntext = " + text;
                        message += "\r\ncurrent = " + current;
                        Log.Exception("MergeOccurrences2: " + message);
                    }
                    else
                    {
                        Write(writer, text.Substring(current, occurrence.ReferenceColumnStart - current));
                    }
                }

                Write(writer, "<i>");
                Write(writer, text.Substring(occurrence.ReferenceColumnStart, occurrence.ReferenceColumnEnd - occurrence.ReferenceColumnStart));
                Write(writer, "</i>");
                current = occurrence.ReferenceColumnEnd;
            }

            if (current < text.Length)
            {
                Write(writer, text.Substring(current, text.Length - current));
            }
        }

        private static void Write(StreamWriter sw, string text)
        {
            sw.Write(text);
        }
    }
}
