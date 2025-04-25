using System.Linq;
using System.Reflection;
using System.Text.Json;
using static SyatiModuleBuildTool.ModuleInfo;

namespace SyatiModuleBuildTool;

public static class ModuleUtility
{
    public static string[] CollectModuleFlags(List<ModuleInfo> Modules)
    {
        List<string> flags = [];
        foreach (ModuleInfo module in Modules)
            if (module.CompilerFlags is not null)
                flags.AddRange(module.CompilerFlags);
        return [.. flags];
    }


    public static void PerformAllModuleCodeGen(List<ModuleInfo> Modules)
    {
        for (int i = 0; i < Modules.Count; i++)
            PerformModuleCodeGen(Modules[i], Modules);
    }

    public static void PerformModuleCodeGen(ModuleInfo MI, List<ModuleInfo> OtherModules)
    {
        if (MI.ModuleExtensionDefinition is null)
            return; //No Codegen to do

        Console.WriteLine($"\"{MI.Name}\" requested CodeGen. Generating...");


        // Prepare workspace

        // If a module extension definition uses the Template field instead, we cannot prepare it here.
        for (int i = 0; i < MI.ModuleExtensionDefinition.Length; i++)
        {
            ModuleExtensionInfo MEI = MI.ModuleExtensionDefinition[i];
            if (MEI.CodeGenTemplateSources is not null && MEI.CodeGenTemplateDestinations is not null)
                HandleTemplateFiles(MEI);

            if (MEI.CodeGenSource is not null && MEI.CodeGenDestination is not null)
            {
                string CodeGenSourcePath = Path.Combine(MI.FolderPath, "codegen", MEI.CodeGenSource);
                string CodeGenOutputPath = Path.Combine(MI.FolderPath, MEI.CodeGenDestination);

                HandleFile(CodeGenSourcePath, CodeGenOutputPath);
            }
        }

        // Begin Generation
        for (int i = 0; i < MI.ModuleExtensionDefinition.Length; i++)
        {
            ModuleExtensionInfo MEI = MI.ModuleExtensionDefinition[i];
            if (MEI.CodeGenTemplateSources is not null && MEI.CodeGenTemplateDestinations is not null)
                DoTemplateExtensionDefinition(MEI);
            if (MEI.CodeGenSource is not null && MEI.CodeGenDestination is not null)
                DoStandardExtensionDefinition(MEI);
        }





        // Standard definitions create one file and one file only. The original CodeGen type.
        void DoStandardExtensionDefinition(ModuleExtensionInfo MEI)
        {
            string[][][] ExtensionData = GetExtensionData(MEI);

            //Okay after that fire in the hole we can do the code generation
            string[] ExtensionValues = new string[MEI.CodeGenData.Length];
            for (int omid = 0; omid < OtherModules.Count; omid++)
            {
                if (ExtensionData[omid] is null)
                    continue;
                for (int meiid = 0; meiid < MEI.CodeGenData.Length; meiid++)
                {
                    if (ExtensionValues[meiid] is null)
                        ExtensionValues[meiid] = "";
                    for (int yy = 0; yy < ExtensionData[omid].Length; yy++)
                    {
                        string d = string.Format(MEI.CodeGenData[meiid].ReplaceFormatData, ExtensionData[omid][yy]);
                        if (d.StartsWith("#include") && d.Contains("\"\""))
                        {
                            // Do nothing as there's nothing to include.
                        }
                        else
                            ExtensionValues[meiid] += d + Environment.NewLine;
                    }
                }
            }

            string CodeGenOutputPath = Path.Combine(MI.FolderPath, MEI.CodeGenDestination);
            string CodeGenSource = File.ReadAllText(CodeGenOutputPath);
            for (int xx = 0; xx < MEI.CodeGenData.Length; xx++)
                CodeGenSource = CodeGenSource.Replace($"{{{{{MEI.CodeGenData[xx].ReplaceTargetName}}}}}", ExtensionValues[xx] ?? "");
            File.WriteAllText(CodeGenOutputPath, CodeGenSource);
        }

        // Template definitions create a new file for each unique filename that's created.
        void DoTemplateExtensionDefinition(ModuleExtensionInfo MEI)
        {
            string[][][] ExtensionData = GetExtensionData(MEI);

            // Lets create some filenames now
            Dictionary<string, List<string[]>> TemplateCodeGenData = [];
            

            for (int omid = 0; omid < OtherModules.Count; omid++)
            {
                if (ExtensionData[omid] is null)
                    continue;

                for (int x = 0; x < ExtensionData[omid].Length; x++)
                {
                    foreach (string item in MEI.CodeGenTemplateDestinations)
                    {
                        string str = string.Format(item, ExtensionData[omid][x]);
                        if (!TemplateCodeGenData.ContainsKey(str))
                            TemplateCodeGenData.Add(str, []);
                        TemplateCodeGenData[str].Add(ExtensionData[omid][x]);
                    }
                }
            }

            // Once we have the filenames to process, we can start filling each of them out.
            foreach (KeyValuePair<string, List<string[]>> dictentry in TemplateCodeGenData)
            {
                // To begin that process, we need to get a single array of all unique elements to be replacing with.
                List<string>[] FinalReplacementParts = new List<string>[MEI.CodeGenData.Length];


                for (int meivar = 0; meivar < MEI.CodeGenData.Length; meivar++)
                {
                    FinalReplacementParts[meivar] = [];
                    bool IsList = MEI.CodeGenData[meivar].ReplaceTargetName.EndsWith("List");

                    for (int arrid = 0; arrid < dictentry.Value.Count; arrid++)
                    {
                        string fmt = string.Format(MEI.CodeGenData[meivar].ReplaceFormatData, dictentry.Value[arrid]);
                        if (fmt.StartsWith("#include") && fmt.Contains("\"\""))
                        {
                            continue;
                        }

                        if (IsList)
                        {
                            // Create multiple strings
                            if (!FinalReplacementParts[meivar].Contains(fmt))
                                FinalReplacementParts[meivar].Add(fmt);
                        }
                        else
                        {
                            // Throw an exception if all of these do not match
                            if (FinalReplacementParts[meivar].Count == 0)
                                FinalReplacementParts[meivar].Add(fmt);
                            else if (!FinalReplacementParts[meivar].Contains(fmt))
                                throw new Exception($"The ModuleExtensionInfo property {MEI.CodeGenData[meivar].ReplaceFormatData} is not a List and cannot have multiple entries (The value across all modules must be identical){Environment.NewLine}Module Extension Info:\t{MEI.Name}");
                        }
                    }
                }

                // Okay with all that out of the way, we can finally put the replacements together

                string CodeGenOutputPath = Path.Combine(MI.FolderPath, dictentry.Key);
                string CodeGenSource = File.ReadAllText(CodeGenOutputPath);
                for (int xx = 0; xx < MEI.CodeGenData.Length; xx++)
                {
                    List<string> current = FinalReplacementParts[xx];
                    string FinalStr = "";
                    for (int yy = 0; yy < current.Count; yy++)
                        FinalStr += current[yy] + (yy < current.Count-1 ? Environment.NewLine : "");

                    CodeGenSource = CodeGenSource.Replace($"{{{{{MEI.CodeGenData[xx].ReplaceTargetName}}}}}", FinalStr);
                }
                File.WriteAllText(CodeGenOutputPath, CodeGenSource);
            }
        }
        void HandleTemplateFiles(ModuleExtensionInfo MEI)
        {
            if (MEI.CodeGenTemplateSources.Length != MEI.CodeGenTemplateDestinations.Length)
                throw new IndexOutOfRangeException($"Module \"{MEI.Name}\" has a desyncronized Template array. ({MEI.CodeGenTemplateSources.Length} : {MEI.CodeGenTemplateDestinations.Length})");

            string[][][] ExtensionData = GetExtensionData(MEI);

            // Lets create some filenames now
            List<string> TemplateSourceList = []; // TODO: Make this actually do something by syncing the 2 arrays
            List<string> TemplateDestinationList = [];

            for (int omid = 0; omid < OtherModules.Count; omid++)
            {
                if (ExtensionData[omid] is null)
                    continue;

                for (int x = 0; x < ExtensionData[omid].Length; x++)
                {
                    for (int xx = 0; xx < MEI.CodeGenTemplateSources.Length; xx++)
                    {
                        string sourcestr = string.Format(MEI.CodeGenTemplateSources[xx], ExtensionData[omid][x]);
                        string deststr = string.Format(MEI.CodeGenTemplateDestinations[xx], ExtensionData[omid][x]);
                        if (!TemplateDestinationList.Contains(deststr))
                        {
                            TemplateSourceList.Add(sourcestr);
                            TemplateDestinationList.Add(deststr);
                        }
                    }
                }
            }

            for (int i = 0; i < TemplateSourceList.Count; i++)
            {
                string CodeGenSourcePath = Path.Combine(MI.FolderPath, "codegen", TemplateSourceList[i]);
                string CodeGenOutputPath = Path.Combine(MI.FolderPath, TemplateDestinationList[i]);
                HandleFile(CodeGenSourcePath, CodeGenOutputPath);
            }
        }

        string[][][] GetExtensionData(ModuleExtensionInfo MEI)
        {
            string[][][] ExtensionData = new string[OtherModules.Count][][];

            for (int o = 0; o < OtherModules.Count; o++)
            {
                ModuleInfo OtherModule = OtherModules[o];
                if (OtherModule.ModuleData is null)
                    continue;

                int ModuleEntryNum = GetJSONItemLength(OtherModule, MEI.Name);
                ExtensionData[o] = new string[ModuleEntryNum][];

                if (ExtensionData[o].Length == 0)
                    continue;

                for (int yy = 0; yy < ModuleEntryNum; yy++)
                {
                    ExtensionData[o][yy] = new string[MEI.Variables.Length];
                    for (int xx = 0; xx < MEI.Variables.Length; xx++)
                    {
                        string? val = GetJSONItemByName(OtherModule, MEI.Name, MEI.Variables[xx], yy);
                        if (val is not null)
                        {
                            ExtensionData[o][yy][xx] = val;
                            if (MEI.Variables[xx].Equals("Include"))
                            {
                                var tmp = CreateModuleIncludePath(OtherModule);
                                if (!MEI.IncludePaths.Contains(tmp))
                                    MEI.IncludePaths.Add(tmp);
                            }
                        }
                    }
                }
            }
            return ExtensionData;
        }

        static void HandleFile(string CodeGenSourcePath, string CodeGenOutputPath)
        {
            if (File.Exists(CodeGenOutputPath))
                File.Delete(CodeGenOutputPath);

            FileInfo FI = new(CodeGenOutputPath);
            if (FI.Directory is not null && !FI.Directory.Exists)
                Directory.CreateDirectory(FI.Directory.FullName);
            File.Copy(CodeGenSourcePath, CodeGenOutputPath);
        }

        static string? GetJSONItemByName(ModuleInfo OtherModule, string DefinitionName, string VariableName, int index)
        {
            foreach (var item in OtherModule.ModuleData)
            {
                if (item is JsonElement JObj)
                {
                    if (JObj.TryGetProperty(DefinitionName, out JsonElement v))
                    {
                        for (int i = 0; i < v.GetArrayLength(); i++)
                        {
                            var cur = v[i];
                            if (cur.TryGetProperty(VariableName, out JsonElement v2))
                            {
                                if (index-- == 0)
                                    return v2.GetString();
                            }
                        }
                    }
                }
            }
            return null;
        }
        static int GetJSONItemLength(ModuleInfo OtherModule, string DefinitionName)
        {
            foreach (var item in OtherModule.ModuleData)
            {
                if (item is JsonElement JObj)
                {
                    if (JObj.TryGetProperty(DefinitionName, out JsonElement v))
                    {
                        return v.GetArrayLength();
                    }
                }
            }
            return 0;
        }
    }


