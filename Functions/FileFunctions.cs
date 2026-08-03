namespace EGM.Core.Infrastructure
{
    public static class FileFunctions
    {
        public static string LogDirectory
        {
            get
            {
                string basePath = AppContext.BaseDirectory;

                string projectRoot = Path.GetFullPath(
                    Path.Combine(basePath, "..", "..", ".."));

                string dataPath = Path.Combine(projectRoot, "Logs");

                Directory.CreateDirectory(dataPath);

                return dataPath;
            }
        }

        public static bool TryWriteFile(string filePath, string content, out string errorMessage) { 
            errorMessage = string.Empty;
            try { 
                File.WriteAllText(filePath, content);
                return true; 
            } 
            catch (Exception ex) {
                errorMessage = $"Failed to write file: {ex.Message}";
                return false;
            } 
        } 
        public static bool TryReadFile(string filePath, out string content, out string errorMessage) { 
            content = string.Empty; 
            errorMessage = string.Empty;
            try {
                if (!File.Exists(filePath)) { 
                    errorMessage = "File does not exist."; 
                    return false; 
                } 
                content = File.ReadAllText(filePath);
                return true;
            } 
            catch (Exception ex) {
                errorMessage = $"Failed to read file: {ex.Message}";
                return false; 
            } 
        }
    }
}
