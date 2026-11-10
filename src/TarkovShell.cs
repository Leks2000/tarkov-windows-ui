using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    public static string Dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

    [STAThread]
    static void Main(string[] args)
    {
        bool welcome = false, daemon = false;
        foreach (string a in args)
        {
            if (a == "--startup") { welcome = true; daemon = true; }
            if (a == "--welcome") welcome = true;
            if (a == "--daemon") daemon = true;
        }
        if (!welcome && !daemon) welcome = true;
        if (welcome)
        {
            string w = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TarkovWelcome.exe");
            if (File.Exists(w)) System.Diagnostics.Process.Start(w);
        }
        if (!daemon) return;
        bool created;
        Mutex m = new Mutex(true, "TarkovShellMutexD", out created);
        if (!created) return;
        Application.EnableVisualStyles();
        Snd.Start();
        Application.Run(new DaemonContext());
        GC.KeepAlive(m);
    }
}

// Отдельный аудио-поток: хук мыши только кладёт задачу в очередь и сразу возвращается.
// Каждый звук открыт в 3 копиях (MCI-алиасы), поэтому клики и наведения не обрывают друг друга.
static class Snd
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern int mciSendString(string cmd, StringBuilder ret, int len, IntPtr cb);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    struct RECT { public int L, T, R, B; }

    static BlockingCollection<string> q = new BlockingCollection<string>(32);
    static int ci, oi;
    static readonly string[] Skip = { "chrome", "msedge", "firefox", "opera", "brave", "yandex", "browser", "vivaldi", "arc" };

    public static void Start()
    {
        Thread t = new Thread(delegate ()
        {
            for (int i = 0; i < 3; i++)
            {
                mciSendString("open \"" + Path.Combine(Program.Dir, "button_click.wav") + "\" type waveaudio alias c" + i, null, 0, IntPtr.Zero);
                mciSendString("open \"" + Path.Combine(Program.Dir, "button_over.wav") + "\" type waveaudio alias o" + i, null, 0, IntPtr.Zero);
            }
            foreach (string a in q.GetConsumingEnumerable())
                mciSendString("play " + a + " from 0", null, 0, IntPtr.Zero);
        });
        t.IsBackground = true; t.Start();
    }

    public static void Click() { q.TryAdd("c" + (ci = (ci + 1) % 3)); }
    public static void Over() { if (!Muted()) q.TryAdd("o" + (oi = (oi + 1) % 3)); }

    // наведение молчит в браузерах и в полноэкранных окнах (игры)
    static bool Muted()
    {
        try
        {
            IntPtr h = GetForegroundWindow(); if (h == IntPtr.Zero) return false;
            uint pid; GetWindowThreadProcessId(h, out pid);
            string n = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName.ToLower();
            foreach (string s in Skip) if (n == s || n.StartsWith(s)) return true;
            if (n == "explorer") return false;
            RECT r; GetWindowRect(h, out r);
            Rectangle b = Screen.FromHandle(h).Bounds;
            return r.L <= b.Left && r.T <= b.Top && r.R >= b.Right && r.B >= b.Bottom;
        }
        catch { return false; }
    }
}

class DaemonContext : ApplicationContext
{
    delegate IntPtr HookProc(int n, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc p, IntPtr h, uint tid);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point p);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromPoint(Point pt, [MarshalAs(UnmanagedType.Interface)] out Accessibility.IAccessible acc, out object child);

    HookProc proc; IntPtr hook; NotifyIcon ni;
    static readonly int[] Roles = { 12, 30, 34, 36, 37, 43, 44, 45, 57, 62 };

    public DaemonContext()
    {
        proc = Cb;
        hook = SetWindowsHookEx(14, proc, GetModuleHandle(null), 0);
        Thread th = new Thread(HoverLoop); th.IsBackground = true; th.Start();
        ni = new NotifyIcon();
        ni.Icon = SystemIcons.Shield;
        ni.Text = "Tarkov UI";
        ContextMenuStrip ms = new ContextMenuStrip();
        ms.Items.Add("Выход", null, delegate { ni.Visible = false; UnhookWindowsHookEx(hook); Application.Exit(); });
        ni.ContextMenuStrip = ms;
        ni.Visible = true;
    }

    IntPtr Cb(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0)
        {
            int msg = (int)w;
            if (msg == 0x201 || msg == 0x204) Snd.Click();
        }
        return CallNextHookEx(hook, n, w, l);
    }

    // Элемент под курсором определяется ролью и прямоугольником (имя у кнопок панели задач меняется — его не берём).
    // Звук только когда новый элемент продержался 2 опроса подряд, не чаще раза в 90 мс.
    void HoverLoop()
    {
        string last = "", cand = ""; int stable = 0; DateTime lastPlay = DateTime.MinValue;
        Point lastPos = Point.Empty;
        while (true)
        {
            Thread.Sleep(45);
            Point pos; if (!GetCursorPos(out pos)) continue;
            string id = "";
            if (pos != lastPos || stable < 2)
            {
                try
                {
                    Accessibility.IAccessible a; object c;
                    if (AccessibleObjectFromPoint(pos, out a, out c) == 0 && a != null)
                    {
                        object r = a.get_accRole(c);
                        int role = r is int ? (int)r : 0;
                        if (Array.IndexOf(Roles, role) >= 0)
                        {
                            int x, y, wd, ht; a.accLocation(out x, out y, out wd, out ht, c);
                            if (wd > 0 && ht > 0 && wd < 1200 && ht < 600) id = role + "|" + x + "," + y + "," + wd + "," + ht;
                        }
                    }
                }
                catch { id = cand; }
            }
            else id = cand;
            lastPos = pos;

            if (id == cand) stable++; else { cand = id; stable = 1; }
            if (stable == 2 && cand != last)
            {
                last = cand;
                if (cand != "" && (DateTime.Now - lastPlay).TotalMilliseconds > 90) { lastPlay = DateTime.Now; Snd.Over(); }
            }
        }
    }
}