    public static void VerifyDependancies(List<ModuleInfo> Modules)
    {
        foreach (ModuleInfo module in Modules)
        {
            _ = GetModuleDependancies(module, Modules);
        }
    }

    public static string[] GetModuleDependancies(ModuleInfo MI, List<ModuleInfo> OtherModules)
    {
        List<string> Deps = [];

        if (MI.ModuleDependancies is not null)
        {
            for (int i = 0; i < MI.ModuleDependancies.Length; i++)
                _ = GetModuleByAPIId(MI.ModuleDependancies[i], OtherModules); //We just need to run this function, as it'll exception on it's own when no module is found
            Deps.AddRange(MI.ModuleDependancies);
        }

        // Optional APIs are... Optional... use Compiler Flags in your code!
        if (MI.ModuleOptionalDependancies is not null)
            for (int i = 0; i < MI.ModuleOptionalDependancies.Length; i++)
                for (int x = 0; x < OtherModules.Count; x++)
                {
                    if (ReferenceEquals(MI, OtherModules[x]))
                        continue;

                    string p = MI.ModuleOptionalDependancies[i];
                    if (OtherModules[x].APIId is not null && OtherModules[x].APIId.Equals(p))
                    {
                        if (!Deps.Contains(p))
                            Deps.Add(p);
                        break;
                    }
                }

        return [.. Deps];
    }

