namespace EGM.Core.Infrastructure
{
    public static class FileFunctions
    {
        public static string LogDirectory
        {
            get
            {
                string projectRoot = FindProjectRoot();
                string dataPath = Path.Combine(projectRoot, "Logs");

                Directory.CreateDirectory(dataPath);

                return dataPath;
            }
        }

        /// <summary>
        /// Locates the repository root by walking up from the running assembly's
        /// directory until it finds the "EGM.Core.csproj" marker.
        ///
        /// The previous implementation hardcoded "..\..\.." relative to
        /// AppContext.BaseDirectory. That only lands on the root for a project whose
        /// output sits exactly three levels below it (e.g. the console app's
        /// bin\Debug\net8.0) - it broke for the EGM.Api project (bin one level deeper)
        /// and for any Release/published layout. Walking up to a known marker is robust
        /// regardless of which project or build configuration is running.
        /// </summary>
        private static string FindProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "EGM.Core.csproj")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            // Fallback: if the marker isn't found (unusual - e.g. a fully self-contained
            // publish that excludes the .csproj), fall back to the old three-levels-up
            // heuristic so behaviour degrades rather than throwing.
            return Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        }

        public static bool TryWriteFile(string filePath, string content, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                File.WriteAllText(filePath, content);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to write file: {ex.Message}";
                return false;
            }
        }
        public static bool TryReadFile(string filePath, out string content, out string errorMessage)
        {
            content = string.Empty;
            errorMessage = string.Empty;
            try
            {
                if (!File.Exists(filePath))
                {
                    errorMessage = "File does not exist.";
                    return false;
                }
                content = File.ReadAllText(filePath);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to read file: {ex.Message}";
                return false;
            }
        }
    }
}
