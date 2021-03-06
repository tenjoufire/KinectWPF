﻿using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KinectWPF
{
    class RecordBody
    {
        private List<String> faceRotationInfoString;
        private FaceInfo faceInfo;
        private TimeLineLabel timeLineLabel;

        //コンストラクタ
        public RecordBody()
        {
            this.faceRotationInfoString = new List<string>();
            faceInfo = new FaceInfo();
            faceInfo.faceInfos = new List<Face>();
            timeLineLabel = new TimeLineLabel();
            timeLineLabel.Labels = new List<Label>();
        }

        public void AddFaceRotationInfo(string faceInfo)
        {
            this.faceRotationInfoString.Add(faceInfo);
        }

        public void AddFaceInfo(int index, string time, double pitch, double yaw, double roll, CameraSpacePoint point, float beamAngle, bool isSpeaking)
        {
            var face = new Face()
            {
                time = time,
                pitch = pitch,
                yaw = yaw,
                roll = roll,
                trackingID = index,
                positionX = point.X,
                positionY = point.Y,
                positionZ = point.Z,
                beamAngle = beamAngle,
                isSpeaking = isSpeaking
            };
            faceInfo.faceInfos.Add(face);
        }

        public void AddLabel(string startTime, string endTime, string type)
        {
            var label = new Label()
            {
                StartTime = startTime,
                EndTime = endTime,
                LabelType = type
            };
            timeLineLabel.Labels.Add(label);
        }


        public void ExportCSV()
        {
            var dt = new DateTime();
            dt = DateTime.Now;
            try
            {
                using (var sw = new StreamWriter($"faceinfo{dt.Month}{dt.Day}{dt.Hour}{dt.Minute}.csv", false))
                {
                    /*
                    foreach (var line in this.faceRotationInfoString)
                    {
                        sw.WriteLine(line + ",");
                    }
                    */
                    var nextLine = true;
                    var prevTime = "";
                    foreach(var line in this.faceRotationInfoString)
                    {
                        nextLine = line.Split(',')[0] == prevTime ? false : true;

                        if (nextLine)
                        {
                            sw.Write(",");
                            sw.WriteLine();
                            sw.Write(line);
                            prevTime = line.Split(',')[0];
                        }
                        else
                        {
                            var subLineArray = line.Split(',');
                            subLineArray[0] = "";
                            sw.Write(String.Join(",", subLineArray));
                            prevTime = line.Split(',')[0];
                        }
                    }
                }
            }
            catch (Exception e)
            {

                throw;
            }
        }

        public void ExportJson()
        {
            //json作成
            var jsonString = JsonConvert.SerializeObject(faceInfo);
            var dt = new DateTime();
            dt = DateTime.Now;
            try
            {
                using (var sw = new StreamWriter($"faceinfo{dt.Month}{dt.Day}{dt.Hour}{dt.Minute}.json", false))
                {
                    sw.WriteLine(jsonString);
                }
            }
            catch (Exception e)
            {

                throw;
            }
        }

        public void ExportLabelJson()
        {
            ConvertFaceInfoToLabel();

            var jsonString = JsonConvert.SerializeObject(timeLineLabel);
            var dt = new DateTime();
            dt = DateTime.Now;
            try
            {
                using (var sw = new StreamWriter($"label{dt.Month}{dt.Day}{dt.Hour}{dt.Minute}.json", false))
                {
                    sw.WriteLine(jsonString);
                }
            }
            catch (Exception e)
            {

                throw;
            }
        }

        public void ExportLabelCsv()
        {
            ConvertFaceInfoToLabel();

            var dt = new DateTime();
            dt = DateTime.Now;
            
            using(var sw = new StreamWriter($"label{dt.Month}{dt.Day}{dt.Hour}{dt.Minute}.csv", false))
            {
                foreach(var label in timeLineLabel.Labels)
                {
                    sw.WriteLine($"{label.StartTime},{label.EndTime},{label.LabelType},");
                }
            }

        }

        public void ConvertFaceInfoToLabel()
        {
            bool prevSpeaking = false;
            string startTime = "";
            string type = "";
            float prevBeamAngle = 0f;

            //int defaultID = faceInfo.faceInfos[0].trackingID;

            //音声情報のラベルは1人分から分析したいのでfaceのフィルタリングを行う
            foreach (var face in faceInfo.faceInfos/*.Where(x => x.trackingID == defaultID)*/)
            {
                //音声情報部門
                //発話の開始
                if(face.isSpeaking && !prevSpeaking)
                {
                    startTime = face.time;
                    type = face.beamAngle > 0 ? "LeftSpeak" : "RightSpeak";
                }

                //発話の終了
                if(!face.isSpeaking && prevSpeaking)
                {
                    AddLabel(startTime, face.time, type);
                }
                else if (face.beamAngle * prevBeamAngle < 0)//発話者交代
                {
                    AddLabel(startTime, face.time, type);
                    startTime = face.time;
                    type = face.beamAngle > 0 ? "LeftSpeak" : "RightSpeak";

                }

                //直前の発話方向の格納
                prevBeamAngle = face.beamAngle;

                //直前の発話状況の格納
                if (face.isSpeaking)
                {
                    prevSpeaking = true;
                }
                else
                {
                    prevSpeaking = false;
                }

                
            }
        }


    }
}
