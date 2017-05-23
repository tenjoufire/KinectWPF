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

namespace KinectWPF
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor kinect;
        BodyFrameReader bodyFrameReader;
        Body[] bodies;
        int timer = 0;
        //Joint[] heads;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //open the kinect
                kinect = KinectSensor.GetDefault();
                kinect.Open();

                //open the body reader
                bodyFrameReader = kinect.BodyFrameSource.OpenReader();
                bodyFrameReader.FrameArrived += bodyFrameReader_FrameArrived;

                //init Body data array
                bodies = new Body[kinect.BodyFrameSource.BodyCount];
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
                Close();
            }
        }

        void bodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            UpdateBodyFrame(e);
            DrawBodyFrame();
            timer++;
            if (timer == 60)
            {
                ShowHeadDirection();
                timer = 0;
            }
            
        }

        private void UpdateBodyFrame(BodyFrameArrivedEventArgs e)
        {
            using(var bodyFrame = e.FrameReference.AcquireFrame())
            {
                if(bodyFrame == null)
                {
                    return;
                }
                //Get Body Data
                bodyFrame.GetAndRefreshBodyData(bodies);
            }
        }


        private void DrawBodyFrame()
        {
            CanvasBody.Children.Clear();

            //use tracking body only
            foreach(var body in bodies.Where(b => b.IsTracked))
            {
                //all joints in the body
                foreach(var joint in body.Joints)
                {
                    //tracked
                    if(joint.Value.TrackingState == TrackingState.Tracked)
                    {
                        DrawEllipse(joint.Value, 10, Brushes.Blue);
                    }
                    //inferred
                    if(joint.Value.TrackingState == TrackingState.Inferred)
                    {
                        DrawEllipse(joint.Value, 10, Brushes.Yellow);
                    }
                }
            }
        }

        private void ShowHeadDirection()
        {
            int i = 0;
            //use tracking body only
            foreach(var body in bodies.Where(b => b.IsTracked))
            {
                //頭の情報を取得
                Joint head = body.Joints[JointType.Head];
                JointOrientation headOrientation = body.JointOrientations[JointType.Neck];
                int pitch, yaw, roll;                

                //trackingされてなければスキップ
                if (head.TrackingState == TrackingState.NotTracked)
                    continue;

                //変換したい
                ConvertQuaternionToEulerAngle(headOrientation.Orientation, out pitch, out yaw, out roll);
                string faceInfo = $"pitch: {pitch} yaw: {yaw} roll: {roll}";

                //GUIスレッドにて非同期に書き込み
                Dispatcher.Invoke(() =>
                {
                    LogText.Text += $"[{i}] {head.Position.X} {head.Position.Y}{Environment.NewLine}{faceInfo}{Environment.NewLine}";
                    LogText.ScrollToEnd();
                });
                i++;
            }
        }

        private void DrawEllipse(Joint joint, int R, Brush brush)
        {
            var ellipse = new Ellipse() { Width = R, Height = R, Fill = brush};

            //座標系をカメラ座標系からDepth座標系へ
            var point = kinect.CoordinateMapper.MapCameraPointToDepthSpace(joint.Position);
            if (point.X < 0 || point.Y < 0) return;

            Canvas.SetLeft(ellipse, point.X - (R / 2));
            Canvas.SetTop(ellipse, point.Y - (R / 2));

            CanvasBody.Children.Add(ellipse);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(bodyFrameReader != null)
            {
                bodyFrameReader.Dispose();
                bodyFrameReader = null;
            }

            if(kinect != null)
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

        }
    }
}
