using System.Runtime.InteropServices;

namespace SyatiModuleBuildTool;

internal class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 4)
        {
            Help();
            return;
        }

        #region Parse Build Target and Region and Version
        if (args[0].Length < 4)
        {
            Error(new IndexOutOfRangeException($"The provided build target \"{args[0]}\" is not valid. (Too short)"));
            return;
        }

        string BuildTarget = args[0].ToUpper();
        char RegionTarget = ' ';
        int VersionTarget = -1;

        string[] split = BuildTarget.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length > 1)
        {
            if (!int.TryParse(split[1], out VersionTarget))
            {
                Error(new InvalidDataException($"Failed to parse build target \"{BuildTarget}\""));
                return;
            }
            BuildTarget = split[0];
        }
        int lastdash;
        if ((lastdash = BuildTarget.LastIndexOf('-')) >= 0)
            BuildTarget = BuildTarget[..^lastdash];
        if (BuildTarget.Length == 4)
        {
            RegionTarget = BuildTarget[^1];
            BuildTarget = BuildTarget[..3];
        }
        if (!((char[])['E', 'P', 'J', 'K', 'W']).Contains(RegionTarget))
        {
            Error(new InvalidDataException($"Invalid Region \'{RegionTarget}\'"));
            return;
        }
        #endregion

        string HeaderRepositoryPath = args[1].PathSanitize();
        string ModuleFolderPath = args[2].PathSanitize();

        // TODO: Make these... not hardcoded...
        //       Idk how, but at the very least have like a config or something for these...
        #region Check for Compiler, Assembler, and Linker
        string LinkerPath, CompilerPath, AssemblerPath;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            LinkerPath = $"{Path.Combine(HeaderRepositoryPath, "deps/Kamek/Kamek.exe")}";
            CompilerPath = $"{Path.Combine(HeaderRepositoryPath, "deps/CodeWarrior/mwcceppc.exe")}";
            AssemblerPath = $"{Path.Combine(HeaderRepositoryPath, "deps/CodeWarrior/mwasmeppc.exe")}";
        }
        else
        {
            LinkerPath = $"{Path.Combine(HeaderRepositoryPath, "deps/Kamek/Kamek")}";
            CompilerPath = $"{Path.Combine(HeaderRepositoryPath, "deps/CodeWarrior/mwcceppc")}";
            AssemblerPath = $"{Path.Combine(HeaderRepositoryPath, "deps/CodeWarrior/mwasmeppc")}";
        }
        if (!File.Exists(CompilerPath))
        {
            Error(new MissingMethodException($"Could not locate CodeWarrior C++ Compiler at \"{CompilerPath}\""));
            return;
        }
        if (!File.Exists(AssemblerPath))
        {
            Error(new MissingMethodException($"Could not locate CodeWarrior PPC Assembler at \"{AssemblerPath}\""));
            return;
        }
        if (!File.Exists(LinkerPath))
        {
            Error(new MissingMethodException($"Could not locate Kamek Linker at \"{LinkerPath}\""));
            return;
        }
        #endregion


        #region Collect Modules for compiling
        List<ModuleInfo> Modules = [];
        int LoadedModuleNum = 0;

        // Find all modules, including ones from shortcuts and symlinks
        Console.Write("Loading Modules...");
        string[] DirectoriesInsideModules = Directory.GetDirectories(ModuleFolderPath, "*", SearchOption.TopDirectoryOnly);
        string[] ShortcutsInsideModules = Directory.GetFiles(ModuleFolderPath, "*.lnk", SearchOption.TopDirectoryOnly); //Shortcuts to module folders
        string[] SymLinksInsideModules = Directory.GetFiles(ModuleFolderPath, "*.", SearchOption.TopDirectoryOnly); //Symlinks to module folders
        Console.WriteLine($"{DirectoriesInsideModules.Length + ShortcutsInsideModules.Length + SymLinksInsideModules.Length} found");

        // This feature lets you ignore modules inside the modules folder. Suggested by Bavario & VTXG.
        string[] ModuleIgnore = [];
        string IgnoreFilePath = Path.Combine(ModuleFolderPath, ".moduleignore");
        if (File.Exists(IgnoreFilePath))
            ModuleIgnore = File.ReadAllLines(IgnoreFilePath);

        // Load the modules
        for (int i = 0; i < DirectoriesInsideModules.Length; i++)
            TryLoadModule(DirectoriesInsideModules[i]);

        for (int i = 0; i < ShortcutsInsideModules.Length; i++)
        {
            string? Target = Utility.GetShortcutTarget(ShortcutsInsideModules[i]);
            if (Target is null || !File.GetAttributes(Target).HasFlag(FileAttributes.Directory))
            {
                Console.WriteLine($"Failed to load module: \"{Target ?? ShortcutsInsideModules[i]}\"");
                continue;
            }

            TryLoadModule(Target);
        }

        for (int i = 0; i < SymLinksInsideModules.Length; i++)
        {
            string[] SymLinks;
            try
            {
                SymLinks = File.ReadAllLines(SymLinksInsideModules[i]);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var str in SymLinks)
            {
                string t = str;
                if (t.StartsWith('.'))
                {
                    string pt = Path.Combine(ModuleFolderPath, t);
                    t = Path.GetFullPath(new Uri(pt).LocalPath);
                }
                TryLoadModule(t);
            }
        }



        void TryLoadModule(string ModulePath)
        {
            string pth = ModulePath.PathSanitize();
            for (int x = 0; x < Modules.Count; x++)
            {
                if (Modules[x].FolderPath.Equals(pth)) //Duplicate
                {
                    //Console.WriteLine($"Duplicate Module: \"{pth}\"");
                    return;
                }
            }

            // Check for Ignores
            string pthName = Path.GetFileName(pth);
            if (ModuleIgnore.Contains(pthName))
            {
                Console.WriteLine($"{pthName} Ignored by .moduleignore");
                return;
            }

            ModuleInfo? Info = ModuleInfo.Load(pth);
            if (Info is null)
            {
                Console.WriteLine($"Failed to load module: \"{pth}\"");
                return;
            }

            if (Info.SupportedGames is null)
                Console.WriteLine("!! WARNING !! ^^^ does not have SupportedGames defined. This will not be supported in the future!");
            else
            {
                foreach (string sg in Info.SupportedGames)
                {
                    if (!sg.Equals(BuildTarget) && // Supports all regions of the build target [Example: SB4]
                        !sg.Equals(BuildTarget + RegionTarget) && // Supports the specific region [Example: SB4E]
                        !sg.Equals(BuildTarget + '-' + VersionTarget.ToString()) && // Supports all regions of the specific revision [Example: SB4-0]
                        !sg.Equals(BuildTarget + RegionTarget + '-' + VersionTarget.ToString()) // Supports the specific region and revision [Example: SB4E-0]
                        )
                        continue;
                    goto LoadModule; // Rare goto moment
                }
                Console.WriteLine($"\t^^^ Module doesn't support {args[0]} and will be ignored.");
                return;
            }
        LoadModule:

            if (Info.ModuleDependancies is not null || Info.ModuleOptionalDependancies is not null)
            {
                Console.WriteLine("!! WARNING !! ModuleDependancies and ModuleOptionalDependancies are OBSOLETE, replaced with RequiredAPIs and OptionalAPIs respectively.");
            }

            Modules.Add(Info);
            LoadedModuleNum++;
        }
        #endregion


        Console.WriteLine($"{LoadedModuleNum} modules loaded!");

        // Verify that all the needed Module APIs are present
        ModuleUtility.VerifyAllModuleAPIUsage(Modules);

        // Perform CodeGen code generation
        ModuleUtility.PerformAllModuleCodeGen(Modules);

        // Gather the include paths that apply to all modules.
        string[] IncludePaths = [
            "\"" + Path.Combine(HeaderRepositoryPath, "include") + "\""
        ];


        // Gather the compiler flags, including user flags
        List<string> FlagSet = [$"-D{BuildTarget}", $"-D{BuildTarget}{RegionTarget}"];
        if (VersionTarget >= 0)
            FlagSet.Add($"-DREV_{VersionTarget}");

        // WARNING: This will be going away eventually!!!
        switch (RegionTarget)
        {
            case 'E':
                FlagSet.Add($"-DUSA");
                break;
            case 'P':
                FlagSet.Add($"-DPAL");
                break;
            case 'J':
                FlagSet.Add($"-DJPN");
                break;
            case 'K':
                FlagSet.Add($"-DKOR");
                break;
            case 'W':
                FlagSet.Add($"-DTWN");
                break;
            default:
                break; // ???
        }


        for (int i = 4; i < args.Length; i++)
        {
            if (args[i].StartsWith("-DUSR_"))
                FlagSet.Add(args[i]);
        }
        Console.WriteLine();


        // Actually do the compile tasks
        List<string> AllObjectOutputs = [];
        string[] Flags = [.. FlagSet];
        if (args.Any(o => o.Equals("-u")))
            ModuleUtility.CompileAllUnibuild(Modules, Flags, IncludePaths, HeaderRepositoryPath, args[3].PathSanitize(), ref AllObjectOutputs);
        else
            ModuleUtility.CompileAllModules(Modules, Flags, IncludePaths, HeaderRepositoryPath, ref AllObjectOutputs);


        // If we made it here, we have a successful compile. Hooray!
        // I hope linking works...

        Console.WriteLine();


        string FinalRegionRevString = $"{BuildTarget}{RegionTarget}";
        if (VersionTarget >= 0)
            FinalRegionRevString += $"_REV{VersionTarget}";
        List<string> SymbolPaths =
        [
            Path.Combine(HeaderRepositoryPath, "symbols").PathSanitize(),
            .. ModuleUtility.CollectModuleSymbols(Modules)
        ];
        List<string> Externals = [];
        for (int y = 0; y < SymbolPaths.Count; y++)
        {
            string path = $"{SymbolPaths[y]}/{FinalRegionRevString}.txt"; // Symbols for the specific game/region/revision
            if (File.Exists(path))
                Externals.Add($"-externals=\"{path}\"");

            path = $"{SymbolPaths[y]}/{BuildTarget}{RegionTarget}.txt"; // Symbols for the specific gmae/region (all revisions)
            if (File.Exists(path))
                Externals.Add($"-externals=\"{path}\"");

            path = $"{SymbolPaths[y]}/{BuildTarget}.txt"; // Symbols for the specific game (all regions, all revisions)
            if (File.Exists(path))
                Externals.Add($"-externals=\"{path}\"");

            // WARNING: This will be going away eventually!!!
            switch (RegionTarget)
            {
                case 'E':
                    path = $"{SymbolPaths[y]}/USA.txt";
                    if (File.Exists(path))
                        Externals.Add($"-externals=\"{path}\"");
                    break;
                case 'P':
                    path = $"{SymbolPaths[y]}/PAL.txt";
                    if (File.Exists(path))
                        Externals.Add($"-externals=\"{path}\"");
                    break;
                case 'J':
                    path = $"{SymbolPaths[y]}/JPN.txt";
                    if (File.Exists(path))
                        Externals.Add($"-externals=\"{path}\"");
                    break;
                case 'K':
                    path = $"{SymbolPaths[y]}/KOR.txt";
                    if (File.Exists(path))
                        Externals.Add($"-externals=\"{path}\"");
                    break;
                case 'W':
                    path = $"{SymbolPaths[y]}/TWN.txt";
                    if (File.Exists(path))
                        Externals.Add($"-externals=\"{path}\"");
                    break;
                default:
                    break; // ???
            }
        }
        string MapFile = $"-output-map=\"{Path.Combine(args[3], $"CustomCode_{FinalRegionRevString}.map").PathSanitize()}\"";
        string Output = $"-output-kamek=\"{Path.Combine(args[3], $"CustomCode_{FinalRegionRevString}.bin").PathSanitize()}\"";
        string LinkerCommand = $"{string.Join(" ", AllObjectOutputs)} {string.Join(" ", Externals)} {Output} {MapFile}";
        int result = Utility.LaunchProcess(LinkerPath, LinkerCommand);

        if (result != 0)
        {
            Error(new InvalidOperationException("Linker Failure"));
            return;
        }

        if (args.Any(o => o.Equals("-c")))
        {
            string[] lines = ModuleUtility.CollectAllModuleAuthors(Modules);
            string path = Path.Combine(args[3], $"ModuleAuthors_{FinalRegionRevString}.txt").PathSanitize();
            File.WriteAllLines(path, lines);
        }

        Console.WriteLine("Complete!");
    }

    static void Help() => Console.WriteLine(
            """
            SyatiModuleBuildTool.exe <BUILD_TARGET> <HEADER_REPOSITORY> <MODULES_FOLDER> <OUTPUT_FOLDER>

            Extra options:
            -u      Enable UniBuild. UniBuild can shrink the final .bin file size at the potential cost of debuggability. Should only be used when you have a lot of modules. (roughly 10 or more)
            -c      Outputs a text file with modules and their authors.
            """);
    static void Error(Exception ex) => throw ex;
}