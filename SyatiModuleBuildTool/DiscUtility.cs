namespace SyatiModuleBuildTool;

public static class DiscUtility {
    public static void CopyAllFiles(List<ModuleInfo> modules, string output) {
        foreach (ModuleInfo module in modules)
            CopyFiles(module, output);
    }

    public static void CopyFiles(ModuleInfo module, string output) {
        var sourceDiscFolder = Path.Combine(module.FolderPath, "disc");

        if (!Directory.Exists(sourceDiscFolder))
            return;

        Console.WriteLine($"Copying files from {sourceDiscFolder}");

        var discPaths = Directory.GetFiles(sourceDiscFolder, "*", SearchOption.AllDirectories);

        foreach (var sourcePath in discPaths) {
            var relativePath = Path.GetRelativePath(sourceDiscFolder, sourcePath);
            var targetPath = Path.Combine(output, relativePath);

            try {
                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (targetDirectory is not null)
                    Directory.CreateDirectory(targetDirectory);

                File.Copy(sourcePath, targetPath, true);
            }
            catch (Exception e) {
                Console.WriteLine($"Error while copying \"{sourceDiscFolder}\": {e.Message}");
            }
        }
    }
}
