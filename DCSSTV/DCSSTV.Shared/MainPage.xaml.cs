using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using FrameGenerator;
using ICSharpCode.SharpZipLib.Zip;
using Putty;
using SkiaSharp;
using SkiaSharp.Views.UWP;
using TtyRecDecoder;
#if __WASM__
using Uno.Foundation;
#endif

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DCSSTV
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private CancellationTokenSource cancellations;
        public SKBitmap skBitmap = new SKBitmap(new SKImageInfo(1602, 768));
        private MainGenerator generator;
        private DCSSReplayDriver driver;
        private TtyRecKeyframeDecoder decoder;
        private bool readyToRefresh = false;
        public MainPage()
        {
            InitializeComponent();
            generator = new MainGenerator(new UnoFileReader(), 69);
            driver = new DCSSReplayDriver(generator, RefreshImage, ReadyForRefresh);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await SetOutputText("Navigated");
            try
            {
                await SetOutputText("Trying To Initialise Generator, Please Wait");
                await generator.InitialiseGenerator();
            }
            catch (Exception exception)
            {
                try
                {
                    await SetOutputText("Caught exception On Navigation Load, trying to Cache missing Images, Please Wait");
                    Console.WriteLine("Caught exception On Navigation Load, trying to Cache missing Images");
                    Debug.WriteLine(exception);
                    await LoadExtraFolderIndexedDB();
                    await SetOutputText("Done Loading Cache, Reloading Image generator, please wait...");
                    await generator.ReinitializeGenerator();
                    Console.WriteLine("Done Loading");
                }
                catch (Exception e1)
                {
                    await SetOutputText("Something Bad Happened.");
                    Console.WriteLine(e1);
                    throw;
                }
            }

            await SetOutputText("Image generator Initialized, Start Playback");
        }

        private Task SetOutputText(string text)
        {
            output.Text = text;
            return Task.CompletedTask;
        }

        private Visibility Not(bool? value) => (!value ?? false) ? Visibility.Visible : Visibility.Collapsed;

        private void OnPaintSwapChain(object sender, SKPaintGLSurfaceEventArgs e)
        {
            if (driver.currentFrame == null) return;
            // the the canvas and properties
            var canvas = e.Surface.Canvas;

            Render(canvas, new Size(e.BackendRenderTarget.Width, e.BackendRenderTarget.Height), SKColors.Black, "SkiaSharp Red Hardware Rendering", driver.currentFrame);
            readyToRefresh = true;
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            if (driver.currentFrame == null) return;
            // the the canvas and properties
            var canvas = e.Surface.Canvas;
            var info = e.Info;

            Render(canvas, new Size(info.Width, info.Height), SKColors.Black, "SkiaSharp Blue Software Rendering", driver.currentFrame);
            readyToRefresh = true;
        }

        private static void Render(SKCanvas canvas, Size size, SKColor color, string text, SKBitmap bitmap)
        {
            // get the screen density for scaling
            var display = DisplayInformation.GetForCurrentView();
            var scale = display.LogicalDpi / 96.0f;

            var scaledHeight = (size.Height / scale);
            var scaledWidth = (size.Width / scale);
            int scaledBitmapWidth, scaledBitmapHeight;
            SKBitmap scaledBitmap;
            if (scaledWidth < 1602 || scaledHeight < 768)
            {
                Debug.WriteLine("rescale");
                if ((size.Width / scale) * 0.4794D > (size.Height / scale))// 768/1602 = 0.4794D
                {
                    scaledBitmapWidth = (int)(size.Height / scale * 2.0859375D);
                    scaledBitmapHeight = (int)(size.Height / scale);
                }
                else
                {
                    scaledBitmapWidth = (int)(size.Width / scale);
                    scaledBitmapHeight = (int)(size.Width / scale * 0.4794D);
                }
                scaledBitmap = new SKBitmap(new SKImageInfo(scaledBitmapWidth, scaledBitmapHeight));
                bitmap.ScalePixels(scaledBitmap, SKFilterQuality.Low);
            }
            else
            {
                scaledBitmap = bitmap;
            }
            
            // handle the device screen density
            canvas.Scale(scale);
            
            // make sure the canvas is blank
            canvas.Clear(color);
            //draw bitmap scaled to device size, fitting to width
            canvas.DrawBitmap(scaledBitmap, 0, 0); 

            // Width 41.6587026 => 144.34135
            // Height 56 => 147
        }

        private async void OnLogoButtonClicked(object sender, RoutedEventArgs e)
        {
            await SetOutputText("Waiting for File Selection, loading file");
            MainPage.FileSelectedEvent -= OnFileSelectedEvent;
            MainPage.FileSelectedEvent += OnFileSelectedEvent;
#if __WASM__
            WebAssemblyRuntime.InvokeJS("openFilePicker();");
#endif

        }

        public static void SelectFile(string imageAsDataUrl) => FileSelectedEvent?.Invoke(null, new FileSelectedEventHandlerArgs(imageAsDataUrl));

        private async void OnFileSelectedEvent(object sender, FileSelectedEventHandlerArgs e)
        {
            await SetOutputText("File Selected, loading...");
            MainPage.FileSelectedEvent -= OnFileSelectedEvent;
            var base64Data = Regex.Match(e.FileAsDataUrl, @"data:(?<type1>.+?)/(?<type2>.+?),(?<data>.+)").Groups["data"].Value;
            var binData = Convert.FromBase64String(base64Data);
            var stream = new MemoryStream(binData);
            decoder = new TtyRecKeyframeDecoder(new List<Stream> { stream }, TimeSpan.Zero, driver.MaxDelayBetweenPackets)
            {
                PlaybackSpeed = +1,
                SeekTime = TimeSpan.Zero
            };
            await SetOutputText("Done Loading.");
            await StartImageLoop();
        }

        private static event FileSelectedEventHandler FileSelectedEvent;

        private delegate void FileSelectedEventHandler(object sender, FileSelectedEventHandlerArgs args);

        private class FileSelectedEventHandlerArgs
        {
            public string FileAsDataUrl { get; }
            public FileSelectedEventHandlerArgs(string fileAsDataUrl) => FileAsDataUrl = fileAsDataUrl;

        }
  
        private async Task LoadExtraFolderIndexedDB()
        {
            try
            {
                var file =
                    await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Extra.zip"));
                var bytes = await FileIO.ReadBufferAsync(file);
                var stream = bytes.AsStream();
                await ExtractExtraFileFolder(stream);
                await SetOutputText("Data Cached");

            }
            catch (Exception ex)
            {
                output.Text = ex.ToString();
            }
        }

        public async void LoadPackageFile(object sender, RoutedEventArgs e)
        {
            await LoadExtraFolderIndexedDB();
        }

        public async Task StartImageLoop()
        {
            var side = false;
            try
            {
                await generator.InitialiseGenerator();
                driver.ttyrecDecoder = decoder;
                driver.PlaybackSpeed = int.Parse(speed.Text);
                driver.framerateControlTimeout = int.Parse(framerate.Text);
                readyToRefresh = true;
                await driver.StartImageGeneration();
            }
            catch (Exception ex)
            {
                output.Text = ex.ToString();
            }
            //while (int.Parse(speed.Text) >= 0)
            //{

            //    if (side)
            //    {
            //        await LoadDCSSImage();
            //    }
            //    else
            //    {
            //        await WriteAnotherImage();
            //    }

            //    side = !side;
            //    await Task.Delay(int.Parse(speed.Text));
            //}
        }

        public async Task EndImageLoop()
        {
            try
            {
                await driver.CancelImageGeneration();
            }
            catch (Exception ex)
            {
                output.Text = ex.ToString();
            }
        }


        private async Task ExtractExtraFileFolder(Stream stream)
        {
            try
            {
                var path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), @"Extra.zip");
                var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                Console.WriteLine(localFolder.Path);
                var folder = await localFolder.CreateFolderAsync("Extra", CreationCollisionOption.OpenIfExists);

                var zipInStream = new ZipInputStream(stream);
                var entry = zipInStream.GetNextEntry();
                while (entry != null && entry.CanDecompress)
                {
                    var outputFile = Path.Combine(folder.Path, entry.Name);

                    var outputDirectory = Path.GetDirectoryName(outputFile);
                    //Console.WriteLine(outputDirectory);
                    var correctFolder = await folder.CreateFolderAsync(outputDirectory, CreationCollisionOption.OpenIfExists);


                    if (entry.IsFile)
                    {

                        int size;
                        byte[] buffer = new byte[zipInStream.Length];
                        zipInStream.Read(buffer, 0, buffer.Length);
                        File.WriteAllBytes(outputFile, buffer);
                    }

                    entry = zipInStream.GetNextEntry();
                }
                zipInStream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }


        private async void Button_Click2(object sender, RoutedEventArgs e)
        {
            await EndImageLoop();
        }
        private async void Button_Click3(object sender, RoutedEventArgs e)
        {
            MainPage.FileSelectedEvent -= OnFileSelectedEvent;
            MainPage.FileSelectedEvent += OnFileSelectedEvent;
#if __WASM__
            WebAssemblyRuntime.InvokeJS("openFilePicker();");
#endif
           
        }

        public bool ReadyForRefresh() => readyToRefresh;

        public void RefreshImage()
        {
            if (hwAcceleration.IsChecked.Value)
            {
                //Console.WriteLine("refresh hardware");
                readyToRefresh = false;
                swapChain.Invalidate();
                
            }
            else
            {
                //Console.WriteLine("refresh software");
                readyToRefresh = false;
                canvas.Invalidate();
            }
        }


    }
}