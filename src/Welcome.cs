using System;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

static class P
{
    [STAThread]
    static void Main(string[] args)
    {
        bool created;
        System.Threading.Mutex m = new System.Threading.Mutex(true, "TarkovWelcomeMutex", out created);
        if (!created) return;
        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        bool fpsTest = Array.IndexOf(args, "--fps") >= 0;
        Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(240));
        new Application().Run(new W(dir, fpsTest));
        GC.KeepAlive(m);
    }
}

class W : Window
{
    MediaPlayer click = new MediaPlayer(), over = new MediaPlayer();
    Image bg, glow; TranslateTransform tr = new TranslateTransform();
    ScaleTransform bs = new ScaleTransform(1, 1), zoom = new ScaleTransform(1.07, 1.07);
    Grid btn; Border shade, flash; bool hover, pressed, leaving;
    double ox, oy; int frames; DateTime t0 = DateTime.Now, t1 = DateTime.Now;
    bool fpsTest;
    string label = "ВОЙТИ", who = "ВЛАСТИТЕЛЬ МИРОВ";

    BitmapImage Load(string dir, string f)
    {
        BitmapImage b = new BitmapImage();
        b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(Path.Combine(dir, f)); b.EndInit(); b.Freeze();
        return b;
    }

    public W(string dir, bool test)
    {
        fpsTest = test;
        click.Open(new Uri(Path.Combine(dir, "button_click.wav"))); click.Volume = 0.8;
        over.Open(new Uri(Path.Combine(dir, "button_over.wav"))); over.Volume = 0.8;
        string cfg = Path.Combine(dir, "label.txt");
        if (File.Exists(cfg)) label = File.ReadAllText(cfg, System.Text.Encoding.UTF8).Trim();
        string nm = Path.Combine(dir, "name.txt");
        if (File.Exists(nm)) who = File.ReadAllText(nm, System.Text.Encoding.UTF8).Trim();

        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0; Top = 0; Width = SystemParameters.PrimaryScreenWidth; Height = SystemParameters.PrimaryScreenHeight;
        Background = Brushes.Black;

        Grid root = new Grid { ClipToBounds = true };
        bg = new Image { Source = Load(dir, "forest.png"), Stretch = Stretch.UniformToFill, RenderTransform = tr, RenderTransformOrigin = new Point(0.5, 0.5) };
        RenderOptions.SetBitmapScalingMode(bg, BitmapScalingMode.HighQuality);
        TransformGroup tg = new TransformGroup(); tg.Children.Add(zoom); tg.Children.Add(tr); bg.RenderTransform = tg;
        bg.CacheMode = new BitmapCache();
        bg.Width = Width; bg.Height = Height; bg.HorizontalAlignment = HorizontalAlignment.Center; bg.VerticalAlignment = VerticalAlignment.Center;
        root.Children.Add(bg);

        double bw = Width * 0.22, bh = bw * 85.0 / 476.0;
        btn = new Grid { Width = bw, Height = bh, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, Height * 0.72, 0, 0), Cursor = Cursors.Hand };
        Image bi = new Image { Source = Load(dir, "button_back_big.png"), Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(bi, BitmapScalingMode.HighQuality);
        btn.Children.Add(bi);
        btn.RenderTransform = bs; btn.RenderTransformOrigin = new Point(0.5, 0.5);
        glow = new Image { Source = Load(dir, "glow.png"), Stretch = Stretch.Fill, Width = bw * 1.5, Height = bh * 3.2, Opacity = 0, IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, Height * 0.72 - bh * 1.1, 0, 0) };
        root.Children.Add(glow);
        shade = new Border { Background = Brushes.Black, Opacity = 0.08, IsHitTestVisible = false };
        btn.Children.Add(shade);
        btn.Children.Add(new TextBlock { Text = label, FontFamily = new FontFamily("Bahnschrift"), FontWeight = FontWeights.Bold, FontSize = bh * 0.4,
            Foreground = new SolidColorBrush(Color.FromRgb(28, 28, 24)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
        root.Children.Add(btn);

        root.Children.Add(new TextBlock { Text = DateTime.Now.ToString("HH:mm") + "  ·  " + who, FontFamily = new FontFamily("Bahnschrift"), FontSize = Height * 0.018,
            Foreground = new SolidColorBrush(Color.FromArgb(190, 220, 215, 190)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, Height * 0.04) });
        flash = new Border { Background = new SolidColorBrush(Color.FromRgb(235, 230, 205)), Opacity = 0, IsHitTestVisible = false };
        root.Children.Add(flash);
        Content = root;

        btn.MouseEnter += delegate { if (leaving) return; hover = true; Hover(true); Play(over); };
        btn.MouseLeave += delegate { if (leaving) return; hover = false; pressed = false; Hover(false); };
        btn.MouseLeftButtonDown += delegate { if (leaving) return; pressed = true; Press(); Play(click); };
        btn.MouseLeftButtonUp += delegate { if (pressed && !leaving) { pressed = false; Release(); } };
        KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); };

        Opacity = 0;
        Loaded += delegate {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)));
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--anim")
            {
                System.Windows.Threading.DispatcherTimer dt = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                int step = 0;
                dt.Tick += delegate { step++; if (step == 1) { Hover(true); Play(over); } else if (step == 2) { Press(); Play(click); } else { dt.Stop(); Release(); } };
                dt.Start();
            }
        };
        CompositionTarget.Rendering += OnFrame;
    }

    static void Anim(DependencyObject o, DependencyProperty p, double to, int ms, IEasingFunction e)
    {
        DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)); a.EasingFunction = e;
        ((IAnimatable)o).BeginAnimation(p, a, HandoffBehavior.SnapshotAndReplace);
    }
    static IEasingFunction Out() { return new QuadraticEase { EasingMode = EasingMode.EaseOut }; }

    void Scale(double v, int ms, IEasingFunction e) { Anim(bs, ScaleTransform.ScaleXProperty, v, ms, e); Anim(bs, ScaleTransform.ScaleYProperty, v, ms, e); }

    void Hover(bool on)
    {
        shade.Background = on ? Brushes.White : Brushes.Black;
        Anim(shade, OpacityProperty, on ? 0.16 : 0.08, 150, Out());
        Anim(glow, OpacityProperty, on ? 0.55 : 0, 200, Out());
        Scale(on ? 1.04 : 1.0, 180, Out());
    }

    void Press()
    {
        shade.Background = Brushes.Black;
        Anim(shade, OpacityProperty, 0.3, 60, Out());
        Scale(0.92, 70, Out());
    }

    void Release()
    {
        leaving = true;
        Anim(shade, OpacityProperty, 0, 120, Out());
        // отскок кнопки
        Scale(1.1, 160, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 });
        Anim(glow, OpacityProperty, 1, 120, Out());
        // вспышка и наезд камеры, затем затухание и выход
        DoubleAnimation f = new DoubleAnimation(0, 0.35, TimeSpan.FromMilliseconds(110)); f.AutoReverse = true;
        flash.BeginAnimation(OpacityProperty, f);
        Anim(zoom, ScaleTransform.ScaleXProperty, 1.25, 900, new CubicEase { EasingMode = EasingMode.EaseIn });
        Anim(zoom, ScaleTransform.ScaleYProperty, 1.25, 900, new CubicEase { EasingMode = EasingMode.EaseIn });
        DoubleAnimation fo = new DoubleAnimation(0, TimeSpan.FromMilliseconds(550)); fo.BeginTime = TimeSpan.FromMilliseconds(300);
        fo.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        fo.Completed += delegate { Close(); };
        BeginAnimation(OpacityProperty, fo, HandoffBehavior.SnapshotAndReplace);
    }

    static void Play(MediaPlayer m) { m.Stop(); m.Position = TimeSpan.Zero; m.Play(); }

    void OnFrame(object s, EventArgs e)
    {
        if ((DateTime.Now - t0).TotalMilliseconds < 1500) { frames = 0; t1 = DateTime.Now; } else frames++;
        Point p = Mouse.GetPosition(this);
        double tx = -((p.X - Width / 2) / (Width / 2)) * 28, ty = -((p.Y - Height / 2) / (Height / 2)) * 18;
        ox += (tx - ox) * 0.1; oy += (ty - oy) * 0.1;
        tr.X = ox; tr.Y = oy;
        if (fpsTest && (DateTime.Now - t0).TotalMilliseconds > 3000)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "tarkov_fps.txt"), (frames * 1000.0 / (DateTime.Now - t1).TotalMilliseconds).ToString("F0") + " fps, tier " + (RenderCapability.Tier >> 16));
            Close();
        }
    }
}
