using EGM.Core.Interfaces;
namespace EGM.Core.Validators
{
    public class PackageValidator : IPackageValidator
    {
        public bool TryValidateAndExtractVersion(string packagePath, Version currentVersion, out Version newVersion, out string errorMessage)
        {
            errorMessage = string.Empty;
            newVersion = default;
            if (!File.Exists(packagePath))
            {
                errorMessage = $"Package file: {packagePath}, does not exist.";
            }
            else
            {
                string fileName = Path.GetFileNameWithoutExtension(packagePath);
                var parts = fileName.Split('_');

                if (parts.Length < 3)
                    errorMessage = "Invalid package format. Expected update_pkg_x.y.z";

                else if (!Version.TryParse(parts[^1], out  newVersion))
                   errorMessage =  "Invalid version format.";

                else if (newVersion <= currentVersion)
                    errorMessage = "Downgrade or same version not allowed.";
            }
            return errorMessage == string.Empty;
        }
    }
}
