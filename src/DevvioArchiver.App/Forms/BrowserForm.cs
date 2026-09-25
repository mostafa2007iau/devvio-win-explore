using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Devvio.Archiver.Core;
using Devvio.Archiver.Core.Engines;

namespace Devvio.Archiver.App.Forms
{
    /// <summary>
    /// Archive browser: virtual-mode list of entries with folder navigation, search,
    /// per-file extraction (double click opens the file), extraction of the whole
    /// archive or a selection, and archive testing.
    /// </summary>
    public sealed class BrowserForm : Form
    {
        private readonly string archivePath;
        private readonly ListView list;
        private readonly TextBox searchBox;
        private readonly ToolStrip toolStrip;
        private readonly ToolStripStatusLabel statusLabel;
        private readonly List<string> tempFolders = new List<string>();

        private List<ArchiveEntry> entries = new List<ArchiveEntry>();
        private List<ArchiveEntry> view = new List<ArchiveEntry>();
        private string currentDir = string.Empty;
        private int sortColumn = 0;
        private bool sortDescending;

        public BrowserForm(string archivePath)
        {
            this.archivePath = archivePath;

            Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Strings.S("BrowserTitle"), Path.GetFileName(archivePath));
            Font = SystemFonts.MessageBoxFont;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(880, 560);
            MinimumSize = new Size(560, 320);
            RightToLeft = Strings.IsRtl ? RightToLeft.Yes : RightToLeft.No;

            var extractAllButton = new ToolStripButton(Strings.S("BrowserExtractAll"));
            extractAllButton.Click += delegate { ExtractEntries(null); };

            var extractSelectedButton = new ToolStripButton(Strings.S("BrowserExtractSelected"));
            extractSelectedButton.Click += delegate { ExtractSelected(); };

            var testButton = new ToolStripButton(Strings.S("BrowserTest"));
            testButton.Click += delegate { TestArchive(); };

            var refreshButton = new ToolStripButton(Strings.S("BrowserRefresh"));
            refreshButton.Click += delegate { LoadArchive(); };

            var upButton = new ToolStripButton(Strings.S("BrowserUp"));
            upButton.Click += delegate
            {
                int slash = currentDir.LastIndexOf('/');
                currentDir = slash > 0 ? currentDir.Substring(0, slash) : string.Empty;
                searchBox.Text = string.Empty;
                RebuildView();
            };

            searchBox = new TextBox
            {
                Width = 180,
                BorderStyle = BorderStyle.FixedSingle
            };
            searchBox.TextChanged += delegate { RebuildView(); };
            var searchHost = new ToolStripControlHost(searchBox)
            {
                AutoSize = false,
                Width = 190
            };
            SetSearchCue();

            toolStrip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(16, 16)
            };
            toolStrip.Items.Add(extractAllButton);
            toolStrip.Items.Add(extractSelectedButton);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(testButton);
            toolStrip.Items.Add(refreshButton);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(upButton);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(searchHost);

