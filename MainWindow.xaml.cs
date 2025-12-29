using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ScreenTextFinder
{
    public partial class MainWindow : Window
    {
        private IKeyboardMouseEvents? globalHook;

        public MainWindow()
        {
            InitializeComponent();

            globalHook = Hook.GlobalEvents();
            globalHook.KeyDown += GlobalHook_KeyDown;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
                Top = 0;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (globalHook != null)
            {
                globalHook.KeyDown -= GlobalHook_KeyDown;
                globalHook.Dispose();
                globalHook = null;
            }
            base.OnClosed(e);
        }


        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }


        private void GlobalHook_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == System.Windows.Forms.Keys.Oem5)
            {
                Dispatcher.Invoke(() =>
                {
                    if (WindowState == WindowState.Minimized)
                        WindowState = WindowState.Normal;

                    Topmost = true;
                    Activate();
                    Topmost = false;

                    KeywordBox.Focus();
                    KeywordBox.SelectAll();
                });
            }
            if (e.KeyCode == System.Windows.Forms.Keys.D0)
            {
                Dispatcher.Invoke(() =>
                {
                    GetCursorPos(out POINT p);
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)p.X, (uint)p.Y, 0, UIntPtr.Zero);
                });
            }
        }


        private void KeywordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Scan_Click(sender, new RoutedEventArgs());
                Keyboard.ClearFocus();
            }
        }

        private async void Scan_Click(object sender, RoutedEventArgs e)
        {
            var keywords = KeywordBox.Text
                .Split(',')
                .Select(k => k.Trim().ToLower())
                .Where(k => k.Length > 0)
                .ToList();

            if (keywords.Count == 0)
                return;

            Bitmap raw = CaptureScreen();
            Bitmap processed = PreprocessForOcr(raw);

            var words = await RunOcr(processed);

            raw.Dispose();
            processed.Dispose();

            var matches = words
                .Where(w => keywords.Any(k => w.Text.ToLower().Contains(k)))
                .Select(w => w.Box)
                .OrderBy(r => r.Top)
                .ToList();

            if (matches.Count == 0)
            {
                MessageBox.Show("No matches found.");
                return;
            }

            KeywordBox.Clear();
            Keyboard.ClearFocus();

            var overlay = new OverlayWindow(matches)
            {
                Owner = this
            };
            overlay.Show();
        }


        private Bitmap CaptureScreen()
        {
            int width = (int)SystemParameters.PrimaryScreenWidth;
            int height = (int)SystemParameters.PrimaryScreenHeight;

            Bitmap bmp = new Bitmap(width, height);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
            }

            return bmp;
        }

        private Bitmap PreprocessForOcr(Bitmap original)
        {
            Bitmap processed = new Bitmap(original.Width, original.Height);

            float contrast = 1.35f;
            float t = 0.5f * (1.0f - contrast);

            using (Graphics g = Graphics.FromImage(processed))
            {
                var matrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                {
                    new float[] {0.299f * contrast, 0.299f * contrast, 0.299f * contrast, 0, 0},
                    new float[] {0.587f * contrast, 0.587f * contrast, 0.587f * contrast, 0, 0},
                    new float[] {0.114f * contrast, 0.114f * contrast, 0.114f * contrast, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {t, t, t, 0, 1}
                });

                using var attributes = new System.Drawing.Imaging.ImageAttributes();
                attributes.SetColorMatrix(matrix);

                g.DrawImage(
                    original,
                    new Rectangle(0, 0, original.Width, original.Height),
                    0, 0, original.Width, original.Height,
                    GraphicsUnit.Pixel,
                    attributes
                );
            }

            return processed;
        }

        private async Task<List<OcrWord>> RunOcr(Bitmap bmp)
        {
            var list = new List<OcrWord>();

            using var stream = new InMemoryRandomAccessStream();
            bmp.Save(stream.AsStreamForWrite(), System.Drawing.Imaging.ImageFormat.Bmp);
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            var result = await engine.RecognizeAsync(softwareBitmap);

            foreach (var line in result.Lines)
            {
                foreach (var word in line.Words)
                {
                    list.Add(new OcrWord
                    {
                        Text = word.Text ?? string.Empty,
                        Box = new Rect(
                            word.BoundingRect.X,
                            word.BoundingRect.Y,
                            word.BoundingRect.Width,
                            word.BoundingRect.Height)
                    });
                }
            }

            return list;
        }
    }

    public class OcrWord
    {
        public string Text { get; set; } = string.Empty;
        public Rect Box { get; set; }
    }
}
