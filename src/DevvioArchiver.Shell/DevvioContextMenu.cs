using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;

namespace Devvio.Archiver.Shell
{
    /// <summary>
    /// Explorer context menu integration for Devvio Archiver.
    ///
    /// Registered (7-Zip style) for:
    ///   - all files            (HKCR\*\ShellEx\ContextMenuHandlers)
    ///   - directories          (HKCR\Directory\ShellEx\ContextMenuHandlers)
    ///   - folder backgrounds   (HKCR\Directory\Background\ShellEx\ContextMenuHandlers)
    ///   - drives               (HKCR\Drive\ShellEx\ContextMenuHandlers)
    ///
    /// On archives it offers extraction/testing/browsing; on any other file or folder
    /// it offers compression. All work is delegated to DevvioArchiver.App.exe so the
    /// shell thread never blocks.
    /// </summary>
    [ComVisible(true)]
    [Guid("991DE108-BB35-4F0D-B518-466CDEBC7E53")]
    [RegistrationName("Devvio Archiver")]
    [COMServerAssociation(AssociationType.AllFilesAndFolders)]
    [COMServerAssociation(AssociationType.Directory)]
    [COMServerAssociation(AssociationType.DirectoryBackground)]
    [COMServerAssociation(AssociationType.Drive)]
    public sealed class DevvioContextMenu : SharpContextMenu
    {
        protected override bool CanShowMenu()
        {
            try
            {
                var items = SelectedItemPaths.ToList();
                if (items.Count == 0)
                {
                    // Empty area of a folder/drive.
                    return !string.IsNullOrEmpty(FolderPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected override ContextMenuStrip CreateMenu()
        {
            var menu = new ContextMenuStrip();
            try
            {
                menu.RightToLeft = Strings.IsRtl ? RightToLeft.Yes : RightToLeft.No;
            }
            catch
            {
            }

            try
            {
                var items = SelectedItemPaths.Where(p => !string.IsNullOrEmpty(p)).ToList();
                var archives = items.Where(ArchiveFormats.IsArchivePath).ToList();

                if (archives.Count > 0)
                {
                    AddExtractGroup(menu, archives);
                }

                if (items.Count > 0)
                {
                    if (archives.Count > 0)
                    {
                        menu.Items.Add(new ToolStripSeparator());
                    }
                    AddCompressSubmenu(menu, items, null);
                }
                else if (!string.IsNullOrEmpty(FolderPath))
                {
                    AddCompressSubmenu(menu, null, FolderPath);
                }
            }
            catch
            {
                // Never let an exception escape into Explorer.
            }
            return menu;
        }

        #region Menu construction

        private void AddExtractGroup(ContextMenuStrip menu, List<string> archives)
        {
            string first = archives[0];
            string baseName = ArchiveFormats.StripArchiveExtension(Path.GetFileName(first));

            var extractHere = new ToolStripMenuItem(Strings.S("MenuExtractHere"));
            extractHere.Click += delegate { Launch(BuildArgs("extract --here", archives)); };
            menu.Items.Add(extractHere);

            var extractTo = new ToolStripMenuItem(Strings.S("MenuExtractTo", baseName));
            extractTo.Click += delegate { Launch(BuildArgs("extract --sub", archives)); };
            menu.Items.Add(extractTo);

            var extractDialog = new ToolStripMenuItem(Strings.S("MenuExtractDialog"));
            extractDialog.Click += delegate { Launch(BuildArgs("extract --dialog", archives)); };
            menu.Items.Add(extractDialog);

            menu.Items.Add(new ToolStripSeparator());

            var test = new ToolStripMenuItem(Strings.S("MenuTestArchive"));
            test.Click += delegate { Launch(BuildArgs("test", archives)); };
            menu.Items.Add(test);

            var browse = new ToolStripMenuItem(Strings.S("MenuOpenBrowser"));
            browse.Click += delegate { Launch(BuildArgs("browse", new[] { first })); };
            menu.Items.Add(browse);
        }

        private void AddCompressSubmenu(ContextMenuStrip menu, List<string> items, string directory)
        {
            string defaultBase = GetDefaultArchiveBaseName(items, directory);
            bool background = items == null;

            var submenu = new ToolStripMenuItem(Strings.S("MenuTitle"));

            var addDialog = new ToolStripMenuItem(
                background ? Strings.S("MenuCompressAll") : Strings.S("MenuAddToArchive"));
            addDialog.Click += delegate
            {
                Launch(background
                    ? BuildArgs("compress --dialog --dir", new[] { directory })
                    : BuildArgs("compress --dialog", items));
            };
            submenu.DropDownItems.Add(addDialog);

            string sevenZipName = defaultBase + ".7z";
            var quick7z = new ToolStripMenuItem(
                background ? Strings.S("MenuCompressAllTo", sevenZipName) : Strings.S("MenuCompressTo", sevenZipName));
            quick7z.Click += delegate
            {
                Launch(background
                    ? BuildArgs("compress --quick --format 7z --dir", new[] { directory })
                    : BuildArgs("compress --quick --format 7z", items));
            };
            submenu.DropDownItems.Add(quick7z);

            string zipName = defaultBase + ".zip";
            var quickZip = new ToolStripMenuItem(
                background ? Strings.S("MenuCompressAllTo", zipName) : Strings.S("MenuCompressTo", zipName));
            quickZip.Click += delegate
            {
                Launch(background
                    ? BuildArgs("compress --quick --format zip --dir", new[] { directory })
                    : BuildArgs("compress --quick --format zip", items));
            };
            submenu.DropDownItems.Add(quickZip);

            menu.Items.Add(submenu);
        }

        private static string GetDefaultArchiveBaseName(List<string> items, string directory)
        {
            try
            {
                if (items != null && items.Count > 0)
                {
                    if (items.Count == 1)
                    {
                        string path = items[0];
                        if (Directory.Exists(path))
                        {
                            return Path.GetFileName(Path.GetFullPath(path).TrimEnd('\\'));
                        }
                        return ArchiveFormats.StripArchiveExtension(Path.GetFileName(path));
                    }
                    string parent = CommonParent(items);
                    if (parent != null)
                    {
                        string name = Path.GetFileName(Path.GetFullPath(parent).TrimEnd('\\'));
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                    return "Archive";
                }
                if (!string.IsNullOrEmpty(directory))
                {
                    return Path.GetFileName(Path.GetFullPath(directory).TrimEnd('\\'));
                }
            }
            catch
            {
            }
            return "Archive";
        }

        #endregion

        #region Launching the helper app

        private static void Launch(string arguments)
        {
            try
            {
                string exe = FindHelperApp();
                if (exe == null)
                {
                    return;
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                });
            }
            catch
            {
            }
        }

        private static string FindHelperApp()
        {
            try
            {
                string dir = null;
                try
                {
                    dir = Path.GetDirectoryName(typeof(DevvioContextMenu).Assembly.Location);
                }
                catch
                {
                }
                if (!string.IsNullOrEmpty(dir))
                {
                    string local = Path.Combine(dir, "DevvioArchiver.App.exe");
                    if (File.Exists(local))
                    {
                        return local;
                    }
                }
                foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    try
                    {
                        using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\DevvioArchiver"))
                        {
                            if (key == null)
                            {
                                continue;
                            }
                            string path = key.GetValue("AppPath") as string;
                            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                            {
                                return path;
                            }
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
            return null;
        }

        /// <summary>
        /// Builds the command line for the helper app. When the paths are too long for a
        /// command line they are written to a temporary list file instead.
        /// </summary>
        private static string BuildArgs(string command, IList<string> paths)
        {
            var sb = new StringBuilder(command);
            int inline = 2 + command.Length;
            foreach (string path in paths)
            {
                inline += path.Length + 3;
            }
            if (inline < 7000)
            {
                foreach (string path in paths)
                {
                    sb.Append(" \"").Append(path.Replace("\"", string.Empty)).Append('"');
                }
                return sb.ToString();
            }

            string listFile = null;
            try
            {
                listFile = Path.Combine(Path.GetTempPath(), "DevvioArchiverCmd-" + Guid.NewGuid().ToString("N") + ".txt");
                var content = new StringBuilder();
                foreach (string path in paths)
                {
                    content.AppendLine(path);
                }
                File.WriteAllText(listFile, content.ToString(), new UTF8Encoding(true));
                sb.Append(" --list \"").Append(listFile).Append('"');
            }
            catch
            {
                // Fall back to inlining on failure.
                if (listFile != null)
                {
                    try { File.Delete(listFile); }
                    catch { }
                }
                sb = new StringBuilder(command);
                foreach (string path in paths)
                {
                    sb.Append(" \"").Append(path.Replace("\"", string.Empty)).Append('"');
                }
            }
            return sb.ToString();
        }

        #endregion

        #region Helpers

        private static string CommonParent(IEnumerable<string> paths)
        {
            string parent = null;
            foreach (string path in paths)
            {
                string dir;
                try
                {
                    dir = Path.GetDirectoryName(Path.GetFullPath(path));
                }
                catch
                {
                    return null;
                }
                if (dir == null)
                {
                    return null;
                }
                if (parent == null)
                {
                    parent = dir;
                }
                else if (!string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
            return parent;
        }

        #endregion
    }
}
