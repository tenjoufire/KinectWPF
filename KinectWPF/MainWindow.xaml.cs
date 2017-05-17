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
    }
}
