using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Devvio.Archiver.Core;

namespace Devvio.Archiver.App.Forms
{
    /// <summary>Shown when the app is started directly (no command line).</summary>
    public sealed class WelcomeForm : Form
    {
        public WelcomeForm()
        {
            Text = Strings.S("WelcomeTitle");
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(480, 260);
            RightToLeft = Strings.IsRtl ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = Strings.IsRtl;

            var text = new Label
            {
                Location = new Point(16, 16),
                Size = new Size(448, 150),
                Text = Strings.S("WelcomeText")
            };

            var openButton = new Button
            {
                Location = new Point(140, 190),
                Size = new Size(200, 32),
                Text = Strings.S("WelcomeOpenArchive")
            };
            openButton.Click += delegate
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = Strings.S("WelcomeOpenArchive");
                    dialog.Filter = BuildFilter() + "|" + Strings.S("AllFiles") + "|*.*";
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        using (var browser = new BrowserForm(dialog.FileName))
                        {
                            browser.ShowDialog(this);
                        }
                    }
                }
            };

            var closeButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(356, 190),
                Size = new Size(90, 32),
                Text = Strings.S("Close")
            };
            CancelButton = closeButton;

            Controls.Add(text);
            Controls.Add(openButton);
            Controls.Add(closeButton);
        }

        public static string BuildFilter()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Strings.S("ArchiveFilter")).Append('|');
            bool first = true;
            foreach (string ext in ArchiveFormats.KnownExtensions)
            {
                if (!first)
                {
                    sb.Append(';');
                }
                sb.Append('*').Append(ext);
                first = false;
            }
            sb.Append('|');
            return sb.ToString();
        }
    }
}
