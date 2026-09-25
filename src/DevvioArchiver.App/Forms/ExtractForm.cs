using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Devvio.Archiver.Core;

namespace Devvio.Archiver.App.Forms
{
    /// <summary>Dialog with extraction options. Sets <see cref="Options"/> on OK.</summary>
    public sealed class ExtractForm : Form
    {
        private readonly List<string> archives;
        private readonly TextBox destBox;
        private readonly ComboBox overwriteCombo;
        private readonly TextBox passwordBox;
        private readonly CheckBox unwrapCheck;
        private readonly CheckBox openCheck;
        private readonly Button okButton;

        public ExtractOptions Options { get; private set; }

        public ExtractForm(List<string> archives)
        {
            this.archives = archives ?? new List<string>();
            if (this.archives.Count == 0)
            {
                this.archives.Add(string.Empty);
            }

            string first = this.archives[0];
            string defaultDir;
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(first));
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }
                defaultDir = this.archives.Count == 1
                    ? Path.Combine(dir, ArchiveFormats.StripArchiveExtension(Path.GetFileName(first)))
                    : dir;
            }
            catch
            {
                defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            Text = Strings.S("ExtractTitle");
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 268);
            RightToLeft = Strings.IsRtl ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = Strings.IsRtl;

            var info = new Label
            {
                AutoEllipsis = true,
                Location = new Point(12, 12),
                Size = new Size(496, 20),
                Text = this.archives.Count == 1
                    ? Path.GetFileName(first)
                    : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.S("ItemsToCompress"), this.archives.Count)
            };

            var destLabel = new Label
            {
                Location = new Point(12, 40),
                Size = new Size(100, 20),
                Text = Strings.S("ExtractTo")
            };

            destBox = new TextBox
            {
                Location = new Point(116, 37),
                Size = new Size(330, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = defaultDir
            };

            var browseButton = new Button
            {
                Location = new Point(452, 35),
                Size = new Size(56, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = "..."
            };
            browseButton.Click += delegate
            {
                using (var dialog = new FolderBrowserDialog { ShowNewFolderButton = true, SelectedPath = destBox.Text })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        destBox.Text = dialog.SelectedPath;
                    }
                }
            };

            var overwriteLabel = new Label
            {
                Location = new Point(12, 74),
                Size = new Size(100, 20),
                Text = Strings.S("OverwritePolicy")
            };

            overwriteCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(116, 71),
                Size = new Size(330, 23)
            };
            overwriteCombo.Items.Add(Strings.S("AutoRename"));
            overwriteCombo.Items.Add(Strings.S("Overwrite"));
            overwriteCombo.Items.Add(Strings.S("Skip"));
            overwriteCombo.SelectedIndex = 0;

            var passwordLabel = new Label
            {
                Location = new Point(12, 106),
                Size = new Size(100, 20),
                Text = Strings.S("Password")
            };

            passwordBox = new TextBox
            {
                Location = new Point(116, 103),
                Size = new Size(330, 23),
                UseSystemPasswordChar = true
            };

            unwrapCheck = new CheckBox
            {
                Location = new Point(12, 136),
                Size = new Size(496, 24),
                Checked = true,
                Text = Strings.S("UnwrapTar")
            };

            openCheck = new CheckBox
            {
                Location = new Point(12, 162),
                Size = new Size(496, 24),
                Checked = true,
                Text = Strings.S("OpenFolderWhenDone")
            };

            okButton = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(340, 224),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = Strings.S("Start")
            };
            okButton.Click += delegate { ValidateAndAccept(); };

            var cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(428, 224),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = Strings.S("Cancel")
            };

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Controls.Add(info);
            Controls.Add(destLabel);
            Controls.Add(destBox);
            Controls.Add(browseButton);
            Controls.Add(overwriteLabel);
            Controls.Add(overwriteCombo);
            Controls.Add(passwordLabel);
            Controls.Add(passwordBox);
            Controls.Add(unwrapCheck);
            Controls.Add(openCheck);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
        }

        private void ValidateAndAccept()
        {
            string destination = destBox.Text.Trim();
            if (destination.Length == 0)
            {
                MessageBox.Show(this, Strings.S("Destination"), Strings.S("Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            try
            {
                Directory.CreateDirectory(destination);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Strings.S("Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            OverwritePolicy overwrite;
            switch (overwriteCombo.SelectedIndex)
            {
                case 1: overwrite = OverwritePolicy.Overwrite; break;
                case 2: overwrite = OverwritePolicy.Skip; break;
                default: overwrite = OverwritePolicy.AutoRename; break;
            }

            Options = new ExtractOptions
            {
                Destination = destination,
                Overwrite = overwrite,
                Password = passwordBox.Text.Length > 0 ? passwordBox.Text : null,
                UnwrapNestedTar = unwrapCheck.Checked,
                OpenDestinationWhenDone = openCheck.Checked
            };
            DialogResult = DialogResult.OK;
        }
    }
}
