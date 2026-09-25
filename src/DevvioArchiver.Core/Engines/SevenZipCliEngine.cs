using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Devvio.Archiver.Core.Engines
{
    /// <summary>
    /// Archive engine based on the 7-Zip command line tool (7z.exe + 7z.dll).
    /// Supports the full 7-Zip codec set: reading 7z/zip/rar/cab/iso/wim/lzh/arj/cpio/deb/rpm/
    /// tar/gzip/bzip2/xz/... and creating 7z/zip/tar/tar.gz/tar.bz2/tar.xz/gzip/bzip2/xz/wim,
    /// including AES encryption and split volumes.
    /// </summary>
    public sealed class SevenZipCliEngine : IArchiveEngine
    {
        private static readonly Regex ProgressTokenRegex =
            new Regex(@"^[\s*]*(\d{1,3})%\s*(?:\d+\s*)?[-+]?\s*(.*)$", RegexOptions.Compiled);

        public string Name
        {
            get { return "7-Zip"; }
        }

        public bool IsAvailable
        {
            get { return SevenZipLocator.FindSevenZipExe() != null; }
        }

        public bool CanRead(ArchiveFormatKind kind)
        {
            return kind != ArchiveFormatKind.Other;
        }

        public bool CanCreate(ArchiveFormatKind kind)
        {
            switch (kind)
            {
                case ArchiveFormatKind.SevenZip:
                case ArchiveFormatKind.Zip:
                case ArchiveFormatKind.Tar:
                case ArchiveFormatKind.TarGz:
                case ArchiveFormatKind.TarBz2:
                case ArchiveFormatKind.TarXz:
                case ArchiveFormatKind.Gzip:
                case ArchiveFormatKind.BZip2:
                case ArchiveFormatKind.Xz:
                case ArchiveFormatKind.Wim:
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
                ToolResult result = RunTool(
                    "l -slt -ba -sccUTF-8 " + PasswordArg(password) + "-- " + Quote(archivePath),
                    SafeWorkDir(archivePath),
                    null,
                    null);

                if (result.ExitCode == 0)
                {
                    return ParseListOutput(archivePath, result.Output);
                }
                if (IsPasswordFailure(result))
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
                    continue;
                }
                throw new EngineException(Describe(result));
            }
        }

        private static ArchiveInfo ParseListOutput(string archivePath, string stdout)
        {
            var info = new ArchiveInfo
            {
                ArchivePath = archivePath,
                Kind = ArchiveFormats.Detect(archivePath),
                EngineName = "7-Zip"
            };

            ArchiveEntry current = null;
            int index = 0;
            string[] lines = stdout.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd('\r');
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    if (line.Trim().Length == 0)
                    {
                        current = null;
                    }
                    continue;
                }
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                if (string.Equals(key, "Path", StringComparison.OrdinalIgnoreCase))
                {
                    current = new ArchiveEntry { Index = index++, Path = value };
                    info.Entries.Add(current);
                    continue;
                }
                if (current == null)
                {
                    continue;
                }
                if (string.Equals(key, "Size", StringComparison.OrdinalIgnoreCase))
                {
                    current.Size = ParseLong(value);
                }
                else if (string.Equals(key, "Packed Size", StringComparison.OrdinalIgnoreCase))
                {
                    current.PackedSize = ParseLong(value);
                }
                else if (string.Equals(key, "Modified", StringComparison.OrdinalIgnoreCase))
                {
                    current.Modified = ParseDate(value);
                }
                else if (string.Equals(key, "Attributes", StringComparison.OrdinalIgnoreCase))
                {
                    if (value.StartsWith("D", StringComparison.Ordinal) || value.StartsWith("d", StringComparison.Ordinal))
                    {
                        current.IsDirectory = true;
                    }
                }
                else if (string.Equals(key, "Folder", StringComparison.OrdinalIgnoreCase))
                {
                    if (value == "+" || value == "1")
                    {
                        current.IsDirectory = true;
                    }
                }
                else if (string.Equals(key, "Encrypted", StringComparison.OrdinalIgnoreCase))
                {
                    if (value == "+" || value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        current.IsEncrypted = true;
                    }
                }
            }

            info.RecalculateTotals();
            return info;
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
                string listFile = null;
                try
                {
                    Directory.CreateDirectory(options.Destination);

                    var args = new StringBuilder("x -y -sccUTF-8 -spd -bsp1 ");
                    args.Append("-o").Append(Quote(options.Destination)).Append(' ');
                    args.Append(OverwriteArg(options.Overwrite)).Append(' ');
                    if (options.OnlyEntries != null && options.OnlyEntries.Count > 0)
                    {
                        listFile = WriteListFile(options.OnlyEntries);
                        args.Append("-i@").Append(Quote(listFile)).Append(' ');
                    }
                    args.Append(PasswordArg(password));
                    args.Append("-- ").Append(Quote(archivePath));

                    ToolResult result = RunTool(args.ToString(), SafeWorkDir(archivePath), context, null);

                    if (result.ExitCode == 0 || result.ExitCode == 1)
                    {
                        if (options.UnwrapNestedTar)
                        {
                            UnwrapNestedTar(archivePath, options, context);
                        }
                        return;
                    }
                    if (context.CancelRequested)
                    {
                        throw new OperationCancelledException();
                    }
                    if (IsPasswordFailure(result))
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
                        continue;
                    }
                    throw new EngineException(Describe(result));
                }
                finally
                {
                    DeleteFileQuietly(listFile);
                }
            }
        }

        /// <summary>
        /// 7-Zip usually unpacks .tar.gz chains in one go; if for any reason an intermediate
        /// .tar was produced, this extracts it into the same destination and removes it.
        /// </summary>
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
            ArchiveFormatKind kind = options.Kind;
            if (!CanCreate(kind))
            {
                throw new EngineException("The 7-Zip engine cannot create this archive format.");
            }
            if (!string.IsNullOrEmpty(options.Password) && kind != ArchiveFormatKind.SevenZip && kind != ArchiveFormatKind.Zip)
            {
                throw new EngineException("Passwords are only supported for 7z and zip archives.");
            }
            if ((kind == ArchiveFormatKind.Gzip || kind == ArchiveFormatKind.BZip2 || kind == ArchiveFormatKind.Xz)
                && options.RelativeSources.Count != 1)
            {
                throw new EngineException("GZip/BZip2/XZ archives can contain a single file only.");
            }

            string tool = SevenZipLocator.FindSevenZipExe();
            if (tool == null)
            {
                throw new EngineException("The 7-Zip tool (7z.exe) was not found.");
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

            string listFile = WriteListFile(options.RelativeSources);
            try
            {
                context.ReportFile(Path.GetFileName(archiveFullPath));

                if (kind == ArchiveFormatKind.TarGz || kind == ArchiveFormatKind.TarBz2 || kind == ArchiveFormatKind.TarXz)
                {
                    CreateTarCompressed(kind, options, archiveFullPath, listFile, context);
                    return;
                }

                var args = new StringBuilder("a -t");
                args.Append(TypeToken(kind));
                args.Append(" -y -sccUTF-8 -scsUTF-8 -spd -bsp1");
                if (kind != ArchiveFormatKind.Tar && kind != ArchiveFormatKind.Wim)
                {
                    args.Append(" -mx").Append(Math.Max(0, Math.Min(9, options.Level)));
                }

                if (kind == ArchiveFormatKind.SevenZip)
                {
                    if (options.Level > 0 && !string.IsNullOrEmpty(options.Method) && options.Method != "LZMA2")
                    {
                        args.Append(" -m0=").Append(options.Method);
                    }
                    if (options.Level > 0
                        && (string.IsNullOrEmpty(options.Method) || options.Method == "LZMA2" || options.Method == "LZMA")
                        && !string.IsNullOrEmpty(options.DictionarySize))
                    {
                        args.Append(" -md=").Append(options.DictionarySize);
                    }
                    args.Append(options.Solid ? " -ms=on" : " -ms=off");
                }
                if (kind == ArchiveFormatKind.Zip && options.Level > 0
                    && !string.IsNullOrEmpty(options.Method) && options.Method != "Deflate")
                {
                    args.Append(" -m0=").Append(options.Method);
                }

                if (!string.IsNullOrEmpty(options.Password))
                {
                    args.Append(" -p\"").Append(options.Password.Replace("\"", string.Empty)).Append('"');
                    if (kind == ArchiveFormatKind.SevenZip && options.EncryptFileNames)
                    {
                        args.Append(" -mhe=on");
                    }
                    if (kind == ArchiveFormatKind.Zip)
                    {
                        args.Append(" -mem=AES256");
                    }
                }
                if (!string.IsNullOrEmpty(options.SplitVolume))
                {
                    args.Append(" -v").Append(options.SplitVolume);
                }

                args.Append(" -- ").Append(Quote(archiveFullPath)).Append(" @").Append(Quote(listFile));

                ToolResult result = RunTool(args.ToString(), options.WorkDirectory, context, null);
                if (result.ExitCode != 0 && result.ExitCode != 1)
                {
                    if (context.CancelRequested)
                    {
                        throw new OperationCancelledException();
                    }
                    throw new EngineException(Describe(result));
                }
            }
            finally
            {
                DeleteFileQuietly(listFile);
            }
        }

        private void CreateTarCompressed(ArchiveFormatKind kind, CreateOptions options, string archiveFullPath, string listFile, OperationContext context)
        {
            string baseName = ArchiveFormats.StripArchiveExtension(Path.GetFileName(archiveFullPath));
            string tempDir = Path.Combine(Path.GetTempPath(), "DevvioArchiver-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string tarPath = Path.Combine(tempDir, baseName + ".tar");

                context.ReportMessage("Creating the tar layer ...");
                ToolResult tarResult = RunTool(
                    "a -ttar -y -sccUTF-8 -scsUTF-8 -spd -bsp1 -- " + Quote(tarPath) + " @" + Quote(listFile),
                    options.WorkDirectory,
                    context,
                    null);
                if (tarResult.ExitCode != 0 && tarResult.ExitCode != 1)
                {
                    if (context.CancelRequested)
                    {
                        throw new OperationCancelledException();
                    }
                    throw new EngineException(Describe(tarResult));
                }

                context.ReportMessage("Compressing " + Path.GetFileName(archiveFullPath) + " ...");
                string compressor = kind == ArchiveFormatKind.TarGz ? "gzip"
                    : kind == ArchiveFormatKind.TarBz2 ? "bzip2"
                    : "xz";
                ToolResult result = RunTool(
                    "a -t" + compressor + " -mx" + Math.Max(0, Math.Min(9, options.Level))
                    + " -y -sccUTF-8 -scsUTF-8 -bsp1 -- " + Quote(archiveFullPath) + " " + Quote(baseName + ".tar"),
                    tempDir,
                    context,
                    null);
                if (result.ExitCode != 0 && result.ExitCode != 1)
                {
                    if (context.CancelRequested)
                    {
                        throw new OperationCancelledException();
                    }
                    throw new EngineException(Describe(result));
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                }
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
                ToolResult result = RunTool(
                    "t -y -sccUTF-8 -bsp1 " + PasswordArg(password) + "-- " + Quote(archivePath),
                    SafeWorkDir(archivePath),
                    context,
                    null);

                if (result.ExitCode == 0 || result.ExitCode == 1)
                {
                    return;
                }
                if (context.CancelRequested)
                {
                    throw new OperationCancelledException();
                }
                if (IsPasswordFailure(result))
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
                    continue;
                }
                throw new EngineException(Describe(result));
            }
        }

        #endregion

        #region Tool process helpers

        private sealed class ToolResult
        {
            public int ExitCode;
            public string Output;
            public string Error;
        }

        /// <summary>
        /// Runs 7z.exe with redirected output, feeding progress tokens to the context
        /// and supporting cooperative cancellation.
        /// </summary>
        private ToolResult RunTool(string arguments, string workingDirectory, OperationContext context, object unused)
        {
            string tool = SevenZipLocator.FindSevenZipExe();
            if (tool == null)
            {
                throw new EngineException("The 7-Zip tool (7z.exe) was not found. Reinstall Devvio Archiver or install 7-Zip.");
            }

            var psi = new ProcessStartInfo
            {
                FileName = tool,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? SafeWorkDir(null) : workingDirectory
            };

            using (var process = new Process { StartInfo = psi })
            {
                var output = new StringBuilder();
                var error = new StringBuilder();
                process.Start();
                try
                {
                    process.StandardInput.Close(); // never let 7z wait for console input
                }
                catch
                {
                }

                Thread outThread = null;
                Thread errThread = null;
                try
                {
                    outThread = new Thread(delegate() { Pump(process.StandardOutput, output, context); });
                    errThread = new Thread(delegate() { Pump(process.StandardError, error, context); });
                    outThread.IsBackground = true;
                    errThread.IsBackground = true;
                    outThread.Start();
                    errThread.Start();
                }
                catch
                {
                    // Reading threads could not start - fall back to blocking reads.
                    if (outThread == null || !outThread.IsAlive)
                    {
                        output.Append(process.StandardOutput.ReadToEnd());
                    }
                    if (errThread == null || !errThread.IsAlive)
                    {
                        error.Append(process.StandardError.ReadToEnd());
                    }
                }

                while (!process.WaitForExit(150))
                {
                    if (context != null && context.CancelRequested)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                    }
                }

                if (outThread != null)
                {
                    outThread.Join(4000);
                }
                if (errThread != null)
                {
                    errThread.Join(4000);
                }

                return new ToolResult
                {
                    ExitCode = ExitCodeOf(process),
                    Output = output.ToString(),
                    Error = error.ToString()
                };
            }
        }

        private static int ExitCodeOf(Process process)
        {
            try
            {
                return process.ExitCode;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Reads a redirected stream, splitting it into tokens on CR/LF/backspace so that
        /// 7-Zip progress lines (" 45% 12 - file") can be parsed incrementally.
        /// </summary>
        private static void Pump(StreamReader reader, StringBuilder sink, OperationContext context)
        {
            try
            {
                var buffer = new char[512];
                var token = new StringBuilder();
                while (true)
                {
                    int read = reader.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }
                    lock (sink)
                    {
                        sink.Append(buffer, 0, read);
                    }
                    for (int i = 0; i < read; i++)
                    {
                        char c = buffer[i];
                        if (c == '\b' || c == '\r' || c == '\n')
                        {
                            FlushToken(token, context);
                        }
                        else
                        {
                            token.Append(c);
                        }
                    }
                }
                FlushToken(token, context);
            }
            catch
            {
            }
        }

        private static void FlushToken(StringBuilder token, OperationContext context)
        {
            if (token.Length == 0)
            {
                return;
            }
            string text = token.ToString();
            token.Length = 0;
            if (context == null)
            {
                return;
            }
            string trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return;
            }
            Match m = ProgressTokenRegex.Match(trimmed);
            if (m.Success)
            {
                int percent;
                if (int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out percent))
                {
                    context.ReportPercent(percent);
                }
                string file = m.Groups[2].Value.Trim();
                if (file.Length > 0)
                {
                    context.ReportFile(file);
                }
            }
            else
            {
                context.ReportMessage(trimmed);
            }
        }

        #endregion

        #region Small helpers

        private static string SafeWorkDir(string archivePath)
        {
            if (!string.IsNullOrEmpty(archivePath))
            {
                try
                {
                    string dir = Path.GetDirectoryName(Path.GetFullPath(archivePath));
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        return dir;
                    }
                }
                catch
                {
                }
            }
            return Environment.CurrentDirectory;
        }

        private static string TypeToken(ArchiveFormatKind kind)
        {
            switch (kind)
            {
                case ArchiveFormatKind.SevenZip: return "7z";
                case ArchiveFormatKind.Zip: return "zip";
                case ArchiveFormatKind.Tar: return "tar";
                case ArchiveFormatKind.Gzip: return "gzip";
                case ArchiveFormatKind.BZip2: return "bzip2";
                case ArchiveFormatKind.Xz: return "xz";
                case ArchiveFormatKind.Wim: return "wim";
                default: return "7z";
            }
        }

        /// <summary>Always passes -p so that 7z.exe never blocks waiting for console input.</summary>
        private static string PasswordArg(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return "-p ";
            }
            return "-p\"" + password.Replace("\"", string.Empty) + "\" ";
        }

        private static string OverwriteArg(OverwritePolicy policy)
        {
            switch (policy)
            {
                case OverwritePolicy.Overwrite: return "-aoa";
                case OverwritePolicy.Skip: return "-aos";
                default: return "-aou";
            }
        }

        private static bool IsPasswordFailure(ToolResult result)
        {
            string text = (result.Error ?? string.Empty) + "\n" + (result.Output ?? string.Empty);
            return text.IndexOf("Wrong password", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Cannot open encrypted archive", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Describe(ToolResult result)
        {
            string details = (result.Error ?? string.Empty).Trim();
            if (details.Length == 0)
            {
                details = (result.Output ?? string.Empty).Trim();
            }
            details = details.Replace("\r", string.Empty).Replace("\n", " | ");
            if (details.Length > 400)
            {
                details = details.Substring(details.Length - 400);
            }
            if (details.Length > 0)
            {
                return "7-Zip failed (code " + result.ExitCode + "): " + details;
            }
            return "7-Zip failed with exit code " + result.ExitCode + ".";
        }

        private static string WriteListFile(IEnumerable<string> entries)
        {
            string path = Path.Combine(Path.GetTempPath(), "DevvioArchiver-" + Guid.NewGuid().ToString("N") + ".txt");
            var sb = new StringBuilder();
            foreach (string entry in entries)
            {
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }
                sb.Append(entry.TrimEnd()).Append('\n');
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }

        private static void DeleteFileQuietly(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }

        private static long ParseLong(string value)
        {
            long parsed;
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            return 0;
        }

        private static DateTime? ParseDate(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            DateTime parsed;
            if (DateTime.TryParseExact(value, new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed;
            }
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed;
            }
            return null;
        }

        /// <summary>Quotes an argument using the Microsoft command line rules.</summary>
        private static string Quote(string argument)
        {
            if (argument == null)
            {
                argument = string.Empty;
            }
            var sb = new StringBuilder(argument.Length + 2);
            sb.Append('"');
            int backslashes = 0;
            foreach (char c in argument)
            {
                if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                }
                else
                {
                    if (c == '\\')
                    {
                        backslashes++;
                    }
                    else
                    {
                        backslashes = 0;
                    }
                    sb.Append(c);
                }
            }
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        #endregion
    }
}
