using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace CampusClock
{
    public static class Program
    {
        private static Mutex singleInstance;
        private static EventWaitHandle showSignal;

        [STAThread]
        public static void Main(string[] args)
        {
            bool selftest = false;
            bool uitest = false;
            string makeIcon = null;
            string icsArg = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--selftest") selftest = true;
                else if (args[i] == "--uitest") uitest = true;
                else if (args[i] == "--shots" && i + 1 < args.Length) Selftest.ShotDir = args[++i];
                else if (args[i] == "--make-icon" && i + 1 < args.Length) makeIcon = args[++i];
                else if (args[i] == "--ics" && i + 1 < args.Length) icsArg = args[++i];
                else if (icsArg == null && args[i].EndsWith(".ics", StringComparison.OrdinalIgnoreCase)) icsArg = args[i];
            }

            if (makeIcon != null)
            {
                Paths.Init();
                TrayIcon.WriteIco(makeIcon);
                Console.WriteLine("icon written: " + makeIcon);
                return;
            }

            if (uitest)
            {
                int uiCode;
                try
                {
                    uiCode = UiTest.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("uitest 崩溃：" + ex);
                    uiCode = 2;
                }
                Environment.ExitCode = uiCode;
                Console.Out.Flush();
                return;
            }

            if (selftest)
            {
                Paths.Init();
                Paths.UseTempForTest();
                string target = icsArg;
                if (target == null)
                {
                    target = File.Exists(Paths.IcsFile) ? Paths.IcsFile : Storage.AutoDetectDesktopIcs();
                }
                int code;
                try
                {
                    code = Selftest.Run(target);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("selftest 崩溃：" + ex);
                    code = 2;
                }
                Environment.ExitCode = code;
                Console.Out.Flush();
                return;
            }

            Paths.Init();
            Log.Info("==== CampusClock 启动 ====");

            bool created;
            singleInstance = new Mutex(true, "Local\\CampusClock.SingleInstance", out created);
            if (!created)
            {
                try
                {
                    EventWaitHandle ev = EventWaitHandle.OpenExisting("Local\\CampusClock.ShowWindow");
                    ev.Set();
                }
                catch
                {
                }
                return;
            }
            try
            {
                showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\CampusClock.ShowWindow");
            }
            catch
            {
            }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.DispatcherUnhandledException += delegate (object s, DispatcherUnhandledExceptionEventArgs e)
            {
                Log.Error("未处理的界面异常：" + e.Exception);
                MessageBox.Show("发生了一个错误：\n" + e.Exception.Message + "\n\n详细信息已写入日志。",
                    "CampusClock", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Log.Error("未处理的异常：" + e.ExceptionObject);
            };

            ThemeResources.Apply();

            AppCore core = new AppCore();
            core.LoadTimetable();
            MainWindow window = new MainWindow(core);
            if (showSignal != null)
            {
                Thread watcher = new Thread(delegate ()
                {
                    while (true)
                    {
                        try
                        {
                            showSignal.WaitOne();
                            if (window.Dispatcher.HasShutdownStarted) return;
                            window.Dispatcher.Invoke(new Action(delegate
                            {
                                window.RestoreFromTray();
                                window.ShowPage("timetable");
                            }));
                        }
                        catch
                        {
                            return;
                        }
                    }
                });
                watcher.IsBackground = true;
                watcher.Start();
            }
            if (!core.Config.StartMinimized) window.Show();
            app.Run();
            Log.Info("==== CampusClock 退出 ====");
        }
    }
}
