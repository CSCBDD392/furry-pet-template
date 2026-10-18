// FurryPet v2 — the desktop pet window and its behaviour.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace FurryPet
{
    internal sealed class PetForm : Form
    {
        private const int W = 400;
        private const int H = 430;
        private const int BaseY = 408;
        private const int PetX = 16;

        private readonly UmiFrames _umi;
        private readonly bool _umiReady;
        private readonly Bitmap _bluePet;
        private readonly Bitmap _glow;
        private readonly Bitmap _shadow;
        private readonly Bitmap _card;
        private readonly Bitmap _canvas;
        private readonly Timer _timer;
        private readonly Random _rng = new Random();
        private readonly MusicWatcher _music = new MusicWatcher();
        private readonly List<Burst> _burst = new List<Burst>();
        private readonly double _startedAt = App.Now;

        private Skin _skin = Skin.Umi;
        private Lang _lang = Lang.Zh;
        private int _size = 190;
        private int _drawW;
        private int _drawH;
        private bool _topMost = true;
        private bool _soundOn = true;
        private bool _reminders = true;

        private int _x;
        private int _y;

        private Mood _mood = Mood.Idle;
        private double _moodSince = App.Now;
        private double _moodUntil = double.MaxValue;
        private double _nextActivity = App.Now + 6;
        private double _lastPokeText = -10;
        private bool _demo;
        private int _demoIndex;
        private double _demoNext;

        private string _bubbleText = "";
        private double _bubbleAt = -10;
        private double _bubbleUntil = -10;
        private RectangleF _bubbleRect;

        private double _pokeAt = -10;
        private double _lastClick = -10;
        private int _pokeCount;
        private double _pressedAt;

        private bool _dragging;
        private bool _moved;
        private Point _dragOffset;
        private RectangleF _tagCard;
        private RectangleF _tagPanel;
        private bool _tagCardHot;
        private bool _tagPanelHot;
        private bool _hover;
        private float _lean;
        private float _musicSway;

        private double _lastTick = -10;
        private int _activeSeconds;
        private double _restDue = 45 * 60;
        private double _waterDue = 90 * 60;
        private double _lastMusicBubble = -100;
        private double _lastLateNight = -10000;
        private string _lastSong = "";
        private bool _away;
        private int _lastIdle;

        private PanelForm _panel;
        private CardForm _cardForm;
        private NotifyIcon _tray;
        private Icon _trayIcon;
        private ContextMenuStrip _trayMenu;

        private const int HK_TOGGLE = 1, HK_CARD = 2, HK_PANEL = 3;

        public PetForm(bool openCard, bool openPanel, bool poke)
        {
            _umi = new UmiFrames(Path.Combine(Program.Dir, "assets", "umi"));
            _umiReady = _umi.Count > 0;
            _bluePet = LoadBitmap(Path.Combine(Program.Dir, "assets", "blue", "pet.png"));
            _glow = LoadBitmap(Path.Combine(Program.Dir, "assets", "blue", "glow.png"));
            _shadow = LoadBitmap(Path.Combine(Program.Dir, "assets", "blue", "shadow.png"));
            _card = LoadBitmap(Path.Combine(Program.Dir, "assets", "card.png"));

            var cfg = Config.Load();
            _size = Math.Max(120, Math.Min(300, cfg.Size));
            _topMost = cfg.TopMost;
            _soundOn = cfg.Sound;
            _reminders = cfg.Reminders;
            _skin = cfg.Skin == "blue" ? Skin.Blue : Skin.Umi;
            if (!_umiReady) _skin = Skin.Blue;
            _lang = cfg.Language == "en" ? Lang.En : Lang.Zh;
            Loc.Current = _lang;
            Phrases.Load(_lang);
            RecomputeSize();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "桌面小挂件";
            TopMost = true;
            MinimumSize = new Size(1, 1);
            BackColor = Color.Black;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            _canvas = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            _timer = new Timer();
            _timer.Interval = 40;
            _timer.Tick += delegate { Render(); };

            var wa = Screen.PrimaryScreen.WorkingArea;
            _x = cfg.HasPosition ? cfg.X : wa.Left - 24;
            _y = cfg.HasPosition ? cfg.Y : wa.Bottom - BaseY;
            ClampPosition();

            Load += delegate
            {
                try
                {
                    Native.SetExStyle(Handle, Native.WS_EX_LAYERED | Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
                    Location = new Point(_x, _y);
                    Show();
                    TopMost = _topMost;
                    InitTray();
                    RegisterHotkeys();
                    _music.Start();
                    _moodSince = App.Now;
                    _nextActivity = App.Now + 8;

                    if (openCard) BeginInvoke(new Action(ShowCard));
                    if (openPanel) BeginInvoke(new Action(ShowPanel));
                    if (poke) BeginInvoke(new Action(delegate { Pat(); }));
                    else BeginInvoke(new Action(delegate { Greet(); }));
                    Render();
                    _timer.Start();
                    Program.Log("v2 loaded, pos=" + _x + "," + _y + " size=" + _size + " skin=" + _skin + " frames=" + _umi.Count);
                }
                catch (Exception ex)
                {
                    Program.Log("Load failed: " + ex);
                }
            };
        }

        private static Bitmap LoadBitmap(string path)
        {
            using (var b = new Bitmap(path)) return new Bitmap(b);
        }

        private void RecomputeSize()
        {
            _drawH = _size;
            _drawW = _skin == Skin.Umi ? _size : (int)Math.Round(_size * (_bluePet.Width / (float)_bluePet.Height));
        }

        private float PetCenterX { get { return PetX + _drawW / 2f; } }
        private float PetTop { get { return BaseY - _drawH; } }
        private float TagX { get { return _skin == Skin.Umi ? PetX + _drawW * 0.86f : PetX + _drawW + 2f; } }

        public Skin CurrentSkin { get { return _skin; } }
        public Lang CurrentLang { get { return _lang; } }
        public int CurrentSize { get { return _size; } }
        public bool IsTopMost { get { return _topMost; } }
        public bool SoundOn { get { return _soundOn; } }
        public bool RemindersOn { get { return _reminders; } }
        public Mood CurrentMood { get { return _mood; } }
        public int PokeCount { get { return _pokeCount; } }
        public TimeSpan Together { get { return TimeSpan.FromSeconds(App.Now - _startedAt); } }
        public string MoodLabel { get { return Loc.Mood(_mood); } }
        public string FacePath
        {
            get
            {
                if (_skin == Skin.Umi && _umiReady)
                {
                    string umiFace = Path.Combine(Program.Dir, "assets", "umi", "face.png");
                    if (File.Exists(umiFace)) return umiFace;
                }
                return Path.Combine(Program.Dir, "assets", "blue", "face.png");
            }
        }
        public string MusicLine
        {
            get
            {
                if (_music.Playing && !string.IsNullOrEmpty(_music.Song))
                    return "♪ " + _music.Song + (string.IsNullOrEmpty(_music.Artist) ? "" : " · " + _music.Artist);
                return Loc.T("nothing");
            }
        }

        public Bitmap PreviewFrame()
        {
            return FrameFor(App.Now);
        }

        // ------------------------------------------------------------ lifecycle
        private void ClampPosition()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            if (_x < wa.Left - _drawW) _x = wa.Left - 24;
            if (_x > wa.Right - 80) _x = wa.Right - 80;
            if (_y < wa.Top - 40) _y = wa.Top - 40;
            if (_y > wa.Bottom - 60) _y = wa.Bottom - 60;
        }

        private void SaveConfig()
        {
            Config.Save(_x, _y, _size, _topMost, _skin == Skin.Blue ? "blue" : "umi", _lang == Lang.En ? "en" : "zh", _soundOn, _reminders);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if (m.Result.ToInt32() == Native.HTCLIENT)
                {
                    int lp = m.LParam.ToInt32();
                    int sx = (short)(lp & 0xFFFF);
                    int sy = (short)((lp >> 16) & 0xFFFF);
                    if (AlphaAt(sx - _x, sy - _y) < 12) m.Result = (IntPtr)Native.HTTRANSPARENT;
                }
                return;
            }
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HK_TOGGLE) ToggleVisible();
                else if (id == HK_CARD) ShowCard();
                else if (id == HK_PANEL) ShowPanel();
                return;
            }
            base.WndProc(ref m);
        }

        private int AlphaAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _canvas.Width || y >= _canvas.Height) return 0;
            try { return _canvas.GetPixel(x, y).A; } catch (Exception) { return 255; }
        }

        private void RegisterHotkeys()
        {
            bool a = Native.Hotkey(Handle, HK_TOGGLE, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT | Native.MOD_NOREPEAT, 0x55);
            bool b = Native.Hotkey(Handle, HK_CARD, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT | Native.MOD_NOREPEAT, 0x43);
            bool c = Native.Hotkey(Handle, HK_PANEL, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT | Native.MOD_NOREPEAT, 0x50);
            Program.Log("hotkeys U=" + a + " C=" + b + " P=" + c);
        }

        // ------------------------------------------------------------ actions
        private void Greet()
        {
            int hour = DateTime.Now.Hour;
            string cat;
            if (hour >= 5 && hour < 11) cat = "greeting_morning";
            else if (hour >= 11 && hour < 14) cat = "greeting_noon";
            else if (hour >= 14 && hour < 18) cat = "greeting_afternoon";
            else if (hour >= 18 && hour < 23) cat = "greeting_evening";
            else if (hour >= 23 || hour < 2) cat = "greeting_night";
            else cat = "greeting_late";
            ShowBubble(Phrases.Say(cat), 7);
        }

        public void Pat()
        {
            _demo = false;
            _pokeCount++;
            _lastPokeText = App.Now;
            _pokeAt = App.Now;
            string cat = "poke_idle";
            if (_mood == Mood.Thinking || _mood == Mood.Working || _mood == Mood.Watching || _mood == Mood.Searching) cat = "poke_busy";
            else if (_mood == Mood.Waiting || _mood == Mood.Sleeping) cat = "poke_waiting";
            ShowBubble(Phrases.Say(cat), 4.5);
            SetMood(Mood.Happy, 1.6, false, null);
            for (int i = 0; i < 8; i++)
            {
                double ang = -Math.PI / 2 + (_rng.NextDouble() - 0.5) * 2.6;
                float speed = 55f + (float)_rng.NextDouble() * 80f;
                _burst.Add(new Burst
                {
                    X = PetCenterX + (float)(_rng.NextDouble() - 0.5) * _drawW * 0.4f,
                    Y = PetTop + _drawH * 0.45f,
                    VX = (float)Math.Cos(ang) * speed,
                    VY = (float)Math.Sin(ang) * speed - 25f,
                    Size = 3.5f + (float)_rng.NextDouble() * 3.5f,
                    Rot = (float)_rng.NextDouble() * 90f,
                    Born = App.Now,
                });
            }
            Sound.Play("boop.wav", _soundOn);
        }

        public void RunDemo()
        {
            if (_demo) { _demo = false; SetMood(Mood.Idle, 0, false, null); return; }
            _demo = true;
            _demoIndex = -1;
            _demoNext = 0;
            ShowBubble(Phrases.Say("demo"), 4);
        }

        private void DemoStep(double t)
        {
            if (!_demo) return;
            if (t < _demoNext) return;
            _demoIndex++;
            Mood[] seq = new Mood[] { Mood.Thinking, Mood.Working, Mood.Watching, Mood.Searching, Mood.Waiting, Mood.Sleeping, Mood.Happy, Mood.Error, Mood.Listening };
            if (_demoIndex >= seq.Length) { _demo = false; SetMood(Mood.Idle, 0, false, null); return; }
            SetMood(seq[_demoIndex], 0, false, null);
            string cat = CategoryFor(seq[_demoIndex]);
            if (cat != null) ShowBubble(Phrases.Say(cat), 4.4);
            _demoNext = t + 5.2;
        }

        private void SetMood(Mood m, double seconds, bool say, string category)
        {
            _mood = m;
            _moodSince = App.Now;
            _moodUntil = seconds > 0 ? App.Now + seconds : double.MaxValue;
            if (say)
            {
                string cat = category ?? CategoryFor(m);
                if (cat != null) ShowBubble(Phrases.Say(cat), 4.5);
            }
        }

        private static string CategoryFor(Mood m)
        {
            switch (m)
            {
                case Mood.Thinking: return "thinking";
                case Mood.Working: return "editing";
                case Mood.Watching: return "running";
                case Mood.Searching: return "searching";
                case Mood.Waiting: return "waiting";
                case Mood.Happy: return "success";
                case Mood.Error: return "error";
                case Mood.Listening: return "listening";
                case Mood.Idle: return "idle";
                default: return null;
            }
        }

        private void ShowBubble(string text, double seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            _bubbleText = text;
            _bubbleAt = App.Now;
            _bubbleUntil = App.Now + seconds;
        }

        // ------------------------------------------------------------ public setters (panel/menu)
        public void SetSkin(Skin skin)
        {
            if (skin == Skin.Umi && !_umiReady) return;
            if (_skin == skin) return;
            _skin = skin;
            RecomputeSize();
            if (_cardForm != null && !_cardForm.IsDisposed) { _cardForm.Close(); _cardForm = null; }
            _mood = Mood.Idle;
            _moodSince = App.Now;
            UpdateTrayIcon();
            ShowBubble(Phrases.Say("idle"), 4);
            SaveConfig();
            Render();
        }

        public void SetLanguage(Lang lang)
        {
            if (_lang == lang) return;
            _lang = lang;
            Loc.Current = lang;
            Phrases.Load(lang);
            if (_cardForm != null && !_cardForm.IsDisposed) { _cardForm.Close(); _cardForm = null; }
            if (_panel != null && !_panel.IsDisposed) _panel.Invalidate();
            UpdateTrayText();
            ShowBubble(Phrases.Say("idle"), 5);
            SaveConfig();
            Render();
        }

        public void SetSize(int size)
        {
            _size = Math.Max(140, Math.Min(280, size));
            RecomputeSize();
            ClampPosition();
            SaveConfig();
            Render();
        }

        public void SetTopMost(bool on)
        {
            _topMost = on;
            TopMost = on;
            SaveConfig();
        }

        public void SetSound(bool on)
        {
            _soundOn = on;
            SaveConfig();
        }

        public void SetReminders(bool on)
        {
            _reminders = on;
            SaveConfig();
        }

        public bool ToggleAutostart()
        {
            bool on = Autostart.IsOn;
            bool ok = Autostart.Set(!on);
            return ok;
        }

        public void MoveHome()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            _x = wa.Left - 24;
            _y = wa.Bottom - BaseY;
            SaveConfig();
            Render();
        }

        public void ShowCard()
        {
            if (_cardForm != null && !_cardForm.IsDisposed) { _cardForm.Activate(); return; }
            _cardForm = new CardForm(this);
            _cardForm.FormClosed += delegate { _cardForm = null; };
            _cardForm.Show();
        }

        public void ShowPanel()
        {
            if (_panel != null && !_panel.IsDisposed) { _panel.Activate(); return; }
            _panel = new PanelForm(this);
            _panel.FormClosed += delegate { _panel = null; };
            _panel.Show();
        }

        // ------------------------------------------------------------ main loop
        private void Render()
        {
            if (!Visible) return;
            try
            {
                double t = App.Now;
                if (t - _lastTick > 1.0) { _lastTick = t; Tick(t); }
                if (_demo) DemoStep(t);
                else Schedule(t);

                float sleep = _mood == Mood.Sleeping ? 1f : 0f;
                float breathe = (float)Math.Sin(t * 1.25);
                float bob = (float)Math.Sin(t * 0.8) * 3.0f * (1 - sleep);
                float sway = (float)Math.Sin(t * 0.5) * 0.8f * (1 - sleep) + _musicSway;
                if (_skin == Skin.Blue && sleep < 0.1f)
                {
                    switch (_mood)
                    {
                        case Mood.Working: bob = (float)Math.Sin(t * 4.2) * 3.2f; sway = (float)Math.Sin(t * 2.1) * 1.1f; break;
                        case Mood.Watching: sway = (float)Math.Sin(t * 0.95) * 3.0f; break;
                        case Mood.Searching: bob = (float)Math.Sin(t * 7.0) * 2.0f; sway = (float)Math.Sin(t * 5.2) * 1.6f; break;
                        case Mood.Listening: sway = -2.0f + (float)Math.Sin(t * 1.4) * 0.6f; break;
                        case Mood.Error: sway = 4.0f; break;
                        case Mood.Happy: bob = (float)Math.Abs(Math.Sin(t * 3.2)) * -6.0f; break;
                    }
                }
                float poke = PokeEnv(t);
                float hop = -9f * poke;
                float cx = PetCenterX + (_hover ? _lean : 0f);
                float by = BaseY + hop;

                using (var g = Graphics.FromImage(_canvas))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;

                    float aura = 0.45f + 0.10f * (float)Math.Sin(t * 1.4);
                    Ui.DrawImageAlpha(g, _glow, new RectangleF(cx - 160, BaseY - 300, 320, 320), aura * (0.30f + 0.70f * (1 - sleep)));
                    Ui.DrawImageAlpha(g, _shadow, new RectangleF(cx - 86, BaseY - 26, 172, 44), 0.70f * (1 - sleep) + 0.10f);

                    DrawSparkle(g, cx - _drawW * 0.46f, PetTop + _drawH * 0.20f, 5.5f, (float)(t * 22), 0.40f + 0.25f * (float)Math.Sin(t * 1.3));
                    DrawSparkle(g, cx + _drawW * 0.50f, PetTop + _drawH * 0.44f, 4.5f, (float)(-t * 16), 0.35f + 0.3f * (float)Math.Sin(t * 1.1 + 2));
                    DrawSparkle(g, cx - _drawW * 0.28f, BaseY - 38, 4.0f, (float)(t * 18), 0.30f + 0.2f * (float)Math.Sin(t * 1.7 + 4));

                    var frame = FrameFor(t);
                    var state = g.Save();
                    g.TranslateTransform(cx, by);
                    g.RotateTransform(sway);
                    g.ScaleTransform(1f + 0.055f * poke, 1f - 0.07f * poke + 0.008f * breathe * (1 - sleep));
                    g.TranslateTransform(0, bob);
                    DrawPet(g, frame, sleep);
                    g.Restore(state);

                    DrawTags(g, t);
                    DrawBurst(g, t);
                    DrawBubble(g, t, cx);
                    if (sleep > 0.2f) DrawZzz(g, t, cx + _drawW * 0.26f, PetTop + 12);
                }
                Push();
            }
            catch (Exception ex)
            {
                Program.Log("Render: " + ex);
            }
        }

        private void Tick(double t)
        {
            int idle = Native.IdleSeconds;
            _lastIdle = idle;
            bool active = idle < 3;
            if (active)
            {
                _activeSeconds++;
                if (_away)
                {
                    _away = false;
                    if (_mood == Mood.Sleeping || _mood == Mood.Waiting)
                    {
                        SetMood(Mood.Happy, 2.4, false, null);
                        ShowBubble(Phrases.Say("wake"), 5);
                        Sound.Play("ding.wav", _soundOn);
                    }
                }
            }
            else if (idle > 60)
            {
                _away = true;
            }

            if (active && _reminders)
            {
                if (_activeSeconds >= _restDue)
                {
                    _restDue = _activeSeconds + 45 * 60;
                    SetMood(Mood.Happy, 3.0, false, null);
                    ShowBubble(Phrases.Say("reminder_rest"), 10);
                    Sound.Play("ding.wav", _soundOn);
                }
                if (_activeSeconds >= _waterDue)
                {
                    _waterDue = _activeSeconds + 90 * 60;
                    ShowBubble(Phrases.Say("reminder_water"), 10);
                }
            }

            if (_reminders)
            {
                int hour = DateTime.Now.Hour;
                if ((hour >= 23 || hour < 5) && App.Now - _lastLateNight > 3600)
                {
                    _lastLateNight = App.Now;
                    ShowBubble(Phrases.Say("late_night"), 10);
                }
            }

            if (_music.Playing)
            {
                _musicSway = (float)Math.Sin(App.Now * 2.4) * 1.1f;
                if (_music.Song != _lastSong && App.Now - _lastMusicBubble > 90 && _rng.NextDouble() < 0.45)
                {
                    _lastSong = _music.Song;
                    _lastMusicBubble = App.Now;
                    ShowBubble(Phrases.SayFormat("music", _music.Song), 7);
                }
            }
            else
            {
                _musicSway = 0;
            }

            if (!_demo)
            {
                if (idle > 180 && _mood != Mood.Sleeping)
                {
                    SetMood(Mood.Sleeping, 0, false, null);
                }
                else if (idle > 75 && idle <= 180 && (_mood == Mood.Idle || _mood == Mood.Listening))
                {
                    SetMood(Mood.Waiting, 0, true, "waiting");
                }
            }
            if (_panel != null && !_panel.IsDisposed) _panel.Invalidate();
        }

        private void Schedule(double t)
        {
            if (_mood == Mood.Sleeping) return;
            if (_mood != Mood.Idle && t > _moodUntil)
            {
                SetMood(Mood.Idle, 0, false, null);
                _nextActivity = t + 5 + _rng.NextDouble() * 8;
            }
            if (_mood == Mood.Waiting && _lastIdle < 3)
            {
                SetMood(Mood.Idle, 0, false, null);
                _nextActivity = t + 4;
            }
            if (_mood == Mood.Idle && t > _nextActivity)
            {
                double r = _rng.NextDouble();
                Mood m;
                if (r < 0.30) m = Mood.Thinking;
                else if (r < 0.50) m = Mood.Working;
                else if (r < 0.70) m = Mood.Watching;
                else if (r < 0.85) m = Mood.Searching;
                else m = Mood.Listening;
                SetMood(m, 4 + _rng.NextDouble() * 5, _rng.NextDouble() < 0.45, null);
                _nextActivity = t + 7 + _rng.NextDouble() * 9;
            }
        }

        // ------------------------------------------------------------ frames
        private Bitmap FrameFor(double t)
        {
            if (_skin == Skin.Blue) return _bluePet;
            double mt = t - _moodSince;
            string name = "idle-open";

            if (_bubbleUntil > t && t - _bubbleAt < 1.5 &&
                (_mood == Mood.Idle || _mood == Mood.Listening || _mood == Mood.Waiting || _mood == Mood.Happy))
            {
                string[] mouths = new string[] { "talk-closed", "talk-small", "talk-medium", "talk-wide", "talk-medium", "talk-small" };
                name = mouths[(int)(t * 9) % mouths.Length];
            }
            else
            {
                switch (_mood)
                {
                    case Mood.Thinking:
                        name = (mt % 3.1) < 2.95 ? "think" : "think-blink";
                        break;
                    case Mood.Working:
                        {
                            double p = mt % 1.0;
                            name = (p < 0.38 || p >= 0.5) ? "work" : "work-blink";
                        }
                        break;
                    case Mood.Watching:
                        {
                            string[] seq = new string[] { "watch-left", "watch-left-far", "watch-left", "watch", "watch-right", "watch-right-far", "watch-right", "watch" };
                            name = seq[((int)(mt / 0.42)) % seq.Length];
                        }
                        break;
                    case Mood.Searching:
                        {
                            if (_umi.SearchFrames.Count > 3)
                            {
                                int idx = ((int)(mt / 0.07)) % _umi.SearchFrames.Count;
                                name = _umi.SearchFrames[idx];
                            }
                            else name = "search";
                        }
                        break;
                    case Mood.Waiting:
                        name = (mt % 3.4) < 3.25 ? "wait" : "wait-blink";
                        break;
                    case Mood.Sleeping:
                        name = "rest";
                        break;
                    case Mood.Happy:
                        {
                            double p = mt % 1.0;
                            name = p < 0.72 ? "happy" : (p < 0.9 ? "happy-blink" : "happy");
                        }
                        break;
                    case Mood.Error:
                        name = "error";
                        break;
                    case Mood.Listening:
                        name = (mt % 2.4) < 2.25 ? "listen" : "listen-blink";
                        break;
                    default:
                        {
                            double p = mt % 13.0;
                            if (p < 7.5) name = "idle-open";
                            else if (p < 7.63) name = "idle-blink";
                            else if (p < 11.5) name = "idle-open";
                            else if (p < 11.63) name = "idle-blink";
                            else if (p < 12.9) name = "idle-open";
                            else if (p < 13.2) name = "idle-wink";
                            else name = "idle-open";
                        }
                        break;
                }
            }
            Bitmap bmp = _umi.Get(name);
            if (bmp == null) bmp = _umi.Get("idle-open");
            if (bmp == null) bmp = _bluePet;
            return bmp;
        }

        private void DrawPet(Graphics g, Bitmap frame, float sleep)
        {
            var dest = new Rectangle((int)Math.Round(-_drawW / 2f), -_drawH, _drawW, _drawH);
            if (sleep < 0.01f)
            {
                g.DrawImage(frame, dest, 0, 0, frame.Width, frame.Height, GraphicsUnit.Pixel);
                return;
            }
            float dim = 1f - 0.20f * sleep;
            var cm = new ColorMatrix(new float[][]
            {
                new float[] { dim, 0, 0, 0, 0 },
                new float[] { 0, dim, 0, 0, 0 },
                new float[] { 0, 0, dim, 0, 0 },
                new float[] { 0, 0, 0, 1f, 0 },
                new float[] { 0, 0, 0, 0, 1f }
            });
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                g.DrawImage(frame, dest, 0, 0, frame.Width, frame.Height, GraphicsUnit.Pixel, ia);
            }
        }

        private static void DrawSparkle(Graphics g, float x, float y, float r, float rot, float a)
        {
            a = Math.Max(0f, Math.Min(1f, a));
            Ui.Star(g, x, y, r * 1.8f, rot, Color.FromArgb((int)(70 * a), 255, 238, 190));
            Ui.Star(g, x, y, r, rot, Color.FromArgb((int)(210 * a), 255, 214, 120));
            using (var b = new SolidBrush(Color.FromArgb((int)(230 * a), 255, 255, 255)))
                g.FillEllipse(b, x - r * 0.16f, y - r * 0.16f, r * 0.32f, r * 0.32f);
        }

        private void DrawTags(Graphics g, double t)
        {
            float tx = TagX;
            float y = BaseY - 46 - (_tagCardHot ? 3 : 0);
            _tagCard = new RectangleF(tx, y, 50, 24);
            _tagPanel = new RectangleF(tx + 54, BaseY - 47 - (_tagPanelHot ? 3 : 0), 26, 26);

            // card tag
            using (var p = Ui.Round(_tagCard, 8))
            {
                using (var b = new LinearGradientBrush(_tagCard, Color.White, _tagCardHot ? Color.FromArgb(205, 238, 255) : Color.FromArgb(232, 246, 255), 90))
                    g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(_tagCardHot ? 235 : 165, 96, 186, 236), _tagCardHot ? 1.6f : 1.2f))
                    g.DrawPath(pen, p);
            }
            using (var b = new SolidBrush(Color.FromArgb(255, 236, 246, 255))) g.FillEllipse(b, tx + 4, _tagCard.Y + 9.5f, 5, 5);
            using (var pen = new Pen(Color.FromArgb(120, 96, 186, 236), 0.9f)) g.DrawEllipse(pen, tx + 4, _tagCard.Y + 9.5f, 5, 5);
            using (var b = new SolidBrush(Color.FromArgb(255, 38, 66, 96)))
                g.DrawString(Loc.T("tagCard"), Ui.SmallB, b, tx + 13, _tagCard.Y + 4.5f);
            Ui.Star(g, tx + 45, _tagCard.Y + 4.5f, 3.0f, 0, Color.FromArgb(245, 255, 196, 80));

            // panel tag
            Ui.Fill(g, _tagPanel, 8, _tagPanelHot ? Color.FromArgb(255, 236, 250, 255) : Color.White);
            Ui.Stroke(g, _tagPanel, 8, Color.FromArgb(_tagPanelHot ? 235 : 165, 96, 186, 236), _tagPanelHot ? 1.6f : 1.2f);
            using (var b = new SolidBrush(Color.FromArgb(255, 70, 104, 140)))
            {
                g.FillEllipse(b, _tagPanel.X + 6, _tagPanel.Y + 17.5f, 2.6f, 2.6f);
                g.FillEllipse(b, _tagPanel.X + 11.7f, _tagPanel.Y + 17.5f, 2.6f, 2.6f);
                g.FillEllipse(b, _tagPanel.X + 17.4f, _tagPanel.Y + 17.5f, 2.6f, 2.6f);
            }
        }

        private void DrawBurst(Graphics g, double t)
        {
            for (int i = _burst.Count - 1; i >= 0; i--)
            {
                var b = _burst[i];
                double age = t - b.Born;
                if (age > 0.85) { _burst.RemoveAt(i); continue; }
                float a = (float)(1.0 - age / 0.85);
                float x = b.X + b.VX * (float)age;
                float y = b.Y + b.VY * (float)age + 60f * (float)(age * age);
                DrawSparkle(g, x, y, b.Size * (0.6f + 0.6f * (float)age), b.Rot + (float)(age * 180), a * 0.95f);
            }
        }

        private void DrawBubble(Graphics g, double t, float cx)
        {
            if (t < _bubbleAt || t > _bubbleUntil + 0.32) { _bubbleRect = RectangleF.Empty; return; }
            float a = 1f;
            if (t < _bubbleAt + 0.18) a = (float)((t - _bubbleAt) / 0.18);
            else if (t > _bubbleUntil) a = (float)(1.0 - (t - _bubbleUntil) / 0.32);
            a = Math.Max(0f, Math.Min(1f, a));

            var size = g.MeasureString(_bubbleText, Ui.Bubble, 280);
            float bw = Math.Min(310f, size.Width + 32f);
            float bh = size.Height + 20f;
            float bx = cx - bw / 2f;
            float minX = Math.Max(8f, -_x + 8f);
            float maxX = Math.Min(W - bw - 8f, Screen.PrimaryScreen.WorkingArea.Right - _x - bw - 8f);
            if (maxX < minX) maxX = minX;
            bx = Math.Max(minX, Math.Min(bx, maxX));
            float by = PetTop - bh + 22f;
            var r = new RectangleF(bx, by, bw, bh);

            using (var path = Ui.BubblePath(r, 15f, cx, 10f))
            {
                using (var sp = Ui.BubblePath(new RectangleF(r.X + 1, r.Y + 4, r.Width, r.Height), 15f, cx, 10f))
                using (var shadow = new SolidBrush(Color.FromArgb((int)(38 * a), 4, 22, 40)))
                    g.FillPath(shadow, sp);
                using (var fill = new SolidBrush(Color.FromArgb((int)(248 * a), 255, 255, 255)))
                    g.FillPath(fill, path);
                using (var pen = new Pen(Color.FromArgb((int)(200 * a), 168, 200, 232), 1.4f))
                    g.DrawPath(pen, path);
            }

            var textRect = new RectangleF(r.X + 16, r.Y + 9, r.Width - 32, r.Height - 18);
            using (var sf = new StringFormat())
            using (var brush = new SolidBrush(Color.FromArgb((int)(255 * a), 32, 48, 70)))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(_bubbleText, Ui.Bubble, brush, textRect, sf);
            }
            _bubbleRect = r;
        }

        private void DrawZzz(Graphics g, double t, float x, float y)
        {
            for (int i = 0; i < 3; i++)
            {
                double ph = t - i * 0.9;
                float rise = (float)((ph % 2.7) / 2.7);
                float a = (float)Math.Sin(rise * Math.PI);
                if (a <= 0.01f) continue;
                float fs = 10f + i * 3f + rise * 4f;
                using (var f = new Font("Microsoft YaHei UI", fs, FontStyle.Bold))
                using (var b = new SolidBrush(Color.FromArgb((int)(185 * a), 190, 214, 240)))
                    g.DrawString("z", f, b, x + i * 13f + rise * 8f, y - rise * 26f);
            }
        }

        private float PokeEnv(double t)
        {
            double p = (t - _pokeAt) / 0.55;
            if (p < 0 || p > 1) return 0f;
            return (float)Math.Sin(Math.PI * p);
        }

        private void Push()
        {
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hBitmap = _canvas.GetHbitmap(Color.FromArgb(0));
                old = Native.SelectObject(memDc, hBitmap);
                var size = new Native.SIZE { cx = _canvas.Width, cy = _canvas.Height };
                var src = new Native.POINT { X = 0, Y = 0 };
                var dst = new Native.POINT { X = _x, Y = _y };
                var blend = new Native.BLENDFUNCTION
                {
                    BlendOp = Native.AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = Native.AC_SRC_ALPHA,
                };
                Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
            }
            finally
            {
                if (old != IntPtr.Zero) Native.SelectObject(memDc, old);
                if (hBitmap != IntPtr.Zero) Native.DeleteObject(hBitmap);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        // ------------------------------------------------------------ mouse
        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            if (!_demo && _mood == Mood.Idle) SetMood(Mood.Listening, 2.2, false, null);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _lean = 0;
            _tagCardHot = false;
            _tagPanelHot = false;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                var sc = PointToScreen(new Point(e.X, e.Y));
                int nx = sc.X - _dragOffset.X;
                int ny = sc.Y - _dragOffset.Y;
                if (Math.Abs(nx - _x) + Math.Abs(ny - _y) > 0)
                {
                    _moved = _moved || Math.Abs(nx - _x) > 3 || Math.Abs(ny - _y) > 3;
                    _x = nx; _y = ny;
                }
            }
            else
            {
                bool ch = _tagCard.Contains(e.Location);
                bool ph = _tagPanel.Contains(e.Location);
                if (ch != _tagCardHot || ph != _tagPanelHot)
                {
                    _tagCardHot = ch; _tagPanelHot = ph;
                    Cursor = (ch || ph) ? Cursors.Hand : Cursors.Default;
                }
                float target = 0;
                if (e.X < PetX + _drawW * 0.35f) target = -3f;
                else if (e.X > PetX + _drawW * 0.65f) target = 3f;
                _lean += (target - _lean) * 0.25f;
                if (_mood == Mood.Idle && _bubbleUntil < App.Now) { }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _moved = false;
                _pressedAt = App.Now;
                var sc = PointToScreen(e.Location);
                _dragOffset = new Point(sc.X - _x, sc.Y - _y);
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Capture = false;
                bool wasDrag = _dragging && _moved;
                _dragging = false;
                if (!wasDrag)
                {
                    if (_tagCard.Contains(e.Location)) { ShowCard(); _lastClick = -10; }
                    else if (_tagPanel.Contains(e.Location)) { ShowPanel(); _lastClick = -10; }
                    else if (_bubbleRect.Contains(e.Location)) { _bubbleUntil = 0; }
                    else
                    {
                        double now = App.Now;
                        if (now - _lastClick < 0.36)
                        {
                            _lastClick = -10;
                            ShowCard();
                        }
                        else
                        {
                            _lastClick = now;
                            Pat();
                        }
                    }
                }
                else
                {
                    var wa = Screen.PrimaryScreen.WorkingArea;
                    if (_x < wa.Left + 34 && _y > wa.Bottom - BaseY - 60)
                    {
                        _x = wa.Left - 24;
                        _y = wa.Bottom - BaseY;
                    }
                    ClampPosition();
                    SaveConfig();
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                ShowMenu();
            }
            base.OnMouseUp(e);
        }

        // ------------------------------------------------------------ menus / tray
        private void ShowMenu()
        {
            var m = new ContextMenuStrip();
            m.Font = Ui.Menu;
            m.Items.Add(Loc.T("menu.card"), null, delegate { ShowCard(); });
            m.Items.Add(Loc.T("menu.pat"), null, delegate { Pat(); });
            m.Items.Add(Loc.T("menu.panel"), null, delegate { ShowPanel(); });
            m.Items.Add(Loc.T("menu.demo"), null, delegate { RunDemo(); });
            m.Items.Add(new ToolStripSeparator());

            var size = new ToolStripMenuItem(Loc.T("menu.size"));
            int[] sizes = new int[] { 160, 190, 220, 260 };
            string[] names = new string[] { "sizeS", "sizeM", "sizeL", "sizeXL" };
            for (int i = 0; i < sizes.Length; i++)
            {
                int v = sizes[i];
                var it = new ToolStripMenuItem(Loc.T(names[i]) + "（" + v + "）", null, delegate { SetSize(v); });
                it.Checked = _size == v;
                size.DropDownItems.Add(it);
            }
            m.Items.Add(size);

            var skin = new ToolStripMenuItem(Loc.T("menu.skin"));
            var su = new ToolStripMenuItem(Loc.T("skinUmi"), null, delegate { SetSkin(Skin.Umi); });
            su.Checked = _skin == Skin.Umi;
            var sb = new ToolStripMenuItem(Loc.T("skinBlue"), null, delegate { SetSkin(Skin.Blue); });
            sb.Checked = _skin == Skin.Blue;
            skin.DropDownItems.Add(su);
            skin.DropDownItems.Add(sb);
            m.Items.Add(skin);

            var lang = new ToolStripMenuItem(Loc.T("menu.lang"));
            var lz = new ToolStripMenuItem("中文", null, delegate { SetLanguage(Lang.Zh); });
            lz.Checked = _lang == Lang.Zh;
            var le = new ToolStripMenuItem("English", null, delegate { SetLanguage(Lang.En); });
            le.Checked = _lang == Lang.En;
            lang.DropDownItems.Add(lz);
            lang.DropDownItems.Add(le);
            m.Items.Add(lang);

            m.Items.Add(new ToolStripSeparator());
            var top = new ToolStripMenuItem(Loc.T("menu.top"), null, delegate { SetTopMost(!_topMost); });
            top.Checked = _topMost;
            m.Items.Add(top);
            var rem = new ToolStripMenuItem(Loc.T("menu.remind"), null, delegate { SetReminders(!_reminders); });
            rem.Checked = _reminders;
            m.Items.Add(rem);
            var snd = new ToolStripMenuItem(Loc.T("menu.sound"), null, delegate { SetSound(!_soundOn); });
            snd.Checked = _soundOn;
            m.Items.Add(snd);
            m.Items.Add(Loc.T("menu.corner"), null, delegate { MoveHome(); });
            var auto = new ToolStripMenuItem(Loc.T("menu.auto"), null, delegate { if (!ToggleAutostart()) Program.Log("autostart failed"); });
            auto.Checked = Autostart.IsOn;
            m.Items.Add(auto);

            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Loc.T("menu.hide"), null, delegate { Hide(); });
            m.Items.Add(Loc.T("menu.exit"), null, delegate { Close(); });
            m.Show(Cursor.Position);
        }

        private void InitTray()
        {
            _trayIcon = LoadIcon();
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Font = Ui.Menu;
            _trayMenu.Opening += delegate
            {
                _trayMenu.Items.Clear();
                _trayMenu.Items.Add(Loc.T("menu.panel"), null, delegate { ShowPanel(); });
                _trayMenu.Items.Add(Loc.T("menu.card"), null, delegate { ShowCard(); });
                _trayMenu.Items.Add(Loc.T("menu.pat"), null, delegate { Pat(); });
                _trayMenu.Items.Add(new ToolStripSeparator());
                _trayMenu.Items.Add(Loc.T("tray.show"), null, delegate { ToggleVisible(); });
                _trayMenu.Items.Add(Loc.T("menu.exit"), null, delegate { Close(); });
            };
            _tray = new NotifyIcon();
            _tray.Icon = _trayIcon;
            _tray.Text = Loc.T("tray.tip");
            _tray.Visible = true;
            _tray.ContextMenuStrip = _trayMenu;
            _tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ToggleVisible(); };
            _tray.DoubleClick += delegate { ShowPanel(); };
        }

        private Icon LoadIcon()
        {
            string dir = (_skin == Skin.Umi && _umiReady) ? "umi" : "blue";
            string path = Path.Combine(Program.Dir, "assets", dir, "face.ico");
            try { return new Icon(path); }
            catch (Exception)
            {
                try { return Icon.FromHandle(new Bitmap(FacePath).GetHicon()); }
                catch (Exception) { return SystemIcons.Application; }
            }
        }

        private void UpdateTrayIcon()
        {
            if (_tray == null) return;
            try
            {
                var old = _trayIcon;
                _trayIcon = LoadIcon();
                _tray.Icon = _trayIcon;
                if (old != null) old.Dispose();
            }
            catch (Exception) { }
        }

        private void UpdateTrayText()
        {
            if (_tray != null) _tray.Text = Loc.T("tray.tip");
        }

        private void ToggleVisible()
        {
            if (Visible) Hide();
            else { Show(); TopMost = _topMost; Render(); }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _music.Stop();
            SaveConfig();
            if (_cardForm != null && !_cardForm.IsDisposed) _cardForm.Close();
            if (_panel != null && !_panel.IsDisposed) _panel.Close();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            if (_trayIcon != null) { _trayIcon.Dispose(); _trayIcon = null; }
            Native.Unhotkey(Handle, HK_TOGGLE);
            Native.Unhotkey(Handle, HK_CARD);
            Native.Unhotkey(Handle, HK_PANEL);
            base.OnFormClosed(e);
        }

        private sealed class Burst
        {
            public float X, Y, VX, VY, Size, Rot;
            public double Born;
        }
    }
}
