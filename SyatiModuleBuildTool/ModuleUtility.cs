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
        for (int i = 0; i < MI.ModuleExtensionDefinition.Length; i++)
        {
            ModuleExtensionInfo MEI = MI.ModuleExtensionDefinition[i];
            string CodeGenSourcePath = Path.Combine(MI.FolderPath, "codegen", MEI.CodeGenSource);
            string CodeGenOutputPath = Path.Combine(MI.FolderPath, MEI.CodeGenDestination);

            if (File.Exists(CodeGenOutputPath))
                File.Delete(CodeGenOutputPath);

            FileInfo FI = new(CodeGenOutputPath);
            if (FI.Directory is not null && !FI.Directory.Exists)
                Directory.CreateDirectory(FI.Directory.FullName);
            File.Copy(CodeGenSourcePath, CodeGenOutputPath);
        }

        // Begin generation
        for (int i = 0; i < MI.ModuleExtensionDefinition.Length; i++)
        {
            ModuleExtensionInfo MEI = MI.ModuleExtensionDefinition[i];

            //List<string> ExtensionData = [];
            string[][][] ExtensionData = new string[OtherModules.Count][][];

            for (int o = 0; o < OtherModules.Count; o++)
            {
                ModuleInfo OtherModule = OtherModules[o];
                if (OtherModule.ModuleData is null)
                    continue;

                int ModuleEntryNum = GetJSONItemLength(MEI.Name);
                ExtensionData[o] = new string[ModuleEntryNum][];

                if (ExtensionData[o].Length == 0)
                    continue;

                for (int yy = 0; yy < ModuleEntryNum; yy++)
                {
                    ExtensionData[o][yy] = new string[MEI.Variables.Length];
                    for (int xx = 0; xx < MEI.Variables.Length; xx++)
                    {
                        string? val = GetJSONItemByName(MEI.Name, MEI.Variables[xx], yy);
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

                string? GetJSONItemByName(string DefinitionName, string VariableName, int index)
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

                int GetJSONItemLength(string DefinitionName)
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

            //Okay after that fire in the hole we can do the code generation

            string CodeGenOutputPath = Path.Combine(MI.FolderPath, MEI.CodeGenDestination);


            string CodeGenSource = File.ReadAllText(CodeGenOutputPath);
            string[] ExtensionValues = new string[MEI.CodeGenData.Length];
            for (int v = 0; v < OtherModules.Count; v++)
            {
                if (ExtensionData[v] is null)
                    continue;
                for (int xx = 0; xx < MEI.CodeGenData.Length; xx++)
                {
                    if (ExtensionValues[xx] is null)
                        ExtensionValues[xx] = "";
                    for (int yy = 0; yy < ExtensionData[v].Length; yy++)
                    {
                        string d = string.Format(MEI.CodeGenData[xx].ReplaceFormatData, ExtensionData[v][yy]);
                        if (d.StartsWith("#include") && d.Contains("\"\""))
                        {
                            // Do nothing as there's nothing to include.
                        }
                        else
                            ExtensionValues[xx] += d + Environment.NewLine;
                    }
                }
            }
            ;
            for (int xx = 0; xx < MEI.CodeGenData.Length; xx++)
            {
                CodeGenSource = CodeGenSource.Replace($"{{{{{MEI.CodeGenData[xx].ReplaceTargetName}}}}}", ExtensionValues[xx] ?? "");
            }
            File.WriteAllText(CodeGenOutputPath, CodeGenSource);
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
        if (MI.ModuleDependancies is null)
            return [];


        for (int i = 0; i < MI.ModuleDependancies.Length; i++)
        {
            bool Found = false;
            for (int x = 0; x < OtherModules.Count; x++)
            {
                if (ReferenceEquals(MI, OtherModules[x]))
                    continue;

                if (OtherModules[x].Name.Equals(MI.ModuleDependancies[i]))
                {
                    Found = true;
                    break;
                }
            }

            if (!Found)
                throw new MissingMemberException($"Dependancy \"{MI.ModuleDependancies[i]}\" could not be found");
        }
        return MI.ModuleDependancies;
    }

    public static ModuleInfo GetDependancyByName(string name, List<ModuleInfo> OtherModules)
    {
        for (int i = 0; i < OtherModules.Count; i++)
        {
            if (OtherModules[i].Name.Equals(name))
                return OtherModules[i];
        }
        throw new MissingMemberException($"Dependancy \"{name}\" could not be found");
    }

    public static string[] CreateModuleDependancyIncludes(ModuleInfo MI, List<ModuleInfo> OtherModules)
    {
        string[] DepNames = GetModuleDependancies(MI, OtherModules);
        if (DepNames.Length == 0)
            return DepNames; // No point in making another empty array...

        string[] Includes = new string[DepNames.Length];
        for (int i = 0; i < DepNames.Length; i++)
        {
            ModuleInfo Dep = GetDependancyByName(DepNames[i], OtherModules);
            Includes[i] = CreateModuleIncludePath(Dep);
        }
        return Includes;
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
                string BuildPath = File.Replace("source", "build").Replace(".cpp", ".o");
                CompilerTasks.Add((File, BuildPath));
            }

            AllFiles = Directory.GetFiles(SourcePaths[i], "*.s", SearchOption.AllDirectories);
            foreach (string File in AllFiles)
            {
                string BuildPath = File.Replace("source", "build").Replace(".s", ".o");
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
            OutputObjectFiles.Add(CompilerTasks[i].build);
        for (int i = 0; i < AssemblerTasks.Count; i++)
            OutputObjectFiles.Add(AssemblerTasks[i].build);
    }






    public static string CreateModuleCodeGenBuildPath(ModuleInfo MI) => Path.Combine(MI.FolderPath, "codebuild").Replace("\\", "/");
    public static string CreateModuleIncludePath(ModuleInfo MI) => Path.Combine(MI.FolderPath, "include").Replace("\\", "/");
    public static string CreateModuleSourcePath(ModuleInfo MI) => Path.Combine(MI.FolderPath, "source").Replace("\\", "/");





}
