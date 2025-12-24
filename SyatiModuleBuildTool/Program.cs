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

        if (!CompileUtility.ValidateRegion(args[0]))
        {
            Error(new ArgumentException($"Invalid region {args[0]}", nameof(args)));
            return;
        }

        string HeaderRepositoryPath = args[1].Replace("\\", "/");
        string ModuleFolderPath = args[2].Replace("\\", "/");
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


        List<ModuleInfo> Modules = [];

        //Find all modules
        Console.Write("Loading Modules...");
        string[] DirectoriesInsideModules = Directory.GetDirectories(ModuleFolderPath, "*", SearchOption.TopDirectoryOnly);
        string[] ShortcutsInsideModules = Directory.GetFiles(ModuleFolderPath, "*.lnk", SearchOption.TopDirectoryOnly); //Shortcuts to module folders
        string[] SymLinksInsideModules = Directory.GetFiles(ModuleFolderPath, "*.", SearchOption.TopDirectoryOnly); //Symlinks to module folders
        Console.WriteLine($"{DirectoriesInsideModules.Length + ShortcutsInsideModules.Length} found");

        // This feature lets you ignore modules inside the modules folder. Suggested by Bavario & VTXG.
        string[] ModuleIgnore = [];
        string IgnoreFilePath = Path.Combine(ModuleFolderPath, ".moduleignore");
        if (File.Exists(IgnoreFilePath))
            ModuleIgnore = File.ReadAllLines(IgnoreFilePath);

        int LoadedModuleNum = 0;
        for (int i = 0; i < DirectoriesInsideModules.Length; i++)
        {
            TryLoadModule(DirectoriesInsideModules[i]);
        }

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
            string pth = ModulePath.Replace("\\", "/");
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
            Modules.Add(Info);
            LoadedModuleNum++;
        }



        Console.WriteLine($"{LoadedModuleNum} modules loaded!");


        ModuleUtility.VerifyDependancies(Modules);
        ModuleUtility.PerformAllModuleCodeGen(Modules);

        List<string> AllObjectOutputs = [];
        string[] IncludePaths = [
            "\"" + Path.Combine(HeaderRepositoryPath, "include") + "\""
        ];
        List<string> FlagSet = [ $"-D{args[0]}" ];
        for (int i = 3; i < args.Length; i++)
        {
            if (args[i].StartsWith("-DUSR_"))
                FlagSet.Add(args[i]);
        }
        string[] Flags = [.. FlagSet];
        Console.WriteLine();
        if (args.Any(o => o.Equals("-u")))
            ModuleUtility.CompileAllUnibuild(Modules, Flags, IncludePaths, HeaderRepositoryPath, args[3], ref AllObjectOutputs);
        else
            ModuleUtility.CompileAllModules(Modules, Flags, IncludePaths, HeaderRepositoryPath, ref AllObjectOutputs);

        // If we made it here, we have a successful compile. Hooray!
        // I hope linking works...

        Console.WriteLine();
        Console.WriteLine("Linking...");

        List<string> SymbolPaths =
        [
            Path.Combine(HeaderRepositoryPath, "symbols"),
        ];
        SymbolPaths.AddRange(ModuleUtility.CollectModuleSymbols(Modules));
        string Symbols = "";
        for (int y = 0; y < SymbolPaths.Count; y++)
        {
            Symbols += $"-externals=\"{SymbolPaths[y]}/{args[0]}.txt\" ";
        }
        string MapFile = $"-output-map=\"{Path.Combine(args[3], $"CustomCode_{args[0]}.map")}\"";
        string Output = $"-output-kamek=\"{Path.Combine(args[3], $"CustomCode_{args[0]}.bin")}\"";
        int result = Utility.LaunchProcess(LinkerPath, $"{string.Join(" ", AllObjectOutputs)} {Symbols} {Output} {MapFile}");

        if (result != 0)
        {
            throw new InvalidOperationException("Linker Failure");
        }

        Console.WriteLine("Complete!");
    }

    static void Help()
    {
        Console.WriteLine(
            """
            SyatiModuleBuildTool.exe <REGION> <Path_To_Header_Repo> <Path_To_Modules_Folder> <Path_To_Output_Folder>

            Extra options:
            -u      Enable UniBuild. UniBuild can shrink the final .bin file size at the potential cost of debuggability. Should only be used when you have a lot of modules. (roughly 10 or more)
            """);
    }
    static void Error(Exception ex)
    {
        throw ex;
    }
}