// Windows screensaver host for the {Shan, Shui}* landscape page (web/index.html, embedded).
// Args per the .scr convention: /s run, /c[:hwnd] configure (also no args), /p <hwnd> preview inside the Settings dialog's monitor picture.
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace ShanShui;

static class Program
{
    const string RegKey = @"Software\ShanShuiSaver";
    static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShanShuiSaver");

    [STAThread]
    static void Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "/c";
        ApplicationConfiguration.Initialize();
        if (mode.StartsWith("/p"))
        {
            // Windows passes "/p <hwnd>" (two args) or "/p:<hwnd>".
            string h = mode.Length > 2 ? mode.Substring(2).Trim(':', ' ') : args.ElementAtOrDefault(1);
            if (long.TryParse(h, out long hwnd))
                try { RunPreview(new IntPtr(hwnd)); } catch (WebView2RuntimeNotFoundException) { } // blank preview
        }
        else if (mode.StartsWith("/s"))
        {
            try { RunSaver(); }
            catch (WebView2RuntimeNotFoundException)
            {
                MessageBox.Show("The Shan Shui screensaver needs the Microsoft Edge WebView2 Runtime.\n\n" +
                    "Install it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and try again.",
                    "Shan Shui Screensaver", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else Application.Run(new SettingsForm());
    }

    public static (string theme, int speed) Load()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RegKey);
        return ((k?.GetValue("Theme") as string) ?? "light", (k?.GetValue("Speed") as int?) ?? 30);
    }

    public static void Save(string theme, int speed)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RegKey);
        k.SetValue("Theme", theme);
        k.SetValue("Speed", speed, RegistryValueKind.DWord);
    }

    // The page is served from the embedded resource (see SaverForm), so nothing is written to disk.
    public static readonly byte[] Page = ReadPage();
    static byte[] ReadPage()
    {
        using var s = typeof(Program).Assembly.GetManifestResourceStream("index.html");
        using var m = new MemoryStream();
        s.CopyTo(m);
        return m.ToArray();
    }

    static CoreWebView2Environment Prepare() =>
        CoreWebView2Environment.CreateAsync(null, Path.Combine(DataDir, "WebView2")).GetAwaiter().GetResult();

    static void RunPreview(IntPtr parent)
    {
        if (!GetClientRect(parent, out RECT r)) return;
        var env = Prepare();
        var (theme, speed) = Load();
        // Keep the same apparent pace as fullscreen: speed is px/s, so scale by preview/screen height.
        double s = Math.Max(0.5, speed * (double)r.Bottom / Screen.PrimaryScreen.Bounds.Height);
        var form = new SaverForm(new Rectangle(0, 0, r.Right, r.Bottom), env,
            $"theme={theme}&speed={s.ToString(System.Globalization.CultureInfo.InvariantCulture)}&seed={DateTime.Now.Ticks}", parent);
        form.Show();
        form.AttachToPreview();
        // The picture's window is destroyed when the dialog closes or another saver is picked.
        var timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (_, _) => { if (!IsWindow(parent)) Application.Exit(); };
        timer.Start();
        Application.Run();
    }

    static void RunSaver()
    {
        var env = Prepare();
        var (theme, speed) = Load();
        long seed = DateTime.Now.Ticks;
        var forms = Screen.AllScreens.Select((scr, i) =>
            new SaverForm(scr.Bounds, env, $"theme={theme}&speed={speed}&seed={seed + i}")).ToList();
        forms.ForEach(f => f.Show());
        Cursor.Hide();

        // Exit on real input, watched host-side so it works whatever has focus: the cursor
        // moving >10px, or a new input event (a key) while the cursor stayed put (ignores jitter).
        var start = Cursor.Position;
        var lastPos = start;
        uint lastInput = LastInputTick();
        var armed = DateTime.Now.AddSeconds(1);
        var timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (_, _) =>
        {
            var pos = Cursor.Position;
            uint input = LastInputTick();
            bool moved = Math.Abs(pos.X - start.X) + Math.Abs(pos.Y - start.Y) > 10;
            bool key = input != lastInput && pos == lastPos;
            if (DateTime.Now > armed && (moved || key)) Application.Exit();
            lastPos = pos;
            lastInput = input;
        };
        timer.Start();
        Application.Run();
    }

    struct RECT { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool GetClientRect(IntPtr hwnd, out RECT r);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    struct LASTINPUTINFO { public uint cbSize, dwTime; }
    static uint LastInputTick()
    {
        var i = new LASTINPUTINFO { cbSize = 8 };
        GetLastInputInfo(ref i);
        return i.dwTime;
    }
}

