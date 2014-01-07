//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.ColorBasics
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Threading;
    using System.Windows.Media.Composition;
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //=====================================Skeleton===============================
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;
        //============================================================================
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        // 読み込んだ画像を格納する変数
        private BitmapImage StartImage;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Title = "Kinect Animation";
            Button1.Content = "Start.png\nを読み込む";
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            this.drawingGroup = new DrawingGroup();
            this.imageSource = new DrawingImage(this.drawingGroup);
            Image.Source = this.imageSource;
            
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the color stream to receive color frames
                //this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.sensor.ColorStream.Enable();

                // Allocate space to put the pixels we'll receive
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.Image.Source = this.colorBitmap;

                // Skeletonを有効にする
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                //this.sensor.ColorFrameReady += new EventHandler<ColorImageFrameReadyEventArgs>(sensor_AllFramesReady);
                // Skeletonのイベントメソッド
                //this.sensor.SkeletonFrameReady += this.sensorSkeletonFrameReady;
                //this.sensor.SkeletonFrameReady += sensor_SkeletonFrameReady;

                this.sensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(sensor_AllFramesReady);

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }

            this.sensor.ElevationAngle = 0; // Kinectセンサーの角度を0にする

            //起動時に人物がいない背景を撮影する
            if ((int)MessageBox.Show("最初に人物が存在しない背景を撮影します。\n撮影の準備ができたら[OK]を押してください。",
                "撮影開始ダイアログ", MessageBoxButton.OKCancel, MessageBoxImage.Information) == 1)
            {
                ShotPicture(0);
            }
            else
            {
                Close();
            }
        }


        void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }

            //Skeleton[] skeletons = new Skeleton[0];
            //using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            //{                                 
            //    if (skeletonFrame != null)
            //    {
            //        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
            //        skeletonFrame.CopySkeletonDataTo(skeletons);
            //    }
            //}

            //if (skeletons.Length != 0)
            //{
            //    foreach (Skeleton skl in skeletons)
            //    {
            //        if (skl.TrackingState == SkeletonTrackingState.Tracked)
            //            this.skeleton = skl;
            //    }
            //}
        }

        //void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        //{
        //    Skeleton[] skeletons = new Skeleton[0];

        //    using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
        //    {
        //        if (skeletonFrame != null)
        //        {
        //            skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
        //            skeletonFrame.CopySkeletonDataTo(skeletons);
        //        }
        //    }

        //    using (DrawingContext dc = this.drawingGroup.Open())
        //    {
        //        // Draw a transparent background to set the render size
        //        dc.DrawRectangle(null, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

        //        if (skeletons.Length != 0)
        //        {
        //            foreach (Skeleton skel in skeletons)
        //            {
        //                //RenderClippedEdges(skel, dc);

        //                if (skel.TrackingState == SkeletonTrackingState.Tracked)
        //                {
        //                    this.DrawBonesAndJoints(skel, dc);
        //                }
        //                else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
        //                {
        //                    dc.DrawEllipse(
        //                    this.centerPointBrush,
        //                    null,
        //                    this.SkeletonPointToScreen(skel.Position),
        //                    BodyCenterThickness,
        //                    BodyCenterThickness);
        //                }
        //            }
        //        }

        //        // prevent drawing outside of our render area
        //        this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
        //    }
        //}

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's ColorFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        //private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        //{
        //    using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
        //    {
        //        if (colorFrame != null)
        //        {
        //            // Copy the pixel data from the image to a temporary array
        //            colorFrame.CopyPixelDataTo(this.colorPixels);

        //            // Write the pixel data into our bitmap
        //            this.colorBitmap.WritePixels(
        //                new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
        //                this.colorPixels,
        //                this.colorBitmap.PixelWidth * sizeof(int),
        //                0);
        //        }
        //    }
        //}

        ///  Skeletonのイベントメソッド

        /////  Skeletonの骨格情報の取得
        //private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        //{
        //    // Render Torso
        //    this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
        //    this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
        //    this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
        //    this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
        //    this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
        //    this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
        //    this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

        //    // Left Arm
        //    this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
        //    this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
        //    this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

        //    // Right Arm
        //    this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
        //    this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
        //    this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

        //    // Left Leg
        //    this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
        //    this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
        //    this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

        //    // Right Leg
        //    this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
        //    this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
        //    this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

        //    // Render Joints
        //    foreach (Joint joint in skeleton.Joints)
        //    {
        //        Brush drawBrush = null;

        //        if (joint.TrackingState == JointTrackingState.Tracked)
        //        {
        //            drawBrush = this.trackedJointBrush;
        //        }
        //        else if (joint.TrackingState == JointTrackingState.Inferred)
        //        {
        //            drawBrush = this.inferredJointBrush;
        //        }

        //        if (drawBrush != null)
        //        {
        //            drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
        //        }
        //    }
        //}

        /////  Skeletonの描画
        //private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        //{
        //    Joint joint0 = skeleton.Joints[jointType0];
        //    Joint joint1 = skeleton.Joints[jointType1];

        //    // If we can't find either of these joints, exit
        //    if (joint0.TrackingState == JointTrackingState.NotTracked ||
        //        joint1.TrackingState == JointTrackingState.NotTracked)
        //    {
        //        return;
        //    }

        //    // Don't draw if both points are inferred
        //    if (joint0.TrackingState == JointTrackingState.Inferred &&
        //        joint1.TrackingState == JointTrackingState.Inferred)
        //    {
        //        return;
        //    }

        //    // We assume all drawn bones are inferred unless BOTH joints are tracked
        //    Pen drawPen = this.inferredBonePen;
        //    if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
        //    {
        //        drawPen = this.trackedBonePen;
        //    }

        //    drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        //}

        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ButtonScreenshotClick(object sender, RoutedEventArgs e)
        {
            // スクリーンショットのメソッド
            ShotPicture(1);
        }
        
        // スクリーンショットの撮影メソッド
        private void ShotPicture(int flg)
        {
            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                return;
            }

            // create a png bitmap encoder which knows how to save a .png file
            BitmapEncoder encoder = new PngBitmapEncoder();

            // create frame from the writable bitmap and add to encoder
            encoder.Frames.Add(BitmapFrame.Create(this.colorBitmap));

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

            string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            string path = Path.Combine(myPhotos, "KinectSnapshot-" + time + ".png");

            // 開始時のスクリーンショットの撮影のファイル名
            string path2 = Path.Combine(myPhotos, "Start.png");

            // write the new file to disk
            try
            {

                if (flg == 1)
                {
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                    this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path);
                }
                else
                {
                    using (FileStream fs = new FileStream(path2, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                    this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path2);
                }
            }
            catch (IOException)
            {
                if (flg == 1)
                {
                    this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteFailed, path);
                }
                else
                {
                    this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path2);
                }
            }
        }

        public void LoadPicture()
        {
            // ファイルを開くダイアログを表示して画像を読み込む
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            dlg.FileName = "Start.png";
            dlg.DefaultExt = "Start.png";
            dlg.Filter = "PNG画像(*.png)|*.png";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                string filename = dlg.FileName;

                // すでに読み込まれていたら開放する
                if (StartImage != null)
                {
                    StartImage = null;
                }

                // BitmapImageにファイルから画像を読み込む
                StartImage = new BitmapImage();
                StartImage.BeginInit();
                StartImage.UriSource = new Uri(filename);
                StartImage.EndInit();

                // imageに画像を表示
                Image.Source = StartImage;
            }
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            LoadPicture();
        }
    }
}