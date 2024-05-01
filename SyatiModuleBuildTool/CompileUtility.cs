using System.Diagnostics;

namespace SyatiModuleBuildTool;

public static class CompileUtility
{
    //TODO: Something to consider would be to allow modules to define regions
    private static readonly string[] REGIONS = ["PAL", "USA", "JPN", "TWN", "KOR"];
    public static bool ValidateRegion(string Region) => REGIONS.Contains(Region);

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
        string Compiler = $"{Path.Combine(SyatiFolderPath, "deps/CodeWarrior/mwcceppc.exe")}";
        string Assembler = $"{Path.Combine(SyatiFolderPath, "deps/CodeWarrior/mwasmeppc.exe")}";


        string CompileCommand = $"{string.Join(" ", CompilerFlags)} {Includes}";
        string AssembleCommand = $"{string.Join(" ", CompilerFlags)} {Includes}";

        for (int i = 0; i < CompilerTasks.Count; i++)
        {
            Console.WriteLine($"Compiling {CompilerTasks[i].source}");
            string dir = new FileInfo(CompilerTasks[i].build).DirectoryName ?? throw new Exception();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            if (LaunchProcess(Compiler, $"{CompileCommand} {Flags} \"{CompilerTasks[i].source}\" -o \"{CompilerTasks[i].build}\"") != 0)
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
            if (LaunchProcess(Assembler, $"{AssembleCommand} {Flags} \"{AssemblerTasks[i].source}\" -o \"{AssemblerTasks[i].build}\"") != 0)
            {
                throw new Exception($"Failed to assemble \"{AssemblerTasks[i].source}\"");
            }
        }
    }


    public static int LaunchProcess(string Program, string Args)
    {
        Process process = new()
        {
            EnableRaisingEvents = true
        };
        process.OutputDataReceived += new DataReceivedEventHandler(Process_OutputDataReceived);
        process.ErrorDataReceived += new DataReceivedEventHandler(Process_ErrorDataReceived);
        process.Exited += new EventHandler(Process_Exited);

        process.StartInfo.FileName = Program;
        process.StartInfo.Arguments = Args;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        //below line is optional if we want a blocking call
        process.WaitForExit();
        return process.ExitCode;
    }

    static void Process_Exited(object? sender, EventArgs e)
    {
        //Console.WriteLine(string.Format("process exited with code {0}\n", process.ExitCode.ToString()));
    }

    static void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if ((e.Data?.Length ?? 0) > 0)
            Console.WriteLine(e.Data);
    }

    static void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if ((e.Data?.Length ?? 0) > 0)
            Console.WriteLine(e.Data);
    }
}
