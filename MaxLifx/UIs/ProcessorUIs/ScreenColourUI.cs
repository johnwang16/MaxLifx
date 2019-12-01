﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MaxLifx.Processors.ProcessorSettings;
using MaxLifx.Threads;
using MaxLifx.Controllers;
using MaxLifx.Payload;

namespace MaxLifx.UIs
{
    public partial class ScreenColourUI : UiFormBase
    {
        private readonly ScreenColourSettings Settings;
        private MaxLifxBulbController BulbController;
        private Form2 _f;

        public ScreenColourUI(ScreenColourSettings settings, MaxLifxBulbController bulbController)
        {
            InitializeComponent();
            SuspendUI = true;

            Settings = settings;
            BulbController = bulbController;
            SetPositionTextBoxesFromSettings();

            var bulbs = bulbController.Bulbs.Where(x => x.Zones < 2).Select(x => x.Label).ToList();

            foreach (var bulbObj in bulbController.Bulbs.Where(x => x.Zones > 1))
                for (var i = 0; i < bulbObj.Zones; i++)
                    bulbs.Add(bulbObj.Label + $" (Zone {(i + 1).ToString().PadLeft(3, '0')})");

            SetupLabels(lbLabels, bulbs, Settings);
            SuspendUI = false;
        }

        private bool SuspendUI { get; set; }

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private bool _dragging;
        private Point _offset;
        private void Borderless_MouseDown(object sender, MouseEventArgs e)
        {
            _offset.X = e.X;
            _offset.Y = e.Y;
            _dragging = true;
        }

        private void Borderless_MouseUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
        }
        private void Borderless_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                Point currentScreenPos = _f.PointToScreen(e.Location);
                _f.Location = new Point
                    (currentScreenPos.X - _offset.X,
                     currentScreenPos.Y - _offset.Y);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (_f != null) return;
            _f = new Form2();


