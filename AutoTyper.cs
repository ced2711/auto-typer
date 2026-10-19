using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Used by build.bat to render the exe icon from the same drawing code the window uses.
        if (args.Length == 2 && args[0] == "--write-icon")
        {
            File.WriteAllBytes(args[1], Logo.Ico());
            return;
        }
        Native.DpiAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

// English by default; Chinese on request.
static class Lang
{
    public static bool Zh;

    public static string T(string en, string zh) { return Zh ? zh : en; }
}

static class Native
{
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_RETURN = 0x0D;
    public const ushort VK_TAB = 0x09;
    public const uint VK_F9 = 0x78;
    public const uint VK_F10 = 0x79;
    public const int WM_PAINT = 0x000F, WM_SETICON = 0x0080, WM_HOTKEY = 0x0312, WM_DPICHANGED = 0x02E0;

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public InputUnion U; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr h);
    [DllImport("winmm.dll")]
    public static extern uint timeBeginPeriod(uint uMilliseconds);
    [DllImport("user32.dll")]
    static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr mon, int type, out uint x, out uint y);
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr h, string app, string idList);

    // Per-monitor v2 keeps everything crisp when the window moves between displays.
    public static void DpiAware()
    {
        try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }
        try { SetProcessDPIAware(); } catch { }
    }

    public static float ScaleOf(IntPtr h)
    {
        try { uint d = GetDpiForWindow(h); if (d > 0) return d / 96f; } catch { }
        return ScaleAt(Cursor.Position);
    }

    public static float ScaleAt(Point p)
    {
        try
        {
            uint dx, dy;
            POINT pt = new POINT { X = p.X, Y = p.Y };
            if (GetDpiForMonitor(MonitorFromPoint(pt, 2), 0, out dx, out dy) == 0) return dx / 96f;
        }
        catch { }
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
    }

    public static void Dwm(IntPtr h, int attr, int val)
    {
        try { DwmSetWindowAttribute(h, attr, ref val, 4); } catch { }
    }

    public static void DarkScrollbars(IntPtr h)
    {
        try { SetWindowTheme(h, "DarkMode_Explorer", null); } catch { }
    }

    static INPUT Key(ushort vk, ushort scan, uint flags)
    {
        INPUT i = new INPUT();
        i.type = INPUT_KEYBOARD;
        i.U.ki.wVk = vk;
        i.U.ki.wScan = scan;
        i.U.ki.dwFlags = flags;
        return i;
    }

    // Types any text (incl. CJK and emoji) as Unicode key events, independent of keyboard layout / IME.
    public static void SendUnicode(string s)
    {
        INPUT[] inputs = new INPUT[s.Length * 2];
        for (int k = 0; k < s.Length; k++)
        {
            inputs[k * 2] = Key(0, s[k], KEYEVENTF_UNICODE);
            inputs[k * 2 + 1] = Key(0, s[k], KEYEVENTF_UNICODE | KEYEVENTF_KEYUP);
        }
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void SendVk(ushort vk, bool withShift)
    {
        INPUT[] inputs = withShift
            ? new INPUT[] { Key(VK_SHIFT, 0, 0), Key(vk, 0, 0), Key(vk, 0, KEYEVENTF_KEYUP), Key(VK_SHIFT, 0, KEYEVENTF_KEYUP) }
            : new INPUT[] { Key(vk, 0, 0), Key(vk, 0, KEYEVENTF_KEYUP) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}

static class Ui
{
    public static readonly Color Bg = Color.FromArgb(23, 23, 27);
    public static readonly Color Card = Color.FromArgb(31, 31, 36);
    public static readonly Color Surface = Color.FromArgb(42, 42, 48);
    public static readonly Color SurfaceHot = Color.FromArgb(56, 56, 64);
    public static readonly Color Line = Color.FromArgb(44, 44, 50);
    public static readonly Color Fg = Color.FromArgb(236, 236, 240);
    public static readonly Color Sub = Color.FromArgb(136, 136, 148);
    public static readonly Color Dim = Color.FromArgb(84, 84, 94);
    public static readonly Color Off = Color.FromArgb(58, 58, 66);
    public static readonly Color Accent = Color.FromArgb(124, 176, 255);
    public static readonly Color AccentHot = Color.FromArgb(156, 197, 255);
    public static readonly Color Good = Color.FromArgb(126, 212, 156);
    public static readonly Color Warn = Color.FromArgb(240, 140, 110);

    public static string Font { get { return Lang.Zh ? "Microsoft YaHei UI" : "Segoe UI"; } }

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        GraphicsPath p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
    {
        using (GraphicsPath p = Round(r, radius))
        using (SolidBrush b = new SolidBrush(c))
            g.FillPath(b, p);
    }

    public static void Switch(Graphics g, Rectangle r, bool on, bool hot)
    {
        Color track = on ? (hot ? AccentHot : Accent) : (hot ? SurfaceHot : Off);
        FillRound(g, track, r, r.Height / 2f);
        float pad = r.Height * 0.16f, k = r.Height - 2 * pad;
        float x = on ? r.Right - pad - k : r.X + pad;
        using (SolidBrush b = new SolidBrush(on ? Bg : Fg))
            g.FillEllipse(b, x, r.Y + pad, k, k);
    }
}

// The app icon: lines of text with a blue caret, drawn in code so it is crisp at every size.
static class Logo
{
    public static Bitmap Render(int size)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Ui.FillRound(g, Color.FromArgb(30, 30, 36), new RectangleF(0, 0, size, size), size * 0.24f);
            float x = size * 0.2f, h = Math.Max(1.6f, size * 0.1f);
            Color line = Color.FromArgb(228, 228, 234);
            Ui.FillRound(g, line, new RectangleF(x, size * 0.25f, size * 0.6f, h), h / 2);
            Ui.FillRound(g, line, new RectangleF(x, size * 0.45f, size * 0.6f, h), h / 2);
            Ui.FillRound(g, line, new RectangleF(x, size * 0.65f, size * 0.3f, h), h / 2);
            // Caret right after the last, unfinished line.
            Ui.FillRound(g, Ui.Accent, new RectangleF(x + size * 0.38f, size * 0.57f, h, size * 0.26f), h / 2);
        }
        return bmp;
    }

    // A multi-resolution .ico made of PNG frames.
    public static byte[] Ico()
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
        List<byte[]> frames = new List<byte[]>();
        foreach (int s in sizes)
            using (Bitmap bmp = Render(s))
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                frames.Add(ms.ToArray());
            }
        using (MemoryStream o = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(o))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                w.Write(dim); w.Write(dim);
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(frames[i].Length); w.Write(offset);
                offset += frames[i].Length;
            }
            foreach (byte[] f in frames) w.Write(f);
            w.Flush();
            return o.ToArray();
        }
    }
}

