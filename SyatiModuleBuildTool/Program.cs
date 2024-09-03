using System.Diagnostics;
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

        string SyatiFolderPath = args[1].Replace("\\", "/");
        string ModuleFolderPath = args[2].Replace("\\", "/");


        List<ModuleInfo> Modules = [];

        //Find all modules
        Console.Write("Loading Modules...");
        string[] DirectoriesInsideModules = Directory.GetDirectories(ModuleFolderPath, "*", SearchOption.TopDirectoryOnly);
        string[] ShortcutsInsideModules = Directory.GetFiles(ModuleFolderPath, "*.lnk", SearchOption.TopDirectoryOnly); //Shortcuts to module folders
        string[] SymLinksInsideModules = Directory.GetFiles(ModuleFolderPath, "*.", SearchOption.TopDirectoryOnly); //Symlinks to module folders
        Console.WriteLine($"{DirectoriesInsideModules.Length + ShortcutsInsideModules.Length} found");

        int LoadedModuleNum = 0;
        for (int i = 0; i < DirectoriesInsideModules.Length; i++)
        {
            TryLoadModule(DirectoriesInsideModules[i]);
        }

        for (int i = 0; i < ShortcutsInsideModules.Length; i++)
        {
            string Target = Utility.GetShortcutTarget(ShortcutsInsideModules[i]);
            if (!File.GetAttributes(Target).HasFlag(FileAttributes.Directory))
            {
                Console.WriteLine($"Failed to load module: \"{Target}\"");
                continue;
            }

            TryLoadModule(Target);
        }

        for (int i = 0; i < SymLinksInsideModules.Length; i++)
        {
            string t;
            try
            {
                t = File.ReadAllText(SymLinksInsideModules[i]);
                if (t.StartsWith('.'))
                {
                    string pt = Path.Combine(ModuleFolderPath, t);
                    t = Path.GetFullPath(new Uri(pt).LocalPath);
                }
            }
            catch (Exception)
            {
                continue;
            }

            TryLoadModule(t);
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
            "\"" + Path.Combine(SyatiFolderPath, "include") + "\""
        ];
        string[] Flags = [
            $"-D{args[0]}"
        ];
        Console.WriteLine();
        ModuleUtility.CompileAllModules(Modules, Flags, IncludePaths, SyatiFolderPath, ref AllObjectOutputs);

        // If we made it here, we have a successful compile. Hooray!
        // I hope linking works...

        Console.WriteLine();
        Console.WriteLine("Linking...");
        string Kamek;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            Kamek = $"{Path.Combine(SyatiFolderPath, "deps/Kamek/Kamek.exe")}";
        } else {
            Kamek = $"{Path.Combine(SyatiFolderPath, "deps/Kamek/Kamek")}";
        }
        List<string> SymbolPaths =
        [
            Path.Combine(SyatiFolderPath, "symbols"),
        ];
        SymbolPaths.AddRange(ModuleUtility.CollectModuleSymbols(Modules));
        string Symbols = "";
        for (int y = 0; y < SymbolPaths.Count; y++)
        {
            Symbols += $"-externals=\"{SymbolPaths[y]}/{args[0]}.txt\" ";
        }
        string MapFile = $"-output-map=\"{Path.Combine(args[3], $"CustomCode_{args[0]}.map")}\"";
        string Output = $"-output-kamek=\"{Path.Combine(args[3], $"CustomCode_{args[0]}.bin")}\"";
        int result = CompileUtility.LaunchProcess(Kamek, $"{string.Join(" ", AllObjectOutputs)} {Symbols} {Output} {MapFile}");

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
            SyatiModuleBuildTool.exe <REGION> <Path_To_Syati_Repo> <Path_To_Modules_Folder> <Path_To_Output_Folder>
            """);
    }
    static void Error(Exception ex)
    {
        throw ex;
    }
}