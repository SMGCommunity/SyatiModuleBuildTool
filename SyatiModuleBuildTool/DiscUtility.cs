namespace SyatiModuleBuildTool;

public static class DiscUtility {
    public static void CopyAllFiles(List<ModuleInfo> modules, string output) {
        foreach (ModuleInfo module in modules)
            CopyFiles(module, output);
    }

    public static void CopyFiles(ModuleInfo module, string output) {
        var discFolder = Path.Combine(module.FolderPath, "disc");

        if (!Directory.Exists(discFolder))
            return;

        Console.WriteLine($"Copying files from {discFolder}");

        var discPaths = Directory.GetFiles(discFolder, "*", SearchOption.AllDirectories);

        foreach (var sourcePath in discPaths) {
            var relativePath = Path.GetRelativePath(discFolder, sourcePath);
            var targetPath = Path.Combine(output, relativePath);

            try {
                if (Path.Exists(targetPath))
                    Console.WriteLine($" - File will replace {targetPath}");

                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (targetDirectory is not null)
                    Directory.CreateDirectory(targetDirectory);

                File.Copy(sourcePath, targetPath, true);
            }
            catch (Exception e) {
                Console.WriteLine($"Error while copying {discFolder}: {e.Message}");
            }
        }
    }
}
