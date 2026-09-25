using System;
using System.Collections.Generic;

namespace Devvio.Archiver.Core
{
    /// <summary>A single entry (file or directory) inside an archive.</summary>
    public sealed class ArchiveEntry
    {
        public int Index;
        public string Path;
        public long Size;
        public long PackedSize;
        public DateTime? Modified;
        public bool IsDirectory;
        public bool IsEncrypted;
        /// <summary>True when the entry is a synthesized directory row (no explicit entry in the archive).</summary>
        public bool IsSynthetic;
    }

    /// <summary>Result of listing an archive.</summary>
    public sealed class ArchiveInfo
    {
        public string ArchivePath;
        public ArchiveFormatKind Kind;
        public string EngineName;
        public bool IsEncrypted;
        public readonly List<ArchiveEntry> Entries = new List<ArchiveEntry>();
        public long TotalSize;
        public long TotalPacked;
        public int FileCount;
        public int DirectoryCount;

        public void RecalculateTotals()
        {
            TotalSize = 0;
            TotalPacked = 0;
            FileCount = 0;
            DirectoryCount = 0;
            bool encrypted = false;
            foreach (ArchiveEntry entry in Entries)
            {
                if (entry.IsDirectory)
                {
                    DirectoryCount++;
                }
                else
                {
                    FileCount++;
                    TotalSize += entry.Size;
                    TotalPacked += entry.PackedSize;
                }
                if (entry.IsEncrypted)
                {
                    encrypted = true;
                }
            }
            IsEncrypted = IsEncrypted || encrypted;
        }
    }

    /// <summary>What to do when an extracted file already exists on disk.</summary>
    public enum OverwritePolicy
    {
        Overwrite,
        AutoRename,
        Skip
    }

    /// <summary>Options for extracting an archive.</summary>
    public sealed class ExtractOptions
    {
        public string Destination;
        public OverwritePolicy Overwrite = OverwritePolicy.AutoRename;
        public string Password;
        /// <summary>Entry paths to extract; null means extract everything.</summary>
        public List<string> OnlyEntries;
        /// <summary>Automatically unpack the intermediate .tar of .tar.gz/.tar.bz2/.tar.xz archives.</summary>
        public bool UnwrapNestedTar = true;
        public bool OpenDestinationWhenDone;
    }

    /// <summary>Options for creating an archive.</summary>
    public sealed class CreateOptions
    {
        public string ArchivePath;
        public ArchiveFormatKind Kind = ArchiveFormatKind.SevenZip;
        /// <summary>Compression level 0 (store) .. 9 (ultra).</summary>
        public int Level = 5;
        public string Password;
        /// <summary>7z only: also encrypt file/directory names.</summary>
        public bool EncryptFileNames = true;
        /// <summary>Split volume size, e.g. "100m"; null for a single archive.</summary>
        public string SplitVolume;
        /// <summary>7z only: solid archive.</summary>
        public bool Solid = true;
        /// <summary>7z: LZMA2/LZMA/PPMd/BZip2/Deflate - Zip: Deflate/Deflate64/BZip2/LZMA/PPMd.</summary>
        public string Method = "LZMA2";
        /// <summary>Dictionary size for LZMA/LZMA2, e.g. "64m"; null = engine default.</summary>
        public string DictionarySize;
        /// <summary>Directory the relative source paths are based on.</summary>
        public string WorkDirectory;
        /// <summary>Items (relative to WorkDirectory) to add.</summary>
        public List<string> RelativeSources = new List<string>();
        /// <summary>When true RelativeSources contain absolute paths (no common root exists).</summary>
        public bool UseFullPaths;
    }

    /// <summary>
    /// Called by engines when an encrypted archive needs a password.
    /// Return the password, or null to cancel the operation.
    /// </summary>
    public delegate string ArchivePasswordProvider(string archivePath, bool previousAttemptFailed);

    /// <summary>Progress / cancellation hub shared between the UI and the engines.</summary>
    public sealed class OperationContext
    {
        private readonly Action<int?> percentChanged;
        private readonly Action<string> fileChanged;
        private readonly Action<string> messageChanged;

        public volatile bool CancelRequested;

        public OperationContext(Action<int?> percentChanged, Action<string> fileChanged, Action<string> messageChanged)
        {
            this.percentChanged = percentChanged;
            this.fileChanged = fileChanged;
            this.messageChanged = messageChanged;
        }

        public void ReportPercent(int? value)
        {
            var handler = percentChanged;
            if (handler != null)
            {
                try { handler(value); }
                catch { }
            }
        }

        public void ReportFile(string path)
        {
            var handler = fileChanged;
            if (handler != null && !string.IsNullOrEmpty(path))
            {
                try { handler(path); }
                catch { }
            }
        }

        public void ReportMessage(string text)
        {
            var handler = messageChanged;
            if (handler != null && !string.IsNullOrEmpty(text))
            {
                try { handler(text); }
                catch { }
            }
        }

        public void ThrowIfCancelled()
        {
            if (CancelRequested)
            {
                throw new OperationCancelledException();
            }
        }
    }

    [Serializable]
    public class OperationCancelledException : Exception
    {
        public OperationCancelledException() : base("Operation cancelled.") { }
        public OperationCancelledException(string message) : base(message) { }
    }

    [Serializable]
    public class ArchivePasswordException : Exception
    {
        public ArchivePasswordException(string message) : base(message) { }
    }

    [Serializable]
    public class EngineException : Exception
    {
        public EngineException(string message) : base(message) { }
        public EngineException(string message, Exception inner) : base(message, inner) { }
    }
}
