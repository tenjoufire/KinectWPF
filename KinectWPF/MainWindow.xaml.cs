﻿using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

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
        private AudioBeamFrameReader audioBeamFrameReader;
        private readonly byte[] audioBuffer;
        private int bodyCount;
        private int timer = 0;
        private bool canWriteLine = false;
        private double[] initPitch;
        private double[] initYaw;
        private double[] initRoll;
        public bool isRecording = false;
        public bool drawBody = true;
        private RecordBody recordBody;
        private Stopwatch stopWatch;
        private float beamAngle = 0.0f;
        private bool isSpeaking = false;
        private readonly float SpeakThreshold = -80;

        private float accumulatedSquareSum;
        private int accumulatedSampleCount;
        private const int SamplesPerColumn = 40;
        private float energy;

        private int csvCount = 10;
        private string prevMotionDataUtilityTime = "";



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
            initPitch = new double[bodyCount];
            initYaw = new double[bodyCount];
            initRoll = new double[bodyCount];

            //audio関連のreaderをopen
            AudioSource audioSource = kinect.AudioSource;
            audioBuffer = new byte[audioSource.SubFrameLengthInBytes];
            audioBeamFrameReader = audioSource.OpenReader();

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

            if(audioBeamFrameReader != null)
            {
                audioBeamFrameReader.FrameArrived += AudioBeamFrameReader_FrameArrived;
            }

        }

        void BodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            UpdateBodyFrame(e);
            if (drawBody)
            {
                DrawBodyFrame();
            }

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

        void AudioBeamFrameReader_FrameArrived(object sender, AudioBeamFrameArrivedEventArgs e)
        {
            AudioBeamFrameReference frameReference = e.FrameReference;
            AudioBeamFrameList frameList = frameReference.AcquireBeamFrames();

            if(frameList != null)
            {
                using (frameList)
                {
                    //Audio beamは1つしかサポートされていないため1つのsubframeを取る
                    IReadOnlyList<AudioBeamSubFrame> subFrameList = frameList[0].SubFrames;

                    foreach(var subFrame in subFrameList)
                    {
                        
                        if(subFrame.BeamAngle != beamAngle)
                        {
                            beamAngle = subFrame.BeamAngle;
                        }

                        //Kinectが閾値以上の音を拾っているかどうか検出
                        subFrame.CopyFrameDataToArray(audioBuffer);
                        for(int i = 0; i < audioBuffer.Length; i += sizeof(float))
                        {
                            float audioSample = BitConverter.ToSingle(this.audioBuffer, i);
                            accumulatedSquareSum += audioSample * audioSample;
                            ++accumulatedSampleCount;
                            float meanSquare = this.accumulatedSquareSum / SamplesPerColumn;
                            if (meanSquare > 1.0f)
                            {
                                meanSquare = 1.0f;
                            }
                            if (meanSquare > 0)
                            {
                                energy = (float)(10.0 * Math.Log10(meanSquare));
                            }
                            SpeakEnergy.Text = energy.ToString() + " " + isSpeaking;
                            this.accumulatedSquareSum = 0;
                            this.accumulatedSampleCount = 0;
                            if(energy > SpeakThreshold)
                            {
                                isSpeaking = true;
                            }
                            else
                            {
                                isSpeaking = false;
                            }
                        }

                    }
                }
            }
            if(beamAngle < 0)
            {
                WhoIsSpeak.Text = $"Kinectから見て{Environment.NewLine}右の人が話しています";
            }
            else
            {
                WhoIsSpeak.Text = $"Kinectから見て{Environment.NewLine}左の人が話しています";
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

                //Console.WriteLine(faceFrameSources.Count(x => x.IsTrackingIdValid));

                //show face info
                for (int i = 0; i < bodyCount; i++)
                {
                    if (faceFrameSources[i].IsTrackingIdValid)
                    {
                        ShowHeadDirection(i, faceFrameSources.Count(x => x.IsTrackingIdValid));
                    }
                    else
                    {
                        if (bodies[i].IsTracked)
                        {
                            faceFrameSources[i].TrackingId = bodies[i].TrackingId;
                        }
                    }
                }

                timer++;
                if(timer == 60)
                {
                    canWriteLine = true;
                }
                if(timer > 60)
                {
                    canWriteLine = false;
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

        private void ShowHeadDirection(int i, int bodyNum)
        {
            //顔の回転情報の取得
            var faceQuaternion = faceFrameResults[i].FaceRotationQuaternion;

            double pitch, yaw, roll;

            //クォータニオンを角度に変換
            ConvertQuaternionToEulerAngle(faceQuaternion, out pitch, out yaw, out roll);
            if (canWriteLine)
            {
                //GUIの更新
                Dispatcher.Invoke(() =>
                {
                    LogText.Text += $"[{i}] pitch {initPitch[i] - pitch} yaw {initYaw[i] - yaw} roll {initRoll[i] - roll} {Environment.NewLine}";
                    LogText.Text += $"init[{i}] {initPitch[i]} {initYaw[i]}  {beamAngle} {Environment.NewLine}";
                    LogText.ScrollToEnd();
                });
            }

            //記録中は記録用のクラスへ顔情報を登録
            if (isRecording)
            {
                //記録開始時からの時間を取得
                string time = $"{stopWatch.Elapsed.Hours}:{stopWatch.Elapsed.Minutes}:{stopWatch.Elapsed.Seconds}.{stopWatch.Elapsed.Milliseconds}";

                var motionDataUtilityTime = $"{stopWatch.Elapsed.TotalSeconds}";

                //顔の位置情報の取得
                var facePositon = bodies[i].Joints[JointType.Head].Position;

                //両肩の中心の位置情報の取得
                var shoulderPosition = bodies[i].Joints[JointType.SpineShoulder].Position;

                //両手の位置情報の取得
                var leftHandPosition = bodies[i].Joints[JointType.HandLeft].Position;
                var rightHandposition = bodies[i].Joints[JointType.HandRight].Position;

                //モーションデータユーティリティで表示するための座標調整
                int offsetY = 1500;
                int offsetRate = 1000;

                var faceCSVString = $"{facePositon.X * offsetRate},{facePositon.Y * offsetRate + offsetY},{facePositon.Z * offsetRate}";
                var shoulderCSVString = $"{shoulderPosition.X * offsetRate},{shoulderPosition.Y * offsetRate + offsetY},{shoulderPosition.Z * offsetRate}";
                var leftHandCSVString = $"{leftHandPosition.X * offsetRate},{leftHandPosition.Y * offsetRate + offsetY},{leftHandPosition.Z * offsetRate}";
                var rightHandCSVString = $"{rightHandposition.X * offsetRate},{rightHandposition.Y * offsetRate + offsetY},{rightHandposition.Z * offsetRate}";

                csvCount++;
                if (bodyNum <= csvCount)
                {
                    prevMotionDataUtilityTime = motionDataUtilityTime;
                    csvCount = 0;
                }
                

                //記録用クラスへ登録
                //recordBody.AddFaceRotationInfo($"{time},[{i}] pitch {initPitch[i] - pitch} yaw {initYaw[i] - yaw} roll {initRoll[i] - roll}");
                //recordBody.AddFaceRotationInfo($"{prevMotionDataUtilityTime},{i+1},{facePositon.X * 1000},{facePositon.Y * 1000 + 100},{facePositon.Z * 1000},{100},{roll},{pitch},{yaw}");
                recordBody.AddFaceRotationInfo($"{prevMotionDataUtilityTime},{i + 1},{shoulderCSVString},{i + 1},{leftHandCSVString},{i + 1},{rightHandCSVString},{i + 1},{faceCSVString},{100},{roll},{pitch},{yaw}");


                recordBody.AddFaceInfo(i, motionDataUtilityTime, initPitch[i] - pitch, initYaw[i] - yaw, initRoll[i] - roll, facePositon, beamAngle, isSpeaking);
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

            if(audioBeamFrameReader != null)
            {
                audioBeamFrameReader.Dispose();
                audioBeamFrameReader = null;
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

        private void ConvertQuaternionToEulerAngle(Vector4 rotQuaternion, out double pitch, out double yaw, out double roll)
        {
            double x = rotQuaternion.X;
            double y = rotQuaternion.Y;
            double z = rotQuaternion.Z;
            double w = rotQuaternion.W;

            // convert face rotation quaternion to Euler angles in degrees
            //double yawD, pitchD, rollD;
            pitch = Math.Atan2(2 * ((y * z) + (w * x)), (w * w) - (x * x) - (y * y) + (z * z)) / Math.PI * 180.0;
            yaw = Math.Asin(2 * ((w * y) - (x * z))) / Math.PI * 180.0;
            roll = Math.Atan2(2 * ((x * y) + (w * z)), (w * w) + (x * x) - (y * y) - (z * z)) / Math.PI * 180.0;

            //Console.WriteLine($"roll:{rollD} pitch:{pitchD} yaw:{yawD}");

            /*
            // clamp the values to a multiple of the specified increment to control the refresh rate
            double increment = 5.0;
            pitch = (int)(Math.Floor((pitchD + ((increment / 2.0) * (pitchD > 0 ? 1.0 : -1.0))) / increment) * increment);
            yaw = (int)(Math.Floor((yawD + ((increment / 2.0) * (yawD > 0 ? 1.0 : -1.0))) / increment) * increment);
            roll = (int)(Math.Floor((rollD + ((increment / 2.0) * (rollD > 0 ? 1.0 : -1.0))) / increment) * increment);
            */
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
                recordBody.ExportJson();
                recordBody.ExportLabelJson();
                recordBody.ExportLabelCsv();
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

        private void DrowBodyCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            drawBody = false;
        }

        private void DrowBodyCheck_Checked(object sender, RoutedEventArgs e)
        {
            drawBody = true;
        }
    }
}
