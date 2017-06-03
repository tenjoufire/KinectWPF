using Microsoft.Kinect;
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

        //コンストラクタ
        public RecordBody()
        {
            this.faceRotationInfoString = new List<string>();
            faceInfo = new FaceInfo();
        }

        public void AddFaceRotationInfo(string faceInfo)
        {
            this.faceRotationInfoString.Add(faceInfo);
        }

        public void AddFaceInfo(int index, string time, int pitch, int yaw, int roll, CameraSpacePoint point)
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
            };
            faceInfo.faceInfos.Add(face);
        }

        public void ExportCSV()
        {
            var dt = new DateTime();
            dt = DateTime.Now;
            try
            {
                using (var sw = new StreamWriter($"faceinfo{dt.Month}{dt.Day}{dt.Hour}{dt.Minute}.csv", false))
                {
                    foreach(var line in this.faceRotationInfoString)
                    {
                        sw.WriteLine(line + ",");
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


    }
}
