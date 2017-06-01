using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectWPF
{
    class RecordBody
    {
        private List<String> faceRotationInfo;

        //コンストラクタ
        public RecordBody()
        {
            this.faceRotationInfo = new List<string>();
        }

        public void AddFaceRotationInfo(string faceInfo)
        {
            this.faceRotationInfo.Add(faceInfo);
        }

        public void ExportCSV()
        {
            var dt = new DateTime();
            dt = DateTime.Now;
            try
            {
                using (var sw = new StreamWriter($"faceinfo{dt.Month}{dt.Day}{dt.Hour}{dt.Minute}.csv", false))
                {
                    foreach(var line in this.faceRotationInfo)
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


    }
}
