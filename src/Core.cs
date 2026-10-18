// FurryPet v2 — desktop companion. Core services: app, native, theme, text, phrases.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FurryPet
{
    internal enum Skin { Umi, Blue }
    internal enum Lang { Zh, En }
    internal enum Mood { Idle, Thinking, Working, Watching, Searching, Waiting, Sleeping, Happy, Error, Listening, Demo }

    internal static class Program
    {
        internal static string Dir;
        internal static string LogFile;
        private static Mutex _mutex;

        [STAThread]
        private static void Main(string[] args)
        {
            Dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            try { Directory.CreateDirectory(Path.Combine(Dir, "logs")); } catch (Exception) { }
            LogFile = Path.Combine(Dir, "logs", "app.log");

            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool card = false, panel = false, poke = false;
            foreach (string a in args)
            {
                if (a == "--card") card = true;
                if (a == "--panel") panel = true;
                if (a == "--poke") poke = true;
            }

            bool created;
            _mutex = new Mutex(true, "FurryPet.DesktopCompanion.v2", out created);
            if (!created) return;

            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log("UI EX: " + e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log("FATAL: " + e.ExceptionObject); };

            try
            {
                Log("start v2, dir=" + Dir);
                Application.Run(new PetForm(card, panel, poke));
            }
            catch (Exception ex)
            {
                Log("FATAL: " + ex);
                MessageBox.Show(ex.ToString(), "FurryPet");
            }
            GC.KeepAlive(_mutex);
        }

        internal static void Log(string msg)
        {
            try { File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff  ") + msg + "\r\n", Encoding.UTF8); }
            catch (Exception) { }
        }
    }

    internal static class App
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();
        public static double Now { get { return Watch.Elapsed.TotalSeconds; } }
    }

    internal static class Native
    {
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int ULW_ALPHA = 0x00000002;
        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;

        public const int HTCLIENT = 1;
        public const int HTTRANSPARENT = -1;
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_HOTKEY = 0x0312;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
            ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        public static void SetExStyle(IntPtr hwnd, int ex)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, -20, new IntPtr(ex));
            else SetWindowLong32(hwnd, -20, ex);
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public static int IdleSeconds
        {
            get
            {
                var lii = new LASTINPUTINFO();
                lii.cbSize = (uint)Marshal.SizeOf(lii);
                if (!GetLastInputInfo(ref lii)) return 0;
                uint idle = unchecked((uint)(Environment.TickCount - (int)lii.dwTime));
                return (int)(idle / 1000);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_NOREPEAT = 0x4000;

        public static bool Hotkey(IntPtr h, int id, uint mods, uint vk) { return RegisterHotKey(h, id, mods, vk); }
        public static void Unhotkey(IntPtr h, int id) { UnregisterHotKey(h, id); }
    }

    internal static class Ui
    {
        public static readonly Color Page = Color.FromArgb(16, 24, 39);
        public static readonly Color Card = Color.FromArgb(24, 35, 53);
        public static readonly Color Info = Color.FromArgb(40, 53, 71);
        public static readonly Color Text = Color.FromArgb(229, 237, 245);
        public static readonly Color Muted = Color.FromArgb(164, 178, 197);
        public static readonly Color Accent = Color.FromArgb(137, 180, 230);
        public static readonly Color Gold = Color.FromArgb(230, 198, 142);
        public static readonly Color Pink = Color.FromArgb(237, 159, 167);
        public static readonly Color Mint = Color.FromArgb(155, 214, 196);
        public static readonly Color Border = Color.FromArgb(58, 82, 112);
        public static readonly Color PanelTop = Color.FromArgb(26, 39, 57);
        public static readonly Color PanelBottom = Color.FromArgb(13, 21, 34);

        public static readonly Font Title = Font("Microsoft YaHei UI", 19f, FontStyle.Bold);
        public static readonly Font H2 = Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
        public static readonly Font Sub = Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
        public static readonly Font Body = Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular);
        public static readonly Font BodyB = Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
        public static readonly Font Small = Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
        public static readonly Font SmallB = Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        public static readonly Font Bubble = Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular);
        public static readonly Font Menu = Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
        public static readonly Font Huge = Font("Microsoft YaHei UI", 30f, FontStyle.Bold);

        private static Font Font(string family, float size, FontStyle style)
        {
            try { return new Font(family, size, style, GraphicsUnit.Point); }
            catch (Exception) { return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point); }
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2f;
            if (d <= 0 || r.Width <= 0 || r.Height <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath BubblePath(RectangleF r, float rad, float tailX, float tailLen)
        {
            var p = new GraphicsPath();
            float d = rad * 2f;
            tailX = Math.Max(r.X + rad + 12f, Math.Min(tailX, r.Right - rad - 12f));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddLine(r.Right - rad, r.Bottom, tailX + 9f, r.Bottom);
            p.AddLine(tailX + 9f, r.Bottom, tailX, r.Bottom + tailLen);
            p.AddLine(tailX, r.Bottom + tailLen, tailX - 9f, r.Bottom);
            p.AddLine(tailX - 9f, r.Bottom, r.X + rad, r.Bottom);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Star(Graphics g, float cx, float cy, float r, float rot, Color color)
        {
            if (r <= 0) return;
            var pts = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                double a = (i * 45.0 + rot) * Math.PI / 180.0;
                float rr = (i % 2 == 0) ? r : r * 0.24f;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr);
            }
            using (var b = new SolidBrush(color)) g.FillPolygon(b, pts);
        }

        public static void DrawImageAlpha(Graphics g, Image img, RectangleF dest, float alpha)
        {
            if (img == null || alpha <= 0.002f) return;
            var cm = new ColorMatrix();
            cm.Matrix33 = Math.Min(1f, alpha);
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                g.DrawImage(img, Rectangle.Round(dest), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
        }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            using (var p = Round(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
        }

        public static void Label(Graphics g, string s, Font f, Color c, float x, float y)
        {
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, x, y);
        }

        public static void TextCenter(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            using (var b = new SolidBrush(c))
            using (var sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(s, f, b, r, sf);
            }
        }

        public static string Clip(Graphics g, string s, Font f, float maxWidth)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (g.MeasureString(s, f).Width <= maxWidth) return s;
            while (s.Length > 2 && g.MeasureString(s + "…", f).Width > maxWidth) s = s.Substring(0, s.Length - 1);
            return s + "…";
        }

        public static void Pill(Graphics g, RectangleF r, Color fill, Color border)
        {
            Fill(g, r, r.Height / 2f, fill);
            if (border.A > 0) Stroke(g, r, r.Height / 2f, border, 1f);
        }
    }

    internal static class Loc
    {
        public static Lang Current = Lang.Zh;
        private static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>();

        static Loc()
        {
            Add("app", "桌面小挂件", "Desktop Buddy");
            Add("pet", "小团子", "Mochi");
            Add("petBlue", "备用", "Alt");
            Add("card", "我的扩列卡", "My card");
            Add("cardSub", "扫一扫，QQ / 微信都能加我", "Scan to add me on QQ / WeChat");
            Add("cardZoom", "点击卡片放大　·　Esc 关闭", "Click to zoom · Esc to close");
            Add("cardSize", "扩列卡 · 90 × 67.5 mm", "Card · 90 × 67.5 mm");
            Add("zoomHint", "点击任意处或按 Esc 关闭", "Click anywhere or press Esc");
            Add("panel", "控制面板", "Control panel");
            Add("panelSub", "换个姿势、换装、改大小，都在这里", "Poses, outfits and settings live here");
            Add("pat", "摸摸头", "Pat");
            Add("demo", "演示一轮", "Demo");
            Add("cardBtn", "看名片", "Card");
            Add("skin", "换装", "Character");
            Add("skinUmi", "小团子", "Mochi");
            Add("skinBlue", "备用", "Alt");
            Add("language", "语言", "Language");
            Add("langZh", "中文", "中文");
            Add("langEn", "English", "English");
            Add("size", "大小", "Size");
            Add("sizeS", "小", "S");
            Add("sizeM", "中", "M");
            Add("sizeL", "大", "L");
            Add("sizeXL", "特大", "XL");
            Add("options", "开关", "Options");
            Add("top", "窗口置顶", "Always on top");
            Add("sound", "声音", "Sound");
            Add("reminders", "休息提醒", "Break reminders");
            Add("autostart", "开机自启", "Start with Windows");
            Add("info", "状态", "Now");
            Add("music", "正在听", "Now playing");
            Add("nothing", "还没有在放歌", "Nothing playing");
            Add("together", "今天陪了你", "Together for");
            Add("pokes", "被戳了", "Poked");
            Add("pokesUnit", "次", "times");
            Add("hint", "右键挂件也能打开这些", "Right-click the pet for these too");
            Add("on", "开", "ON");
            Add("off", "关", "OFF");
            Add("saved", "已保存", "Saved");
            Add("autoFail", "设置失败：权限不允许", "Failed: permission denied");
            Add("mood.idle", "发呆中", "Idle");
            Add("mood.thinking", "在想事情", "Thinking");
            Add("mood.working", "认真工作中", "Working");
            Add("mood.watching", "帮你盯着", "Watching");
            Add("mood.searching", "翻资料中", "Searching");
            Add("mood.waiting", "等你回来", "Waiting");
            Add("mood.sleeping", "打盹中", "Sleeping");
            Add("mood.happy", "开心", "Happy");
            Add("mood.error", "有点沮丧", "Oops");
            Add("mood.listening", "在听你说", "Listening");
            Add("mood.demo", "演示中", "Demo");
            Add("menu.card", "显示名片", "Show card");
            Add("menu.pat", "摸摸头", "Pat");
            Add("menu.demo", "演示一轮", "Run a demo");
            Add("menu.panel", "控制面板", "Control panel");
            Add("menu.size", "大小", "Size");
            Add("menu.skin", "换装", "Character");
            Add("menu.lang", "语言", "Language");
            Add("menu.top", "窗口置顶", "Always on top");
            Add("menu.remind", "休息提醒", "Break reminders");
            Add("menu.sound", "声音", "Sound");
            Add("menu.corner", "回到左下角", "Back to corner");
            Add("menu.auto", "开机自启", "Start with Windows");
            Add("menu.hide", "隐藏到托盘", "Hide to tray");
            Add("menu.exit", "退出", "Quit");
            Add("tray.show", "显示 / 隐藏挂件", "Show / hide");
            Add("tray.tip", "桌面小挂件 · 双击打开面板", "Desktop buddy · double-click for panel");
            Add("tagCard", "名片", "Card");
        }

        private static void Add(string key, string zh, string en) { Map[key] = new string[] { zh, en }; }

        public static string T(string key)
        {
            string[] v;
            if (Map.TryGetValue(key, out v)) return Current == Lang.Zh ? v[0] : v[1];
            return key;
        }

        public static string Mood(Mood m) { return T("mood." + m.ToString().ToLowerInvariant()); }
    }

    internal static class Phrases
    {
        private static readonly Dictionary<string, List<string>> Data = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, string> Last = new Dictionary<string, string>();
        private static readonly Random Rng = new Random();

        public static void Load(Lang lang)
        {
            Data.Clear();
            string file = Path.Combine(Program.Dir, "assets", lang == Lang.Zh ? "phrases.zh.json" : "phrases.en.json");
            try
            {
                var ser = new JavaScriptSerializer();
                var root = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(file, Encoding.UTF8));
                var ph = root["phrases"] as Dictionary<string, object>;
                if (ph != null)
                {
                    foreach (var kv in ph)
                    {
                        var list = new List<string>();
                        if (kv.Value is ArrayList)
                        {
                            foreach (object o in (ArrayList)kv.Value) if (o != null) list.Add(Convert.ToString(o));
                        }
                        else if (kv.Value is object[])
                        {
                            foreach (object o in (object[])kv.Value) if (o != null) list.Add(Convert.ToString(o));
                        }
                        if (list.Count > 0) Data[kv.Key] = list;
                    }
                }
            }
            catch (Exception ex) { Program.Log("phrases: " + ex.Message); }
            if (Data.Count == 0)
            {
                Data["idle"] = new List<string> { lang == Lang.Zh ? "我在这儿呢" : "I'm right here" };
                Data["thinking"] = new List<string> { lang == Lang.Zh ? "让我想想" : "Let me think" };
            }
        }

        public static string Say(string cat)
        {
            List<string> list;
            if (!Data.TryGetValue(cat, out list) || list.Count == 0)
            {
                if (!Data.TryGetValue("idle", out list) || list.Count == 0) return "";
            }
            string pick = list[Rng.Next(list.Count)];
            string prev;
            if (list.Count > 1 && Last.TryGetValue(cat, out prev))
            {
                int guard = 0;
                while (pick == prev && guard++ < 5) pick = list[Rng.Next(list.Count)];
            }
            Last[cat] = pick;
            return pick;
        }

        public static string SayFormat(string cat, params object[] args)
        {
            string s = Say(cat);
            try { return string.Format(s, args); } catch (Exception) { return s; }
        }
    }

    internal sealed class UmiFrames
    {
        private readonly Dictionary<string, Bitmap> _map = new Dictionary<string, Bitmap>();
        public readonly List<string> SearchFrames = new List<string>();

        public UmiFrames(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Program.Log("umi frames missing: " + dir);
                return;
            }
            foreach (string f in Directory.GetFiles(dir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(f);
                if (name == "face") continue;
                try { _map[name] = new Bitmap(f); } catch (Exception ex) { Program.Log("frame " + name + ": " + ex.Message); }
                if (name.StartsWith("search-video-")) SearchFrames.Add(name);
            }
            SearchFrames.Sort();
        }

        public Bitmap Get(string name)
        {
            Bitmap b;
            return _map.TryGetValue(name, out b) ? b : null;
        }

        public int Count { get { return _map.Count; } }
    }

    internal sealed class Config
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Size { get; set; }
        public bool TopMost { get; set; }
        public string Skin { get; set; }
        public string Language { get; set; }
        public bool Sound { get; set; }
        public bool Reminders { get; set; }
        public bool HasPosition { get; set; }

        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(Program.Dir, "data");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.json");
            }
        }

        public static Config Load()
        {
            var c = new Config();
            c.Size = 190;
            c.TopMost = true;
            c.Skin = "umi";
            c.Language = "zh";
            c.Sound = true;
            c.Reminders = true;
            try
            {
                string p = FilePath;
                if (!File.Exists(p)) return c;
                var ser = new JavaScriptSerializer();
                var d = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(p, Encoding.UTF8));
                if (d == null) return c;
                c.X = I(d, "X", 0);
                c.Y = I(d, "Y", 0);
                c.Size = I(d, "Size", 190);
                c.TopMost = B(d, "TopMost", true);
                c.Skin = S(d, "Skin", "umi");
                c.Language = S(d, "Language", "zh");
                c.Sound = B(d, "Sound", true);
                c.Reminders = B(d, "Reminders", true);
                c.HasPosition = B(d, "HasPosition", true);
            }
            catch (Exception ex) { Program.Log("config load: " + ex.Message); }
            return c;
        }

        private static int I(Dictionary<string, object> d, string k, int def)
        {
            object v; if (d.TryGetValue(k, out v) && v != null) { try { return Convert.ToInt32(v); } catch (Exception) { } }
            return def;
        }
        private static bool B(Dictionary<string, object> d, string k, bool def)
        {
            object v; if (d.TryGetValue(k, out v) && v != null) { try { return Convert.ToBoolean(v); } catch (Exception) { } }
            return def;
        }
        private static string S(Dictionary<string, object> d, string k, string def)
        {
            object v; if (d.TryGetValue(k, out v) && v != null) { try { return Convert.ToString(v); } catch (Exception) { } }
            return def;
        }

        public static void Save(int x, int y, int size, bool topMost, string skin, string lang, bool sound, bool reminders)
        {
            try
            {
                var ser = new JavaScriptSerializer();
                var d = new Dictionary<string, object>();
                d["X"] = x; d["Y"] = y; d["Size"] = size; d["TopMost"] = topMost;
                d["Skin"] = skin; d["Language"] = lang; d["Sound"] = sound; d["Reminders"] = reminders;
                d["HasPosition"] = true;
                File.WriteAllText(FilePath, ser.Serialize(d), Encoding.UTF8);
            }
            catch (Exception ex) { Program.Log("config save: " + ex.Message); }
        }
    }

    internal static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "FurryPetTemplate";

        public static bool IsOn
        {
            get
            {
                try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(ValueName) != null; }
                catch (Exception) { return false; }
            }
        }

        public static bool Set(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return false;
                    if (on) k.SetValue(ValueName, "\"" + Path.Combine(Program.Dir, "FurryPet.exe") + "\"");
                    else k.DeleteValue(ValueName, false);
                    return true;
                }
            }
            catch (Exception ex) { Program.Log("autostart: " + ex.Message); return false; }
        }
    }

    internal sealed class MusicWatcher
    {
        public volatile bool Playing;
        public volatile string Song = "";
        public volatile string Artist = "";
        public volatile bool Available;
        private Thread _thread;
        private volatile bool _stop;

        public void Start()
        {
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Start();
        }

        public void Stop() { _stop = true; }

        private void Loop()
        {
            while (!_stop)
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:28123/api/state");
                    req.Timeout = 2500;
                    req.ReadWriteTimeout = 2500;
                    using (var resp = req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        var ser = new JavaScriptSerializer();
                        var d = ser.Deserialize<Dictionary<string, object>>(sr.ReadToEnd());
                        if (d != null)
                        {
                            bool playing = d.ContainsKey("playing") && Convert.ToBoolean(d["playing"]);
                            long stale = d.ContainsKey("stale") ? Convert.ToInt64(d["stale"]) : 99999;
                            string song = d.ContainsKey("songName") ? Convert.ToString(d["songName"]) : "";
                            string artist = d.ContainsKey("artist") ? Convert.ToString(d["artist"]) : "";
                            Available = true;
                            Playing = playing && stale < 12000 && !string.IsNullOrEmpty(song);
                            Song = song;
                            Artist = artist;
                        }
                    }
                }
                catch (Exception)
                {
                    Available = false;
                    Playing = false;
                }
                for (int i = 0; i < 12 && !_stop; i++) Thread.Sleep(500);
            }
        }
    }

    internal static class Sound
    {
        public static void Play(string file, bool enabled)
        {
            if (!enabled) return;
            string path = Path.Combine(Program.Dir, "assets", "sounds", file);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { using (var p = new SoundPlayer(path)) { p.Load(); p.Play(); } }
                catch (Exception) { }
            });
        }
    }
}
