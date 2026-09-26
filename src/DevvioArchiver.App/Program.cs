using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Devvio.Archiver.Core;
using Devvio.Archiver.Core.Engines;
using Devvio.Archiver.App.Forms;

namespace Devvio.Archiver.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            CleanupOldTempViews();

            try
            {
                Run(args ?? new string[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Strings.S("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Run(string[] args)
        {
            if (args.Length == 0)
            {
                using (var welcome = new WelcomeForm())
                {
                    welcome.ShowDialog();
                }
                return;
            }

            string mode = args[0].ToLowerInvariant();
            List<string> paths = new List<string>();
            string format = null;
            string dir = null;
            bool dialog = false;
            bool quick = false;
            bool here = false;
            bool sub = false;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--dialog") { dialog = true; }
                else if (arg == "--quick") { quick = true; }
                else if (arg == "--here") { here = true; }
                else if (arg == "--sub") { sub = true; }
                else if (arg == "--dir") { i++; if (i < args.Length) dir = args[i]; }
                else if (arg == "--format") { i++; if (i < args.Length) format = args[i]; }
                else if (arg == "--list")
                {
                    i++;
                    if (i < args.Length)
                    {
                        paths.AddRange(ReadListFile(args[i]));
                    }
                }
                else
                {
                    paths.Add(arg);
                }
            }

            paths = paths
                .Where(delegate(string p) { return File.Exists(p) || Directory.Exists(p); })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            switch (mode)
            {
                case "browse":
                    if (paths.Count > 0)
                    {
                        Application.Run(new BrowserForm(paths[0]));
                    }
                    break;

                case "test":
                    if (paths.Count > 0)
                    {
                        RunTestJob(paths[0]);
                    }
                    break;

                case "extract":
                    if (paths.Count == 0)
                    {
                        return;
                    }
                    if (dialog)
                    {
                        RunExtractDialog(paths);
                    }
                    else
                    {
                        RunExtractQuick(paths, here);
                    }
                    break;

                case "compress":
                    if (dir != null && Directory.Exists(dir))
                    {
                        if (quick)
                        {
                            RunCompressQuickDir(dir, format == "zip" ? ArchiveFormatKind.Zip : ArchiveFormatKind.SevenZip);
                        }
                        else
                        {
                            using (var form = new CompressForm(null, dir))
                            {
                                if (form.ShowDialog() == DialogResult.OK)
                                {
                                    RunCreateJob(form.Options, true);
                                }
                            }
                        }
                    }
                    else if (paths.Count > 0)
                    {
                        if (quick)
                        {
                            RunCompressQuickItems(paths, format == "zip" ? ArchiveFormatKind.Zip : ArchiveFormatKind.SevenZip);
                        }
                        else
                        {
                            using (var form = new CompressForm(paths, null))
                            {
                                if (form.ShowDialog() == DialogResult.OK)
                                {
                                    RunCreateJob(form.Options, true);
                                }
                            }
                        }
                    }
                    break;
            }
        }

        #region Quick jobs

        private static void RunTestJob(string archivePath)
        {
            var engine = ArchiveEngineSelector.ForReading(ArchiveFormats.KindFromPath(archivePath));
            using (var progress = new ProgressForm(Strings.S("ProgressTitleTest"), Path.GetFileName(archivePath)))
            {
                progress.Run(delegate (OperationContext ctx, ProgressForm form)
                {
                    engine.Test(archivePath, form.PasswordProvider, ctx);
                });
                if (progress.Succeeded)
                {
                    MessageBox.Show(Strings.S("TestOk"), Strings.S("AppName"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    progress.ShowError();
                }
            }
        }

        private static void RunExtractQuick(List<string> archives, bool extractHere)
        {
            var jobs = new List<KeyValuePair<string, ExtractOptions>>();
            foreach (string archive in archives)
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(archive));
                string baseName = ArchiveFormats.StripArchiveExtension(Path.GetFileName(archive));
                string destination = extractHere
                    ? dir
                    : Path.Combine(dir, baseName);
                var options = new ExtractOptions
                {
                    Destination = destination,
                    Overwrite = OverwritePolicy.AutoRename,
                    UnwrapNestedTar = true
                };
                jobs.Add(new KeyValuePair<string, ExtractOptions>(archive, options));
            }

            using (var progress = new ProgressForm(Strings.S("ProgressTitleExtract"), null))
            {
                progress.DestinationFolder = jobs.Count > 0 ? jobs[0].Value.Destination : null;
                progress.Run(delegate (OperationContext ctx, ProgressForm form)
                {
                    foreach (KeyValuePair<string, ExtractOptions> job in jobs)
                    {
                        ctx.ThrowIfCancelled();
                        ctx.ReportPercent(0);
                        ctx.ReportMessage(Path.GetFileName(job.Key));
                        var engine = ArchiveEngineSelector.ForReading(ArchiveFormats.KindFromPath(job.Key));
                        engine.Extract(job.Key, job.Value, form.PasswordProvider, ctx);
                    }
                });
                if (!progress.Succeeded)
                {
                    progress.ShowError();
                }
            }
        }

        private static void RunExtractDialog(List<string> archives)
        {
            ExtractOptions template;
            using (var form = new ExtractForm(archives))
            {
                if (form.ShowDialog() != DialogResult.OK || form.Options == null)
                {
                    return;
                }
                template = form.Options;
            }

            var jobs = new List<KeyValuePair<string, ExtractOptions>>();
            if (archives.Count == 1)
            {
                jobs.Add(new KeyValuePair<string, ExtractOptions>(archives[0], template));
            }
            else
            {
                foreach (string archive in archives)
                {
                    string baseName = ArchiveFormats.StripArchiveExtension(Path.GetFileName(archive));
                    var copy = new ExtractOptions
                    {
                        Destination = Path.Combine(template.Destination, baseName),
                        Overwrite = template.Overwrite,
                        Password = template.Password,
                        UnwrapNestedTar = template.UnwrapNestedTar,
                        OpenDestinationWhenDone = false
                    };
                    jobs.Add(new KeyValuePair<string, ExtractOptions>(archive, copy));
                }
            }

            using (var progress = new ProgressForm(Strings.S("ProgressTitleExtract"), null))
            {
                progress.Run(delegate (OperationContext ctx, ProgressForm form)
                {
                    foreach (KeyValuePair<string, ExtractOptions> job in jobs)
                    {
                        ctx.ThrowIfCancelled();
                        ctx.ReportPercent(0);
                        ctx.ReportMessage(Path.GetFileName(job.Key));
                        var engine = ArchiveEngineSelector.ForReading(ArchiveFormats.KindFromPath(job.Key));
                        engine.Extract(job.Key, job.Value, form.PasswordProvider, ctx);
                    }
                    if (template.OpenDestinationWhenDone && Directory.Exists(template.Destination))
                    {
                        OpenFolder(template.Destination);
                    }
                });
                if (!progress.Succeeded)
                {
                    progress.ShowError();
                }
            }
        }

        private static void RunCompressQuickItems(List<string> items, ArchiveFormatKind kind)
        {
            CreateOptions options = SourceSet.BuildCreateOptions(items);
            options.Kind = kind;
            options.Level = 5;
            string baseName = items.Count == 1
                ? (Directory.Exists(items[0])
                    ? Path.GetFileName(Path.GetFullPath(items[0]).TrimEnd('\\'))
                    : ArchiveFormats.StripArchiveExtension(Path.GetFileName(items[0])))
                : Path.GetFileName(Path.GetFullPath(options.WorkDirectory).TrimEnd('\\'));
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Archive";
            }
            string parent = Path.GetDirectoryName(Path.GetFullPath(items[0]));
            options.ArchivePath = MakeUniquePath(Path.Combine(parent, baseName + ArchiveFormats.DefaultExtension(kind)));
            RunCreateJob(options, true);
        }

        private static void RunCompressQuickDir(string directory, ArchiveFormatKind kind)
        {
            var options = new CreateOptions
            {
                Kind = kind,
                Level = 5,
                WorkDirectory = directory,
                RelativeSources = new List<string>()
            };
            foreach (string child in Directory.GetFileSystemEntries(directory))
            {
                options.RelativeSources.Add(Path.GetFileName(child));
            }
            if (options.RelativeSources.Count == 0)
            {
                return;
            }
            string dirName = Path.GetFileName(Path.GetFullPath(directory).TrimEnd('\\'));
            if (string.IsNullOrEmpty(dirName))
            {
                dirName = "Archive";
            }
            options.ArchivePath = MakeUniquePath(Path.Combine(directory, dirName + ArchiveFormats.DefaultExtension(kind)));
            RunCreateJob(options, true);
        }

        private static void RunCreateJob(CreateOptions options, bool autoClose)
        {
            var engine = ArchiveEngineSelector.ForCreating(options.Kind);
            using (var progress = new ProgressForm(Strings.S("ProgressTitleCompress"),
                Path.GetFileName(options.ArchivePath), autoClose))
            {
                progress.Run(delegate (OperationContext ctx, ProgressForm form)
                {
                    engine.Create(options, ctx);
                });
                if (!progress.Succeeded)
                {
                    progress.ShowError();
                }
            }
        }

        #endregion

        #region Helpers

        private static List<string> ReadListFile(string path)
        {
            var result = new List<string>();
            try
            {
                result.AddRange(File.ReadAllLines(path));
            }
            catch
            {
            }
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
            return result;
        }

        private static string MakeUniquePath(string path)
        {
            if (!File.Exists(path))
            {
                return path;
            }
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
            return Path.Combine(dir, baseName + " (" + Guid.NewGuid().ToString("N").Substring(0, 6) + ")" + ext);
        }

        public static void OpenFolder(string path)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        private static void CleanupOldTempViews()
        {
            try
            {
                string root = Path.Combine(Path.GetTempPath(), "DevvioArchiver");
                if (!Directory.Exists(root))
                {
                    return;
                }
                foreach (string dir in Directory.GetDirectories(root))
                {
                    try
                    {
                        if (Directory.GetLastWriteTime(dir) < DateTime.Now.AddDays(-1))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        #endregion
    }

    /// <summary>
    /// Computes the WorkDirectory + relative source items for a create operation.
    /// </summary>
    internal static class SourceSet
    {
        public static CreateOptions BuildCreateOptions(List<string> items)
        {
            var options = new CreateOptions();
            var fulls = new List<string>();
            foreach (string item in items)
            {
                fulls.Add(Path.GetFullPath(item));
            }

            string commonRoot = CommonRootDirectory(fulls);
            if (commonRoot == null)
            {
                options.WorkDirectory = Path.GetDirectoryName(fulls[0]);
                options.UseFullPaths = true;
                options.RelativeSources = fulls;
                return options;
            }

            options.WorkDirectory = commonRoot;
            options.RelativeSources = new List<string>();
            string prefix = commonRoot.TrimEnd('\\') + "\\";
            foreach (string full in fulls)
            {
                string relative = full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? full.Substring(prefix.Length)
                    : full.Substring(commonRoot.Length).TrimStart('\\', '/');
                options.RelativeSources.Add(relative);
            }
            return options;
        }

        private static string CommonRootDirectory(List<string> fullPaths)
        {
            if (fullPaths == null || fullPaths.Count == 0)
            {
                return null;
            }
            string[] segments = SplitPath(fullPaths[0]);
            foreach (string path in fullPaths)
            {
                string[] other = SplitPath(path);
                int common = Math.Min(segments.Length, other.Length);
                int match = 0;
                while (match < common
                    && string.Equals(segments[match], other[match], StringComparison.OrdinalIgnoreCase))
                {
                    match++;
                }
                if (match < segments.Length)
                {
                    var reduced = new string[match];
                    Array.Copy(segments, 0, reduced, 0, match);
                    segments = reduced;
                }
            }
            if (segments.Length == 0)
            {
                return null;
            }
            var sb = new System.Text.StringBuilder(segments[0]);
            for (int i = 1; i < segments.Length; i++)
            {
                sb.Append('\\').Append(segments[i]);
            }
            return sb.ToString();
        }

        private static string[] SplitPath(string fullPath)
        {
            string trimmed = fullPath.TrimEnd('\\', '/');
            var parts = new List<string>();
            if (trimmed.Length >= 2 && trimmed[1] == ':')
            {
                parts.Add(trimmed.Substring(0, 2));
                trimmed = trimmed.Substring(2);
            }
            foreach (string part in trimmed.Split('\\', '/'))
            {
                if (part.Length > 0 && part != ".")
                {
                    parts.Add(part);
                }
            }
            return parts.ToArray();
        }
    }
}
