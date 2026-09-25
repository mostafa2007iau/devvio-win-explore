using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;
using SharpCompress.Writers.Zip;

namespace Devvio.Archiver.Core.Engines
{
    /// <summary>
    /// Pure managed (in-process) archive engine built on SharpCompress. Always available,
    /// even without 7z.exe. Reads zip/7z/rar/tar/gzip/bzip2/xz/arj/... and creates
    /// zip/7z/tar/tar.gz/tar.bz2/gzip/bzip2. Encrypted creation and split volumes
    /// require the 7-Zip engine.
    /// </summary>
    public sealed class SharpCompressEngine : IArchiveEngine
    {
        public string Name
        {
            get { return "Managed (SharpCompress)"; }
        }

        public bool IsAvailable
        {
            get { return true; }
        }

        public bool CanRead(ArchiveFormatKind kind)
        {
            switch (kind)
            {
                case ArchiveFormatKind.Zip:
                case ArchiveFormatKind.SevenZip:
                case ArchiveFormatKind.Rar:
                case ArchiveFormatKind.Tar:
                case ArchiveFormatKind.TarGz:
                case ArchiveFormatKind.TarBz2:
                case ArchiveFormatKind.TarXz:
                case ArchiveFormatKind.Gzip:
                case ArchiveFormatKind.BZip2:
                case ArchiveFormatKind.Xz:
                case ArchiveFormatKind.Jar:
                case ArchiveFormatKind.Arj:
                    return true;
                default:
                    return false;
            }
        }

        public bool CanCreate(ArchiveFormatKind kind)
        {
            switch (kind)
            {
                case ArchiveFormatKind.Zip:
                case ArchiveFormatKind.SevenZip:
                case ArchiveFormatKind.Tar:
                case ArchiveFormatKind.TarGz:
                case ArchiveFormatKind.TarBz2:
                case ArchiveFormatKind.Gzip:
                case ArchiveFormatKind.BZip2:
                    return true;
                default:
                    return false;
            }
        }

        #region Listing

        public ArchiveInfo List(string archivePath, ArchivePasswordProvider passwordProvider)
        {
            string password = passwordProvider != null ? passwordProvider(archivePath, false) : null;
            while (true)
            {
                try
                {
                    return ListOnce(archivePath, password);
                }
                catch (Exception ex) when (IsPasswordRelated(ex))
                {
                    if (passwordProvider == null)
                    {
                        throw new ArchivePasswordException("This archive is encrypted and needs a password.");
                    }
                    string next = passwordProvider(archivePath, true);
                    if (next == null)
                    {
                        throw new OperationCancelledException();
                    }
                    if (next == password)
                    {
                        throw new ArchivePasswordException("Wrong password.");
                    }
                    password = next;
                }
            }
        }

