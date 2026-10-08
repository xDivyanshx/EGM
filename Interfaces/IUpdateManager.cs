using EGM.Core.Entities;

namespace EGM.Core.Interfaces
{
    public interface IUpdateManager
    {
        /// <summary>
        /// Attempts a transactional install of <paramref name="packagePath"/>.
        /// Never throws for an expected failure - validation errors, a machine that
        /// is not IDLE, and a failed install that rolled back all come back as an
        /// <see cref="UpdateOutcome"/>.
        /// </summary>
        UpdateOutcome InstallPackage(string packagePath);
    }
}
