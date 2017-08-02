using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectWPF
{
    class TimeLineLabel
    {
        public List<Label> Labels { get; set; }
    }

    class Label
    {
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string LabelType { get; set; }

    }
}
