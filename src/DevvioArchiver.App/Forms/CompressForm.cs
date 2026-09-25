using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Devvio.Archiver.Core;
using Devvio.Archiver.Core.Engines;

namespace Devvio.Archiver.App.Forms
{
    /// <summary>
    /// "Add to archive" dialog: destination, format, level, method, dictionary,
    /// password (AES), solid archive, split volumes. Produces a CreateOptions.
    /// </summary>
    public sealed class CompressForm : Form
    {
        private sealed class FormatItem
        {
            public ArchiveFormatKind Kind;
            public string Display;
            public string Extension;
            public override string ToString() { return Display; }
        }

        private readonly List<string> items;
        private readonly string directoryMode;

        private readonly TextBox destBox;
        private readonly ComboBox formatCombo;
        private readonly ComboBox levelCombo;
        private readonly ComboBox methodCombo;
        private readonly ComboBox dictCombo;
        private readonly ComboBox volumeCombo;
        private readonly TextBox volumeBox;
        private readonly CheckBox solidCheck;
        private readonly CheckBox encryptNamesCheck;
        private readonly TextBox passwordBox;
        private readonly TextBox password2Box;
        private readonly CheckBox showPasswordCheck;
        private readonly Label summaryLabel;
        private readonly Label noticeLabel;
        private readonly Button okButton;
        private bool showManagedNotice;

        public CreateOptions Options { get; private set; }

        public CompressForm(List<string> items, string directory)
        {
            this.items = items;
            this.directoryMode = directory;

            Text = Strings.S("CompressTitle");
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(540, 432);
            RightToLeft = Strings.IsRtl ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = Strings.IsRtl;

            int sourceCount = GetSourceCount();
            string defaultBase = GetDefaultBaseName();

            // --- Row: archive path -------------------------------------------------
            var destLabel = new Label
            {
                Location = new Point(12, 14),
                Size = new Size(90, 20),
                Text = Strings.S("ArchiveName")
            };
            destBox = new TextBox
            {
                Location = new Point(108, 11),
                Size = new Size(360, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = BuildDefaultArchivePath(defaultBase, ".7z")
            };
            var browseButton = new Button
            {
                Location = new Point(472, 9),
                Size = new Size(56, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = "..."
            };
            browseButton.Click += delegate { BrowseDestination(); };

            // --- Format & level ----------------------------------------------------
            var formatLabel = new Label
            {
                Location = new Point(12, 46),
                Size = new Size(90, 20),
                Text = Strings.S("Format")
            };
            formatCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(108, 43),
                Size = new Size(170, 23)
            };
            PopulateFormats(sourceCount);
            formatCombo.SelectedIndex = 0;
            formatCombo.SelectedIndexChanged += delegate { FormatChanged(); };

            var levelLabel = new Label
            {
                Location = new Point(296, 46),
                Size = new Size(90, 20),
                Text = Strings.S("Level")
            };
            levelCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(392, 43),
                Size = new Size(136, 23)
            };
            levelCombo.Items.Add(Strings.S("LevelStore"));
            levelCombo.Items.Add(Strings.S("LevelFastest"));
            levelCombo.Items.Add(Strings.S("LevelFast"));
            levelCombo.Items.Add(Strings.S("LevelNormal"));
            levelCombo.Items.Add(Strings.S("LevelMaximum"));
            levelCombo.Items.Add(Strings.S("LevelUltra"));
            levelCombo.SelectedIndex = 3;
            levelCombo.SelectedIndexChanged += delegate { FormatChanged(); };

            // --- Method & dictionary ------------------------------------------------
            var methodLabel = new Label
            {
                Location = new Point(12, 78),
                Size = new Size(90, 20),
                Text = Strings.S("Method")
            };
            methodCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(108, 75),
                Size = new Size(170, 23)
            };
            var dictLabel = new Label
            {
                Location = new Point(296, 78),
                Size = new Size(90, 20),
                Text = Strings.S("Dictionary")
            };
            dictCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(392, 75),
                Size = new Size(136, 23)
            };
            foreach (string dict in new[] { "default", "16m", "32m", "64m", "128m", "256m", "512m", "1024m" })
            {
                dictCombo.Items.Add(dict);
            }
            dictCombo.SelectedIndex = 0;

