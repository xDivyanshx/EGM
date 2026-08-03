using EGM.Core.Entities;

namespace EGM.Core.Interfaces
{
    public interface IInstallHistoryStore
    {
        void RecordInstall(Version previousVersion, Version newVersion);
        void RecordRollback(Version targetVersion);
        IReadOnlyCollection<InstallRecord> GetHistory();
    }
}