// Saved in %APPDATA%\AutoTyper\settings.ini.
class Settings
{
    public int Speed = 300, Jitter = 30, Delay = 3, PauseMs = 150;
    public bool ShiftEnter, TopMost;
    public string Language = "en";
    public int Width = 780, Height = 580;

    static string FilePath
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoTyper", "settings.ini"); }
    }

    public static Settings Load()
    {
        Settings s = new Settings();
        try
        {
            if (!File.Exists(FilePath)) return s;
            foreach (string line in File.ReadAllLines(FilePath))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                int n;
                bool isNum = int.TryParse(v, out n);
                switch (k)
                {
                    case "speed": if (isNum) s.Speed = Clamp(n, 10, 3000); break;
                    case "jitter": if (isNum) s.Jitter = Clamp(n, 0, 90); break;
                    case "countdown": if (isNum) s.Delay = Clamp(n, 0, 30); break;
                    case "pause": if (isNum) s.PauseMs = Clamp(n, 0, 1000); break;
                    case "shiftEnter": s.ShiftEnter = v == "1"; break;
                    case "topMost": s.TopMost = v == "1"; break;
                    case "language": s.Language = v == "zh" ? "zh" : "en"; break;
                    case "width": if (isNum) s.Width = Clamp(n, 660, 4000); break;
                    case "height": if (isNum) s.Height = Clamp(n, 520, 3000); break;
                }
            }
        }
        catch { }
        return s;
    }

    public void Save()
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("speed=" + Speed);
            sb.AppendLine("jitter=" + Jitter);
            sb.AppendLine("countdown=" + Delay);
            sb.AppendLine("pause=" + PauseMs);
            sb.AppendLine("shiftEnter=" + (ShiftEnter ? 1 : 0));
            sb.AppendLine("topMost=" + (TopMost ? 1 : 0));
            sb.AppendLine("language=" + Language);
            sb.AppendLine("width=" + Width);
            sb.AppendLine("height=" + Height);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, sb.ToString());
        }
        catch { }
    }

    public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }
}

// A borderless multiline text box that shows a hint while it is empty.
class Editor : TextBox
{
    public string Placeholder = "";
    public Color PlaceholderColor = Color.Gray;
    bool wasEmpty = true;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == Native.WM_PAINT && TextLength == 0 && Placeholder.Length > 0)
            using (Graphics g = CreateGraphics())
                TextRenderer.DrawText(g, Placeholder, Font, new Rectangle(1, 0, ClientSize.Width - 2, ClientSize.Height),
                                      PlaceholderColor, BackColor, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        bool empty = TextLength == 0;
        if (empty != wasEmpty) Invalidate();
        wasEmpty = empty;
        FitScrollbar();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        FitScrollbar();
    }

    bool fitting;

    // Shows the scroll bar only when the text does not fit, instead of an empty track.
    void FitScrollbar()
    {
        if (!IsHandleCreated || fitting) return;
        int lines = (int)Native.SendMessage(Handle, 0x00BA, IntPtr.Zero, IntPtr.Zero); // EM_GETLINECOUNT
        ScrollBars want = lines * Font.Height > ClientSize.Height ? ScrollBars.Vertical : ScrollBars.None;
        if (want == ScrollBars) return;
        fitting = true;
        int start = SelectionStart, length = SelectionLength;
        ScrollBars = want; // recreates the window
        Select(start, length);
        ScrollToCaret();
        fitting = false;
    }
}

enum Kind { Text, Status, Info, Card, Slider, Row, Sep, Link, Pill, Segment, Button, Key, Progress }