    public static ModuleInfo GetModuleByAPIId(string ApiID, List<ModuleInfo> OtherModules)
    {
        for (int i = 0; i < OtherModules.Count; i++)
        {
            if (OtherModules[i].APIId is not null && OtherModules[i].APIId.Equals(ApiID))
                return OtherModules[i];
        }
        throw new MissingMemberException($"API with ID \"{ApiID}\" could not be found");
    }

    public static string[] CreateModuleDependancyIncludes(ModuleInfo MI, List<ModuleInfo> OtherModules)
    {
        string[] APINames = GetModuleDependancies(MI, OtherModules);
        if (APINames.Length == 0)
            return APINames; // No point in making another empty array...

        List<string> Includes = [];
        for (int i = 0; i < APINames.Length; i++)
        {
            ModuleInfo Dep = GetModuleByAPIId(APINames[i], OtherModules);
            string inc = CreateModuleIncludePath(Dep);
            if (Path.Exists(inc.Replace("\"", "")))
                Includes.Add(inc);
            string cb = CreateModuleCodeGenBuildExportPath(Dep);
            if (Path.Exists(cb.Replace("\"", "")))
                Includes.Add(cb);
        }
        return Includes.ToArray();
    }


    public static string[] GetModuleExtensionIncludes(ModuleInfo MI)
    {
        if (MI.ModuleExtensionDefinition is null)
            return [];
        List<string> All = [];
        foreach (ModuleExtensionInfo item in MI.ModuleExtensionDefinition)
        {
            All.AddRange(item.IncludePaths);
        }
        return [.. All];
    }