        private ArchiveInfo ListOnce(string archivePath, string password)
        {
            var info = new ArchiveInfo
            {
                ArchivePath = archivePath,
                Kind = ArchiveFormats.Detect(archivePath),
                EngineName = Name
            };

            try
            {
                using (IArchive archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions { Password = password }))
                {
                    foreach (IArchiveEntry entry in archive.Entries)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.Key))
                        {
                            continue;
                        }
                        AddListedEntry(info, entry.Key, entry.Size, entry.CompressedSize, entry.IsDirectory, entry.IsEncrypted, entry.LastModifiedTime);
                    }
                    info.IsEncrypted = archive.IsEncrypted || info.IsEncrypted;
                    info.RecalculateTotals();
                    return info;
                }
            }
            catch (Exception ex) when (!IsPasswordRelated(ex) && !(ex is OperationCancelledException))
            {
                // Formats that only support forward-only reading (tar.gz, tar.xz, lzw, ...)
                info.Entries.Clear();
                info.IsEncrypted = false;
                try
                {
                    using (IReader reader = ReaderFactory.OpenReader(archivePath, new ReaderOptions { Password = password }))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            IEntry entry = reader.Entry;
                            if (entry == null || string.IsNullOrEmpty(entry.Key))
                            {
                                continue;
                            }
                            AddListedEntry(info, entry.Key, entry.Size, entry.CompressedSize, entry.IsDirectory, entry.IsEncrypted, entry.LastModifiedTime);
                        }
                    }
                    info.RecalculateTotals();
                    return info;
                }
                catch (Exception inner) when (!IsPasswordRelated(inner) && !(inner is OperationCancelledException))
                {
                    throw new EngineException("Could not open the archive: " + inner.Message, inner);
                }
            }
        }

        private static void AddListedEntry(ArchiveInfo info, string key, long size, long packed, bool isDirectory, bool isEncrypted, DateTime? modified)
        {
            info.Entries.Add(new ArchiveEntry
            {
                Index = info.Entries.Count,
                Path = key,
                Size = size,
                PackedSize = packed,
                IsDirectory = isDirectory || key.EndsWith("/", StringComparison.Ordinal) || key.EndsWith("\\", StringComparison.Ordinal),
                IsEncrypted = isEncrypted,
                Modified = modified
            });
        }

        #endregion

        #region Extraction

        public void Extract(string archivePath, ExtractOptions options, ArchivePasswordProvider passwordProvider, OperationContext context)
        {
            if (options == null || string.IsNullOrEmpty(options.Destination))
            {
                throw new EngineException("No extraction destination was provided.");
            }
            string password = options.Password;
            if (password == null && passwordProvider != null)
            {
                password = passwordProvider(archivePath, false);
            }

            while (true)
            {
                context.ThrowIfCancelled();
                try
                {
                    ExtractOnce(archivePath, options, password, context);
                    if (options.UnwrapNestedTar)
                    {
                        UnwrapNestedTar(archivePath, options, context);
                    }
                    return;
                }
                catch (Exception ex) when (IsPasswordRelated(ex) && !(ex is OperationCancelledException))
                {
                    if (passwordProvider == null)
                    {
                        throw new ArchivePasswordException("This archive is encrypted and needs a password.");
                    }
                    string next = passwordProvider(archivePath, true);
                    if (next == null)
                    {
                        throw new OperationCancelledException();
                    }
                    if (next == password)
                    {
                        throw new ArchivePasswordException("Wrong password.");
                    }
                    password = next;
                }
            }
        }

        private void ExtractOnce(string archivePath, ExtractOptions options, string password, OperationContext context)
        {
            Directory.CreateDirectory(options.Destination);
            var extraction = new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true,
                PreserveFileTime = true,
                CheckCrc = true
            };

            try
            {
                using (IArchive archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions { Password = password }))
                {
                    if (options.OnlyEntries == null && options.Overwrite == OverwritePolicy.Overwrite)
                    {
                        // Fast path; SharpCompress handles solid archives optimally here.
                        archive.WriteToDirectory(options.Destination, extraction, new ProgressBridge(context));
                        return;
                    }

                    List<IArchiveEntry> entries = archive.Entries.Where(e => e != null && !string.IsNullOrEmpty(e.Key)).ToList();
                    long total = 0;
                    foreach (IArchiveEntry entry in entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            total += entry.Size;
                        }
                    }
                    long processed = 0;
                    foreach (IArchiveEntry entry in entries)
                    {
                        context.ThrowIfCancelled();
                        if (!ShouldExtract(options.OnlyEntries, entry.Key))
                        {
                            if (!entry.IsDirectory)
                            {
                                processed += entry.Size;
                            }
                            continue;
                        }
                        if (entry.IsDirectory)
                        {
                            ExtractDirectoryEntry(options, entry.Key);
                            continue;
                        }
                        context.ReportFile(entry.Key);
                        context.ReportPercent(total > 0 ? (int)(processed * 100 / total) : (int?)null);
                        ExtractFileEntry(entry.Key, entry.Size, options, delegate (string target)
                        {
                            entry.WriteToFile(target, extraction);
                        });
                        processed += entry.Size;
                    }
                    context.ReportPercent(100);
                }
            }
            catch (Exception ex) when (!IsPasswordRelated(ex) && !(ex is OperationCancelledException))
            {
                // Forward-only formats (tar.gz, ...) go through the reader API.
                using (IReader reader = ReaderFactory.OpenReader(archivePath, new ReaderOptions { Password = password }))
                {
                    while (reader.MoveToNextEntry())
                    {
                        context.ThrowIfCancelled();
                        IEntry entry = reader.Entry;
                        if (entry == null || string.IsNullOrEmpty(entry.Key))
                        {
                            continue;
                        }
                        if (!ShouldExtract(options.OnlyEntries, entry.Key))
                        {
                            continue;
                        }
                        if (entry.IsDirectory)
                        {
                            ExtractDirectoryEntry(options, entry.Key);
                            continue;
                        }
                        context.ReportFile(entry.Key);
                        string currentKey = entry.Key;
                        ExtractFileEntry(entry.Key, entry.Size, options, delegate (string target)
                        {
                            reader.WriteEntryToFile(target, extraction);
                        });
                    }
                    context.ReportPercent(100);
                }
            }
        }

        private void UnwrapNestedTar(string archivePath, ExtractOptions options, OperationContext context)
        {
            ArchiveFormatKind kind = ArchiveFormats.KindFromPath(archivePath);
            if (kind != ArchiveFormatKind.TarGz && kind != ArchiveFormatKind.TarBz2 && kind != ArchiveFormatKind.TarXz)
            {
                return;
            }
            string baseName = ArchiveFormats.StripArchiveExtension(Path.GetFileName(archivePath));
            string expected = Path.Combine(options.Destination, baseName + ".tar");
            string tarPath = File.Exists(expected) ? expected : FindSingleTar(options.Destination);
            if (tarPath == null)
            {
                return;
            }
            context.ReportMessage("Unpacking " + Path.GetFileName(tarPath) + " ...");
            var inner = new ExtractOptions
            {
                Destination = options.Destination,
                Overwrite = options.Overwrite,
                Password = null,
                OnlyEntries = options.OnlyEntries,
                UnwrapNestedTar = false
            };
            Extract(tarPath, inner, null, context);
            try
            {
                File.Delete(tarPath);
            }
            catch
            {
            }
        }

        private static string FindSingleTar(string directory)
        {
            try
            {
                string[] tars = Directory.GetFiles(directory, "*.tar");
                return tars.Length == 1 ? tars[0] : null;
            }
            catch
            {
                return null;
            }
        }

        private static void ExtractDirectoryEntry(ExtractOptions options, string key)
        {
            string target = BuildTargetPath(options.Destination, key);
            if (target != null)
            {
                Directory.CreateDirectory(target);
            }
        }

        /// <summary>
        /// Handles overwrite policy for a file entry and invokes the extractor callback
        /// with the final target path.
        /// </summary>
        private static void ExtractFileEntry(string key, long size, ExtractOptions options, Action<string> extractTo)
        {
            string target = BuildTargetPath(options.Destination, key);
            if (target == null)
            {
                return; // unsafe entry path - skipped
            }
            string dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (File.Exists(target))
            {
                switch (options.Overwrite)
                {
                    case OverwritePolicy.Skip:
                        return;
                    case OverwritePolicy.AutoRename:
                        target = MakeUniquePath(target);
                        break;
                    default:
                        try { File.Delete(target); }
                        catch { }
                        break;
                }
            }
            extractTo(target);
        }

        /// <summary>
        /// Maps an archive entry key to a safe path inside the destination.
        /// Returns null when the entry tries to escape the destination.
        /// </summary>
        private static string BuildTargetPath(string destination, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            string normalized = key.Replace('\\', '/').TrimStart('/');
            if (normalized.Length == 0)
            {
                return null;
            }
            string[] parts = normalized.Split('/');
            var safeParts = new List<string>(parts.Length);
            foreach (string part in parts)
            {
                if (part.Length == 0 || part == ".")
                {
                    continue;
                }
                if (part == "..")
                {
                    return null;
                }
                if (part.IndexOf(':') >= 0)
                {
                    return null;
                }
                safeParts.Add(part);
            }
            if (safeParts.Count == 0)
            {
                return null;
            }
            return Path.Combine(destination, string.Join(Path.DirectorySeparatorChar.ToString(), safeParts.ToArray()));
        }

        private static string MakeUniquePath(string path)
        {
            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 2; i < 1000; i++)
            {
                string candidate = Path.Combine(dir, baseName + " (" + i + ")" + ext);
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return path + ".new";
        }

        private static bool ShouldExtract(List<string> onlyEntries, string key)
        {
            if (onlyEntries == null || onlyEntries.Count == 0)
            {
                return true;
            }
            foreach (string wanted in onlyEntries)
            {
                if (string.IsNullOrEmpty(wanted))
                {
                    continue;
                }
                if (string.Equals(wanted, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                // Selecting a directory selects its whole subtree.
                if (key.StartsWith(wanted.Replace('\\', '/').TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Creation

        public void Create(CreateOptions options, OperationContext context)
        {
            if (options == null || string.IsNullOrEmpty(options.ArchivePath))
            {
                throw new EngineException("No archive path was provided.");
            }
            if (options.RelativeSources == null || options.RelativeSources.Count == 0)
            {
                throw new EngineException("Nothing to compress.");
            }
            if (!CanCreate(options.Kind))
            {
                throw new EngineException("The managed engine cannot create this format. The 7-Zip engine (7z.exe) is required.");
            }
            if (!string.IsNullOrEmpty(options.Password) || !string.IsNullOrEmpty(options.SplitVolume))
            {
                throw new EngineException("Password-protected and split-volume archives require the 7-Zip engine (7z.exe). Reinstall Devvio Archiver with its bundled 7-Zip files or install 7-Zip.");
            }
            if ((options.Kind == ArchiveFormatKind.Gzip || options.Kind == ArchiveFormatKind.BZip2)
                && options.RelativeSources.Count != 1)
            {
                throw new EngineException("GZip/BZip2 archives can contain a single file only.");
            }
            if (string.IsNullOrEmpty(options.WorkDirectory) || !Directory.Exists(options.WorkDirectory))
            {
                throw new EngineException("The working directory does not exist: " + options.WorkDirectory);
            }

            string archiveFullPath = Path.GetFullPath(options.ArchivePath);
            string archiveDir = Path.GetDirectoryName(archiveFullPath);
            if (!string.IsNullOrEmpty(archiveDir))
            {
                Directory.CreateDirectory(archiveDir);
            }
            if (File.Exists(archiveFullPath))
            {
                File.Delete(archiveFullPath);
            }

            long totalBytes = 0;
            int totalFiles = 0;
            CountSources(options, ref totalBytes, ref totalFiles);
            var state = new WriteState { TotalBytes = totalBytes, ProcessedBytes = 0, Files = 0, TotalFiles = totalFiles };

            using (var output = new FileStream(archiveFullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                switch (options.Kind)
                {
                    case ArchiveFormatKind.Zip:
                    {
                        var writerOptions = new ZipWriterOptions(options.Level <= 0 ? CompressionType.None : CompressionType.Deflate)
                        {
                            CompressionLevel = options.Level,
                            LeaveStreamOpen = true
                        };
                        using (IWriter writer = WriterFactory.OpenWriter(output, ArchiveType.Zip, writerOptions))
                        {
                            WriteItems(writer, options, state, context);
                        }
                        break;
                    }
                    case ArchiveFormatKind.SevenZip:
                    {
                        var writerOptions = new SevenZipWriterOptions(CompressionType.LZMA2)
                        {
                            CompressHeader = true,
                            CompressionLevel = options.Level,
                            LeaveStreamOpen = true
                        };
                        using (IWriter writer = WriterFactory.OpenWriter(output, ArchiveType.SevenZip, writerOptions))
                        {
                            WriteItems(writer, options, state, context);
                        }
                        break;
                    }
                    case ArchiveFormatKind.Tar:
                    {
                        using (IWriter writer = WriterFactory.OpenWriter(output, ArchiveType.Tar, new WriterOptions(CompressionType.None) { LeaveStreamOpen = true }))
                        {
                            WriteItems(writer, options, state, context);
                        }
                        break;
                    }
                    case ArchiveFormatKind.TarGz:
                    {
                        using (var gzip = new GZipStream(output, MapGzipLevel(options.Level), true))
                        using (IWriter writer = WriterFactory.OpenWriter(gzip, ArchiveType.Tar, new WriterOptions(CompressionType.None) { LeaveStreamOpen = true }))
                        {
                            WriteItems(writer, options, state, context);
                        }
                        break;
                    }
                    case ArchiveFormatKind.TarBz2:
                    {
                        using (var bzip2 = BZip2Stream.Create(output, SharpCompress.Compressors.CompressionMode.Compress, false, true))
                        using (IWriter writer = WriterFactory.OpenWriter(bzip2, ArchiveType.Tar, new WriterOptions(CompressionType.None) { LeaveStreamOpen = true }))
                        {
                            WriteItems(writer, options, state, context);
                        }
                        break;
                    }
                    case ArchiveFormatKind.Gzip:
                    {
                        string source = ResolveSource(options, 0);
                        using (var input = File.OpenRead(source))
                        using (var gzip = new GZipStream(output, MapGzipLevel(options.Level), true))
                        {
                            context.ReportFile(Path.GetFileName(source));
                            input.CopyTo(gzip);
                        }
                        break;
                    }
                    case ArchiveFormatKind.BZip2:
                    {
                        string source = ResolveSource(options, 0);
                        using (var input = File.OpenRead(source))
                        using (var bzip2 = BZip2Stream.Create(output, CompressionMode.Compress, false, true))
                        {
                            context.ReportFile(Path.GetFileName(source));
                            input.CopyTo(bzip2);
                        }
                        break;
                    }
                    default:
                        throw new EngineException("Unsupported format for the managed engine.");
                }
            }
            context.ReportPercent(100);
        }

        private sealed class WriteState
        {
            public long TotalBytes;
            public long ProcessedBytes;
            public int Files;
            public int TotalFiles;
        }

        private static string ResolveSource(CreateOptions options, int index)
        {
            string relative = options.RelativeSources[index];
            return options.UseFullPaths ? relative : Path.Combine(options.WorkDirectory, relative);
        }

        private static System.IO.Compression.CompressionLevel MapGzipLevel(int level)
        {
            if (level <= 0)
            {
                return System.IO.Compression.CompressionLevel.NoCompression;
            }
            if (level <= 3)
            {
                return System.IO.Compression.CompressionLevel.Fastest;
            }
            return System.IO.Compression.CompressionLevel.Optimal;
        }

        private static void CountSources(CreateOptions options, ref long bytes, ref int files)
        {
            foreach (string relative in options.RelativeSources)
            {
                string full = options.UseFullPaths ? relative : Path.Combine(options.WorkDirectory, relative);
                CountPath(full, ref bytes, ref files);
            }
        }

        private static void CountPath(string fullPath, ref long bytes, ref int files)
        {
            try
            {
                if (Directory.Exists(fullPath))
                {
                    foreach (string file in Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories))
                    {
                        files++;
                        try
                        {
                            bytes += new FileInfo(file).Length;
                        }
                        catch
                        {
                        }
                    }
                }
                else if (File.Exists(fullPath))
                {
                    files++;
                    bytes += new FileInfo(fullPath).Length;
                }
            }
            catch
            {
            }
        }

        private void WriteItems(IWriter writer, CreateOptions options, WriteState state, OperationContext context)
        {
            foreach (string relative in options.RelativeSources)
            {
                context.ThrowIfCancelled();
                string full = options.UseFullPaths ? relative : Path.Combine(options.WorkDirectory, relative);
                string key = relative.Replace('\\', '/').TrimStart('/');
                WritePath(writer, full, key, state, context);
            }
        }

        private void WritePath(IWriter writer, string fullPath, string key, WriteState state, OperationContext context)
        {
            if (Directory.Exists(fullPath))
            {
                writer.WriteDirectory(key, TryGetModified(fullPath));
                string[] children = Directory.GetFileSystemEntries(fullPath);
                Array.Sort(children, StringComparer.OrdinalIgnoreCase);
                foreach (string child in children)
                {
                    context.ThrowIfCancelled();
                    WritePath(writer, child, key + "/" + Path.GetFileName(child), state, context);
                }
                return;
            }
            if (!File.Exists(fullPath))
            {
                return;
            }
            context.ReportFile(key);
            using (FileStream input = File.OpenRead(fullPath))
            {
                writer.Write(key, input, TryGetModified(fullPath));
            }
            state.Files++;
            try
            {
                state.ProcessedBytes += new FileInfo(fullPath).Length;
            }
            catch
            {
            }
            context.ReportPercent(state.TotalBytes > 0 ? (int)(state.ProcessedBytes * 100 / state.TotalBytes) : (int?)null);
        }

        private static DateTime? TryGetModified(string path)
        {
            try
            {
                return File.GetLastWriteTime(path);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Testing

        public void Test(string archivePath, ArchivePasswordProvider passwordProvider, OperationContext context)
        {
            string password = passwordProvider != null ? passwordProvider(archivePath, false) : null;
            while (true)
            {
                context.ThrowIfCancelled();
                try
                {
                    var extraction = new ExtractionOptions { Overwrite = true, CheckCrc = true };
                    using (IArchive archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions { Password = password }))
                    {
                        List<IArchiveEntry> entries = archive.Entries.Where(e => e != null && !string.IsNullOrEmpty(e.Key) && !e.IsDirectory).ToList();
                        int done = 0;
                        foreach (IArchiveEntry entry in entries)
                        {
                            context.ThrowIfCancelled();
                            context.ReportFile(entry.Key);
                            context.ReportPercent(entries.Count > 0 ? (int)(done * 100 / entries.Count) : (int?)null);
                            entry.WriteTo(Stream.Null, extraction);
                            done++;
                        }
                    }
                    return;
                }
                catch (Exception ex) when (IsPasswordRelated(ex) && !(ex is OperationCancelledException))
                {
                    if (passwordProvider == null)
                    {
                        throw new ArchivePasswordException("This archive is encrypted and needs a password.");
                    }
                    string next = passwordProvider(archivePath, true);
                    if (next == null)
                    {
                        throw new OperationCancelledException();
                    }
                    if (next == password)
                    {
                        throw new ArchivePasswordException("Wrong password.");
                    }
                    password = next;
                }
                catch (Exception ex) when (!IsPasswordRelated(ex) && !(ex is OperationCancelledException))
                {
                    // Forward-only formats.
                    using (IReader reader = ReaderFactory.OpenReader(archivePath, new ReaderOptions { Password = password }))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            context.ThrowIfCancelled();
                            IEntry entry = reader.Entry;
                            if (entry == null || entry.IsDirectory || string.IsNullOrEmpty(entry.Key))
                            {
                                continue;
                            }
                            context.ReportFile(entry.Key);
                            reader.WriteEntryTo(Stream.Null);
                        }
                    }
                    return;
                }
            }
        }

        #endregion

        #region Helpers

        private static bool IsPasswordRelated(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }
            string message = ex.Message;
            if (!string.IsNullOrEmpty(message) && message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return ex is SharpCompressException && !string.IsNullOrEmpty(message)
                && message.IndexOf("encrypt", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class ProgressBridge : IProgress<ProgressReport>
        {
            private readonly OperationContext context;

            public ProgressBridge(OperationContext context)
            {
                this.context = context;
            }

            public void Report(ProgressReport value)
            {
                if (context == null || value == null)
                {
                    return;
                }
                try
                {
                    if (!string.IsNullOrEmpty(value.EntryPath))
                    {
                        context.ReportFile(value.EntryPath);
                    }
                    double? percent = value.PercentComplete;
                    if (percent.HasValue)
                    {
                        context.ReportPercent((int)Math.Round(percent.Value));
                    }
                }
                catch
                {
                }
            }
        }

        #endregion
    }
}
