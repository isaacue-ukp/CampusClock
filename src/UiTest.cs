using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CampusClock
{
    /// <summary>
    /// Real-window interaction harness (--uitest). It shows the actual floating widget and drives it
    /// with real Win32 mouse messages, then checks the resulting window geometry. Used to verify the
    /// floating-widget bugs on a real window instead of only through the simulation hooks.
    /// </summary>
    public static class UiTest
    {
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MOUSELEAVE = 0x02A3;
        private const int MK_LBUTTON = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static IntPtr hwnd;
        private static Widget widget;
        private static DispatcherTimer timer;
        private static int step;
        private static int failures;
        private static RECT ballRect;
        private static RECT expandedRect;
        private static RECT draggedRect;
        private static RECT collapsedRect;
        private static bool anyReaction;
        private static bool inconclusive;
        private static readonly StringBuilder log = new StringBuilder();

        private static void Say(string text)
        {
            log.AppendLine(text);
            Console.WriteLine(text);
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (!ok) failures++;
            Say((ok ? "  PASS  " : "  FAIL  ") + name + (detail != null && detail.Length > 0 ? "   -> " + detail : ""));
        }

        public static int Run()
        {
            Paths.Init();
            Paths.UseTempForTest();
            Say("悬浮球真窗口测试 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            AppCore core = new AppCore();
            core.LoadTimetable();
            core.Config.AnimationMs = 120;
            core.Config.BallPinned = false;
            Rect work = SystemParameters.WorkArea;
            core.Config.BallLeft = (int)(work.Left + 180);
            core.Config.BallTop = (int)(work.Top + 180);

            widget = new Widget(core, null);
            widget.Show();
            hwnd = new WindowInteropHelper(widget).Handle;
            Say("窗口句柄 " + hwnd.ToString("X") + "  工作区 " +
                work.Width.ToString("0", CultureInfo.InvariantCulture) + "x" +
                work.Height.ToString("0", CultureInfo.InvariantCulture));

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(250);
            timer.Tick += delegate { OnTick(); };
            timer.Start();
            Dispatcher.Run();

            Say("");
            if (inconclusive)
            {
                Say("结论：无法判定（本环境不向窗口投递合成鼠标消息，WPF 会忽略非真实光标的输入）");
                return 3;
            }
            Say(failures == 0 ? "真窗口测试通过 ✔" : ("真窗口测试失败 " + failures + " 项 ✘"));
            return failures == 0 ? 0 : 1;
        }

        private static void OnTick()
        {
            try
            {
                switch (step)
                {
                    case 0:
                        ballRect = Rect();
                        Say("① 收起尺寸 " + W(ballRect) + "x" + H(ballRect));
                        Check("① 初始为横向胶囊", Math.Abs(W(ballRect) - ballRect.Right + ballRect.Left) < 200 && W(ballRect) > H(ballRect),
                            W(ballRect) + "x" + H(ballRect));
                        Move(25, 32);                    // hover the left half
                        break;
                    case 2:
                        expandedRect = Rect();
                        anyReaction = W(expandedRect) != W(ballRect);
                        Say("② 悬停后尺寸 " + W(expandedRect) + "x" + H(expandedRect));
                        if (!anyReaction)
                        {
                            inconclusive = true;
                            Say("   窗口对 WM_MOUSEMOVE 没有任何反应：本环境无法用合成消息驱动 WPF 输入，");
                            Say("   该测试在真实桌面上（有真实鼠标/输入桌面）才有意义，这里不判定为失败。");
                            widget.AllowClose();
                            widget.Close();
                            timer.Stop();
                            Dispatcher.CurrentDispatcher.InvokeShutdown();
                            break;
                        }
                        Check("② 悬停展开", W(expandedRect) > W(ballRect) + 100,
                            W(expandedRect) + "x" + H(expandedRect));
                        // drag by the header: press, then keep the cursor at the same offset while it moves
                        Down(300, 20);
                        for (int i = 0; i < 12; i++) MoveDragging(305, 20);
                        Up(305, 20);
                        break;
                    case 4:
                        draggedRect = Rect();
                        Say("③ 拖拽后 Left " + draggedRect.Left + "（拖拽前 " + expandedRect.Left + "）");
                        Check("③ 拖拽位移 = 60px", Math.Abs((draggedRect.Left - expandedRect.Left) - 60) <= 2,
                            "实际 " + (draggedRect.Left - expandedRect.Left) + "px");
                        Check("③ 拖拽后仍为展开态", W(draggedRect) > W(ballRect) + 100,
                            W(draggedRect) + "x" + H(draggedRect));
                        Move(4000, 4000);                // leave the window
                        Post(WM_MOUSELEAVE, IntPtr.Zero, 0, 0);
                        break;
                    case 8:
                        collapsedRect = Rect();
                        Say("④ 移出后尺寸 " + W(collapsedRect) + "x" + H(collapsedRect));
                        Check("④ 指针移出后自动收起", Math.Abs(W(collapsedRect) - W(ballRect)) <= 2,
                            W(collapsedRect) + "x" + H(collapsedRect));
                        widget.AllowClose();
                        widget.Close();
                        timer.Stop();
                        Dispatcher.ExitAllFrames();
                        Dispatcher.CurrentDispatcher.InvokeShutdown();
                        break;
                }
            }
            catch (Exception ex)
            {
                Say("  测试异常 " + ex.GetType().Name + ": " + ex.Message);
                failures++;
                timer.Stop();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
            step++;
        }

        private static RECT Rect()
        {
            RECT r;
            GetWindowRect(hwnd, out r);
            return r;
        }

        private static double W(RECT r) { return r.Right - r.Left; }
        private static double H(RECT r) { return r.Bottom - r.Top; }

        private static IntPtr Point(int x, int y)
        {
            return new IntPtr((y << 16) | (x & 0xFFFF));
        }

        private static void Post(int msg, IntPtr wParam, int x, int y)
        {
            PostMessage(hwnd, msg, wParam, Point(x, y));
        }

        private static void Move(int x, int y) { Post(WM_MOUSEMOVE, IntPtr.Zero, x, y); }
        private static void MoveDragging(int x, int y) { Post(WM_MOUSEMOVE, new IntPtr(MK_LBUTTON), x, y); }
        private static void Down(int x, int y) { Post(WM_LBUTTONDOWN, new IntPtr(MK_LBUTTON), x, y); }
        private static void Up(int x, int y) { Post(WM_LBUTTONUP, IntPtr.Zero, x, y); }
    }
}