            // --- Volume ------------------------------------------------------------
            var volumeLabel = new Label
            {
                Location = new Point(12, 110),
                Size = new Size(90, 20),
                Text = Strings.S("Volume")
            };
            volumeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(108, 107),
                Size = new Size(170, 23)
            };
            foreach (string vol in new[] { Strings.S("VolumeNone"), "1m", "5m", "10m", "100m", "700m", "1g", "4g", Strings.S("VolumeCustom") })
            {
                volumeCombo.Items.Add(vol);
            }
            volumeCombo.SelectedIndex = 0;
            volumeCombo.SelectedIndexChanged += delegate
            {
                bool custom = IsCustomVolume();
                volumeBox.Visible = custom;
                if (custom)
                {
                    volumeBox.Text = "100m";
                    volumeBox.Focus();
                }
            };
            volumeBox = new TextBox
            {
                Location = new Point(108, 133),
                Size = new Size(170, 23),
                Visible = false
            };

            // --- Password ------------------------------------------------------------
            var passwordLabel = new Label
            {
                Location = new Point(12, 166),
                Size = new Size(90, 20),
                Text = Strings.S("Password")
            };
            passwordBox = new TextBox
            {
                Location = new Point(108, 163),
                Size = new Size(170, 23),
                UseSystemPasswordChar = true
            };
            var password2Label = new Label
            {
                Location = new Point(296, 166),
                Size = new Size(90, 20),
                Text = Strings.S("Password2")
            };
            password2Box = new TextBox
            {
                Location = new Point(392, 163),
                Size = new Size(136, 23),
                UseSystemPasswordChar = true
            };
            showPasswordCheck = new CheckBox
            {
                Location = new Point(108, 192),
                Size = new Size(150, 22),
                Text = Strings.S("ShowPassword")
            };
            showPasswordCheck.CheckedChanged += delegate
            {
                bool show = showPasswordCheck.Checked;
                passwordBox.UseSystemPasswordChar = !show;
                password2Box.UseSystemPasswordChar = !show;
            };
            encryptNamesCheck = new CheckBox
            {
                Location = new Point(296, 192),
                Size = new Size(230, 22),
                Checked = true,
                Text = Strings.S("EncryptNames")
            };
            solidCheck = new CheckBox
            {
                Location = new Point(12, 220),
                Size = new Size(250, 22),
                Checked = true,
                Text = Strings.S("SolidArchive")
            };

            // --- Summary / notice ------------------------------------------------------
            summaryLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(12, 250),
                Size = new Size(516, 20),
                Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.S("ItemsToCompress"), sourceCount)
            };

            noticeLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = Color.Firebrick,
                Location = new Point(12, 274),
                Size = new Size(516, 40),
                Visible = false,
                Text = Strings.S("ManagedEngineNotice")
            };

            okButton = new Button
            {
                Location = new Point(360, 388),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = Strings.S("CreateArchive")
            };
            okButton.Click += delegate { ValidateAndAccept(); };

            var cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(448, 388),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = Strings.S("Cancel")
            };

            CancelButton = cancelButton;

            Controls.Add(destLabel);
            Controls.Add(destBox);
            Controls.Add(browseButton);
            Controls.Add(formatLabel);
            Controls.Add(formatCombo);
            Controls.Add(levelLabel);
            Controls.Add(levelCombo);
            Controls.Add(methodLabel);
            Controls.Add(methodCombo);
            Controls.Add(dictLabel);
            Controls.Add(dictCombo);
            Controls.Add(volumeLabel);
            Controls.Add(volumeCombo);
            Controls.Add(volumeBox);
            Controls.Add(passwordLabel);
            Controls.Add(passwordBox);
            Controls.Add(password2Label);
            Controls.Add(password2Box);
            Controls.Add(showPasswordCheck);
            Controls.Add(encryptNamesCheck);
            Controls.Add(solidCheck);
            Controls.Add(summaryLabel);
            Controls.Add(noticeLabel);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            FormatChanged();
        }

        #region Setup helpers

        private int GetSourceCount()
        {
            if (directoryMode != null)
            {
                try { return Directory.GetFileSystemEntries(directoryMode).Length; }
                catch { return 0; }
            }
            return items != null ? items.Count : 0;
        }

        private string GetDefaultBaseName()
        {
            try
            {
                if (directoryMode != null)
                {
                    string name = Path.GetFileName(Path.GetFullPath(directoryMode).TrimEnd('\\'));
                    return string.IsNullOrEmpty(name) ? "Archive" : name;
                }
                if (items != null && items.Count == 1)
                {
                    return Directory.Exists(items[0])
                        ? Path.GetFileName(Path.GetFullPath(items[0]).TrimEnd('\\'))
                        : ArchiveFormats.StripArchiveExtension(Path.GetFileName(items[0]));
                }
                if (items != null && items.Count > 1)
                {
                    string parent = null;
                    foreach (string item in items)
                    {
                        string dir = Path.GetDirectoryName(Path.GetFullPath(item));
                        if (parent == null)
                        {
                            parent = dir;
                        }
                        else if (!string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase))
                        {
                            parent = null;
                            break;
                        }
                    }
                    if (parent != null)
                    {
                        string name = Path.GetFileName(Path.GetFullPath(parent).TrimEnd('\\'));
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                }
            }
            catch
            {
            }
            return "Archive";
        }

        private string BuildDefaultArchivePath(string baseName, string extension)
        {
            try
            {
                string dir;
                if (directoryMode != null)
                {
                    dir = directoryMode;
                }
                else if (items != null && items.Count > 0)
                {
                    dir = Path.GetDirectoryName(Path.GetFullPath(items[0]));
                }
                else
                {
                    dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }
                string candidate = Path.Combine(dir, baseName + extension);
                int i = 2;
                while (File.Exists(candidate) && i < 100)
                {
                    candidate = Path.Combine(dir, baseName + " (" + i + ")" + extension);
                    i++;
                }
                return candidate;
            }
            catch
            {
                return baseName + extension;
            }
        }

        private void PopulateFormats(int sourceCount)
        {
            bool cli = ArchiveEngineSelector.HasCliEngine;
            var formats = new List<FormatItem>
            {
                new FormatItem { Kind = ArchiveFormatKind.SevenZip, Display = "7z (7-Zip)", Extension = ".7z" },
                new FormatItem { Kind = ArchiveFormatKind.Zip, Display = "zip", Extension = ".zip" }
            };
            if (cli)
            {
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.Tar, Display = "tar", Extension = ".tar" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.TarGz, Display = "tar.gz", Extension = ".tar.gz" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.TarBz2, Display = "tar.bz2", Extension = ".tar.bz2" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.TarXz, Display = "tar.xz", Extension = ".tar.xz" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.Gzip, Display = "gzip (single file)", Extension = ".gz" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.BZip2, Display = "bzip2 (single file)", Extension = ".bz2" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.Xz, Display = "xz (single file)", Extension = ".xz" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.Wim, Display = "wim", Extension = ".wim" });
            }
            else
            {
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.Tar, Display = "tar", Extension = ".tar" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.TarGz, Display = "tar.gz", Extension = ".tar.gz" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.TarBz2, Display = "tar.bz2", Extension = ".tar.bz2" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.Gzip, Display = "gzip (single file)", Extension = ".gz" });
                formats.Add(new FormatItem { Kind = ArchiveFormatKind.BZip2, Display = "bzip2 (single file)", Extension = ".bz2" });
                noticeLabel.Visible = true;
            }

            if (sourceCount > 1)
            {
                // Single-file formats make no sense for multiple items.
                formats.RemoveAll(f => f.Kind == ArchiveFormatKind.Gzip || f.Kind == ArchiveFormatKind.BZip2 || f.Kind == ArchiveFormatKind.Xz);
            }
            foreach (FormatItem format in formats)
            {
                formatCombo.Items.Add(format);
            }
        }

        private FormatItem SelectedFormat()
        {
            return formatCombo.SelectedItem as FormatItem;
        }

        #endregion

        #region Interaction

        private void FormatChanged()
        {
            FormatItem format = SelectedFormat();
            if (format == null)
            {
                return;
            }

            // Suggest the matching extension on the destination name.
            string current = destBox.Text.Trim();
            if (current.Length > 0)
            {
                try
                {
                    string dir = Path.GetDirectoryName(current);
                    string baseName = ArchiveFormats.StripArchiveExtension(Path.GetFileName(current));
                    destBox.Text = Path.Combine(string.IsNullOrEmpty(dir) ? "." : dir, baseName + format.Extension);
                }
                catch
                {
                }
            }

            methodCombo.Items.Clear();
            bool sevenZip = format.Kind == ArchiveFormatKind.SevenZip;
            bool zip = format.Kind == ArchiveFormatKind.Zip;
            if (sevenZip)
            {
                foreach (string m in new[] { "LZMA2", "LZMA", "PPMd", "BZip2", "Deflate" })
                {
                    methodCombo.Items.Add(m);
                }
                methodCombo.SelectedIndex = 0;
            }
            else if (zip)
            {
                foreach (string m in new[] { "Deflate", "Deflate64", "BZip2", "LZMA", "PPMd" })
                {
                    methodCombo.Items.Add(m);
                }
                methodCombo.SelectedIndex = 0;
            }

            bool hasMethod = (sevenZip || zip) && LevelValue() > 0;
            methodCombo.Enabled = hasMethod;
            methodLabelEnabled(methodCombo, hasMethod);

            bool lzma = sevenZip && (methodCombo.SelectedIndex <= 1) && LevelValue() > 0;
            dictCombo.Enabled = lzma;
            methodLabelEnabled(dictCombo, lzma);

            solidCheck.Enabled = sevenZip;
            encryptNamesCheck.Visible = sevenZip;

            bool volumes = ArchiveEngineSelector.HasCliEngine && (sevenZip || zip);
            volumeCombo.Enabled = volumes;
            if (!volumes)
            {
                volumeCombo.SelectedIndex = 0;
                volumeBox.Visible = false;
            }
        }

        private void methodLabelEnabled(Control control, bool enabled)
        {
            control.ForeColor = enabled ? SystemColors.WindowText : SystemColors.GrayText;
        }

        private int LevelValue()
        {
            switch (levelCombo.SelectedIndex)
            {
                case 0: return 0;
                case 1: return 1;
                case 2: return 3;
                case 3: return 5;
                case 4: return 7;
                case 5: return 9;
                default: return 5;
            }
        }

        private bool IsCustomVolume()
        {
            return volumeCombo.SelectedIndex == volumeCombo.Items.Count - 1 && volumeCombo.Items.Count > 0;
        }

        private void BrowseDestination()
        {
            FormatItem format = SelectedFormat();
            string ext = format != null ? format.Extension : ".zip";
            using (var dialog = new SaveFileDialog
            {
                Title = Strings.S("ArchiveName"),
                Filter = "*" + ext + "|*" + ext + "|" + Strings.S("AllFiles") + "|*.*",
                FileName = Path.GetFileName(destBox.Text)
            })
            {
                try
                {
                    string dir = Path.GetDirectoryName(Path.GetFullPath(destBox.Text));
                    if (Directory.Exists(dir))
                    {
                        dialog.InitialDirectory = dir;
                    }
                }
                catch
                {
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    destBox.Text = dialog.FileName;
                }
            }
        }

        private void ValidateAndAccept()
        {
            string destination = destBox.Text.Trim();
            if (destination.Length == 0)
            {
                MessageBox.Show(this, Strings.S("ArchiveName"), Strings.S("Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            string password = passwordBox.Text;
            if (password.Length > 0 && password != password2Box.Text)
            {
                MessageBox.Show(this, Strings.S("PasswordMismatch"), Strings.S("Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            string volume = null;
            if (volumeCombo.Enabled && volumeCombo.SelectedIndex > 0)
            {
                volume = IsCustomVolume() ? volumeBox.Text.Trim() : (string)volumeCombo.SelectedItem;
                if (volume == Strings.S("VolumeNone"))
                {
                    volume = null;
                }
                if (volume != null && !System.Text.RegularExpressions.Regex.IsMatch(volume, @"^[0-9]+[bkmgt]?$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    MessageBox.Show(this, Strings.S("Volume") + " - 100m / 1g", Strings.S("Error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
            }

            FormatItem format = SelectedFormat();
            if (format == null)
            {
                return;
            }
            var engine = ArchiveEngineSelector.ForCreating(format.Kind);
            if (!engine.CanCreate(format.Kind))
            {
                MessageBox.Show(this, Strings.S("NoEngineForFormat"), Strings.S("Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (File.Exists(destination))
            {
                DialogResult answer = MessageBox.Show(this,
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.S("ArchiveExists"), Path.GetFileName(destination)),
                    Strings.S("AppName"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel)
                {
                    return;
                }
                if (answer == DialogResult.Yes)
                {
                    try { File.Delete(destination); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, Strings.S("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            CreateOptions options = directoryMode != null
                ? BuildOptionsForDirectory()
                : SourceSet.BuildCreateOptions(items);
            options.ArchivePath = Path.GetFullPath(destination);
            options.Kind = format.Kind;
            options.Level = LevelValue();
            options.Password = password.Length > 0 ? password : null;
            options.EncryptFileNames = encryptNamesCheck.Visible && encryptNamesCheck.Checked;
            options.Solid = solidCheck.Enabled && solidCheck.Checked;
            options.SplitVolume = volume;
            options.Method = methodCombo.Enabled && methodCombo.SelectedItem != null ? (string)methodCombo.SelectedItem : null;
            options.DictionarySize = dictCombo.Enabled && dictCombo.SelectedIndex > 0 ? (string)dictCombo.SelectedItem : null;

            Options = options;
            DialogResult = DialogResult.OK;
        }

        private CreateOptions BuildOptionsForDirectory()
        {
            var options = new CreateOptions
            {
                WorkDirectory = directoryMode,
                RelativeSources = new List<string>()
            };
            foreach (string child in Directory.GetFileSystemEntries(directoryMode))
            {
                options.RelativeSources.Add(Path.GetFileName(child));
            }
            return options;
        }

        #endregion
    }
}
