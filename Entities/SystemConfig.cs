namespace EGM.Core.Entities
{
    public class SystemConfig
    {
        public Version CurrentVersion { get; set; } = new Version(1, 0, 0);
        public Version LastKnownGoodVersion { get; set; } = new Version(0, 0, 0);
        public string TimeZone { get; set; } = "UTC";
        public bool NtpEnabled { get; set; } = true;
    }
}