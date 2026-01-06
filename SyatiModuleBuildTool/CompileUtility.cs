using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SyatiModuleBuildTool;

public static class CompileUtility
{
    //TODO: Something to consider would be to allow modules to define regions
    //private static readonly string[] REGIONS = ["PAL", "USA", "JPN", "TWN", "KOR"];
    //public static bool ValidateRegion(string Region) => REGIONS.Contains(Region);

    //TODO: It would probably be a good idea to let the modules define these compiler flags...
    static readonly string[] CompilerFlags =
    [
        "-c",
        "-Cpp_exceptions off",
        "-nodefaults",
        "-proc gekko",
        "-fp hard",
        "-lang=c++",
        "-O4,s",
        "-inline on",
        "-rtti off",
        "-sdata 0",
        "-sdata2 0",
        "-align powerpc",
        "-func_align 4",
        "-enum int",
        "-DGEKKO",
        "-DMTX_USE_PS",
    ];

    //TODO: It would probably be a good idea to let the modules define these assembler flags...
    static readonly string[] AssemblerFlags =
    [
        "-c",
        "-proc gekko",
    ];


    public static void Compile(string Flags, string Includes, List<(string source, string build)> CompilerTasks, List<(string source, string build)> AssemblerTasks, string SyatiFolderPath)
    {
        string Compiler, Assembler;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            Compiler = $"{Path.Combine(SyatiFolderPath, "deps/CodeWarrior/mwcceppc.exe")}";
            Assembler = $"{Path.Combine(SyatiFolderPath, "deps/CodeWarrior/mwasmeppc.exe")}";
        } else {
            Compiler = $"{Path.Combine(SyatiFolderPath, "deps/CodeWarrior/mwcceppc")}";
            Assembler = $"{Path.Combine(SyatiFolderPath, "deps/CodeWarrior/mwasmeppc")}";
        }
        string CompileCommand = $"{string.Join(" ", CompilerFlags)} {Includes}";
        string AssembleCommand = $"{string.Join(" ", CompilerFlags)} {Includes}";
        

        for (int i = 0; i < CompilerTasks.Count; i++)
        {
            Console.WriteLine($"Compiling {CompilerTasks[i].source}");
            string dir = new FileInfo(CompilerTasks[i].build).DirectoryName ?? throw new Exception();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            if (Utility.LaunchProcess(Compiler, $"{CompileCommand} {Flags} \"{CompilerTasks[i].source}\" -o \"{CompilerTasks[i].build}\"") != 0)
            {
                throw new Exception($"Failed to compile \"{CompilerTasks[i].source}\"");
            }
        }
        for (int i = 0; i < AssemblerTasks.Count; i++)
        {
            Console.WriteLine($"Assembling {AssemblerTasks[i].source}");
            string dir = new FileInfo(AssemblerTasks[i].build).DirectoryName ?? throw new Exception();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            if (Utility.LaunchProcess(Assembler, $"{AssembleCommand} {Flags} \"{AssemblerTasks[i].source}\" -o \"{AssemblerTasks[i].build}\"") != 0)
            {
                throw new Exception($"Failed to assemble \"{AssemblerTasks[i].source}\"");
            }
        }
    }
}