class El
{
    public Kind Kind;
    public Rectangle R;
    public string Text, Sub;
    public Font F;
    public Color C;
    public Func<bool> GetB, Enabled;
    public Action<bool> SetB;
    public Func<int> GetI;
    public Action<int> SetI;
    public Func<int, string> Fmt;
    public int Min, Max, Snap = 1;
    public bool Log, Primary;
    public Action Click;

    public bool Interactive
    {
        get
        {
            bool k = Kind == Kind.Slider || Kind == Kind.Row || Kind == Kind.Link || Kind == Kind.Segment || Kind == Kind.Button;
            return k && (Enabled == null || Enabled());
        }
    }

    public float Frac(int v)
    {
        if (Log) return (float)(Math.Log(v / (double)Min) / Math.Log(Max / (double)Min));
        return (v - Min) / (float)(Max - Min);
    }

    public int FromFrac(float f)
    {
        f = Math.Max(0, Math.Min(1, f));
        double v = Log ? Min * Math.Pow(Max / (double)Min, f) : Min + f * (Max - Min);
        int snap = Log ? LogStep((int)v) : Snap;
        return Settings.Clamp((int)(Math.Round(v / snap) * snap), Min, Max);
    }

    // Rounder steps as the numbers grow, so a log slider lands on tidy values.
    public static int LogStep(int v) { return v < 100 ? 5 : v < 1000 ? 10 : 50; }

    public int Wheel(int dir)
    {
        int v = GetI();
        int step = Log ? LogStep(dir > 0 ? v : v - 1) : Snap;
        return Settings.Clamp(v + dir * step, Min, Max);
    }
}

enum State { Idle, Countdown, Typing, Paused }

// The main window. Apart from the text box, everything is custom drawn so it stays crisp at any DPI.
class MainForm : Form
{
    const int HOTKEY_TOGGLE = 1;
    const int HOTKEY_STOP = 2;
    const string PauseChars = "，。！？；：、,.!?;:\n";

    readonly Settings st = Settings.Load();
    float scale = 1;
    Font fTitle, fBody, fSmall, fButton, fKey, fZh, fEditor;
    List<El> els = new List<El>();
    El hot, drag, pressed;
    readonly Editor txt;
    bool hotkeyConflict;
    IntPtr bigIcon, smallIcon;

    // What the window shows; only touched on the UI thread.
    State state = State.Idle;
    string status = "", info = "";
    Color statusColor = Ui.Sub;
    float progress;

    Thread worker;
    readonly ManualResetEvent go = new ManualResetEvent(true);
    readonly Random rnd = new Random();
    volatile bool stopFlag, paused, needCountdown, shiftEnter;
    volatile int speed, jitter, delaySec, pauseMs;
    IntPtr myHandle;

    bool Busy { get { return state != State.Idle; } }

    int S(float px) { return (int)Math.Round(px * scale); }

    public MainForm()
    {
        Lang.Zh = st.Language == "zh";
        speed = st.Speed;
        jitter = st.Jitter;
        delaySec = st.Delay;
        pauseMs = st.PauseMs;
        shiftEnter = st.ShiftEnter;

        Text = "Auto Typer";
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Ui.Bg;
        TopMost = st.TopMost;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        txt = new Editor();
        txt.Multiline = true;
        txt.AcceptsTab = true;
        txt.AcceptsReturn = true;
        txt.WordWrap = true;
        txt.BorderStyle = BorderStyle.None;
        txt.HideSelection = false; // the typed part stays highlighted while typing
        txt.BackColor = Ui.Card;
        txt.ForeColor = Ui.Fg;
        txt.PlaceholderColor = Ui.Dim;
        txt.HandleCreated += delegate { Native.DarkScrollbars(txt.Handle); };
        txt.TextChanged += delegate { UpdateInfo(); Invalidate(); };
        Controls.Add(txt);

        SetScale(Native.ScaleAt(Cursor.Position));
        ClientSize = new Size(S(st.Width), S(st.Height));
        SetIdleStatus();
        UpdateInfo();

        Native.timeBeginPeriod(1);
    }

    // ---------- window setup ----------

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        myHandle = Handle;
        Native.Dwm(Handle, 20, 1); // dark title bar
        Native.Dwm(Handle, 35, ColorTranslator.ToWin32(Ui.Bg)); // Windows 11: title bar in the window colour
        Native.Dwm(Handle, 34, ColorTranslator.ToWin32(Ui.Line)); // Windows 11: subtle border

        bool ok = Native.RegisterHotKey(Handle, HOTKEY_TOGGLE, 0, Native.VK_F9);
        ok &= Native.RegisterHotKey(Handle, HOTKEY_STOP, 0, Native.VK_F10);
        hotkeyConflict = !ok;

