using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Srtg.ChartRendering {
    public class ChartData {

        public DateTime TimeStamp { get; set; }
        public ulong InCounter { get; set; }
        public ulong OutCounter { get; set; }
        public double InSpeed { get; set; }
        public double OutSpeed { get; set; }

        public Exception Error { get; set; }
        public bool IsPause { get; set; }

        public bool IsError {
            get { return Error != null; }
        }

        public double InBitsSpeed {
            get {
                return InSpeed * 8;
            }
        }

        public double OutBitsSpeed {
            get {
                return OutSpeed * 8;
            }
        }

        public bool IsEmpty {
            get { return this.TimeStamp == default(DateTime); }
        }

    }
}