            list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                VirtualMode = true,
                MultiSelect = true,
                HideSelection = false,
                HeaderStyle = ColumnHeaderStyle.Clickable
            };
            list.Columns.Add(Strings.S("ColName"), 250);
            list.Columns.Add(Strings.S("ColPath"), 330);
            list.Columns.Add(Strings.S("ColPacked"), 85, HorizontalAlignment.Right);
            list.Columns.Add(Strings.S("ColSize"), 85, HorizontalAlignment.Right);
            list.Columns.Add(Strings.S("ColModified"), 125);
            list.Columns.Add(Strings.S("ColType"), 70);
            list.RetrieveVirtualItem += delegate(object sender, RetrieveVirtualItemEventArgs e)
            {
                e.Item = CreateItem(e.ItemIndex);
            };
            list.ColumnClick += delegate(object sender, ColumnClickEventArgs e)
            {
                if (sortColumn == e.Column)
                {
                    sortDescending = !sortDescending;
                }
                else
                {
                    sortColumn = e.Column;
                    sortDescending = false;
                }
                RebuildView();
            };
            list.DoubleClick += delegate { ActivateSelected(); };

            var statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusStrip.Items.Add(statusLabel);

            var content = new Panel { Dock = DockStyle.Fill };
            content.Controls.Add(list);

            Controls.Add(content);
            Controls.Add(toolStrip);
            Controls.Add(statusStrip);

            Load += delegate { LoadArchive(); };
            FormClosed += delegate { CleanupTempFolders(); };
        }

        private void SetSearchCue()
        {
            // SendMessage with EM_SETCUEBANNER (0x1501) shows the hint text when empty.
            try
            {
                SendMessage(searchBox.Handle, 0x1501, (IntPtr)1, Strings.S("BrowserSearch"));
            }
            catch
            {
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        #region Loading

        private void LoadArchive()
        {
            UseWaitCursor = true;
            statusLabel.Text = Strings.S("LoadingArchive");

            ThreadPool.QueueUserWorkItem(delegate
            {
                ArchiveInfo info = null;
                Exception error = null;
                try
                {
                    IArchiveEngine engine = ArchiveEngineSelector.ForReading(ArchiveFormats.KindFromPath(archivePath));
                    info = engine.List(archivePath, PasswordProvider);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        UseWaitCursor = false;
                        if (error != null)
                        {
                            statusLabel.Text = Strings.S("Failed");
                            if (!(error is OperationCancelledException))
                            {
                                MessageBox.Show(this, error.Message, Strings.S("Error"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            Close();
                            return;
                        }
                        entries = info.Entries;
                        currentDir = string.Empty;
                        searchBox.Text = string.Empty;
                        RebuildView();
                    });
                }
                catch
                {
                }
            });
        }

        public string PasswordProvider(string path, bool previousAttemptFailed)
        {
            if (InvokeRequired)
            {
                try
                {
                    return (string)Invoke(new Func<string, bool, string>(PasswordProvider), path, previousAttemptFailed);
                }
                catch
                {
                    return null;
                }
            }
            return PasswordForm.AskPassword(this,
                previousAttemptFailed ? Strings.S("PasswordWrong") : Strings.S("PasswordPrompt"),
                Strings.S("PasswordPromptTitle"));
        }

        #endregion

        #region View building

        private void RebuildView()
        {
            string search = (searchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            bool searching = search.Length > 0;
            var files = new List<ArchiveEntry>();
            var dirs = new Dictionary<string, ArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            string prefix = currentDir.Length == 0 ? string.Empty : currentDir + "/";

            foreach (ArchiveEntry entry in entries)
            {
                string path = entry.Path.Replace('\\', '/');
                if (searching)
                {
                    if (path.ToLowerInvariant().IndexOf(search, StringComparison.Ordinal) >= 0)
                    {
                        if (entry.IsDirectory)
                        {
                            dirs[path] = entry;
                        }
                        else
                        {
                            files.Add(entry);
                        }
                    }
                    continue;
                }

                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string rest = path.Substring(prefix.Length);
                if (rest.Length == 0)
                {
                    continue;
                }
                int slash = rest.IndexOf('/');
                if (slash < 0)
                {
                    if (entry.IsDirectory)
                    {
                        dirs[rest] = entry;
                    }
                    else
                    {
                        files.Add(entry);
                    }
                }
                else
                {
                    // Synthesize a directory row for paths that have no explicit entry.
                    string name = rest.Substring(0, slash);
                    if (!dirs.ContainsKey(name))
                    {
                        dirs[name] = new ArchiveEntry
                        {
                            Path = prefix + name,
                            IsDirectory = true,
                            IsSynthetic = true
                        };
                    }
                }
            }

            var result = new List<ArchiveEntry>(dirs.Count + files.Count);
            result.AddRange(dirs.Values);
            result.AddRange(files);

            int column = sortColumn;
            bool desc = sortDescending;
            result.Sort(delegate(ArchiveEntry a, ArchiveEntry b)
            {
                if (a.IsDirectory != b.IsDirectory)
                {
                    return a.IsDirectory ? -1 : 1;
                }
                int compare;
                switch (column)
                {
                    case 2: compare = a.PackedSize.CompareTo(b.PackedSize); break;
                    case 3: compare = a.Size.CompareTo(b.Size); break;
                    case 4:
                        compare = Nullable.Compare(a.Modified, b.Modified);
                        break;
                    case 5: compare = string.Compare(ExtensionOf(a), ExtensionOf(b), StringComparison.OrdinalIgnoreCase); break;
                    case 1: compare = string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase); break;
                    default: compare = string.Compare(NameOf(a), NameOf(b), StringComparison.OrdinalIgnoreCase); break;
                }
                return desc ? -compare : compare;
            });

            view = result;
            try
            {
                list.VirtualListSize = 0;
                list.VirtualListSize = view.Count;
            }
            catch
            {
            }

            long total = 0;
            int fileCount = 0;
            foreach (ArchiveEntry entry in view)
            {
                if (!entry.IsDirectory)
                {
                    fileCount++;
                    total += entry.Size;
                }
            }
            long packedTotal = 0;
            foreach (ArchiveEntry entry in view)
            {
                if (!entry.IsDirectory)
                {
                    packedTotal += entry.PackedSize;
                }
            }
            statusLabel.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Strings.S("StatusEntries"), fileCount, view.Count - fileCount,
                Fmt.Size(total), Fmt.Size(packedTotal));
        }

        private static string NameOf(ArchiveEntry entry)
        {
            string path = entry.Path.Replace('\\', '/');
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        private static string ExtensionOf(ArchiveEntry entry)
        {
            if (entry.IsDirectory)
            {
                return string.Empty;
            }
            string name = NameOf(entry);
            int dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(dot + 1).ToUpperInvariant() : string.Empty;
        }

        private ListViewItem CreateItem(int index)
        {
            if (index < 0 || index >= view.Count)
            {
                return new ListViewItem(string.Empty);
            }
            ArchiveEntry entry = view[index];
            string name = NameOf(entry);
            var item = new ListViewItem(name);
            item.SubItems.Add(entry.Path);
            item.SubItems.Add(entry.IsDirectory ? string.Empty : Fmt.Size(entry.PackedSize));
            item.SubItems.Add(entry.IsDirectory ? string.Empty : Fmt.Size(entry.Size));
            item.SubItems.Add(entry.Modified.HasValue
                ? entry.Modified.Value.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.CurrentCulture)
                : string.Empty);
            item.SubItems.Add(entry.IsDirectory ? "<DIR>" : ExtensionOf(entry));
            item.Tag = entry;
            return item;
        }

        #endregion

        #region Actions

        private void ActivateSelected()
        {
            if (list.SelectedIndices.Count != 1)
            {
                return;
            }
            ArchiveEntry entry = view[list.SelectedIndices[0]];
            if (entry.IsDirectory)
            {
                currentDir = entry.Path.Replace('\\', '/').TrimEnd('/');
                searchBox.Text = string.Empty;
                RebuildView();
                return;
            }
            OpenEntryInTemp(entry);
        }

        private void OpenEntryInTemp(ArchiveEntry entry)
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "DevvioArchiver", Guid.NewGuid().ToString("N"));
            var options = new ExtractOptions
            {
                Destination = tempRoot,
                Overwrite = OverwritePolicy.Overwrite,
                OnlyEntries = new List<string> { entry.Path },
                UnwrapNestedTar = false
            };
            IArchiveEngine engine = ArchiveEngineSelector.ForReading(ArchiveFormats.KindFromPath(archivePath));
            try
            {
                var context = new OperationContext(null, null, null);
                engine.Extract(archivePath, options, PasswordProvider, context);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Strings.S("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string extracted = Path.Combine(tempRoot, entry.Path.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(extracted))
            {
                try
                {
                    string[] found = Directory.GetFiles(tempRoot, "*", SearchOption.AllDirectories);
                    if (found.Length > 0)
                    {
                        extracted = found[0];
                    }
                }
                catch
                {
                }
            }

            if (!File.Exists(extracted))
            {
                return;
            }
            tempFolders.Add(tempRoot);

            if (ArchiveFormats.IsArchivePath(extracted) && ArchiveFormats.KindFromPath(extracted) != ArchiveFormatKind.Split)
            {
                using (var nested = new BrowserForm(extracted))
                {
                    nested.ShowDialog(this);
                }
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = extracted,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        private void ExtractSelected()
        {
            if (list.SelectedIndices.Count == 0)
            {
                MessageBox.Show(this, Strings.S("NothingSelected"), Strings.S("AppName"),
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            var only = new List<string>();
            foreach (int index in list.SelectedIndices)
            {
                ArchiveEntry entry = view[index];
                if (entry.IsDirectory)
                {
                    only.Add(entry.Path);
                    only.Add(entry.Path.Replace('\\', '/').TrimEnd('/') + "/*");
                }
                else
                {
                    only.Add(entry.Path);
                }
            }
            ExtractEntries(only);
        }

        private void ExtractEntries(List<string> onlyEntries)
        {
            ExtractOptions options;
            using (var form = new ExtractForm(new List<string> { archivePath }))
            {
                if (form.ShowDialog(this) != DialogResult.OK || form.Options == null)
                {
                    return;
                }
                options = form.Options;
            }
            options.OnlyEntries = onlyEntries;

            IArchiveEngine engine = ArchiveEngineSelector.ForReading(ArchiveFormats.KindFromPath(archivePath));
            using (var progress = new ProgressForm(Strings.S("ProgressTitleExtract"), Path.GetFileName(archivePath)))
            {
                progress.DestinationFolder = options.Destination;
                progress.Run(delegate(OperationContext ctx, ProgressForm form)
                {
                    engine.Extract(archivePath, options, form.PasswordProvider, ctx);
                    if (options.OpenDestinationWhenDone && Directory.Exists(options.Destination))
                    {
                        Program.OpenFolder(options.Destination);
                    }
                });
                if (!progress.Succeeded)
                {
                    progress.ShowError();
                }
            }
        }

        private void TestArchive()
        {
            IArchiveEngine engine = ArchiveEngineSelector.ForReading(ArchiveFormats.KindFromPath(archivePath));
            using (var progress = new ProgressForm(Strings.S("ProgressTitleTest"), Path.GetFileName(archivePath)))
            {
                progress.Run(delegate(OperationContext ctx, ProgressForm form)
                {
                    engine.Test(archivePath, form.PasswordProvider, ctx);
                });
                if (progress.Succeeded)
                {
                    MessageBox.Show(this, Strings.S("TestOk"), Strings.S("AppName"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    progress.ShowError();
                }
            }
        }

        private void CleanupTempFolders()
        {
            foreach (string folder in tempFolders)
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch
                {
                }
            }
            tempFolders.Clear();
        }

        #endregion
    }
}
