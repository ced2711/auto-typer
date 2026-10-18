using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        try { Native.SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
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
    public const int WM_HOTKEY = 0x0312;

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public InputUnion U; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();
    [DllImport("winmm.dll")]
    public static extern uint timeBeginPeriod(uint uMilliseconds);

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

class MainForm : Form
{
    const int HOTKEY_TOGGLE = 1;
    const int HOTKEY_STOP = 2;
    const string PauseChars = "，。！？；：、,.!?;:\n";

    TextBox txt;
    NumericUpDown nudSpeed, nudJitter, nudDelay, nudPause;
    CheckBox chkShiftEnter, chkTop;
    Button btnStart, btnPause, btnStop, btnPaste;
    ProgressBar bar;
    Label lblInfo, lblStatus;

    Thread worker;
    readonly ManualResetEvent go = new ManualResetEvent(true);
    readonly Random rnd = new Random();
    volatile bool stopFlag, paused, needCountdown, shiftEnter;
    volatile int speed = 300, jitter = 30, delaySec = 3, pauseMs = 150;
    IntPtr myHandle;

    bool Running { get { return worker != null && worker.IsAlive; } }

    float scale = 1f;

    int S(int px) { return (int)Math.Round(px * scale); }

    public MainForm()
    {
        // Layout is built from docked, auto-sized panels so nothing falls off the window at any DPI.
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;

        SuspendLayout();
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Segoe UI", 9F);
        Text = "Auto Typer";
        ClientSize = new Size(S(580), S(560));
        MinimumSize = new Size(S(520), S(480));
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(S(10));

        txt = new TextBox();
        txt.Multiline = true;
        txt.AcceptsTab = true;
        txt.AcceptsReturn = true;
        txt.ScrollBars = ScrollBars.Vertical;
        txt.Dock = DockStyle.Fill;
        txt.Font = new Font("Segoe UI", 10F);
        txt.TextChanged += delegate { UpdateInfo(); };

        Label lblText = new Label();
        lblText.Text = "Text to type (any language, emoji, newlines and tabs are supported):";
        lblText.AutoSize = true;
        lblText.Dock = DockStyle.Top;
        lblText.Padding = new Padding(0, 0, 0, S(4));

        TableLayoutPanel bottom = new TableLayoutPanel();
        bottom.Dock = DockStyle.Bottom;
        bottom.AutoSize = true;
        bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        bottom.ColumnCount = 4;
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        bottom.Padding = new Padding(0, S(6), 0, 0);

        lblInfo = NewLabel("");
        AddRow(bottom, lblInfo);

        nudSpeed = NewNud(10, 3000, 300, 10);
        nudJitter = NewNud(0, 90, 30, 5);
        AddRow(bottom, NewLabel("Speed (chars/min)"), nudSpeed, NewLabel("Random variation (%)"), nudJitter);

        nudDelay = NewNud(0, 30, 3, 1);
        nudPause = NewNud(0, 3000, 150, 50);
        AddRow(bottom, NewLabel("Countdown (s)"), nudDelay, NewLabel("Extra pause after punctuation (ms)"), nudPause);

        chkShiftEnter = NewCheck("Type newlines as Shift+Enter (so chat apps don't send each line)");
        chkShiftEnter.CheckedChanged += delegate { shiftEnter = chkShiftEnter.Checked; };
        AddRow(bottom, chkShiftEnter);

        chkTop = NewCheck("Always on top");
        chkTop.CheckedChanged += delegate { TopMost = chkTop.Checked; };
        AddRow(bottom, chkTop);

        FlowLayoutPanel buttons = new FlowLayoutPanel();
        buttons.AutoSize = true;
        buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        buttons.WrapContents = true;
        buttons.Dock = DockStyle.Fill;
        buttons.Margin = new Padding(0, S(6), 0, S(6));
        btnStart = NewButton("Start (F9)");
        btnStart.Click += delegate { StartTyping(); };
        btnPause = NewButton("Pause (F9)");
        btnPause.Click += delegate { TogglePause(); };
        btnStop = NewButton("Stop (F10)");
        btnStop.Click += delegate { StopTyping(); };
        btnPaste = NewButton("Paste clipboard");
        btnPaste.Click += delegate { if (Clipboard.ContainsText()) txt.Text = Clipboard.GetText(); };
        buttons.Controls.AddRange(new Control[] { btnStart, btnPause, btnStop, btnPaste });
        AddRow(bottom, buttons);

        bar = new ProgressBar();
        bar.Dock = DockStyle.Fill;
        bar.Height = S(16);
        AddRow(bottom, bar);

        lblStatus = NewLabel("Ready. After pressing Start, switch to the target window and click where the text should go.");
        AddRow(bottom, lblStatus);

        // Dock order: controls added later are docked first, so the Fill text box gets what is left.
        Controls.Add(txt);
        Controls.Add(bottom);
        Controls.Add(lblText);

        nudSpeed.ValueChanged += delegate { speed = (int)nudSpeed.Value; UpdateInfo(); };
        nudJitter.ValueChanged += delegate { jitter = (int)nudJitter.Value; };
        nudDelay.ValueChanged += delegate { delaySec = (int)nudDelay.Value; };
        nudPause.ValueChanged += delegate { pauseMs = (int)nudPause.Value; UpdateInfo(); };

        SetButtons(false);
        UpdateInfo();
        ResumeLayout(true);

        Native.timeBeginPeriod(1);
    }

    // A single control spans the whole row; otherwise controls fill the columns left to right.
    void AddRow(TableLayoutPanel t, params Control[] cells)
    {
        int row = t.RowCount;
        t.RowCount = row + 1;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (int k = 0; k < cells.Length; k++)
        {
            t.Controls.Add(cells[k], k, row);
            if (cells.Length == 1) t.SetColumnSpan(cells[k], t.ColumnCount);
        }
    }

    Label NewLabel(string text)
    {
        Label l = new Label();
        l.Text = text;
        l.AutoSize = true;
        l.Anchor = AnchorStyles.Left;
        l.Margin = new Padding(0, S(4), S(8), S(4));
        return l;
    }

    NumericUpDown NewNud(int min, int max, int value, int step)
    {
        NumericUpDown n = new NumericUpDown();
        n.Minimum = min;
        n.Maximum = max;
        n.Value = value;
        n.Increment = step;
        n.Width = S(80);
        n.Anchor = AnchorStyles.Left;
        n.Margin = new Padding(0, S(3), S(20), S(3));
        return n;
    }

    CheckBox NewCheck(string text)
    {
        CheckBox c = new CheckBox();
        c.Text = text;
        c.AutoSize = true;
        c.Anchor = AnchorStyles.Left;
        c.Margin = new Padding(0, S(3), 0, S(3));
        return c;
    }

    Button NewButton(string text)
    {
        Button b = new Button();
        b.Text = text;
        b.AutoSize = true;
        b.MinimumSize = new Size(S(110), S(32));
        b.Margin = new Padding(0, 0, S(8), S(4));
        return b;
    }

    void UpdateInfo()
    {
        string t = txt.Text.Replace("\r\n", "\n");
        int n = 0, p = 0;
        foreach (char c in t)
        {
            if (char.IsLowSurrogate(c)) continue;
            n++;
            if (PauseChars.IndexOf(c) >= 0) p++;
        }
        double sec = n * 60.0 / speed + p * pauseMs / 1000.0;
        string eta = sec < 60 ? string.Format("{0:0} s", sec) : string.Format("{0} min {1} s", (int)(sec / 60), (int)(sec % 60));
        lblInfo.Text = string.Format("{0} characters  ·  about {1}", n, eta);
    }

    void SetButtons(bool running)
    {
        btnStart.Enabled = !running;
        btnPause.Enabled = running;
        btnStop.Enabled = running;
        txt.ReadOnly = running;
        btnPaste.Enabled = !running;
        if (!running) btnPause.Text = "Pause (F9)";
    }

    void SetStatus(string s) { lblStatus.Text = s; }

    void UI(MethodInvoker a)
    {
        try { if (!IsDisposed) BeginInvoke(a); } catch (InvalidOperationException) { }
    }

    void StartTyping()
    {
        if (Running) return;
        string text = txt.Text;
        if (text.Length == 0) { SetStatus("Type or paste some text first."); return; }
        stopFlag = false;
        paused = false;
        needCountdown = true;
        go.Set();
        SetButtons(true);
        worker = new Thread(delegate () { Work(text); });
        worker.IsBackground = true;
        worker.Start();
    }

    void TogglePause()
    {
        if (!Running) return;
        if (paused)
        {
            paused = false;
            needCountdown = true;
            btnPause.Text = "Pause (F9)";
            go.Set();
        }
        else
        {
            paused = true;
            go.Reset();
            btnPause.Text = "Resume (F9)";
            SetStatus("Paused. Press F9 or Resume (the countdown runs again first).");
        }
    }

    void StopTyping()
    {
        stopFlag = true;
        go.Set();
    }

    void Work(string text)
    {
        string t = text.Replace("\r\n", "\n").Replace('\r', '\n');
        int total = t.Length;
        UI(delegate { bar.Maximum = Math.Max(total, 1); bar.Value = 0; });

        int i = 0;
        while (i < total)
        {
            go.WaitOne();
            if (stopFlag) break;
            if (needCountdown)
            {
                needCountdown = false;
                if (!Countdown()) break;
                continue;
            }
            if (Native.GetForegroundWindow() == myHandle)
            {
                // Never type into ourselves: wait for the user to switch to the target window.
                paused = true;
                go.Reset();
                UI(delegate
                {
                    btnPause.Text = "Resume (F9)";
                    SetStatus("Auto Typer itself has focus, so typing is paused. Switch to the target window and press F9.");
                });
                continue;
            }

            char c = t[i];
            int step = 1;
            if (c == '\n') Native.SendVk(Native.VK_RETURN, shiftEnter);
            else if (c == '\t') Native.SendVk(Native.VK_TAB, false);
            else if (char.IsHighSurrogate(c) && i + 1 < total && char.IsLowSurrogate(t[i + 1]))
            {
                Native.SendUnicode(t.Substring(i, 2));
                step = 2;
            }
            else Native.SendUnicode(c.ToString());
            i += step;

            int done = i;
            UI(delegate { bar.Value = done; SetStatus(string.Format("Typing... {0} / {1}", done, total)); });

            double baseMs = 60000.0 / speed;
            double ms = baseMs * (1 + jitter / 100.0 * (rnd.NextDouble() * 2 - 1));
            if (PauseChars.IndexOf(c) >= 0) ms += pauseMs;
            SleepChecked((int)Math.Max(ms, 1));
        }

        bool stopped = stopFlag;
        UI(delegate
        {
            SetButtons(false);
            SetStatus(stopped ? "Stopped." : "Done.");
        });
    }

    // Returns false if stopped during the countdown.
    bool Countdown()
    {
        for (int s = delaySec; s > 0; s--)
        {
            int left = s;
            UI(delegate { SetStatus(string.Format("Typing starts in {0} s - switch to the target window and place the cursor...", left)); });
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

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        myHandle = Handle;
        bool ok = Native.RegisterHotKey(Handle, HOTKEY_TOGGLE, 0, Native.VK_F9);
        ok &= Native.RegisterHotKey(Handle, HOTKEY_STOP, 0, Native.VK_F10);
        if (!ok && lblStatus != null) SetStatus("F9 / F10 are taken by another program; use the buttons instead.");
    }

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
            if (id == HOTKEY_TOGGLE) { if (Running) TogglePause(); else StartTyping(); }
            else if (id == HOTKEY_STOP) StopTyping();
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopTyping();
        base.OnFormClosing(e);
    }
}
