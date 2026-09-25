namespace Devvio.Archiver.Core.Engines
{
    /// <summary>
    /// An archive engine performs listing, extraction, creation and testing of archives.
    /// All members are synchronous; callers run them on a worker thread and use
    /// <see cref="OperationContext"/> for progress and cancellation.
    /// </summary>
    public interface IArchiveEngine
    {
        string Name { get; }

        bool IsAvailable { get; }

        bool CanRead(ArchiveFormatKind kind);

        bool CanCreate(ArchiveFormatKind kind);

        ArchiveInfo List(string archivePath, ArchivePasswordProvider passwordProvider);

        void Extract(string archivePath, ExtractOptions options, ArchivePasswordProvider passwordProvider, OperationContext context);

        void Create(CreateOptions options, OperationContext context);

        void Test(string archivePath, ArchivePasswordProvider passwordProvider, OperationContext context);
    }

    /// <summary>
    /// Chooses the best engine for a job. The 7-Zip command line tool covers the widest
    /// set of codecs; the managed SharpCompress engine is the always-available fallback.
    /// </summary>
    public static class ArchiveEngineSelector
    {
        private static readonly SevenZipCliEngine CliEngine = new SevenZipCliEngine();
        private static readonly SharpCompressEngine ManagedEngine = new SharpCompressEngine();

        public static bool HasCliEngine
        {
            get { return CliEngine.IsAvailable; }
        }

        public static IArchiveEngine Cli
        {
            get { return CliEngine; }
        }

        public static IArchiveEngine Managed
        {
            get { return ManagedEngine; }
        }

        public static IArchiveEngine ForReading(ArchiveFormatKind kind)
        {
            if (CliEngine.IsAvailable && CliEngine.CanRead(kind))
            {
                return CliEngine;
            }
            if (ManagedEngine.CanRead(kind))
            {
                return ManagedEngine;
            }
            return CliEngine; // produces the best error message
        }

        public static IArchiveEngine ForCreating(ArchiveFormatKind kind)
        {
            if (CliEngine.IsAvailable && CliEngine.CanCreate(kind))
            {
                return CliEngine;
            }
            if (ManagedEngine.CanCreate(kind))
            {
                return ManagedEngine;
            }
            return CliEngine; // produces the best error message
        }
    }
}
