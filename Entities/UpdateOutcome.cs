namespace EGM.Core.Entities
{
    /// <summary>
    /// How an update attempt ended. Only <see cref="Installed"/> leaves the machine
    /// on a new version; the other two leave it on the version it started on.
    /// </summary>
    public enum UpdateStatus
    {
        /// <summary>The package validated, the pre-install hook passed, and the new version is live.</summary>
        Installed,

        /// <summary>Nothing changed - empty input, the machine was not IDLE, or the package was rejected.</summary>
        Rejected,

        /// <summary>The install failed part-way through and the previous version was restored.</summary>
        RolledBack
    }

    /// <summary>
    /// The result of <see cref="Interfaces.IUpdateManager.InstallPackage"/>.
    ///
    /// This type exists because the method used to return <c>void</c>. A failed
    /// install was therefore only observable by reading the log text, and the HTTP
    /// endpoint had nothing to report but "accepted" - it answered
    /// <c>success = true</c> even when the package had just been rolled back.
    /// Returning an outcome lets every caller distinguish installed / rejected /
    /// rolled back without parsing log lines.
    /// </summary>
    /// <param name="Status">Which of the three endings this was.</param>
    /// <param name="Message">A one-line, human-readable summary suitable for a CLI or an API response.</param>
    /// <param name="PreviousVersion">
    /// The version the machine was on before the attempt. On a rollback this is also
    /// the version it is on afterwards.
    /// </param>
    /// <param name="InstalledVersion">The version now live, or <c>null</c> if nothing was installed.</param>
    public sealed record UpdateOutcome(
        UpdateStatus Status,
        string Message,
        Version? PreviousVersion = null,
        Version? InstalledVersion = null)
    {
        public bool Succeeded => Status == UpdateStatus.Installed;

        public static UpdateOutcome Installed(Version previous, Version installed) =>
            new(UpdateStatus.Installed,
                $"Installed {installed} (was {previous}).",
                previous,
                installed);

        public static UpdateOutcome Rejected(string reason) =>
            new(UpdateStatus.Rejected, reason);

        public static UpdateOutcome RolledBackFrom(Version restored, Version attempted, string reason) =>
            new(UpdateStatus.RolledBack,
                $"Install of {attempted} failed and was rolled back to {restored}: {reason}",
                restored,
                null);
    }
}