class SaverForm : Form
{
    readonly IntPtr previewParent;

    // With previewParent set, the form becomes a child window of the Settings dialog's preview picture.
    public SaverForm(Rectangle bounds, CoreWebView2Environment env, string query, IntPtr previewParent = default)
    {
        this.previewParent = previewParent;
        bool preview = previewParent != IntPtr.Zero;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = !preview;
        ShowInTaskbar = false;
        BackColor = Color.Black;

        var view = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
        Controls.Add(view);
        Load += async (_, _) =>
        {
            await view.EnsureCoreWebView2Async(env);
            var cw = view.CoreWebView2;
            cw.Settings.AreDefaultContextMenusEnabled = false;
            cw.Settings.AreDevToolsEnabled = false;
            cw.Settings.IsStatusBarEnabled = false;
            cw.Settings.AreBrowserAcceleratorKeysEnabled = false;
            cw.Settings.IsZoomControlEnabled = false;
            cw.AddWebResourceRequestedFilter("https://shanshui.saver/*", CoreWebView2WebResourceContext.All);
            cw.WebResourceRequested += (_, a) => a.Response = env.CreateWebResourceResponse(
                new MemoryStream(Program.Page), 200, "OK", "Content-Type: text/html; charset=utf-8");
            if (!preview) cw.WebMessageReceived += (_, _) => Application.Exit();
            cw.ProcessFailed += (_, _) => Application.Exit();
            cw.Navigate("https://shanshui.saver/index.html?" + query);
            if (!preview) view.Focus();
        };
    }

    // Must run after Show(): WinForms' Show() re-applies top-level ownership and would undo it.
    public void AttachToPreview()
    {
        const int GWL_STYLE = -16, WS_CHILD = 0x40000000, WS_POPUP = unchecked((int)0x80000000);
        SetWindowLong(Handle, GWL_STYLE, (GetWindowLong(Handle, GWL_STYLE) | WS_CHILD) & ~WS_POPUP);
        SetParent(Handle, previewParent);
        Location = Point.Empty;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hwnd, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}

class SettingsForm : Form
{
    public SettingsForm()
    {
        var (theme, speed) = Program.Load();
        Text = "Shan Shui Screensaver";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var light = new RadioButton { Text = "Light — grey ink on paper", AutoSize = true, Checked = theme != "dark" };
        var dark = new RadioButton { Text = "Dark — gold ink on black", AutoSize = true, Checked = theme == "dark" };
        var speedLabel = new Label { AutoSize = true, Margin = new Padding(3, 12, 3, 0) };
        var bar = new TrackBar { Minimum = 5, Maximum = 150, TickFrequency = 15, Value = Math.Clamp(speed, 5, 150), Width = 260 };
        void UpdateLabel() => speedLabel.Text = $"Scroll speed: {bar.Value} px/s";
        bar.ValueChanged += (_, _) => UpdateLabel();
        UpdateLabel();

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        // Run via Application.Run (not ShowDialog), so DialogResult alone won't close the form.
        ok.Click += (_, _) => { Program.Save(dark.Checked ? "dark" : "light", bar.Value); Close(); };
        cancel.Click += (_, _) => Close();
        AcceptButton = ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill };
        layout.Controls.AddRange(new Control[] {
            new Label { Text = "Theme", AutoSize = true, Font = new Font(Font, FontStyle.Bold) },
            light, dark, speedLabel, bar, buttons });
        Controls.Add(layout);
    }
}