    public static void CompileAllModules(List<ModuleInfo> Modules, string[] Flags, string[] Includes, string SyatiFolderPath, ref List<string> OutputObjectFiles)
    {
        List<string> AllModuleFlags = [];
        AllModuleFlags.AddRange(Flags);
        AllModuleFlags.AddRange(CollectModuleFlags(Modules));

        for (int i = 0; i < Modules.Count; i++)
        {
            List<string> DependancyIncludes = [];
            DependancyIncludes.AddRange(Includes);
            DependancyIncludes.Add(CreateModuleIncludePath(Modules[i]));
            DependancyIncludes.Add(CreateModuleCodeGenBuildPath(Modules[i]));
            DependancyIncludes.Add(CreateModuleCodeGenBuildExportPath(Modules[i]));
            DependancyIncludes.AddRange(CreateModuleDependancyIncludes(Modules[i], Utility.RemoveOneItem(Modules, i)));
            DependancyIncludes.AddRange(GetModuleExtensionIncludes(Modules[i]));

            CompileModule(Modules[i], [.. AllModuleFlags], [.. DependancyIncludes], SyatiFolderPath, ref OutputObjectFiles);
        }
    }

    public static void CompileModule(ModuleInfo MI, string[] Flags, string[] Includes, string SyatiFolderPath, ref List<string> OutputObjectFiles)
    {
        string IncludeString = "-i " + string.Join(" -I- -i ", Includes);
        string FinalFlags = string.Join(" ", Flags);
        // Collect the Source/Build paths
        List<string> SourcePaths =
        [
            Path.Combine(MI.FolderPath, "source").Replace("\\", "/"),
            Path.Combine(MI.FolderPath, "codebuild").Replace("\\", "/"),
        ];
        List<string> BuildPaths =
        [
            Path.Combine(MI.FolderPath, "build").Replace("\\", "/"),
        ];

        List<(string source, string build)> CompilerTasks = [];
        List<(string source, string build)> AssemblerTasks = [];
        for (int i = 0; i < SourcePaths.Count; i++)
        {
            if (!Directory.Exists(SourcePaths[i]))
                continue;
                

            string[] AllFiles = Directory.GetFiles(SourcePaths[i], "*.cpp", SearchOption.AllDirectories);
            foreach (string File in AllFiles)
            {
                string BuildPath = File.Replace(SourcePaths[i], "build").Replace(".cpp", ".o");
                CompilerTasks.Add((File, BuildPath));
            }

            AllFiles = Directory.GetFiles(SourcePaths[i], "*.s", SearchOption.AllDirectories);
            foreach (string File in AllFiles)
            {
                string BuildPath = File.Replace(SourcePaths[i], "build").Replace(".s", ".o");
                AssemblerTasks.Add((File, BuildPath));
            }
        }
        for (int i = 0; i < BuildPaths.Count; i++)
        {
            if (Directory.Exists(BuildPaths[i]))
                Directory.Delete(BuildPaths[i], true);
        }

        CompileUtility.Compile(FinalFlags, IncludeString, CompilerTasks, AssemblerTasks, SyatiFolderPath);

        for (int i = 0; i < CompilerTasks.Count; i++)
            OutputObjectFiles.Add("\"" + CompilerTasks[i].build + "\"");
        for (int i = 0; i < AssemblerTasks.Count; i++)
            OutputObjectFiles.Add("\"" + AssemblerTasks[i].build + "\"");
    }



