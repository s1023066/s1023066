using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Windows.Threading;
using Microsoft.Kinect;

namespace MyKinectTool
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 認識状態
        /// </summary>
        enum State
        {
            None,
            Kamae,
            Up,
            Down,
        };

        /// <summary>
        /// Kinectセンサクラス
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// 認識した人物の骨格情報
        /// </summary>
        private Skeleton skeleton;

        /// <summary>
        /// 座標変換した骨格位置情報
        /// </summary>
        private ColorImagePoint[] cip = new ColorImagePoint[20];

        /// <summary>
        /// ファイルから読み込んできた骨格情報
        /// </summary>
        private Vector4[] pose1, pose2, kamae;

        /// <summary>
        /// タイマーイベント用変数
        /// </summary>
        private DispatcherTimer dispatcherTimer;

        /// <summary>
        /// 骨格情報を表示する線
        /// </summary>
        private Line[] bones;

        /// <summary>
        /// 現在の認識状態
        /// </summary>
        private State nowState;

        /// <summary>
        /// カウンタ
        /// </summary>
        private int counter;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 接続されているセンサを捜索
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
                // SkeletonStreamを有効にする
                this.sensor.SkeletonStream.Enable();

                // SkeletonStreamにイベントを追加
                this.sensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(sensor_SkeletonFrameReady);

                // センサを起動
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            // インスタンス化
            this.skeleton = new Skeleton();

            // スレッドでの呼び出し優先度指定
            this.dispatcherTimer = new DispatcherTimer();

            // 1秒ごとに処理()
            this.dispatcherTimer.Interval = new TimeSpan(100000);

            // イベント追加
            this.dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);

            // タイマー動作開始
            this.dispatcherTimer.Start();

            this.kamae = new Vector4[20];
            this.pose1 = new Vector4[20];
            this.pose2 = new Vector4[20];

            try
            {
                this.kamae = MyFileIO.LoadJoint("kamae");
                this.pose1 = MyFileIO.LoadJoint("pose1");
                this.pose2 = MyFileIO.LoadJoint("pose2");
            }
            catch (Exception ex)
            {
                Console.WriteLine("読み込み失敗");
            }

            this.bones = new Line[19];
            for (int i = 0; i < this.bones.Length; i++)
            {
                this.bones[i] = new Line();

                // 描画設定
                this.bones[i].HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                this.bones[i].VerticalAlignment = System.Windows.VerticalAlignment.Center;
                this.bones[i].StrokeThickness = 2;
                this.bones[i].Stroke = Brushes.YellowGreen;

                // Canvasに追加
                this.canvas1.Children.Add(this.bones[i]);
            }

            // 初期化
            this.nowState = State.None;
            this.counter = 0;
        }

        /// <summary>
        /// SkeletonStreamのイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            if (skeletons.Length != 0)
            {
                foreach (Skeleton skl in skeletons)
                {
                    if (skl.TrackingState == SkeletonTrackingState.Tracked)
                        this.skeleton = skl;
                }
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (this.skeleton.TrackingState == SkeletonTrackingState.Tracked)
            {
                // 内積を計算して類似度を取得
                int kamaeSimilarity = (int)(MyMath.Dot(JointType.ShoulderLeft, JointType.HandLeft, this.skeleton, this.kamae) * 100);
                int pose1Similarity = (int)(MyMath.Dot(JointType.ShoulderLeft, JointType.HandLeft, this.skeleton, this.pose1) * 100);
                int pose2Similarity = (int)(MyMath.Dot(JointType.ShoulderLeft, JointType.HandLeft, this.skeleton, this.pose2) * 100);

                // 状態遷移
                switch (this.nowState)
                {
                    case State.None:

                        if (kamaeSimilarity >= this.slider1.Value)
                        {
                            this.counter++;

                            if (this.counter >= slider4.Value)
                            {
                                this.counter = 0;
                                this.nowState = State.Kamae;
                            }
                        }
                        break;

                    case State.Kamae:

                        if (pose1Similarity >= this.slider2.Value)
                            this.nowState = State.Up;

                        if (pose2Similarity >= this.slider3.Value)
                            this.nowState = State.Down;
                        break;

                    case State.Up:
                    case State.Down:

                        this.counter++;

                        if (this.counter > this.slider4.Value)
                        {
                            this.counter = 0;
                            this.nowState = State.None;
                        }

                        break;

                    default:
                        break;
                }

                // 表示
                this.label4.Content = kamaeSimilarity + " / " + this.slider1.Value.ToString("F0");
                this.label5.Content = pose1Similarity + " / " + this.slider1.Value.ToString("F0");
                this.label6.Content = pose2Similarity + " / " + this.slider1.Value.ToString("F0");


                // 骨格を描画
                for (int i = 0; i < this.skeleton.Joints.Count; i++)
                {
                    // 3次元座標を2次元座標に変換
                    cip[i] = this.sensor.CoordinateMapper.MapSkeletonPointToColorPoint(this.skeleton.Joints[(JointType)i].Position, ColorImageFormat.RgbResolution640x480Fps30);

                    // Canvasに収まるように半分に縮小
                    cip[i].X /= 2;
                    cip[i].Y /= 2;
                }

                // 骨格情報をセット
                // 今は上半身のみ
                this.SetBonePoint(0, 3, 2);
                this.SetBonePoint(1, 2, 4);
                this.SetBonePoint(2, 4, 5);
                this.SetBonePoint(3, 5, 6);
                this.SetBonePoint(4, 6, 7);
                this.SetBonePoint(5, 2, 8);
                this.SetBonePoint(6, 8, 9);
                this.SetBonePoint(7, 9, 10);
                this.SetBonePoint(8, 10, 11);
            }
            else
            {
                this.label4.Content = this.slider1.Value.ToString("F0");
                this.label5.Content = this.slider2.Value.ToString("F0");
                this.label6.Content = this.slider3.Value.ToString("F0");
                this.label8.Content = this.slider4.Value.ToString("F0") + "[ms]";
            }

            this.label9.Content = "認識状態:" + this.nowState.ToString();
        }

        /// <summary>
        /// 骨格情報をセットする
        /// </summary>
        /// <param name="boneNo">描画する骨格番号</param>
        /// <param name="p1">座標変換した骨格位置情報のインデックス1</param>
        /// <param name="p2">座標変換した骨格位置情報のインデックス2</param>
        private void SetBonePoint(int boneNo, int p1, int p2)
        {
            this.bones[boneNo].X1 = cip[p1].X;
            this.bones[boneNo].Y1 = cip[p1].Y;
            this.bones[boneNo].X2 = cip[p2].X;
            this.bones[boneNo].Y2 = cip[p2].Y;
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            MyFileIO.SaveJoint("kamae", this.skeleton);
            this.kamae = MyFileIO.LoadJoint("kamae");
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            MyFileIO.SaveJoint("pose1", this.skeleton);
            this.pose1 = MyFileIO.LoadJoint("pose1");
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            MyFileIO.SaveJoint("pose2", this.skeleton);
            this.pose2 = MyFileIO.LoadJoint("pose2");
        }
    }
}
