using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Devvio.Archiver.Core;

namespace Devvio.Archiver.App.Forms
{
    /// <summary>
    /// Modal progress window that runs an archive operation on a worker thread.
    /// Supports percent/marquee progress, current file, engine messages, cancellation
    /// and password prompts marshalled back to the UI thread.
    /// </summary>
    public sealed class ProgressForm : Form
    {
        private readonly Label statusLabel;
        private readonly Label fileLabel;
        private readonly ProgressBar bar;
        private readonly Button cancelButton;
        private readonly Button openButton;
        private readonly Button closeButton;

        private readonly bool autoCloseOnSuccess;
        private OperationContext context;
        private Thread worker;
        private volatile bool finished;
        private DateTime started;

        public bool Succeeded { get; private set; }
        public Exception Error { get; private set; }
        public string DestinationFolder { get; set; }

        public ProgressForm(string title, string initialStatus)
            : this(title, initialStatus, false)
        {
        }

        public ProgressForm(string title, string initialStatus, bool autoCloseOnSuccess)
        {
            this.autoCloseOnSuccess = autoCloseOnSuccess;

            Text = title ?? Strings.S("AppName");
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 168);
            RightToLeft = Strings.IsRtl ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = Strings.IsRtl;

            statusLabel = new Label
            {
                AutoEllipsis = true,
                Location = new Point(12, 14),
                Size = new Size(492, 20),
                Text = string.IsNullOrEmpty(initialStatus) ? Strings.S("AppName") : initialStatus
            };

            bar = new ProgressBar
            {
                Location = new Point(12, 40),
                Size = new Size(492, 20),
                Style = ProgressBarStyle.Marquee
            };

            fileLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(12, 68),
                Size = new Size(492, 40),
                Text = string.Empty
            };

            cancelButton = new Button
            {
                DialogResult = DialogResult.None,
                Location = new Point(430, 122),
                Size = new Size(78, 28),
                Text = Strings.S("StopButton"),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            cancelButton.Click += delegate
            {
                if (context != null)
                {
                    context.CancelRequested = true;
                }
                cancelButton.Enabled = false;
                cancelButton.Text = "...";
                statusLabel.Text = Strings.S("Cancelled");
            };

            openButton = new Button
            {
                Location = new Point(250, 122),
                Size = new Size(170, 28),
                Text = Strings.S("OpenDestination"),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = false
            };
            openButton.Click += delegate { Program.OpenFolder(DestinationFolder); };

            closeButton = new Button
            {
                Location = new Point(340, 122),
                Size = new Size(78, 28),
                Text = Strings.S("Close"),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = false
            };
            closeButton.Click += delegate { Close(); };

            Controls.Add(statusLabel);
            Controls.Add(bar);
            Controls.Add(fileLabel);
            Controls.Add(cancelButton);
            Controls.Add(openButton);
            Controls.Add(closeButton);
        }

        /// <summary>Password provider that can safely be called from worker threads.</summary>
        public string PasswordProvider(string archivePath, bool previousAttemptFailed)
        {
            if (InvokeRequired)
            {
                try
                {
                    return (string)Invoke(new Func<string, bool, string>(PasswordProviderImpl), archivePath, previousAttemptFailed);
                }
                catch
                {
                    return null;
                }
            }
            return PasswordProviderImpl(archivePath, previousAttemptFailed);
        }

        private string PasswordProviderImpl(string archivePath, bool previousAttemptFailed)
        {
            return PasswordForm.AskPassword(this,
                previousAttemptFailed ? Strings.S("PasswordWrong") : Strings.S("PasswordPrompt"),
                Strings.S("PasswordPromptTitle"));
        }

        /// <summary>
        /// Shows the form modally and runs the work callback on a worker thread.
        /// </summary>
        public void Run(Action<OperationContext, ProgressForm> work)
        {
            context = new OperationContext(OnPercent, OnFile, OnMessage);
            started = DateTime.Now;

            worker = new Thread(delegate()
            {
                Exception error = null;
                try
                {
                    work(context, this);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                try
                {
                    BeginInvoke((MethodInvoker)delegate { WorkFinished(error); });
                }
                catch
                {
                }
            });
            worker.IsBackground = false;
            worker.Start();

            ShowDialog();
        }

        private void WorkFinished(Exception error)
        {
            if (finished)
            {
                return;
            }
            finished = true;
            Error = error;
            Succeeded = error == null;

            if (Succeeded)
            {
                bar.Value = 100;
                bar.Style = ProgressBarStyle.Continuous;
                statusLabel.Text = Strings.S("OperationDone");
                double seconds = (DateTime.Now - started).TotalSeconds;
                fileLabel.Text = seconds > 1 ? strings_TimeLabel(seconds) : string.Empty;

                if (autoCloseOnSuccess && (DestinationFolder == null || !System.IO.Directory.Exists(DestinationFolder)))
                {
                    Close();
                    return;
                }
                if (autoCloseOnSuccess)
                {
                    Close();
                    if (DestinationFolder != null)
                    {
                        Program.OpenFolder(DestinationFolder);
                    }
                    return;
                }
                cancelButton.Visible = false;
                if (DestinationFolder != null && System.IO.Directory.Exists(DestinationFolder))
                {
                    openButton.Visible = true;
                }
                closeButton.Visible = true;
                AcceptButton = closeButton;
            }
            else
            {
                bar.Style = ProgressBarStyle.Continuous;
                cancelButton.Visible = false;
                closeButton.Visible = true;
                closeButton.Text = Strings.S("Close");
                bool cancelled = error is OperationCancelledException;
                statusLabel.Text = cancelled ? Strings.S("Cancelled") : Strings.S("Failed");
                fileLabel.Text = error.Message;
            }
        }

        private static string strings_TimeLabel(double seconds)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0:0.#}s", seconds);
        }

        /// <summary>Shows the stored error (if any) to the user.</summary>
        public void ShowError()
        {
            if (Error != null && !(Error is OperationCancelledException))
            {
                MessageBox.Show(this, Error.Message, Strings.S("Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!finished)
            {
                if (context != null)
                {
                    context.CancelRequested = true;
                }
                e.Cancel = true;
                cancelButton.Enabled = false;
                statusLabel.Text = Strings.S("Cancelled");
                return;
            }
            base.OnFormClosing(e);
        }

        private void OnPercent(int? value)
        {
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke((MethodInvoker)delegate { OnPercent(value); });
                    return;
                }
                if (finished)
                {
                    return;
                }
                if (value.HasValue)
                {
                    bar.Style = ProgressBarStyle.Continuous;
                    int v = Math.Max(0, Math.Min(100, value.Value));
                    if (v >= bar.Value)
                    {
                        bar.Value = v;
                    }
                }
                else
                {
                    bar.Style = ProgressBarStyle.Marquee;
                }
            }
            catch
            {
            }
        }

        private void OnFile(string file)
        {
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke((MethodInvoker)delegate { OnFile(file); });
                    return;
                }
                if (!finished)
                {
                    fileLabel.Text = file;
                }
            }
            catch
            {
            }
        }

        private void OnMessage(string message)
        {
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke((MethodInvoker)delegate { OnMessage(message); });
                    return;
                }
                if (!finished)
                {
                    statusLabel.Text = message;
                }
            }
            catch
            {
            }
        }
    }
}