    public static void CompileAllUnibuild(List<ModuleInfo> Modules, string[] Flags, string[] Includes, string SyatiFolderPath, string OutputFolderPath, ref List<string> OutputObjectFiles)
    {
        List<string> AllModuleFlags = [];
        AllModuleFlags.AddRange(Flags);
        AllModuleFlags.AddRange(CollectModuleFlags(Modules));

        List<string> DependancyIncludes = [];
        List<string> AllCompileIncludePaths = [];
        DependancyIncludes.AddRange(Includes);

        for (int i = 0; i < Modules.Count; i++)
        {
            AddDependancyIncludeIfNotExist(CreateModuleIncludePath(Modules[i]));
            AddDependancyIncludeIfNotExist(CreateModuleCodeGenBuildPath(Modules[i]));
            AddDependancyIncludesIfNotExist(CreateModuleDependancyIncludes(Modules[i], Utility.RemoveOneItem(Modules, i)));
            AddDependancyIncludesIfNotExist(GetModuleExtensionIncludes(Modules[i]));


            // Collect the Source/Build paths
            List<string> SourcePaths =
            [
                Path.Combine(Modules[i].FolderPath, "source").Replace("\\", "/"),
            ];
            List<string> BuildPaths =
            [
                Path.Combine(Modules[i].FolderPath, "build").Replace("\\", "/"),
            ];

            for (int x = 0; x < SourcePaths.Count; x++)
            {
                if (!Directory.Exists(SourcePaths[x]))
                    continue;

                string[] AllFiles = Directory.GetFiles(SourcePaths[x], "*.cpp", SearchOption.AllDirectories);
                foreach (string File in AllFiles)
                    AllCompileIncludePaths.Add(File);

                AllFiles = Directory.GetFiles(SourcePaths[x], "*.s", SearchOption.AllDirectories);
                foreach (string File in AllFiles)
                {
                    throw new NotImplementedException("Idk how to do this with ASM support");
                }
            }
        }

        string MasterCPP =
            """
            {0}
            """;
        string PathString = "";
        for (int i = 0; i < AllCompileIncludePaths.Count; i++)
        {
            PathString += $"#include \"{AllCompileIncludePaths[i].Replace('\\', '/')}\""+Environment.NewLine;
        }
        MasterCPP = string.Format(MasterCPP, PathString);

        string FinalCPPPath = Path.Combine(OutputFolderPath, "UniBuild.cpp");
        File.WriteAllText(FinalCPPPath, MasterCPP);

        List<(string source, string build)> CompilerTasks = [(FinalCPPPath, FinalCPPPath.Replace(".cpp", ".o"))];
        List<(string source, string build)> AssemblerTasks = [];
        string IncludeString = "-i " + string.Join(" -I- -i ", DependancyIncludes);
        string FinalFlags = string.Join(" ", AllModuleFlags);
        CompileUtility.Compile(FinalFlags, IncludeString, CompilerTasks, AssemblerTasks, SyatiFolderPath);

        for (int i = 0; i < CompilerTasks.Count; i++)
            OutputObjectFiles.Add("\"" + CompilerTasks[i].build + "\"");
        for (int i = 0; i < AssemblerTasks.Count; i++)
            OutputObjectFiles.Add("\"" + AssemblerTasks[i].build + "\"");


        void AddDependancyIncludeIfNotExist(string path)
        {
            if (!DependancyIncludes.Contains(path))
                DependancyIncludes.Add(path);
        }

        void AddDependancyIncludesIfNotExist(IList<string> data)
        {
            for (int i = 0; i < data.Count; i++)
                AddDependancyIncludeIfNotExist(data[i]);
        }
    }



    public static string[] CollectModuleSymbols(List<ModuleInfo> Modules)
    {
        List<string> Paths = [];
        foreach (ModuleInfo module in Modules)
        {
            string path = CreateModuleSymbolPath(module).Replace("\"","");
            if (Directory.Exists(path))
                Paths.Add(path);
        }
        return [.. Paths];
    }

    public static string CreateModuleCodeGenBuildExportPath(ModuleInfo MI) => "\"" + Path.Combine(MI.FolderPath, "codebuildexport").Replace("\\", "/") + "\"";
    public static string CreateModuleCodeGenBuildPath(ModuleInfo MI) => "\"" + Path.Combine(MI.FolderPath, "codebuild").Replace("\\", "/") + "\"";
    public static string CreateModuleIncludePath(ModuleInfo MI) => "\"" + Path.Combine(MI.FolderPath, "include").Replace("\\", "/") + "\"";
    public static string CreateModuleSourcePath(ModuleInfo MI) => "\"" + Path.Combine(MI.FolderPath, "source").Replace("\\", "/") + "\"";
    public static string CreateModuleSymbolPath(ModuleInfo MI) => "\"" + Path.Combine(MI.FolderPath, "symbols").Replace("\\", "/") + "\"";
}