            _f.MouseDown += Borderless_MouseDown;
            _f.MouseUp += Borderless_MouseUp;
            _f.MouseMove += Borderless_MouseMove;
            _f.Opacity = .5;
            _f.ResizeEnd += F_ResizeEnd;
            _f.Move += F_ResizeEnd;
            _f.FormClosed += F_FormClosed;
            _f.Width = 50;
            _f.Show();
        }

        private void F_FormClosed(object sender, FormClosedEventArgs e)
        {
            _f.Dispose();
            _f = null;
        }

        private void F_ResizeEnd(object sender, EventArgs e)
        {
            //if (MouseButtons == MouseButtons.Left) return;

            SuspendUI = true;

            if(Settings.SelectedLabels.Count == 1)
            {
                var bulbSetting = Settings.BulbSettings.Single(x => x.Label == Settings.SelectedLabels[0]);
                bulbSetting.TopLeft = new Point(_f.Location.X + 102, _f.Location.Y + 38);
                bulbSetting.BottomRight = new Point(_f.Location.X + _f.Size.Width - 6, _f.Location.Y + _f.Size.Height - 6);
                SetPositionTextBoxesFromSettings(bulbSetting);
            }
            
            SuspendUI = false;

            ProcessorBase.SaveSettings(Settings, null);
        }

        private void SetPositionTextBoxesFromSettings(BulbSetting bulbSetting = null)
        {
            if (bulbSetting != null) {
                tlx.Text = bulbSetting.TopLeft.X.ToString();
                tly.Text = bulbSetting.TopLeft.Y.ToString();
            } else
            {
                tlx.Text = "";
                tly.Text = "";
            }
            fade.Text = Settings.Fade.ToString();
            brightness.Text = Settings.Brightness.ToString();
            saturation.Text = Settings.Saturation.ToString();
            delay.Text = Settings.Delay.ToString();
            tbKelvin.Text = Settings.Kelvin.ToString();
            tbMonitor.Text = Settings.Monitor.ToString();

        }

        private void button2_Click(object sender, EventArgs e)
        {
            var f = new AssignAreaToBulbForm(Settings.BulbSettings);
            f.ShowDialog();

            Settings.BulbSettings = f.LabelsAndLocations;
            ProcessorBase.SaveSettings(Settings, null);
        }

        private void pos_TextChanged(object sender, EventArgs e)
        {
            if (SuspendUI) return;

            SuspendUI = true;

            int tlxval = 0,
                tlyval = 0,
                fadeval = 150,
                delayval = 50,
                brightval = 32767,
                satval = 32767,
                minbrightval  = 0,
                minsatval = 0,
                kelvinval = 3500,
                monitor = 0;

            int.TryParse(tlx.Text, out tlxval);
            int.TryParse(tly.Text, out tlyval);
            int.TryParse(fade.Text, out fadeval);
            int.TryParse(delay.Text, out delayval);
            int.TryParse(saturation.Text, out satval);
            int.TryParse(brightness.Text, out brightval);
            int.TryParse(tbSaturationMin.Text, out minsatval);
            int.TryParse(tbBrightnessMin.Text, out minbrightval);
            int.TryParse(tbKelvin.Text, out kelvinval);
            int.TryParse(tbMonitor.Text, out monitor);

            Settings.CentrePoint = new Point(tlxval, tlyval);
            Settings.Fade = Math.Max(fadeval ,0);
            Settings.Delay = Math.Max(delayval, 0);
            Settings.Saturation = Math.Min(satval, 65535);
            Settings.Brightness = Math.Min(brightval, 65535);
            Settings.MinSaturation = Math.Min(Math.Max(minsatval, 0), Settings.Saturation);
            Settings.MinBrightness = Math.Min(Math.Max(minbrightval, 0), Settings.Brightness);
            Settings.Kelvin = Math.Min(Math.Max(kelvinval, 2500),9000);
            Settings.Monitor = monitor;

            SuspendUI = false;
            ProcessorBase.SaveSettings(Settings, null);
        }

        private void lbLabels_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!SuspendUI)
            {
                var selectedLabels = new List<string>();

                foreach (var q in lbLabels.SelectedItems)
                    selectedLabels.Add(q.ToString());

                Settings.SelectedLabels = selectedLabels;

                var bulbSettings = GetBulbSettingsFromSelectedItemLabel();

                checkBox1.Checked = bulbSettings.Enabled;

                tlx.Text = bulbSettings.TopLeft.X.ToString();
                tly.Text = bulbSettings.TopLeft.Y.ToString();

                if ((Settings.CentrePoint.X != 0 || Settings.CentrePoint.Y != 0) && _f != null && _f.IsDisposed == false  )
                {
                    _f.Location = new Point(Settings.CentrePoint.X - (_f.Width/2 + 3), Settings.CentrePoint.Y - (_f.Height / 2 + 1));
                }

            }
        }

        private BulbSetting GetBulbSettingsFromSelectedItemLabel() => Settings.BulbSettings.Single(x => x.Label == Settings.SelectedLabels[0]);

        private void btnMonitor1_Click(object sender, EventArgs e)
        {
        this.GetSizeFromMonitor(((IEnumerable<Screen>) Screen.AllScreens).Single<Screen>((Func<Screen, bool>) (x => x.Primary)));
        }

        private void GetSizeFromMonitor(Screen monitor)
        {
        this.SuspendUI = true;
        ScreenColourSettings settings1 = this.Settings;
        Rectangle bounds1 = monitor.Bounds;
        int x1 = bounds1.X;
        bounds1 = monitor.Bounds;
        int y1 = bounds1.Y;
        Point point1 = new Point(x1, y1);
        settings1.CentrePoint = point1;
        ScreenColourSettings settings2 = this.Settings;
        Rectangle bounds2 = monitor.Bounds;
        int x2 = bounds2.X;
        bounds2 = monitor.Bounds;
        int width = bounds2.Width;
        int x3 = x2 + width;
        bounds2 = monitor.Bounds;
        int y2 = bounds2.Y;
        bounds2 = monitor.Bounds;
        int height = bounds2.Height;
        int y3 = y2 + height;
        Point point2 = new Point(x3, y3);
        this.SetPositionTextBoxesFromSettings();
        this.SuspendUI = false;
        ProcessorBase.SaveSettings<ScreenColourSettings>(this.Settings, (string) null);
        }

        private void button1_Click(object sender, EventArgs e)
        {
        Screen monitor = ((IEnumerable<Screen>) Screen.AllScreens).Where<Screen>((Func<Screen, bool>) (x => !x.Primary)).FirstOrDefault<Screen>();
        if (monitor == null)
        {
            int num = (int) MessageBox.Show("No secondary monitor found...");
        }
        else
            this.GetSizeFromMonitor(monitor);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            Settings.BulbSettings.Single(x => x.Label == Settings.SelectedLabels[0]).Enabled = ((CheckBox)sender).Checked;
        }
    }

    public class CustomPaintTrackBar : TrackBar
    {
        public CustomPaintTrackBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        }

        public event PaintEventHandler PaintOver;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // WM_PAINT
            if (m.Msg == 0x0F)
            {
                using (var lgGraphics = Graphics.FromHwndInternal(m.HWnd))
                    OnPaintOver(new PaintEventArgs(lgGraphics, ClientRectangle));
            }
        }

        protected virtual void OnPaintOver(PaintEventArgs e)
        {
            if (PaintOver != null)
                PaintOver(this, e);

            var newSize = new Size((int) (Size.Width*.9/6), (int) (Size.Height*.15));

            var colors = new Color[7]
            {Color.Red, Color.Yellow, Color.Green, Color.Cyan, Color.Blue, Color.Magenta, Color.Red};

            Brush b;
            for (var i = 0; i < 6; i++)
            {
                var newLocation = Location + new Size((int) (Size.Width*.9/7*(i + .8)), (int) (Size.Height*.8));
                b =
                    new LinearGradientBrush(
                        Location + new Size((int) (Size.Width*.9/7*(i + .8)) - 1, (int) (Size.Height*.8)),
                        new Point((int) (newLocation.X + Size.Width*.9/6), newLocation.Y), colors[i], colors[i + 1]);
                e.Graphics.FillRectangle(b, new Rectangle(newLocation, newSize));
            }
        }
    }
}