        float s = Native.ScaleOf(Handle);
        if (s != scale)
        {
            SetScale(s);
            ClientSize = new Size(S(st.Width), S(st.Height));
        }
        SetIcons();
        Rebuild();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(Math.Max(wa.Left, wa.Left + (wa.Width - Width) / 2), Math.Max(wa.Top, wa.Top + (wa.Height - Height) / 2));
    }

    void SetScale(float s)
    {
        scale = s;
        if (fTitle != null)
            foreach (Font f in new[] { fTitle, fBody, fSmall, fButton, fKey, fZh, fEditor }) f.Dispose();
        fTitle = new Font("Segoe UI Semibold", 19 * s, GraphicsUnit.Pixel);
        fBody = new Font(Ui.Font, 13 * s, GraphicsUnit.Pixel);
        fSmall = new Font(Ui.Font, 11.5f * s, GraphicsUnit.Pixel);
        fButton = Lang.Zh ? new Font(Ui.Font, 13 * s, GraphicsUnit.Pixel) : new Font("Segoe UI Semibold", 13 * s, GraphicsUnit.Pixel);
        fKey = new Font("Segoe UI Semibold", 10.5f * s, GraphicsUnit.Pixel);
        fZh = new Font("Microsoft YaHei UI", 11.5f * s, GraphicsUnit.Pixel);
        fEditor = new Font(Ui.Font, 14.5f * s, GraphicsUnit.Pixel);
        txt.Font = fEditor;
        txt.Placeholder = Lang.T("Type or paste the text to be typed out…\r\n\r\nAny language, emoji, new lines and tabs work.",
                                 "在这里输入或粘贴要自动输入的文字…\r\n\r\n支持任何语言、emoji、换行和 Tab。");
        txt.Invalidate();
        MinimumSize = Size.Empty;
        MinimumSize = SizeFromClientSize(new Size(S(660), S(520)));
        SetIcons();
        Rebuild();
    }

    void SetIcons()
    {
        if (!IsHandleCreated) return;
        IntPtr oldBig = bigIcon, oldSmall = smallIcon;
        using (Bitmap b = Logo.Render(S(32))) bigIcon = b.GetHicon();
        using (Bitmap b = Logo.Render(S(16))) smallIcon = b.GetHicon();
        Icon = Icon.FromHandle(bigIcon); // keeps WinForms from putting its default icon back
        Native.SendMessage(Handle, Native.WM_SETICON, new IntPtr(1), bigIcon);
        Native.SendMessage(Handle, Native.WM_SETICON, IntPtr.Zero, smallIcon);
        if (oldBig != IntPtr.Zero) Native.DestroyIcon(oldBig);
        if (oldSmall != IntPtr.Zero) Native.DestroyIcon(oldSmall);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Rebuild();
    }

    // ---------- layout ----------

    void Rebuild()
    {
        if (fTitle == null) return;
        els = BuildLayout();
        Invalidate();
    }

    List<El> BuildLayout()
    {
        List<El> L = new List<El>();
        int W = ClientSize.Width, H = ClientSize.Height;
        int m = S(20), x0 = m, x1 = W - m;

        // Header: title, live status, language.
        L.Add(new El { Kind = Kind.Text, R = new Rectangle(x0, S(12), S(300), S(30)), Text = "Auto Typer", F = fTitle, C = Ui.Fg });
        int segW = S(50), segH = S(26);
        Rectangle pill = new Rectangle(x1 - 2 * segW, S(18), 2 * segW, segH);
        L.Add(new El { Kind = Kind.Status, R = new Rectangle(x0, S(43), pill.Left - x0 - S(16), S(20)) });
        L.Add(new El { Kind = Kind.Pill, R = pill });
        L.Add(new El { Kind = Kind.Segment, R = new Rectangle(pill.X, pill.Y, segW, segH), Text = "EN", F = fSmall,
                       GetB = () => !Lang.Zh, Click = () => SetLanguage("en") });
        L.Add(new El { Kind = Kind.Segment, R = new Rectangle(pill.X + segW, pill.Y, segW, segH), Text = "中文", F = fZh,
                       GetB = () => Lang.Zh, Click = () => SetLanguage("zh") });

        // Footer: progress, hotkeys, buttons.
        int btnH = S(38), btnY = H - m - btnH, progY = btnY - S(18);
        int top = S(80), bottom = progY - S(14), gap = S(14), rw = S(286);
        Rectangle edit = new Rectangle(x0, top, x1 - x0 - rw - gap, bottom - top);
        Rectangle side = new Rectangle(edit.Right + gap, top, rw, bottom - top);

        // Text card.
        L.Add(new El { Kind = Kind.Card, R = edit });
        int px = S(16), hy = edit.Y + S(12), hh = S(22);
        int lx = edit.Right - px;
        string[] links = { Lang.T("Clear", "清空"), Lang.T("Paste", "粘贴") };
        Action[] acts = { () => txt.Clear(), () => { if (Clipboard.ContainsText()) txt.Text = Clipboard.GetText(); } };
        for (int k = 0; k < links.Length; k++)
        {
            int lw = Measure(links[k], fBody) + S(16);
            L.Add(new El { Kind = Kind.Link, R = new Rectangle(lx - lw, hy, lw, hh), Text = links[k], F = fBody,
                           Enabled = () => !Busy, Click = acts[k] });
            lx -= lw + S(4);
        }
        L.Add(new El { Kind = Kind.Info, R = new Rectangle(edit.X + px, hy, lx - edit.X - px, hh) });
        L.Add(new El { Kind = Kind.Sep, R = new Rectangle(edit.X, edit.Y + S(46), edit.Width, 1) });
        txt.Bounds = new Rectangle(edit.X + px, edit.Y + S(58), edit.Width - px - S(4), edit.Height - S(58) - S(10));

        // Settings card.
        L.Add(new El { Kind = Kind.Card, R = side });
        int sx = side.X + S(18), sw = side.Width - S(36), y = side.Y + S(16);
        AddSlider(L, ref y, sx, sw, Lang.T("Speed", "速度"), () => speed, v => speed = v, 10, 3000, 0, true,
                  v => v + Lang.T(" chars/min", " 字/分钟"));
        AddSlider(L, ref y, sx, sw, Lang.T("Random variation", "随机波动"), () => jitter, v => jitter = v, 0, 90, 5, false,
                  v => "±" + v + "%");
        AddSlider(L, ref y, sx, sw, Lang.T("Countdown", "倒计时"), () => delaySec, v => delaySec = v, 0, 30, 1, false,
                  v => v + Lang.T(" s", " 秒"));
        AddSlider(L, ref y, sx, sw, Lang.T("Pause after punctuation", "标点后停顿"), () => pauseMs, v => pauseMs = v, 0, 1000, 10, false,
                  v => v + " ms");
        y += S(2);
        L.Add(new El { Kind = Kind.Sep, R = new Rectangle(side.X, y, side.Width, 1) });
        y += S(10);
        AddRow(L, ref y, sx, sw, Lang.T("Shift+Enter for new lines", "用 Shift+Enter 换行"),
               Lang.T("Chat apps won't send each line", "聊天软件里不会每行都发送"), () => shiftEnter, v => shiftEnter = v);
        AddRow(L, ref y, sx, sw, Lang.T("Always on top", "窗口置顶"), null, () => TopMost, v => TopMost = v);

        L.Add(new El { Kind = Kind.Progress, R = new Rectangle(x0, progY, x1 - x0, S(4)) });

        int startW = S(136), stopW = S(100);
        Rectangle bStart = new Rectangle(x1 - startW, btnY, startW, btnH);
        Rectangle bStop = new Rectangle(bStart.X - S(10) - stopW, btnY, stopW, btnH);
        string startText = state == State.Idle ? Lang.T("Start", "开始") : state == State.Paused ? Lang.T("Resume", "继续") : Lang.T("Pause", "暂停");
        L.Add(new El { Kind = Kind.Button, R = bStop, Text = Lang.T("Stop", "停止"), F = fButton, Enabled = () => Busy, Click = StopTyping });
        L.Add(new El { Kind = Kind.Button, R = bStart, Text = startText, F = fButton, Primary = true, Click = ToggleOrStart });

        int ky = btnY + (btnH - S(22)) / 2, kx = x0;
        if (hotkeyConflict)
            L.Add(new El { Kind = Kind.Text, R = new Rectangle(kx, ky, bStop.X - kx - S(10), S(22)), F = fSmall, C = Ui.Warn,
                           Text = Lang.T("F9 / F10 are used by another app; use the buttons", "F9 / F10 被其他程序占用，请用按钮") });
        else
        {
            AddKey(L, ref kx, ky, "F9", Lang.T("Start / pause", "开始 / 暂停"));
            kx += S(14);
            AddKey(L, ref kx, ky, "F10", Lang.T("Stop", "停止"));
        }
        return L;
    }

    int Measure(string s, Font f)
    {
        return TextRenderer.MeasureText(s, f, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
    }

    void AddSlider(List<El> L, ref int y, int x, int w, string label, Func<int> get, Action<int> set,
                   int min, int max, int snap, bool log, Func<int, string> fmt)
    {
        L.Add(new El { Kind = Kind.Text, R = new Rectangle(x, y, w, S(20)), Text = label, F = fBody, C = Ui.Fg, GetI = get, Fmt = fmt });
        y += S(22);
        L.Add(new El { Kind = Kind.Slider, R = new Rectangle(x, y, w, S(20)), GetI = get, SetI = set,
                       Min = min, Max = max, Snap = Math.Max(snap, 1), Log = log });
        y += S(20) + S(12);
    }

    void AddRow(List<El> L, ref int y, int x, int w, string label, string sub, Func<bool> get, Action<bool> set)
    {
        int h = sub != null ? S(44) : S(34);
        L.Add(new El { Kind = Kind.Row, R = new Rectangle(x, y, w, h), Text = label, Sub = sub, F = fBody, GetB = get, SetB = set });
        y += h + S(2);
    }

    void AddKey(List<El> L, ref int x, int y, string key, string label)
    {
        int kw = Math.Max(S(24), Measure(key, fKey) + S(12));
        L.Add(new El { Kind = Kind.Key, R = new Rectangle(x, y, kw, S(22)), Text = key, F = fKey });
        x += kw + S(7);
        int lw = Measure(label, fSmall) + S(2);
        L.Add(new El { Kind = Kind.Text, R = new Rectangle(x, y, lw, S(22)), Text = label, F = fSmall, C = Ui.Sub });
        x += lw;
    }

    void SetLanguage(string lang)
    {
        st.Language = lang;
        Lang.Zh = lang == "zh";
        SetScale(scale);
        UpdateInfo();
        if (state == State.Idle) SetIdleStatus();
    }

    // ---------- painting ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Ui.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        TextFormatFlags left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        TextFormatFlags right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        TextFormatFlags center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        foreach (El el in els)
        {
            bool isHot = el == hot || el == drag;
            bool enabled = el.Enabled == null || el.Enabled();
            switch (el.Kind)
            {
                case Kind.Text:
                    TextRenderer.DrawText(g, el.Text, el.F, el.R, el.C, left | TextFormatFlags.EndEllipsis);
                    if (el.Fmt != null)
                        TextRenderer.DrawText(g, el.Fmt(el.GetI()), el.F, el.R, Ui.Sub, right);
                    break;
                case Kind.Status:
                    TextRenderer.DrawText(g, status, fBody, el.R, statusColor, left | TextFormatFlags.EndEllipsis);
                    break;
                case Kind.Info:
                    TextRenderer.DrawText(g, info, fBody, el.R, Ui.Sub, left | TextFormatFlags.EndEllipsis);
                    break;
                case Kind.Card:
                {
                    RectangleF r = new RectangleF(el.R.X + 0.5f, el.R.Y + 0.5f, el.R.Width - 1, el.R.Height - 1);
                    using (GraphicsPath p = Ui.Round(r, S(10)))
                    using (SolidBrush b = new SolidBrush(Ui.Card))
                    using (Pen pen = new Pen(Ui.Line))
                    {
                        g.FillPath(b, p);
                        g.DrawPath(pen, p);
                    }
                    break;
                }
                case Kind.Slider:
                {
                    int cy = el.R.Top + el.R.Height / 2, th = Math.Max(3, S(4));
                    float fx = el.R.Left + el.Frac(el.GetI()) * el.R.Width;
                    Ui.FillRound(g, Ui.Off, new RectangleF(el.R.Left, cy - th / 2f, el.R.Width, th), th / 2f);
                    if (fx > el.R.Left)
                        Ui.FillRound(g, Ui.Accent, new RectangleF(el.R.Left, cy - th / 2f, fx - el.R.Left, th), th / 2f);
                    float kr = isHot ? S(8) : S(7);
                    using (SolidBrush b = new SolidBrush(Ui.Fg))
                        g.FillEllipse(b, fx - kr, cy - kr, kr * 2, kr * 2);
                    break;
                }
                case Kind.Row:
                {
                    if (el.Sub != null)
                    {
                        int half = el.R.Height / 2;
                        TextRenderer.DrawText(g, el.Text, el.F, new Rectangle(el.R.X, el.R.Y + S(2), el.R.Width - S(48), half - S(2)), Ui.Fg, left);
                        TextRenderer.DrawText(g, el.Sub, fSmall, new Rectangle(el.R.X, el.R.Y + half, el.R.Width - S(48), half - S(2)), Ui.Sub, left | TextFormatFlags.EndEllipsis);
                    }
                    else TextRenderer.DrawText(g, el.Text, el.F, el.R, Ui.Fg, left);
                    int sw = S(36), sh = S(20);
                    Ui.Switch(g, new Rectangle(el.R.Right - sw, el.R.Top + (el.R.Height - sh) / 2, sw, sh), el.GetB(), isHot);
                    break;
                }
                case Kind.Sep:
                    using (Pen p = new Pen(Ui.Line))
                        g.DrawLine(p, el.R.Left, el.R.Top, el.R.Right - 1, el.R.Top);
                    break;
                case Kind.Link:
                    if (isHot) Ui.FillRound(g, Ui.Surface, el.R, S(6));
                    TextRenderer.DrawText(g, el.Text, el.F, el.R, !enabled ? Ui.Dim : isHot ? Ui.Fg : Ui.Accent, center);
                    break;
                case Kind.Pill:
                    Ui.FillRound(g, Ui.Card, el.R, el.R.Height / 2f);
                    break;
                case Kind.Segment:
                {
                    bool sel = el.GetB();
                    if (sel) Ui.FillRound(g, Ui.SurfaceHot, Rectangle.Inflate(el.R, -S(2), -S(2)), el.R.Height / 2f);
                    TextRenderer.DrawText(g, el.Text, el.F, el.R, sel || isHot ? Ui.Fg : Ui.Sub, center);
                    break;
                }
                case Kind.Button:
                {
                    Color fill, fg;
                    if (!enabled) { fill = Ui.Card; fg = Ui.Dim; }
                    else if (el.Primary) { fill = isHot ? Ui.AccentHot : Ui.Accent; fg = Ui.Bg; }
                    else { fill = isHot ? Ui.SurfaceHot : Ui.Surface; fg = Ui.Fg; }
                    Ui.FillRound(g, fill, el.R, S(8));
                    TextRenderer.DrawText(g, el.Text, el.F, el.R, fg, center);
                    break;
                }
                case Kind.Key:
                    Ui.FillRound(g, Ui.Surface, el.R, S(5));
                    TextRenderer.DrawText(g, el.Text, el.F, el.R, Ui.Fg, center);
                    break;
                case Kind.Progress:
                {
                    float h = el.R.Height;
                    Ui.FillRound(g, Ui.Card, el.R, h / 2);
                    if (progress > 0)
                        Ui.FillRound(g, state == State.Paused ? Ui.Sub : state == State.Idle ? Ui.Good : Ui.Accent,
                                     new RectangleF(el.R.X, el.R.Y, Math.Max(h, el.R.Width * progress), h), h / 2);
                    break;
                }
            }
        }
    }

    // ---------- input ----------

    El Hit(Point p)
    {
        for (int i = els.Count - 1; i >= 0; i--)
        {
            El el = els[i];
            if (!el.Interactive) continue;
            Rectangle r = el.Kind == Kind.Slider ? Rectangle.Inflate(el.R, S(8), S(4)) : el.R;
            if (r.Contains(p)) return el;
        }
        return null;
    }

    void SetFromX(El el, int x)
    {
        int v = el.FromFrac((x - el.R.Left) / (float)el.R.Width);
        if (v != el.GetI()) { el.SetI(v); UpdateInfo(); Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        El el = Hit(e.Location);
        if (el == null || e.Button != MouseButtons.Left) return;
        if (el.Kind == Kind.Slider)
        {
            drag = el;
            Capture = true;
            SetFromX(el, e.X);
        }
        else pressed = el;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (drag != null) { SetFromX(drag, e.X); return; }
        El h = Hit(e.Location);
        Cursor = h != null ? Cursors.Hand : Cursors.Default;
        if (h != hot) { hot = h; Invalidate(); }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (drag != null) { drag = null; Capture = false; Invalidate(); return; }
        El el = pressed;
        pressed = null;
        if (el == null || Hit(e.Location) != el) return;
        if (el.Kind == Kind.Row) el.SetB(!el.GetB());
        else el.Click();
        if (!IsDisposed) Rebuild();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hot != null) { hot = null; Invalidate(); }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        El el = Hit(e.Location);
        if (el == null || el.Kind != Kind.Slider) return;
        el.SetI(el.Wheel(e.Delta > 0 ? 1 : -1));
        UpdateInfo();
        Invalidate();
    }

    // ---------- status ----------

    static int CountChars(string t, out int pauses)
    {
        int n = 0;
        pauses = 0;
        foreach (char c in t)
        {
            if (char.IsLowSurrogate(c)) continue;
            n++;
            if (PauseChars.IndexOf(c) >= 0) pauses++;
        }
        return n;
    }

    double Seconds(int chars, int pauses) { return chars * 60.0 / speed + pauses * pauseMs / 1000.0; }

    static string Duration(double sec)
    {
        int s = (int)Math.Ceiling(sec);
        if (s < 60) return s + Lang.T(" s", " 秒");
        return string.Format(Lang.T("{0} min {1} s", "{0} 分 {1} 秒"), s / 60, s % 60);
    }

    void UpdateInfo()
    {
        int p;
        int n = CountChars(txt.Text.Replace("\r\n", "\n"), out p);
        info = n == 0 ? Lang.T("No text yet", "还没有文字")
                      : string.Format(Lang.T("{0} characters  ·  about {1}", "{0} 个字符  ·  约 {1}"), n, Duration(Seconds(n, p)));
    }

    void SetStatus(string s, Color c)
    {
        status = s;
        statusColor = c;
        Invalidate();
    }

    void SetIdleStatus()
    {
        SetStatus(Lang.T("Ready. Press Start, then click where the text should go.", "就绪。按“开始”后，点一下要输入文字的位置。"), Ui.Sub);
    }

    void SetState(State s)
    {
        state = s;
        txt.ReadOnly = s != State.Idle;
        Rebuild();
    }

    void UI(MethodInvoker a)
    {
        try { if (!IsDisposed) BeginInvoke(a); } catch (InvalidOperationException) { }
    }

    // ---------- typing ----------

    void ToggleOrStart()
    {
        if (state == State.Idle) StartTyping();
        else TogglePause();
    }

    void StartTyping()
    {
        if (Busy || (worker != null && worker.IsAlive)) return;
        string t = txt.Text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (t.Length == 0) { SetStatus(Lang.T("Type or paste some text first.", "请先输入或粘贴文字。"), Ui.Warn); return; }
        // Show the text exactly as it will be typed, so the highlight can follow along.
        string shown = t.Replace("\n", "\r\n");
        if (txt.Text != shown) txt.Text = shown;
        txt.Select(0, 0);

        stopFlag = false;
        paused = false;
        needCountdown = true;
        go.Set();
        progress = 0;
        SetState(delaySec > 0 ? State.Countdown : State.Typing);
        worker = new Thread(delegate () { Work(t); });
        worker.IsBackground = true;
        worker.Start();
    }

    void TogglePause()
    {
        if (state == State.Idle) return;
        if (paused)
        {
            paused = false;
            needCountdown = true;
            SetState(delaySec > 0 ? State.Countdown : State.Typing);
            go.Set();
        }
        else
        {
            paused = true;
            go.Reset();
            SetState(State.Paused);
            SetStatus(Lang.T("Paused. Press F9 or Resume to continue (the countdown runs again first).",
                             "已暂停。按 F9 或“继续”恢复（会先重新倒计时）。"), Ui.Fg);
        }
    }

    void StopTyping()
    {
        stopFlag = true;
        go.Set();
    }

    void Work(string t)
    {
        int total = t.Length, pausesLeft;
        int totalChars = CountChars(t, out pausesLeft);
        int i = 0, doneChars = 0, newlines = 0;

        while (i < total)
        {
            go.WaitOne();
            if (stopFlag) break;
            if (needCountdown)
            {
                needCountdown = false;
                if (!Countdown()) break;
                UI(delegate { if (!paused && !stopFlag && state == State.Countdown) SetState(State.Typing); });
                continue;
            }
            if (Native.GetForegroundWindow() == myHandle)
            {
                // Never type into ourselves: wait for the user to switch to the target window.
                paused = true;
                go.Reset();
                UI(delegate
                {
                    if (stopFlag) return;
                    SetState(State.Paused);
                    SetStatus(Lang.T("Paused: Auto Typer has focus. Switch to the target window and press F9.",
                                     "已暂停：焦点在 Auto Typer 上。请切换到目标窗口后按 F9。"), Ui.Warn);
                });
                continue;
            }

            char c = t[i];
            int step = 1;
            if (c == '\n') { Native.SendVk(Native.VK_RETURN, shiftEnter); newlines++; }
            else if (c == '\t') Native.SendVk(Native.VK_TAB, false);
            else if (char.IsHighSurrogate(c) && i + 1 < total && char.IsLowSurrogate(t[i + 1]))
            {
                Native.SendUnicode(t.Substring(i, 2));
                step = 2;
            }
            else Native.SendUnicode(c.ToString());
            i += step;
            doneChars++;
            bool isPause = PauseChars.IndexOf(c) >= 0;
            if (isPause) pausesLeft--;

            int done = i, chars = doneChars, sel = i + newlines, left = pausesLeft;
            UI(delegate
            {
                progress = done / (float)total;
                txt.Select(0, sel);
                txt.ScrollToCaret();
                if (!paused && !stopFlag)
                    SetStatus(string.Format(Lang.T("Typing  {0} / {1}  ·  {2} left", "正在输入  {0} / {1}  ·  还剩 {2}"),
                                            chars, totalChars, Duration(Seconds(totalChars - chars, left))), Ui.Accent);
                Invalidate();
            });

            double baseMs = 60000.0 / speed;
            double ms = baseMs * (1 + jitter / 100.0 * (rnd.NextDouble() * 2 - 1));
            if (isPause) ms += pauseMs;
            SleepChecked((int)Math.Max(ms, 1));
        }

        bool stopped = stopFlag;
        int typed = doneChars;
        UI(delegate
        {
            SetState(State.Idle);
            txt.Select(0, 0);
            if (stopped)
                SetStatus(string.Format(Lang.T("Stopped after {0} of {1} characters.", "已停止，输入了 {0} / {1} 个字符。"), typed, totalChars), Ui.Sub);
            else
                SetStatus(string.Format(Lang.T("Done. Typed {0} characters.", "完成，已输入 {0} 个字符。"), typed), Ui.Good);
        });
    }

    // Returns false if stopped during the countdown.
    bool Countdown()
    {
        for (int s = delaySec; s > 0; s--)
        {
            int left = s;
            UI(delegate
            {
                if (paused || stopFlag) return;
                SetStatus(string.Format(Lang.T("Starting in {0} s. Click where the text should go…", "{0} 秒后开始，请点一下要输入文字的位置…"), left), Ui.Accent);
            });
            for (int k = 0; k < 20; k++)
            {
                if (stopFlag) return false;
                if (paused) { needCountdown = true; return true; }
                Thread.Sleep(50);
            }
        }
        return !stopFlag;
    }

    void SleepChecked(int ms)
    {
        DateTime end = DateTime.UtcNow.AddMilliseconds(ms);
        while (!stopFlag && !paused)
        {
            int left = (int)(end - DateTime.UtcNow).TotalMilliseconds;
            if (left <= 0) break;
            Thread.Sleep(Math.Min(left, 20));
        }
    }

    // ---------- messages ----------

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Native.UnregisterHotKey(Handle, HOTKEY_TOGGLE);
        Native.UnregisterHotKey(Handle, HOTKEY_STOP);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == HOTKEY_TOGGLE) ToggleOrStart();
            else if (id == HOTKEY_STOP) StopTyping();
            return;
        }
        if (m.Msg == Native.WM_DPICHANGED)
        {
            // Moved to a display with another scale: redraw at the new size, where Windows suggests.
            Native.RECT r = (Native.RECT)Marshal.PtrToStructure(m.LParam, typeof(Native.RECT));
            SetScale((m.WParam.ToInt32() & 0xFFFF) / 96f);
            MinimumSize = Size.Empty;
            Bounds = new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            MinimumSize = SizeFromClientSize(new Size(S(660), S(520)));
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopTyping();
        st.Speed = speed;
        st.Jitter = jitter;
        st.Delay = delaySec;
        st.PauseMs = pauseMs;
        st.ShiftEnter = shiftEnter;
        st.TopMost = TopMost;
        if (WindowState == FormWindowState.Normal)
        {
            st.Width = (int)Math.Round(ClientSize.Width / scale);
            st.Height = (int)Math.Round(ClientSize.Height / scale);
        }
        st.Save();
        base.OnFormClosing(e);
    }
}
