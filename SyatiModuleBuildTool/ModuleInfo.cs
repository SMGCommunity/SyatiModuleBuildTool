using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyatiModuleBuildTool;

public class ModuleInfo
{
    [AllowNull]
    public string Name { get; set; }
    [AllowNull]
    public string Author { get; set; }
    [AllowNull]
    public string Description { get; set; }

    [AllowNull]
    public string[] ModuleDependancies { get; set; }
    [AllowNull]
    public string[] SpecificSourcePaths { get; set; }
    [AllowNull]
    public string[] CompilerFlags { get; set; }
    [AllowNull]
    public ModuleExtensionInfo[] ModuleExtensionDefinition { get; set; }

    [AllowNull]
    public object[] ModuleData { get; set; }

    [JsonIgnore]
    public string FolderPath = "";

    public override string ToString() => $"""
            === Module Information ===
            Name: {Name}
            Author(s): {Author}

            {Description}
            --------------------------
            """;



    public static ModuleInfo? Load(string FolderPath)
    {
        //Read the ModuleInfo
        string ModuleInfoFile = Path.Combine(FolderPath, "ModuleInfo.json");
        if (!File.Exists(ModuleInfoFile))
            throw new FileNotFoundException($"!!ERROR!! Missing ModuleInfo.json for module {new DirectoryInfo(FolderPath).Name}.", ModuleInfoFile);
        Console.WriteLine($"Loading module from {FolderPath}");

        string content = File.ReadAllText(ModuleInfoFile);
        ModuleInfo? MI = JsonSerializer.Deserialize<ModuleInfo>(content);
        if (MI is null)
            return null;
        MI.FolderPath = FolderPath;
        return MI;
    }

    public class ModuleExtensionInfo
    {
        [AllowNull]
        public string Name { get; set; }
        [AllowNull]
        public string CodeGenSource { get; set; }
        [AllowNull]
        public string CodeGenDestination { get; set; }

        [AllowNull]
        public string[] Variables { get; set; }
        [AllowNull]
        public CodeGenEntry[] CodeGenData { get; set; }

        [JsonIgnore]
        public List<string> IncludePaths = [];

        public override string ToString() => $"{Name}, {CodeGenSource}";

        public struct CodeGenEntry
        {
            public string ReplaceTargetName { get; set; }
            public string ReplaceFormatData { get; set; }

            public override readonly string ToString() => $"{ReplaceTargetName} ({ReplaceFormatData})";
        }
    }
}