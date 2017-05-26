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
        FaceFrameSource[] faceFrameSources;
        FaceFrameReader[] faceFrameReaders;
        FaceFrameResult[] faceFrameResults;
        int bodyCount;
        int timer = 0;
        int initPitch = 0, initYaw = 0, initRoll = 0;

        
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
            bodyFrameReader.FrameArrived += BodyFrameReader_FrameArrived;

        }

        void BodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            UpdateBodyFrame(e);
            DrawBodyFrame();
            /*
            timer++;
            if (timer == 60)
            {
                ShowHeadDirection();
                timer = 0;
            }
            */
            
        }

        void FaceFrameReader_FrameArrived(object sender, FaceFrameArrivedEventArgs e)
        {
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
            using(var bodyFrame = e.FrameReference.AcquireFrame())
            {
                if(bodyFrame == null)
                {
                    return;
                }
                //Get Body Data
                bodyFrame.GetAndRefreshBodyData(bodies);

                for(int i = 0; i < bodyCount; i++)
                {
                    if (faceFrameSources[i].IsTrackingIdValid)
                    {
                        timer++;
                        if (timer == 60)
                        {
                            ShowHeadDirection(i);
                            timer = 0;
                        }
                    }
                    else
                    {
                        if (bodies[i].IsTracked)
                        {
                            faceFrameSources[i].TrackingId = bodies[i].TrackingId;
                        }
                    }
                }
                
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

        private void ShowHeadDirection(int i)
        {
            var faceQuaternion = faceFrameResults[i].FaceRotationQuaternion;
            int pitch, yaw, roll;
            ConvertQuaternionToEulerAngle(faceQuaternion, out pitch, out yaw, out roll);
            Dispatcher.Invoke(() =>
            {
                LogText.Text += $"[{i}] {pitch} {yaw} {roll} {Environment.NewLine}";
                LogText.ScrollToEnd();
            });
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

            for(int i = 0; i < bodyCount; i++)
            {
                faceFrameReaders[i].Dispose();
                faceFrameSources[i].Dispose();
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
