namespace Core.Settings.Concrete
{
    /// <summary>Restart-scoped startup controls; defaults preserve existing operation.</summary>
    public sealed class StartupExecutionOptions
    {
        public const string SectionName = "StartupExecution";

        public bool MigrationsEnabled { get; set; } = true;
        public bool SeedsEnabled { get; set; } = true;
        public bool ManitouSyncEnabled { get; set; } = true;
        public bool IsolatedRehearsalMode { get; set; }
    }
}
