// FurryPet v2 — control panel, card panel and card viewer.
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
    internal sealed class PanelForm : Form
    {
        private readonly PetForm _pet;
        private Bitmap _face;
        private string _facePath;
        private readonly Timer _timer;
        private readonly List<Hit> _hits = new List<Hit>();
        private Hit _hover;
        private bool _dragging;
        private Point _dragOff;
        private readonly Rectangle _close = new Rectangle(706, 30, 30, 30);
        private bool _closeHot;

        public PanelForm(PetForm pet)
        {
            _pet = pet;
            _facePath = pet.FacePath;
            using (var b = new Bitmap(_facePath)) _face = new Bitmap(b);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Size = new Size(760, 560);
            BackColor = Ui.Page;
            KeyPreview = true;

            BuildHits();
            _timer = new Timer();
            _timer.Interval = 120;
            _timer.Tick += delegate { Invalidate(); };
            _timer.Start();

            Load += delegate
            {
                try
                {
                    Region = new Region(Ui.Round(new RectangleF(0, 0, Width, Height), 22));
                }
                catch (Exception) { }
            };
        }

        private void BuildHits()
        {
            _hits.Add(new Hit(new RectangleF(34, 402, 96, 36), delegate { _pet.Pat(); }));
            _hits.Add(new Hit(new RectangleF(138, 402, 96, 36), delegate { _pet.RunDemo(); }));
            _hits.Add(new Hit(new RectangleF(242, 402, 96, 36), delegate { _pet.ShowCard(); }));

            _hits.Add(new Hit(new RectangleF(452, 104, 88, 32), delegate { _pet.SetSkin(Skin.Umi); }));
            _hits.Add(new Hit(new RectangleF(546, 104, 88, 32), delegate { _pet.SetSkin(Skin.Blue); }));

            _hits.Add(new Hit(new RectangleF(452, 148, 88, 32), delegate { _pet.SetLanguage(Lang.Zh); }));
            _hits.Add(new Hit(new RectangleF(546, 148, 88, 32), delegate { _pet.SetLanguage(Lang.En); }));

            int[] sizes = new int[] { 160, 190, 220, 260 };
            for (int i = 0; i < sizes.Length; i++)
            {
                int v = sizes[i];
                _hits.Add(new Hit(new RectangleF(452 + i * 62, 192, 54, 32), delegate { _pet.SetSize(v); }));
            }

            _hits.Add(new Hit(new RectangleF(584, 276, 132, 30), delegate { _pet.SetTopMost(!_pet.IsTopMost); }));
            _hits.Add(new Hit(new RectangleF(584, 314, 132, 30), delegate { _pet.SetSound(!_pet.SoundOn); }));
            _hits.Add(new Hit(new RectangleF(584, 352, 132, 30), delegate { _pet.SetReminders(!_pet.RemindersOn); }));
            _hits.Add(new Hit(new RectangleF(584, 390, 132, 30), delegate { _pet.ToggleAutostart(); }));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            var full = new RectangleF(0, 0, Width, Height);
            using (var p = Ui.Round(full, 22))
            {
                using (var b = new LinearGradientBrush(full, Ui.PanelTop, Ui.PanelBottom, 90f)) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(255, 52, 78, 106), 1f)) g.DrawPath(pen, p);
            }
            for (int i = 0; i < 5; i++)
            {
                float x = new float[] { 726, 620, 700, 58, 360 }[i];
                float y = new float[] { 500, 470, 524, 470, 544 }[i];
                Ui.Star(g, x, y, new float[] { 5, 4, 3, 4, 3 }[i], i * 18, Color.FromArgb(40 + i * 8, 230, 198, 142));
            }

            if (_facePath != _pet.FacePath)
            {
                try
                {
                    _face.Dispose();
                    _facePath = _pet.FacePath;
                    using (var b = new Bitmap(_facePath)) _face = new Bitmap(b);
                }
                catch (Exception) { }
            }

            // header
            var av = new Rectangle(34, 26, 56, 56);
            using (var clip = new GraphicsPath()) { clip.AddEllipse(av); var old = g.Clip; g.SetClip(clip); g.DrawImage(_face, av); g.Clip = old; }
            using (var pen = new Pen(Color.FromArgb(220, Ui.Accent), 1.8f)) g.DrawEllipse(pen, av);

            Ui.Label(g, Loc.T(_pet.CurrentSkin == Skin.Umi ? "pet" : "petBlue"), Ui.Title, Ui.Text, 106, 20);
            string sub = _pet.MoodLabel;
            float sw = g.MeasureString(sub, Ui.Small).Width;
            var pill = new RectangleF(106, 58, sw + 28, 26);
            Ui.Pill(g, pill, Color.FromArgb(255, 34, 51, 73), Color.FromArgb(140, Ui.Accent));
            using (var b = new SolidBrush(Ui.Mint)) g.FillEllipse(b, 116, 68, 6, 6);
            Ui.Label(g, sub, Ui.Small, Ui.Muted, 128, 61.5f);

            // language switch
            DrawLangSwitch(g);
            DrawClose(g);

            // preview
            var prev = new RectangleF(34, 104, 310, 236);
            Ui.Fill(g, prev, 16, Color.FromArgb(255, 20, 30, 45));
            Ui.Stroke(g, prev, 16, Color.FromArgb(90, Ui.Accent), 1.2f);
            var frame = _pet.PreviewFrame();
            if (frame != null)
            {
                float fh = 196, fw = frame.Width * fh / frame.Height;
                if (fw > 270) { fw = 270; fh = frame.Height * fw / frame.Width; }
                float fx = prev.X + (prev.Width - fw) / 2f;
                float fy = prev.Y + (prev.Height - fh) / 2f + 6;
                var glow = new RectangleF(fx - 40, fy + fh - 120, fw + 80, 140);
                using (var b = new SolidBrush(Color.FromArgb(26, 80, 200, 255))) g.FillEllipse(b, glow);
                g.DrawImage(frame, new Rectangle((int)fx, (int)fy, (int)fw, (int)fh), 0, 0, frame.Width, frame.Height, GraphicsUnit.Pixel);
            }

            var ts = _pet.Together;
            string tstr = ts.TotalHours >= 1
                ? string.Format("{0:0} 小时 {1:0} 分", ts.TotalHours, ts.Minutes)
                : string.Format("{0:0} 分钟", ts.TotalMinutes);
            if (Loc.Current == Lang.En)
                tstr = ts.TotalHours >= 1 ? string.Format("{0:0} h {1:0} m", ts.TotalHours, ts.Minutes) : string.Format("{0:0} min", ts.TotalMinutes);
            Ui.Label(g, Loc.T("together") + " " + tstr, Ui.Small, Ui.Muted, 36, 350);
            string pokeStr;
            if (Loc.Current == Lang.Zh) pokeStr = Loc.T("pokes") + " " + _pet.PokeCount + " 次";
            else pokeStr = _pet.PokeCount == 1 ? "Poked 1 time" : "Poked " + _pet.PokeCount + " times";
            Ui.Label(g, pokeStr, Ui.Small, Ui.Muted, 36, 370);

            DrawActionButton(g, new RectangleF(34, 402, 96, 36), Loc.T("pat"), false);
            DrawActionButton(g, new RectangleF(138, 402, 96, 36), Loc.T("demo"), false);
            DrawActionButton(g, new RectangleF(242, 402, 96, 36), Loc.T("cardBtn"), false);

            // right column
            float rx = 372;
            Section(g, rx, 104, Loc.T("skin"));
            DrawOption(g, new RectangleF(452, 104, 88, 32), Loc.T("skinUmi"), _pet.CurrentSkin == Skin.Umi);
            DrawOption(g, new RectangleF(546, 104, 88, 32), Loc.T("skinBlue"), _pet.CurrentSkin == Skin.Blue);

            Section(g, rx, 148, Loc.T("language"));
            DrawOption(g, new RectangleF(452, 148, 88, 32), Loc.T("langZh"), _pet.CurrentLang == Lang.Zh);
            DrawOption(g, new RectangleF(546, 148, 88, 32), "English", _pet.CurrentLang == Lang.En);

            Section(g, rx, 192, Loc.T("size"));
            string[] sn = new string[] { Loc.T("sizeS"), Loc.T("sizeM"), Loc.T("sizeL"), Loc.T("sizeXL") };
            int[] sv = new int[] { 160, 190, 220, 260 };
            for (int i = 0; i < 4; i++)
                DrawOption(g, new RectangleF(452 + i * 62, 192, 54, 32), sn[i], _pet.CurrentSize == sv[i]);

            Section(g, rx, 236, Loc.T("options"));
            DrawToggleRow(g, 276, Loc.T("top"), _pet.IsTopMost);
            DrawToggleRow(g, 314, Loc.T("sound"), _pet.SoundOn);
            DrawToggleRow(g, 352, Loc.T("reminders"), _pet.RemindersOn);
            DrawToggleRow(g, 390, Loc.T("autostart"), Autostart.IsOn);

            Section(g, rx, 436, Loc.T("info"));
            Ui.Label(g, Ui.Clip(g, _pet.MusicLine, Ui.Small, 344), Ui.Small, Ui.Muted, rx, 466);
            Ui.Label(g, DateTime.Now.ToString("yyyy-MM-dd  HH:mm"), Ui.Small, Color.FromArgb(130, 150, 175), rx, 486);

            using (var b = new SolidBrush(Color.FromArgb(120, 110, 140, 172)))
                g.DrawString(Loc.T("hint"), Ui.Small, b, 34, 522);
            using (var b = new SolidBrush(Color.FromArgb(110, 100, 130, 160)))
                g.DrawString("v2.0", Ui.Small, b, 704, 522);
        }

        private void DrawLangSwitch(Graphics g)
        {
            var r = new RectangleF(636, 30, 60, 30);
            Ui.Fill(g, r, 15, Color.FromArgb(255, 34, 51, 73));
            Ui.Stroke(g, r, 15, Color.FromArgb(110, Ui.Accent), 1f);
            bool zh = _pet.CurrentLang == Lang.Zh;
            var sel = new RectangleF(zh ? r.X + 3 : r.X + r.Width / 2, r.Y + 3, r.Width / 2 - 3, r.Height - 6);
            Ui.Fill(g, sel, 12, Color.FromArgb(200, Ui.Accent));
            Ui.TextCenter(g, "中", Ui.SmallB, zh ? Ui.Page : Ui.Muted, new RectangleF(r.X, r.Y, r.Width / 2, r.Height));
            Ui.TextCenter(g, "EN", Ui.SmallB, zh ? Ui.Muted : Ui.Page, new RectangleF(r.X + r.Width / 2, r.Y, r.Width / 2, r.Height));
        }

        private void DrawClose(Graphics g)
        {
            using (var b = new SolidBrush(_closeHot ? Color.FromArgb(255, 58, 84, 116) : Color.FromArgb(255, 34, 51, 73)))
                g.FillEllipse(b, _close);
            using (var pen = new Pen(Color.FromArgb(235, 175, 198, 223), 1.7f))
            {
                g.DrawLine(pen, _close.X + 10, _close.Y + 10, _close.Right - 10, _close.Bottom - 10);
                g.DrawLine(pen, _close.Right - 10, _close.Y + 10, _close.X + 10, _close.Bottom - 10);
            }
        }

        private void Section(Graphics g, float x, float y, string text)
        {
            Ui.Label(g, text, Ui.SmallB, Color.FromArgb(150, Ui.Muted), x, y + 7);
        }

        private void DrawActionButton(Graphics g, RectangleF r, string text, bool selected)
        {
            bool hot = _hover != null && _hover.Rect == r;
            Color fill = hot ? Color.FromArgb(255, 44, 68, 96) : Color.FromArgb(255, 32, 47, 68);
            Ui.Pill(g, r, fill, Color.FromArgb(120, Ui.Accent));
            Ui.TextCenter(g, text, Ui.Body, Ui.Text, r);
        }

        private void DrawOption(Graphics g, RectangleF r, string text, bool selected)
        {
            bool hot = _hover != null && _hover.Rect == r;
            Ui.Pill(g, r, selected ? Color.FromArgb(220, Ui.Accent) : (hot ? Color.FromArgb(255, 40, 58, 82) : Color.FromArgb(255, 30, 44, 64)),
                selected ? Color.FromArgb(255, Ui.Accent) : Color.FromArgb(100, Ui.Accent));
            Ui.TextCenter(g, text, Ui.SmallB, selected ? Ui.Page : Ui.Muted, r);
        }

        private void DrawToggleRow(Graphics g, float y, string label, bool on)
        {
            Ui.Label(g, label, Ui.Body, Ui.Text, 372, y + 5);
            var box = new RectangleF(640, y, 76, 30);
            Ui.Pill(g, box, on ? Color.FromArgb(220, Ui.Mint) : Color.FromArgb(255, 34, 48, 66), Color.FromArgb(110, Ui.Accent));
            Ui.TextCenter(g, on ? Loc.T("on") : Loc.T("off"), Ui.SmallB, on ? Ui.Page : Ui.Muted, box);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                var sc = PointToScreen(e.Location);
                Location = new Point(sc.X - _dragOff.X, sc.Y - _dragOff.Y);
                return;
            }
            Hit hit = null;
            foreach (var h in _hits) if (h.Rect.Contains(e.Location)) { hit = h; break; }
            bool ch = _close.Contains(e.Location);
            if (hit != _hover || ch != _closeHot)
            {
                _hover = hit;
                _closeHot = ch;
                Cursor = (hit != null || ch) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = null;
            _closeHot = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                bool langHit = e.X > 636 && e.X < 696 && e.Y > 30 && e.Y < 60;
                if (e.Y < 96 && !_close.Contains(e.Location) && !langHit)
                {
                    _dragging = true;
                    var sc = PointToScreen(e.Location);
                    _dragOff = new Point(sc.X - Location.X, sc.Y - Location.Y);
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging) { _dragging = false; base.OnMouseUp(e); return; }
            if (e.Button == MouseButtons.Left)
            {
                if (_close.Contains(e.Location)) { Close(); return; }
                if (e.X > 636 && e.X < 696 && e.Y > 30 && e.Y < 60)
                {
                    _pet.SetLanguage(_pet.CurrentLang == Lang.Zh ? Lang.En : Lang.Zh);
                    Invalidate();
                    return;
                }
                foreach (var h in _hits)
                {
                    if (h.Rect.Contains(e.Location))
                    {
                        h.Act();
                        Invalidate();
                        return;
                    }
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _face.Dispose();
            base.OnFormClosed(e);
        }

        private sealed class Hit
        {
            public readonly RectangleF Rect;
            public readonly Action Act;
            public Hit(RectangleF r, Action a) { Rect = r; Act = a; }
        }
    }

    internal sealed class CardForm : Form
    {
        private readonly PetForm _pet;
        private readonly Bitmap _card;
        private Bitmap _face;
        private string _facePath;
        private readonly Rectangle _img = new Rectangle(38, 104, 628, 471);
        private readonly Rectangle _close = new Rectangle(632, 30, 34, 34);
        private bool _closeHot, _closeDown, _imgHot;
        private bool _dragging;
        private Point _dragOff;
        private CardViewer _viewer;

        public CardForm(PetForm pet)
        {
            _pet = pet;
            using (var b = new Bitmap(Path.Combine(Program.Dir, "assets", "card.png"))) _card = new Bitmap(b);
            _facePath = pet.FacePath;
            using (var b = new Bitmap(_facePath)) _face = new Bitmap(b);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Size = new Size(704, 612);
            BackColor = Ui.Page;
            KeyPreview = true;
            Font = Ui.Body;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (var p = Ui.Round(new RectangleF(0, 0, Width, Height), 22)) Region = new Region(p);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            var full = new RectangleF(0, 0, Width, Height);
            using (var p = Ui.Round(full, 22))
            {
                using (var b = new LinearGradientBrush(full, Ui.PanelTop, Ui.PanelBottom, 90f)) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(255, 52, 78, 106), 1f)) g.DrawPath(pen, p);
            }

            var av = new Rectangle(38, 28, 44, 44);
            using (var clip = new GraphicsPath()) { clip.AddEllipse(av); var old = g.Clip; g.SetClip(clip); g.DrawImage(_face, av); g.Clip = old; }
            using (var pen = new Pen(Color.FromArgb(210, Ui.Accent), 1.6f)) g.DrawEllipse(pen, av);

            Ui.Label(g, Loc.T("card"), Ui.Title, Ui.Text, 96, 24);
            Ui.Label(g, Loc.T("cardSub"), Ui.Body, Ui.Muted, 98, 62);

            using (var b = new SolidBrush(_closeHot ? Color.FromArgb(255, 46, 71, 101) : Color.FromArgb(255, 34, 51, 73)))
                g.FillEllipse(b, _close);
            using (var pen = new Pen(Color.FromArgb(235, 175, 198, 223), 1.8f))
            {
                g.DrawLine(pen, _close.X + 12, _close.Y + 12, _close.Right - 12, _close.Bottom - 12);
                g.DrawLine(pen, _close.Right - 12, _close.Y + 12, _close.X + 12, _close.Bottom - 12);
            }

            for (int i = 4; i >= 1; i--)
            {
                var sr = new RectangleF(_img.X - i * 2, _img.Y + 6 + i * 2, _img.Width + i * 4, _img.Height + i * 4);
                using (var p = Ui.Round(sr, 14))
                using (var b = new SolidBrush(Color.FromArgb(12, 0, 0, 0)))
                    g.FillPath(b, p);
            }

            var oldClip = g.Clip;
            using (var p = Ui.Round(_img, 14))
            {
                g.SetClip(p, CombineMode.Intersect);
                g.DrawImage(_card, _img);
                g.Clip = oldClip;
                using (var pen = new Pen(Color.FromArgb(_imgHot ? 235 : 170, Ui.Accent), _imgHot ? 2.2f : 1.4f))
                    g.DrawPath(pen, p);
            }

            using (var b = new SolidBrush(Color.FromArgb(255, 128, 145, 168)))
                g.DrawString(Loc.T("cardZoom"), Ui.Small, b, 38, 588);
            using (var b = new SolidBrush(Color.FromArgb(150, 110, 140, 172)))
                g.DrawString(Loc.T("cardSize"), Ui.Small, b, 520, 588);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool ch = _close.Contains(e.Location);
            bool ih = _img.Contains(e.Location);
            if (ch != _closeHot || ih != _imgHot)
            {
                _closeHot = ch; _imgHot = ih;
                Cursor = (ch || ih) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            if (_dragging)
            {
                var sc = PointToScreen(e.Location);
                Location = new Point(sc.X - _dragOff.X, sc.Y - _dragOff.Y);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_close.Contains(e.Location)) _closeDown = true;
                else if (e.Y < 92)
                {
                    _dragging = true;
                    var sc = PointToScreen(e.Location);
                    _dragOff = new Point(sc.X - Location.X, sc.Y - Location.Y);
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_closeDown)
            {
                _closeDown = false;
                if (_close.Contains(e.Location)) { Close(); return; }
            }
            if (_dragging) { _dragging = false; }
            else if (e.Button == MouseButtons.Left && _img.Contains(e.Location)) OpenViewer();
            base.OnMouseUp(e);
        }

        private void OpenViewer()
        {
            if (_viewer != null && !_viewer.IsDisposed) { _viewer.Activate(); return; }
            _viewer = new CardViewer(_card);
            _viewer.FormClosed += delegate { _viewer = null; };
            _viewer.Show();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_viewer != null && !_viewer.IsDisposed) _viewer.Close();
            _card.Dispose();
            _face.Dispose();
            base.OnFormClosed(e);
        }
    }

    internal sealed class CardViewer : Form
    {
        private readonly Bitmap _card;

        public CardViewer(Bitmap card)
        {
            _card = card;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.FromArgb(9, 14, 23);
            KeyPreview = true;
            Cursor = Cursors.Hand;
            var wa = Screen.PrimaryScreen.WorkingArea;
            int w = Math.Min(960, wa.Width - 40);
            int h = (int)(w * 0.75f) + 34;
            if (h > wa.Height - 20) { h = wa.Height - 20; w = (int)((h - 34) / 0.75f); }
            Size = new Size(w, h);
            Location = new Point(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            var r = new Rectangle(16, 10, Width - 32, Height - 40);
            using (var p = Ui.Round(r, 10))
            {
                var old = g.Clip;
                g.SetClip(p);
                g.DrawImage(_card, r);
                g.Clip = old;
                using (var pen = new Pen(Color.FromArgb(120, Ui.Accent), 1f)) g.DrawPath(pen, p);
            }
            using (var b = new SolidBrush(Color.FromArgb(220, 160, 185, 210)))
            using (var sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                g.DrawString(Loc.T("zoomHint"), Ui.Small, b, new RectangleF(0, Height - 28, Width, 22), sf);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e) { Close(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); base.OnKeyDown(e); }
    }
}
