using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CornerBreak
{
    internal static class Program
    {
        private const string InstanceName = "Local\\CornerBreak-v1";

        [STAThread]
        public static int Main(string[] args)
        {
            AppOptions options = null;
            bool headless = false;
            foreach (string arg in args)
                if (arg.IndexOf("render", StringComparison.OrdinalIgnoreCase) >= 0) headless = true;
            try
            {
                options = AppOptions.Parse(args);
                if (options.ExitRequested) return SignalExisting(InstanceName + "-Exit") ? 0 : 1;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                string instance = options.Preview ? InstanceName + "-Preview-" + Process.GetCurrentProcess().Id : InstanceName;
                bool first;
                using (var mutex = new Mutex(true, instance, out first))
                {
                    if (!first)
                    {
                        if (SignalExisting(instance + "-Show")) return 0;
                        throw new InvalidOperationException("CornerBreak 已经在运行，但暂时未响应。请稍后从托盘打开。");
                    }
                    try
                    {
                        using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, instance + "-Show"))
                        using (var exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, instance + "-Exit"))
                        using (var mascots = new MascotCatalog())
                        using (var window = new MainWindow(options, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "settings.json"), mascots))
                        using (var signals = new System.Windows.Forms.Timer { Interval = 250 })
                        {
                            if (!String.IsNullOrEmpty(options.RenderPreview) || !String.IsNullOrEmpty(options.RenderSettings) || !String.IsNullOrEmpty(options.RenderWidget))
                            {
                                if (!String.IsNullOrEmpty(options.RenderWidget)) window.RenderWidgetImage(options.RenderWidget);
                                else
                                {
                                    bool settings = !String.IsNullOrEmpty(options.RenderSettings);
                                    window.Render(settings ? options.RenderSettings : options.RenderPreview, settings);
                                }
                                return 0;
                            }
                            signals.Tick += delegate {
                                if (exitEvent.WaitOne(0)) window.RequestExit();
                                else if (showEvent.WaitOne(0)) window.ShowStatus();
                            };
                            window.StartTimers();
                            signals.Start();
                            Application.Run();
                        }
                    }
                    finally { mutex.ReleaseMutex(); }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                if (!headless && (options == null || !options.Preview))
                {
                    try
                    {
                        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                        Directory.CreateDirectory(folder);
                        File.AppendAllText(Path.Combine(folder, "CornerBreak.log"), DateTime.Now.ToString("o", CultureInfo.InvariantCulture) + " " + ex + Environment.NewLine);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    MessageBox.Show("CornerBreak 未能完成操作：\r\n" + ex.Message, "CornerBreak", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return 1;
            }
        }

        private static bool SignalExisting(string name)
        {
            // A second launch can arrive between the first process creating
            // its mutex and creating the signal. Give startup a brief window.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try { using (var signal = EventWaitHandle.OpenExisting(name)) { signal.Set(); return true; } }
                catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(50); }
            }
            return false;
        }
    }
}
