namespace EGM.Core.Entities
{
    public class InstallRecord
    {
        public DateTime TimestampUtc { get; set; }
        public Version PreviousVersion { get; set; } = new(0, 0, 0);
        public Version InstalledVersion { get; set; } = new(0, 0, 0);
        public bool RolledBack { get; set; }
    }
}