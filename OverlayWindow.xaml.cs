using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Gma.System.MouseKeyHook;

namespace ScreenTextFinder
{
    public partial class OverlayWindow : Window
    {
        private readonly List<Rect> boxes;
        private readonly List<Rectangle> highlights = new();
        private IKeyboardMouseEvents? globalHook;

        public OverlayWindow(List<Rect> matches)
        {
            InitializeComponent();

            boxes = matches;

            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = 0;
            Top = 0;

            foreach (var r in boxes)
            {
                var rect = new Rectangle
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 3,
                    Width = r.Width,
                    Height = r.Height
                };
                Canvas.SetLeft(rect, r.X);
                Canvas.SetTop(rect, r.Y);
                Root.Children.Add(rect);
                highlights.Add(rect);
            }

            globalHook = Hook.GlobalEvents();
            globalHook.MouseDownExt += GlobalHook_MouseDown;

            Loaded += (_, __) =>
            {
                this.Focus();
                Keyboard.Focus(this);
            };

            Closing += (_, __) => CleanupHook();
        }

        private void OverlayWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                int idx = e.Key - Key.D1;
                if (idx < boxes.Count)
                {
                    MoveMouseTo(boxes[idx]);
                    CleanupHook();
                    this.Close();
                }
            }
            else if (e.Key == Key.Escape)
            {
                CleanupHook();
                this.Close();
            }
        }

        private void GlobalHook_MouseDown(object? sender, MouseEventExtArgs e)
        {
            CleanupHook();
            this.Close();
        }

        private void MoveMouseTo(Rect r)
        {
            int x = (int)(r.X + r.Width / 2);
            int y = (int)(r.Y + r.Height / 2);
            SetCursorPos(x, y);
        }

        private void CleanupHook()
        {
            if (globalHook != null)
            {
                globalHook.MouseDownExt -= GlobalHook_MouseDown;
                globalHook.Dispose();
                globalHook = null;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);
    }
}
