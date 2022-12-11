using ManagedBass;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
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

        private IEnumerable<Inline> ParseInlines(string text)
        {
            var textBlock = (TextBlock)XamlReader.Parse(
                "<TextBlock xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">"
                + text
                + "</TextBlock>");

            return textBlock.Inlines.ToList(); // must be enumerated
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

            albumnamer.Text = "";
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

                albumnamer.Text = $"{tr.Album}<br><span style=\"font-size:10px; color:#3F3F3F\">{(tr.JoinedAlbumArtists != null ? "by " : "")}{tr.JoinedAlbumArtists ?? "".Replace(";", ",")}</span>";
                rlesedate.Content = tr.Year != 0 ? tr.Year.ToString() : "";

                albumCover = ToImage((tr.Pictures.Length > 0 ? tr.Pictures[0].Data.Data : ConvertBitmapToByteArray(kong)));
            }
            catch (Exception _)
            {
                composer.Content = "";
                songname.Content = _ofd.FileName.Split('\\').Last();

                albumnamer.Text = "";
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
