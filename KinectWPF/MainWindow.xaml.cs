using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using System.Diagnostics;

namespace KinectWPF
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectSensor kinect;
        private BodyFrameReader bodyFrameReader;
        private Body[] bodies;
        private FaceFrameSource[] faceFrameSources;
        private FaceFrameReader[] faceFrameReaders;
        private FaceFrameResult[] faceFrameResults;
        private int bodyCount;
        private int timer = 0;
        private int[] initPitch;
        private int[] initYaw;
        private int[] initRoll;
        public bool isRecording = false;
        private RecordBody recordBody;
        private Stopwatch stopWatch;


        //Joint[] heads;

        public MainWindow()
        {
            // specify the required face frame results
            FaceFrameFeatures faceFrameFeatures =
                FaceFrameFeatures.BoundingBoxInColorSpace
                | FaceFrameFeatures.PointsInColorSpace
                | FaceFrameFeatures.RotationOrientation
                | FaceFrameFeatures.FaceEngagement
                | FaceFrameFeatures.Glasses
                | FaceFrameFeatures.Happy
                | FaceFrameFeatures.LeftEyeClosed
                | FaceFrameFeatures.RightEyeClosed
                | FaceFrameFeatures.LookingAway
                | FaceFrameFeatures.MouthMoved
                | FaceFrameFeatures.MouthOpen;

            //open the kinect
            kinect = KinectSensor.GetDefault();
            kinect.Open();

            //open the body reader
            bodyFrameReader = kinect.BodyFrameSource.OpenReader();

            //set body count
            bodyCount = kinect.BodyFrameSource.BodyCount;

            //init Body data array
            bodies = new Body[bodyCount];

            //face frame reader
            faceFrameSources = new FaceFrameSource[bodyCount];
            faceFrameReaders = new FaceFrameReader[bodyCount];
            for (int i = 0; i < bodyCount; i++)
            {
                faceFrameSources[i] = new FaceFrameSource(kinect, 0, faceFrameFeatures);
                faceFrameReaders[i] = faceFrameSources[i].OpenReader();
            }
            faceFrameResults = new FaceFrameResult[bodyCount];

            //初期頭部方向保存用配列の初期化
            initPitch = new int[bodyCount];
            initYaw = new int[bodyCount];
            initRoll = new int[bodyCount];

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < bodyCount; i++)
            {
                if (faceFrameReaders[i] != null)
                {
                    // wire handler for face frame arrival
                    faceFrameReaders[i].FrameArrived += FaceFrameReader_FrameArrived;
                }
            }
            // wire handler for body frame arrival
            bodyFrameReader.FrameArrived += BodyFrameReader_FrameArrived;

        }

        void BodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            UpdateBodyFrame(e);
            DrawBodyFrame();

        }

        void FaceFrameReader_FrameArrived(object sender, FaceFrameArrivedEventArgs e)
        {
            //faceframeの登録
            using (var faceFrame = e.FrameReference.AcquireFrame())
            {
                int index = GetFaceSourceIndex(faceFrame.FaceFrameSource);
                faceFrameResults[index] = faceFrame.FaceFrameResult;
            }
        }

        private int GetFaceSourceIndex(FaceFrameSource faceFrameSource)
        {
            int index = -1;

            for (int i = 0; i < this.bodyCount; i++)
            {
                if (faceFrameSources[i] == faceFrameSource)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        private void UpdateBodyFrame(BodyFrameArrivedEventArgs e)
        {
            using (var bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame == null)
                {
                    return;
                }
                //Get and Update Body Data
                bodyFrame.GetAndRefreshBodyData(bodies);

                //たくさん実行しすぎないように
                timer++;
                if (timer == 60)
                {
                    //show face info
                    for (int i = 0; i < bodyCount; i++)
                    {
                        if (faceFrameSources[i].IsTrackingIdValid)
                        {
                            ShowHeadDirection(i);
                        }
                        else
                        {
                            if (bodies[i].IsTracked)
                            {
                                faceFrameSources[i].TrackingId = bodies[i].TrackingId;
                            }
                        }
                    }
                    timer = 0;
                }

            }
        }


        private void DrawBodyFrame()
        {
            CanvasBody.Children.Clear();

            //use tracking body only
            foreach (var body in bodies.Where(b => b.IsTracked))
            {
                //all joints in the body
                foreach (var joint in body.Joints)
                {
                    //tracked
                    if (joint.Value.TrackingState == TrackingState.Tracked)
                    {
                        DrawEllipse(joint.Value, 10, Brushes.Blue);
                    }
                    //inferred
                    if (joint.Value.TrackingState == TrackingState.Inferred)
                    {
                        DrawEllipse(joint.Value, 10, Brushes.Yellow);
                    }
                }
            }
        }

        private void ShowHeadDirection(int i)
        {
            //顔の回転情報の取得
            var faceQuaternion = faceFrameResults[i].FaceRotationQuaternion;

            int pitch, yaw, roll;

            //クォータニオンを角度に変換
            ConvertQuaternionToEulerAngle(faceQuaternion, out pitch, out yaw, out roll);

            //GUIの更新
            Dispatcher.Invoke(() =>
            {
                LogText.Text += $"[{i}] pitch {initPitch[i] - pitch} yaw {initYaw[i] -yaw} roll {initRoll[i] - roll} {Environment.NewLine}";
                LogText.Text += $"init[{i}] {initPitch[i]} {initYaw[i]} {Environment.NewLine}";
                LogText.ScrollToEnd();
            });

            //記録中は記録用のクラスへ顔情報を登録
            if (isRecording)
            {
                //記録開始時からの時間を取得
                string time = $"{stopWatch.Elapsed.Hours}:{stopWatch.Elapsed.Minutes}:{stopWatch.Elapsed.Seconds}";

                //記録用クラスへ登録
                recordBody.AddFaceRotationInfo($"{time},[{i}] pitch {initPitch[i] - pitch} yaw {initYaw[i] - yaw} roll {initRoll[i] - roll}");
            }
        }

        private void DrawEllipse(Joint joint, int R, Brush brush)
        {
            var ellipse = new Ellipse() { Width = R, Height = R, Fill = brush };

            //座標系をカメラ座標系からDepth座標系へ
            var point = kinect.CoordinateMapper.MapCameraPointToDepthSpace(joint.Position);
            if (point.X < 0 || point.Y < 0) return;

            Canvas.SetLeft(ellipse, point.X - (R / 2));
            Canvas.SetTop(ellipse, point.Y - (R / 2));

            CanvasBody.Children.Add(ellipse);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (bodyFrameReader != null)
            {
                bodyFrameReader.Dispose();
                bodyFrameReader = null;
            }

            for (int i = 0; i < bodyCount; i++)
            {
                faceFrameReaders[i].Dispose();
                faceFrameSources[i].Dispose();
            }

            if (kinect != null)
            {
                kinect.Close();
            }
        }

        private void ConvertQuaternionToEulerAngle(Vector4 rotQuaternion, out int pitch, out int yaw, out int roll)
        {
            double x = rotQuaternion.X;
            double y = rotQuaternion.Y;
            double z = rotQuaternion.Z;
            double w = rotQuaternion.W;

            // convert face rotation quaternion to Euler angles in degrees
            double yawD, pitchD, rollD;
            pitchD = Math.Atan2(2 * ((y * z) + (w * x)), (w * w) - (x * x) - (y * y) + (z * z)) / Math.PI * 180.0;
            yawD = Math.Asin(2 * ((w * y) - (x * z))) / Math.PI * 180.0;
            rollD = Math.Atan2(2 * ((x * y) + (w * z)), (w * w) + (x * x) - (y * y) - (z * z)) / Math.PI * 180.0;

            // clamp the values to a multiple of the specified increment to control the refresh rate
            double increment = 5.0;
            pitch = (int)(Math.Floor((pitchD + ((increment / 2.0) * (pitchD > 0 ? 1.0 : -1.0))) / increment) * increment);
            yaw = (int)(Math.Floor((yawD + ((increment / 2.0) * (yawD > 0 ? 1.0 : -1.0))) / increment) * increment);
            roll = (int)(Math.Floor((rollD + ((increment / 2.0) * (rollD > 0 ? 1.0 : -1.0))) / increment) * increment);
        }

        private void InitButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < bodyCount; i++)
            {
                if (faceFrameSources[i].IsTrackingIdValid)
                {
                    ConvertQuaternionToEulerAngle(faceFrameResults[i].FaceRotationQuaternion, out initPitch[i], out initYaw[i], out initRoll[i]);
                }
            }
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (isRecording)
            {
                RecordButton.Content = "Record";
                isRecording = false;
                recordBody.ExportCSV();
                stopWatch.Stop();
                stopWatch.Reset();
            }
            else
            {
                RecordButton.Content = "Stop";
                isRecording = true;

                //CSV記録用クラスの初期化
                recordBody = new RecordBody();

                //時間計測タイマーの初期化とタイマーの開始
                stopWatch = new Stopwatch();
                stopWatch.Start();
            }
        }
    }
}
