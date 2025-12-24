using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyatiModuleBuildTool;

public class ModuleInfo
{
    /// <summary>
    /// The name of the module
    /// </summary>
    [AllowNull]
    public string Name { get; set; }
    /// <summary>
    /// The creator(s) of the module (if multiple people, separate with commas like this: Super Hackio, VTXG)
    /// </summary>
    [AllowNull]
    public string Author { get; set; }
    /// <summary>
    /// A description of the module. Keep this brief, but not worthless ("This is my module" isn't great. "My module adds XYZ ..." is better)<para/>
    /// Primary Modules (modules which are very essential to most other modules, such as those which provide a very important function) simply have "[Primary Module]" as the author
    /// </summary>
    [AllowNull]
    public string Description { get; set; }
    /// <summary>
    /// If this module is an API module, the API ID goes here. This must be unique to other modules to avoid API ID conflicts.<para/>
    /// Generally formatted as ModuleName_API
    /// </summary>
    [AllowNull]
    public string APIId { get; set; }
    /// <summary>
    /// This property allows defining what games/regions/versions of a game that this module works with.<para/>
    /// Format: [GAMEID]-VER<para/>
    /// Example: RMGE01-0 (SMG1 US Wii All Versions), SB4E01-0 (SMG2 US Wii All Versions), R49E01-0 (DKJB US Wii All Versions), GYBE01-0 (DKJB US GameCube All Versions), SMNE01-1 (NSMBW US Wii Revision 1)
    /// </summary>
    /// <remarks>The revision numbers are to be decided by community authorities, but 0 will always mean ALL revisions. No revision is the same as ALL revisions</remarks>
    [AllowNull]
    public string[] SupportedGames { get; set; } // Will be added in 2026

    /// <summary>
    /// This property indicates which API modules are REQUIRED for this module to compile.
    /// </summary>
    [AllowNull]
    public string[] ModuleDependancies { get; set; } // This will be renamed in 2026
    /// <summary>
    /// This property indicates which API modules are OPTIONAL for this module to compile.
    /// </summary>
    [AllowNull]
    public string[] ModuleOptionalDependancies { get; set; } // This will be renamed in 2026
    /// <summary>
    /// Not currently used
    /// </summary>
    [AllowNull]
    public string[] SpecificSourcePaths { get; set; } // Will be added (or removed?) in 2026
    /// <summary>
    /// This allows modules to specify compiler flags.<para/>
    /// This is primarily used in combination with <see cref="ModuleOptionalDependancies"/> to allow optional features based on a module's existance
    /// </summary>
    [AllowNull]
    public string[] CompilerFlags { get; set; }

    /// <summary>
    /// If this module has CodeGen, the CodeGen definition goes here
    /// </summary>
    [AllowNull]
    public ModuleExtensionInfo[] ModuleExtensionDefinition { get; set; }
    /// <summary>
    /// If this module has data for OTHER modules, that data goes here.
    /// </summary>
    [AllowNull]
    public object[] ModuleData { get; set; }


    /// <summary>
    /// The absolute folder path this module is located at
    /// </summary>
    [JsonIgnore]
    public string FolderPath = "";


    /// <inheritdoc/>
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
        /// <summary>
        /// The name of this extension declaration
        /// </summary>
        /// <remarks>Must be unique to other modules</remarks>
        [AllowNull]
        public string Name { get; set; }
        [AllowNull]
        public string CodeGenSource { get; set; }
        [AllowNull]
        public string CodeGenDestination { get; set; }

        [AllowNull]
        public string[] CodeGenTemplateSources { get; set; }
        [AllowNull]
        public string[] CodeGenTemplateDestinations { get; set; }

        [AllowNull]
        public string[] Variables { get; set; }
        [AllowNull]
        public CodeGenEntry[] CodeGenData { get; set; }

        [JsonIgnore]
        public List<string> IncludePaths = [];

        /// <inheritdoc/>
        public override string ToString() => $"{Name}, {CodeGenSource}";


        public struct CodeGenEntry
        {
            public string ReplaceTargetName { get; set; }
            public string ReplaceFormatData { get; set; }

            public override readonly string ToString() => $"{ReplaceTargetName} ({ReplaceFormatData})";
        }
    }
}