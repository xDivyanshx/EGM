
namespace EGM.Core.Interfaces
{
    public interface IPackageValidator
    {
        bool TryValidateAndExtractVersion(string packagePath, Version currentVersion, out Version newVersion, out string errMessage);
    }
}
