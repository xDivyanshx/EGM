using EGM.Core.Interfaces;
namespace EGM.Core.Validators
{
    public class PackageValidator : IPackageValidator
    {
        public bool TryValidateAndExtractVersion(string packagePath, Version currentVersion, out Version newVersion, out string errorMessage)
        {
            errorMessage = string.Empty;
            // Non-null placeholder so the non-nullable 'out Version' contract is always
            // satisfied. It's only overwritten with a real value on the success path;
            // on any failure path errorMessage is non-empty, so callers ignore newVersion.
            newVersion = new Version(0, 0);
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

                else if (!Version.TryParse(parts[^1], out Version? parsedVersion))
                   errorMessage =  "Invalid version format.";

                else if (parsedVersion <= currentVersion)
                    errorMessage = "Downgrade or same version not allowed.";

                else
                    newVersion = parsedVersion;
            }
            return errorMessage == string.Empty;
        }
    }
}
