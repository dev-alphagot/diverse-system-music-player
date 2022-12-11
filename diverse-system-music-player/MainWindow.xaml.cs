using ManagedBass;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TagLib;

namespace diverse_system_music_player
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly OpenFileDialog _ofd;

        private int currentSongHandle = 0;

        private BitmapImage albumCover;

        readonly Bitmap kong = Properties.Resources.kong;

        private bool paused = false;

        private static Timer aTimer;

        public double Map(double x, double input_min, double input_max, double output_min, double output_max) {
            return (x - input_min) * (output_max - output_min) / (input_max - input_min) + output_min;
        }

        public BitmapImage ToImage(byte[] array)
        {
            using (var ms = new System.IO.MemoryStream(array))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // here
                image.StreamSource = ms;
                image.EndInit();
                return image;
            }
        }

        public Bitmap MakeGrayscale(Bitmap original)
        {
            //create a blank bitmap the same size as original
            Bitmap newBitmap = new Bitmap(original.Width, original.Height);

            //get a graphics object from the new image
            using (Graphics g = Graphics.FromImage(newBitmap))
            {

                //create the grayscale ColorMatrix
                ColorMatrix colorMatrix = new ColorMatrix(
                   new float[][]
                   {
             new float[] {.3f, .3f, .3f, 0, 0},
             new float[] {.59f, .59f, .59f, 0, 0},
             new float[] {.11f, .11f, .11f, 0, 0},
             new float[] {0, 0, 0, 1, 0},
             new float[] {0, 0, 0, 0, 1}
                   });

                //create some image attributes
                using (ImageAttributes attributes = new ImageAttributes())
                {

                    //set the color matrix attribute
                    attributes.SetColorMatrix(colorMatrix);

                    //draw the original image on the new image
                    //using the grayscale color matrix
                    g.DrawImage(original, new System.Drawing.Rectangle(0, 0, original.Width, original.Height),
                                0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            return newBitmap;
        }

        byte[] ConvertBitmapToByteArray(Bitmap bitmap)
        {
            byte[] result = null;
            if (bitmap != null)
            {
                MemoryStream stream = new MemoryStream();
                bitmap.Save(stream, bitmap.RawFormat);
                result = stream.ToArray();
            }
            else
            {
                Console.WriteLine("Bitmap is null.");
            }
            return result;
        }

        private string StringToUnicode(string value) {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (char c in value)
                    sb.AppendFormat("\\u{0:X4}", (uint)c);
                return sb.ToString();
            }
            catch (Exception e) {
                return e.Message;
            }
        }

        public void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (currentSongHandle != 0) {
                try
                {
                    var ccur = Bass.ChannelBytes2Seconds(currentSongHandle, Bass.ChannelGetPosition(currentSongHandle));
                    var cmax = Bass.ChannelBytes2Seconds(currentSongHandle, Bass.ChannelGetLength(currentSongHandle));

                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                    {
                        if (Application.Current.MainWindow != null) {
                            bg.Margin = new Thickness(0, -Map((ccur / cmax), 0.0, 1.0, 0.0, Application.Current.MainWindow.Width - 540), 0, 0);
                            timespan.Content = $@"{(int)(ccur / 60):######00}:{Math.Floor(ccur % 60):00} / {(int)(cmax / 60):######00}:{Math.Floor(cmax % 60):00}";
                            
                            albumname.Width = (Application.Current.MainWindow.Width / 2) - 230;
                        }
                    }));
                }
                catch (Exception _) { 
                    aTimer.Dispose();
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            composer.Content = "";
            songname.Content = "재생 버튼으로 파일을 재생해주세요";

            albumname.Text = "";
            rlesedate.Content = "";

            Bass.Init();

            _ofd = new OpenFileDialog
            {
                Filter = "All files|*.*"
            };

            albumCover = ToImage(ConvertBitmapToByteArray(kong));

            album.Source = albumCover;
            bg.Source = albumCover;

            aTimer = new Timer(16);
            aTimer.Elapsed += OnTimedEvent;

            aTimer.Enabled = true;
        }

        public void PlayButtonClick(object sender, RoutedEventArgs e) {
            var ov = _ofd.ShowDialog();
            if (!ov.Value)
                return;

            int sample = 16;

            if (currentSongHandle != 0) {
                Bass.ChannelStop(currentSongHandle);
                Bass.SampleFree(currentSongHandle);

                currentSongHandle = 0;
            }

            try
            {
                var tr = TagLib.File.Create(_ofd.FileName).Tag;

                composer.Content = string.Join(", ", tr.Composers);
                songname.Content = (tr.Title == null || tr.Title == "") ? _ofd.FileName.Split('\\').Last() : tr.Title;

                albumname.Text = tr.Album;
                rlesedate.Content = tr.Year != 0 ? tr.Year.ToString() : "";

                albumCover = ToImage((tr.Pictures.Length > 0 ? tr.Pictures[0].Data.Data : ConvertBitmapToByteArray(kong)));
            }
            catch (Exception _)
            {
                composer.Content = "";
                songname.Content = _ofd.FileName.Split('\\').Last();

                albumname.Text = "";
                rlesedate.Content = "";

                albumCover = ToImage((ConvertBitmapToByteArray(kong)));
            }
            finally {
                currentSongHandle = Bass.SampleLoad(_ofd.FileName, 0, 0, 1, BassFlags.Loop);

                Bass.SampleGetChannel(currentSongHandle);

                album.Source = albumCover;
                bg.Source = albumCover;

                Bass.ChannelPlay(currentSongHandle);
            }
        }

        public void PausButtonClick(object sender, RoutedEventArgs e)
        {
            if (!paused && currentSongHandle != 0)
            {
                Bass.ChannelPause(currentSongHandle);
                paused = true;
            }
            else if(paused) {
                Bass.ChannelPlay(currentSongHandle, false);
                paused = false;
            }
        }

        public void StopButtonClick(object sender, RoutedEventArgs e)
        {
            Bass.ChannelStop(currentSongHandle);
        }
    }
}
