using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectWPF
{
    class FaceInfo
    {
        public List<Face> faceInfos { get; set; }
    }

    class Face
    {
        public string time { get; set; }
        public int trackingID { get; set; }
        public double pitch { get; set; }
        public double yaw { get; set; }
        public double roll { get; set; }
        public float positionX { get; set; }
        public float positionY { get; set; }
        public float positionZ { get; set;}
        public float beamAngle { get; set; }
        public bool isSpeaking { get; set; }
    }
}
