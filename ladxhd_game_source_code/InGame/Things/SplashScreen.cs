using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#if WINDOWS
using System.Drawing;
using System.Windows.Forms;
#endif

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Provides a splash screen and progress display during startup operations.
    /// </summary>
    public static class SplashScreen
    {
        private static readonly string GameTitle = "Link's Awakening DX HD";
        private static readonly string Version = "v1.5.2";

#if WINDOWS
        private static Form _splashForm;
        private static Label _statusLabel;
        private static ProgressBar _progressBar;
        private static bool _formReady;
#else
        private static Process _zenityProcess;
        private static StreamWriter _zenityInput;
#endif

        /// <summary>
        /// Shows the splash screen.
        /// </summary>
        public static void Show(string initialStatus = "Starting...")
        {
#if WINDOWS
            ShowWindowsSplash(initialStatus);
#else
            ShowLinuxSplash(initialStatus);
#endif
        }

        /// <summary>
        /// Updates the status text on the splash screen.
        /// </summary>
        public static void SetStatus(string status)
        {
            Console.WriteLine(status);
#if WINDOWS
            UpdateWindowsStatus(status);
#else
            UpdateLinuxStatus(status);
#endif
        }

        /// <summary>
        /// Updates the progress bar (0-100).
        /// </summary>
        public static void SetProgress(int percent)
        {
#if WINDOWS
            UpdateWindowsProgress(percent);
#else
            UpdateLinuxProgress(percent);
#endif
        }

        /// <summary>
        /// Closes the splash screen.
        /// </summary>
        public static void Close()
        {
#if WINDOWS
            CloseWindowsSplash();
#else
            CloseLinuxSplash();
#endif
        }

#if WINDOWS
        private static void ShowWindowsSplash(string initialStatus)
        {
            // Run splash form on a separate thread
            var splashThread = new Thread(() =>
            {
                _splashForm = new Form
                {
                    Text = GameTitle,
                    Size = new Size(450, 280),
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    BackColor = Color.FromArgb(20, 20, 30),
                    ForeColor = Color.White
                };

                // Title label
                var titleLabel = new Label
                {
                    Text = GameTitle,
                    Font = new Font("Segoe UI", 24, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 200, 100),
                    AutoSize = false,
                    Size = new Size(430, 50),
                    Location = new Point(10, 30),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                _splashForm.Controls.Add(titleLabel);

                // Version label
                var versionLabel = new Label
                {
                    Text = Version,
                    Font = new Font("Segoe UI", 12),
                    ForeColor = Color.FromArgb(150, 150, 150),
                    AutoSize = false,
                    Size = new Size(430, 25),
                    Location = new Point(10, 80),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                _splashForm.Controls.Add(versionLabel);

                // Subtitle
                var subtitleLabel = new Label
                {
                    Text = "HD Remake of The Legend of Zelda: Link's Awakening DX",
                    Font = new Font("Segoe UI", 9),
                    ForeColor = Color.FromArgb(120, 120, 120),
                    AutoSize = false,
                    Size = new Size(430, 20),
                    Location = new Point(10, 110),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                _splashForm.Controls.Add(subtitleLabel);

                // Progress bar
                _progressBar = new ProgressBar
                {
                    Size = new Size(400, 20),
                    Location = new Point(25, 160),
                    Style = ProgressBarStyle.Continuous,
                    Value = 0
                };
                _splashForm.Controls.Add(_progressBar);

                // Status label
                _statusLabel = new Label
                {
                    Text = initialStatus,
                    Font = new Font("Segoe UI", 10),
                    ForeColor = Color.FromArgb(200, 200, 200),
                    AutoSize = false,
                    Size = new Size(400, 40),
                    Location = new Point(25, 190),
                    TextAlign = ContentAlignment.TopCenter
                };
                _splashForm.Controls.Add(_statusLabel);

                _formReady = true;
                Application.Run(_splashForm);
            });
            splashThread.SetApartmentState(ApartmentState.STA);
            splashThread.IsBackground = true;
            splashThread.Start();

            // Wait for form to be ready
            while (!_formReady)
                Thread.Sleep(10);
            Thread.Sleep(100); // Give it a moment to render
        }

        private static void UpdateWindowsStatus(string status)
        {
            if (_splashForm == null || _statusLabel == null || _splashForm.IsDisposed)
                return;

            try
            {
                _splashForm.Invoke((Action)(() =>
                {
                    _statusLabel.Text = status;
                }));
            }
            catch { /* Form may have closed */ }
        }

        private static void UpdateWindowsProgress(int percent)
        {
            if (_splashForm == null || _progressBar == null || _splashForm.IsDisposed)
                return;

            try
            {
                _splashForm.Invoke((Action)(() =>
                {
                    _progressBar.Value = Math.Max(0, Math.Min(100, percent));
                }));
            }
            catch { /* Form may have closed */ }
        }

        private static void CloseWindowsSplash()
        {
            if (_splashForm == null || _splashForm.IsDisposed)
                return;

            try
            {
                _splashForm.Invoke((Action)(() =>
                {
                    _splashForm.Close();
                }));
            }
            catch { /* Form may have closed */ }
            
            _splashForm = null;
            _statusLabel = null;
            _progressBar = null;
            _formReady = false;
        }
#else
        private static void ShowLinuxSplash(string initialStatus)
        {
            // Print console banner
            Console.WriteLine("");
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                                                            ║");
            Console.WriteLine("║          Link's Awakening DX HD  v1.5.2                    ║");
            Console.WriteLine("║                                                            ║");
            Console.WriteLine("║    HD Remake of The Legend of Zelda: Link's Awakening DX  ║");
            Console.WriteLine("║                                                            ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine("");
            Console.WriteLine($"  {initialStatus}");
            Console.WriteLine("");

            // Start zenity progress dialog for GUI feedback
            try
            {
                _zenityProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "zenity",
                        Arguments = $"--progress --title=\"{GameTitle}\" --text=\"{initialStatus}\" --percentage=0 --width=450 --no-cancel",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                _zenityProcess.Start();
                _zenityInput = _zenityProcess.StandardInput;
            }
            catch
            {
                // zenity not available, fall back to console only
                Console.WriteLine("  (Install 'zenity' for a graphical progress dialog)");
                _zenityProcess = null;
                _zenityInput = null;
            }
        }

        private static void UpdateLinuxStatus(string status)
        {
            // Update zenity if running
            if (_zenityInput != null && _zenityProcess != null && !_zenityProcess.HasExited)
            {
                try
                {
                    _zenityInput.WriteLine($"# {status}");
                    _zenityInput.Flush();
                }
                catch { /* Process may have ended */ }
            }
        }

        private static void UpdateLinuxProgress(int percent)
        {
            if (_zenityInput != null && _zenityProcess != null && !_zenityProcess.HasExited)
            {
                try
                {
                    _zenityInput.WriteLine(percent.ToString());
                    _zenityInput.Flush();
                }
                catch { /* Process may have ended */ }
            }
        }

        private static void CloseLinuxSplash()
        {
            if (_zenityProcess != null)
            {
                try
                {
                    if (!_zenityProcess.HasExited)
                    {
                        // Send 100% to trigger auto-close, then close input
                        _zenityInput?.WriteLine("100");
                        _zenityInput?.Flush();
                        Thread.Sleep(100); // Give zenity a moment to close
                    }
                    _zenityInput?.Close();
                    
                    // Wait briefly, then kill if still running
                    if (!_zenityProcess.WaitForExit(500))
                    {
                        try { _zenityProcess.Kill(); } catch { }
                    }
                }
                catch { /* Process may have ended */ }
                finally
                {
                    _zenityProcess = null;
                    _zenityInput = null;
                }
            }
        }
#endif

        /// <summary>
        /// Shows a message box for errors.
        /// </summary>
        public static void ShowError(string message, string title = "Error")
        {
#if WINDOWS
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
#else
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "zenity",
                        Arguments = $"--error --title=\"{title}\" --text=\"{message}\" --width=400",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            catch
            {
                // Fall back to console
                Console.WriteLine($"\n[ERROR] {title}: {message}\n");
            }
#endif
        }

        /// <summary>
        /// Shows an info message box.
        /// </summary>
        public static void ShowInfo(string message, string title = "Information")
        {
#if WINDOWS
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
#else
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "zenity",
                        Arguments = $"--info --title=\"{title}\" --text=\"{message}\" --width=400",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            catch
            {
                // Fall back to console
                Console.WriteLine($"\n[INFO] {title}: {message}\n");
            }
#endif
        }
    }
}
