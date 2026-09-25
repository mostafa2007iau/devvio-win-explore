using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Devvio.Archiver.Core;

namespace Devvio.Archiver.App.Forms
{
    /// <summary>Password prompt dialog. Returns the entered password or null.</summary>
    public sealed class PasswordForm : Form
    {
        private readonly TextBox box;
        private readonly Button okButton;
        private string password;

        private PasswordForm(string message, string title)
        {
            Text = string.IsNullOrEmpty(title) ? Strings.S("PasswordPromptTitle") : title;
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 138);
            RightToLeft = Strings.IsRtl ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = Strings.IsRtl;

            var messageLabel = new Label
            {
                AutoEllipsis = true,
                Location = new Point(12, 12),
                Size = new Size(352, 36),
                Text = message ?? Strings.S("PasswordPrompt")
            };

            box = new TextBox
            {
                Location = new Point(12, 54),
                Size = new Size(352, 23),
                UseSystemPasswordChar = true
            };

            var showCheck = new CheckBox
            {
                Location = new Point(12, 84),
                Size = new Size(150, 22),
                Text = Strings.S("ShowPassword")
            };
            showCheck.CheckedChanged += delegate
            {
                box.UseSystemPasswordChar = !showCheck.Checked;
            };

            okButton = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(200, 106),
                Size = new Size(80, 26),
                Text = Strings.S("Ok")
            };

            var cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(286, 106),
                Size = new Size(80, 26),
                Text = Strings.S("Cancel")
            };

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Controls.Add(messageLabel);
            Controls.Add(box);
            Controls.Add(showCheck);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
        }

        public static string AskPassword(IWin32Window owner, string message, string title)
        {
            using (var form = new PasswordForm(message, title))
            {
                return form.ShowDialog(owner) == DialogResult.OK ? form.password : null;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            box.Focus();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            password = box.Text;
            base.OnClosing(e);
        }
    }
}
