using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace DesktopPets
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            App app = new App();
            app.Run();
        }
    }

    internal sealed class App : Application
    {
        private readonly List<PetWindow> pets = new List<PetWindow>();
        private Forms.NotifyIcon trayIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            PetWindow penguin = new PetWindow("Emperor Chick", "emperor_chick", 80, 120);
            PetWindow rabbit = new PetWindow("Yellow Rabbit", "yellow_rabbit", 300, 120);
            pets.Add(penguin);
            pets.Add(rabbit);

            foreach (PetWindow pet in pets)
            {
                pet.PetClosed += OnPetClosed;
                pet.Show();
            }

            CreateTrayIcon();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            base.OnExit(e);
        }

        private void CreateTrayIcon()
        {
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示全部", null, delegate { SetAllVisible(true); });
            menu.Items.Add("隐藏全部", null, delegate { SetAllVisible(false); });
            menu.Items.Add(new Forms.ToolStripSeparator());

            AddPetMenu(menu, pets[0]);
            AddPetMenu(menu, pets[1]);

            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("全部打招呼", null, delegate { PlayAll("waving", 2); });
            menu.Items.Add("全部跳一下", null, delegate { PlayAll("jumping", 1); });
            menu.Items.Add("全部趴下/失败", null, delegate { PlayAll("failed", 1); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Shutdown(); });

            trayIcon = new Forms.NotifyIcon();
            Assembly asm =
                Assembly.GetExecutingAssembly();
            
            Stream iconStream =
                asm.GetManifestResourceStream(
                    "DesktopPets.Assets.icon.ico"
                );
            
            if (iconStream != null)
            {
                trayIcon.Icon =
                    new Drawing.Icon(iconStream);
            }
            else
            {
                trayIcon.Icon =
                    Drawing.SystemIcons.Application;
            }
            trayIcon.Text = "企鹅和兔";
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { SetAllVisible(true); };
        }

        private void AddPetMenu(Forms.ContextMenuStrip menu, PetWindow pet)
        {
            Forms.ToolStripMenuItem item = new Forms.ToolStripMenuItem(pet.DisplayName);
            item.DropDownItems.Add("显示/隐藏", null, delegate { pet.SetVisible(!pet.IsVisible); });
            item.DropDownItems.Add("Idle", null, delegate { pet.PlayManual("idle"); });
            item.DropDownItems.Add("Waiting", null, delegate { pet.PlayManual("waiting"); });
            item.DropDownItems.Add("Waving", null, delegate { pet.PlayManual("waving"); });
            item.DropDownItems.Add("Jumping", null, delegate { pet.PlayManual("jumping"); });
            item.DropDownItems.Add("Running", null, delegate { pet.PlayManual("running"); });
            item.DropDownItems.Add("Review", null, delegate { pet.PlayManual("review"); });
            item.DropDownItems.Add("Failed", null, delegate { pet.PlayManual("failed"); });
            menu.Items.Add(item);
        }

        private void SetAllVisible(bool visible)
        {
            foreach (PetWindow pet in pets)
            {
                pet.SetVisible(visible);
            }
        }

        private void PlayAll(string state, int cycles)
        {
            foreach (PetWindow pet in pets)
            {
                if (!pet.IsClosed)
                {
                    pet.Play(state, cycles);
                }
            }
        }

        private void OnPetClosed(PetWindow closedPet)
        {
            foreach (PetWindow pet in pets)
            {
                if (!pet.IsClosed)
                {
                    return;
                }
            }

            Shutdown();
        }
    }

    internal sealed class PetWindow : Window
    {
        private static readonly Dictionary<string, int> FrameCounts = new Dictionary<string, int>
        {
            { "idle", 6 },
            { "running-right", 8 },
            { "running-left", 8 },
            { "waving", 4 },
            { "jumping", 5 },
            { "failed", 8 },
            { "waiting", 6 },
            { "running", 6 },
            { "review", 6 }
        };

        private readonly Dictionary<string, List<BitmapSource>> frames;
        private readonly Grid root;
        private readonly Image image;
        private readonly Button closeButton;
        private readonly DispatcherTimer animationTimer;
        private readonly DispatcherTimer idleTimer;
        private readonly Random random;

        private string currentState = "idle";
        private int frameIndex;
        private int cyclesRemaining = -1;
        private int idleSeconds;
        private int nextAutoSeconds;
        private bool mouseDown;
        private bool dragging;
        private bool suppressClick;
        private Point dragStartCursor;
        private double dragStartLeft;
        private double dragStartTop;
        private int lastDragDirection;
        private int shakeChanges;
        private double scale = 0.65;

        public string DisplayName { get; private set; }
        public bool IsClosed { get; private set; }
        public event Action<PetWindow> PetClosed;

        public PetWindow(string displayName, string petKey, double left, double top)
        {
            DisplayName = displayName;
            random = new Random(Environment.TickCount ^ displayName.GetHashCode());
            frames = LoadFrames(petKey);

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Left = left;
            Top = top;

            root = new Grid();

            image = new Image();
            image.Stretch = Stretch.Fill;
            image.SnapsToDevicePixels = true;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

            closeButton = new Button();
            
            closeButton.Content = "×";
            
            closeButton.Width = 24;
            closeButton.Height = 24;
            
            closeButton.FontSize = 15;
            closeButton.FontWeight = FontWeights.Bold;
            
            closeButton.Foreground = Brushes.White;
            
            closeButton.Background =
                new SolidColorBrush(
                    Color.FromArgb(210, 25, 25, 25)
                );
            
            closeButton.BorderThickness = new Thickness(0);
            
            closeButton.Cursor = Cursors.Hand;
            
            closeButton.HorizontalAlignment =
                HorizontalAlignment.Right;
            
            closeButton.VerticalAlignment =
                VerticalAlignment.Top;
            
            closeButton.Margin =
                new Thickness(0, 4, 4, 0);
            
            closeButton.Template =
                CreateRoundButtonTemplate();
				
			closeButton.RenderTransformOrigin =
			    new Point(0.5, 0.5);
			
			closeButton.MouseEnter += (s, e) =>
			{
			    closeButton.Background =
			        new SolidColorBrush(
			            Color.FromArgb(230, 220, 60, 60)
			        );
			
			    closeButton.RenderTransform =
			        new ScaleTransform(1.1, 1.1);
			};
			
			closeButton.MouseLeave += (s, e) =>
			{
			    closeButton.Background =
			        new SolidColorBrush(
			            Color.FromArgb(210, 25, 25, 25)
			        );
			
			    closeButton.RenderTransform =
			        new ScaleTransform(1.0, 1.0);
			};
            
            closeButton.Click += OnCloseButtonClick;

            root.Children.Add(image);
            root.Children.Add(closeButton);
            Content = root;

            animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(115);
            animationTimer.Tick += OnAnimationTick;

            idleTimer = new DispatcherTimer();
            idleTimer.Interval = TimeSpan.FromSeconds(1);
            idleTimer.Tick += OnIdleTick;

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseWheel += OnMouseWheel;

            ResetAutoSchedule();
            Play("idle", -1);
            animationTimer.Start();
            idleTimer.Start();
        }

        public void SetVisible(bool visible)
        {
            if (IsClosed)
            {
                return;
            }

            if (visible)
            {
                Show();
                Activate();
            }
            else
            {
                Hide();
            }
        }

        public void PlayManual(string state)
        {
            int cycles = state == "idle" ? -1 : 2;
            Play(state, cycles);
        }

        public void Play(string state, int cycles)
        {
            if (IsClosed)
            {
                return;
            }

            if (!frames.ContainsKey(state))
            {
                return;
            }

            if (currentState == state && cyclesRemaining == cycles)
            {
                return;
            }

            currentState = state;
            cyclesRemaining = cycles;
            frameIndex = 0;
            idleSeconds = 0;
            ShowFrame();
        }

        private Dictionary<string, List<BitmapSource>> LoadFrames(string petKey)
        {
            Dictionary<string, List<BitmapSource>> result = new Dictionary<string, List<BitmapSource>>();
            foreach (KeyValuePair<string, int> pair in FrameCounts)
            {
                List<BitmapSource> list = new List<BitmapSource>();
                string stateKey = pair.Key.Replace("-", "_");
                for (int i = 0; i < pair.Value; i++)
                {
                    string resourceName = "DesktopPets.Assets." + petKey + "." + stateKey + "." + i.ToString("00") + ".png";
                    list.Add(LoadBitmap(resourceName));
                }
                result[pair.Key] = list;
            }
            return result;
        }

        private BitmapSource LoadBitmap(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new InvalidOperationException("Missing embedded resource: " + resourceName);
            }

            using (stream)
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            List<BitmapSource> stateFrames = frames[currentState];
            frameIndex++;

            if (frameIndex >= stateFrames.Count)
            {
                frameIndex = 0;
                if (cyclesRemaining > 0)
                {
                    cyclesRemaining--;
                    if (cyclesRemaining == 0)
                    {
                        Play("idle", -1);
                        return;
                    }
                }
            }

            ShowFrame();
        }

        private void ShowFrame()
        {
            BitmapSource bitmap = frames[currentState][frameIndex];
            image.Source = bitmap;
            Width = Math.Ceiling(bitmap.PixelWidth * scale);
            Height = Math.Ceiling(bitmap.PixelHeight * scale);
            root.Width = Width;
            root.Height = Height;
            image.Width = Width;
            image.Height = Height;
        }

        private void OnIdleTick(object sender, EventArgs e)
        {
            if (mouseDown || currentState == "running-left" || currentState == "running-right")
            {
                return;
            }

            if (currentState != "idle")
            {
                return;
            }

            idleSeconds++;
            if (idleSeconds == 8)
            {
                Play("waiting", 2);
                return;
            }

            if (idleSeconds == 18)
            {
                Play("review", 2);
                return;
            }

            if (idleSeconds >= nextAutoSeconds)
            {
                int roll = random.Next(0, 3);
                if (roll == 0)
                {
                    Play("waving", 2);
                }
                else if (roll == 1)
                {
                    Play("running", 2);
                }
                else
                {
                    Play("jumping", 1);
                }
                ResetAutoSchedule();
            }
        }

        private void ResetAutoSchedule()
        {
            idleSeconds = 0;
            nextAutoSeconds = random.Next(28, 48);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BringIntoView();
            idleSeconds = 0;

            if (e.ClickCount >= 2)
            {
                suppressClick = true;
                Play("waving", 2);
                return;
            }

            mouseDown = true;
            dragging = false;
            suppressClick = false;
            shakeChanges = 0;
            lastDragDirection = 0;
            dragStartCursor = GetCursorPositionInDips();
            dragStartLeft = Left;
            dragStartTop = Top;
            CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!mouseDown)
            {
                return;
            }

            Point now = GetCursorPositionInDips();
            double dx = now.X - dragStartCursor.X;
            double dy = now.Y - dragStartCursor.Y;

            if (Math.Abs(dx) > 4 || Math.Abs(dy) > 4)
            {
                dragging = true;
                Left = dragStartLeft + dx;
                Top = dragStartTop + dy;

                int direction = dx > 1 ? 1 : (dx < -1 ? -1 : 0);
                if (direction != 0)
                {
                    string runState = direction > 0 ? "running-right" : "running-left";
                    Play(runState, -1);

                    if (lastDragDirection != 0 && direction != lastDragDirection)
                    {
                        shakeChanges++;
                    }
                    lastDragDirection = direction;
                }
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (mouseDown)
            {
                ReleaseMouseCapture();
            }

            mouseDown = false;

            if (suppressClick)
            {
                suppressClick = false;
                return;
            }

            if (dragging)
            {
                if (shakeChanges >= 5 || IsNearScreenEdge())
                {
                    Play("failed", 1);
                }
                else
                {
                    Play("idle", -1);
                }
                dragging = false;
            }
            else
            {
                Play("jumping", 1);
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                return;
            }

            if (e.Delta > 0)
            {
                scale += 0.1;
            }
            else
            {
                scale -= 0.1;
            }

            if (scale < 0.35)
            {
                scale = 0.35;
            }
            if (scale > 1.5)
            {
                scale = 1.5;
            }
            ShowFrame();
        }

        private Point GetCursorPositionInDips()
        {
            Point point = new Point(Forms.Cursor.Position.X, Forms.Cursor.Position.Y);
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                point = source.CompositionTarget.TransformFromDevice.Transform(point);
            }
            return point;
        }
		
		private ControlTemplate CreateRoundButtonTemplate()
		{
		    FrameworkElementFactory border =
		        new FrameworkElementFactory(typeof(Border));
		
		    border.SetValue(
		        Border.CornerRadiusProperty,
		        new CornerRadius(999)
		    );
		
		    border.SetBinding(
		        Border.BackgroundProperty,
		        new System.Windows.Data.Binding("Background")
		        {
		            RelativeSource =
		                new System.Windows.Data.RelativeSource(
		                    System.Windows.Data.RelativeSourceMode.TemplatedParent
		                )
		        }
		    );
		
		    FrameworkElementFactory content =
		        new FrameworkElementFactory(typeof(ContentPresenter));
		
		    content.SetValue(
		        ContentPresenter.HorizontalAlignmentProperty,
		        HorizontalAlignment.Center
		    );
		
		    content.SetValue(
		        ContentPresenter.VerticalAlignmentProperty,
		        VerticalAlignment.Center
		    );
		
		    border.AppendChild(content);
		
		    ControlTemplate template =
		        new ControlTemplate(typeof(Button));
		
		    template.VisualTree = border;
		
		    return template;
		}

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            IsClosed = true;
            animationTimer.Stop();
            idleTimer.Stop();
            Hide();
            if (PetClosed != null)
            {
                PetClosed(this);
            }
            Close();
        }

        private bool IsNearScreenEdge()
        {
            Rect area = SystemParameters.WorkArea;
            const double margin = 8.0;
            return Left < area.Left + margin ||
                   Top < area.Top + margin ||
                   Left + Width > area.Right - margin ||
                   Top + Height > area.Bottom - margin;
        }
    }
}
