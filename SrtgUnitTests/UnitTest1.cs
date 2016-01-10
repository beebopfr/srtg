using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SrtgUnitTests {
    [TestClass]
    public class UnitTest1 {
        [TestMethod]
        public void FormatSpeedRoundedToUpperUnit() {
            var str = Srtg.ChartRendering.ChartRenderer.FormatSpeed(999999, true);
            Assert.AreEqual("1,0 M", str);
        }

        [TestMethod]
        public void FormatSpeedGreaterThanMaxUnit() {
            var str = Srtg.ChartRendering.ChartRenderer.FormatSpeed(99999999999999999, true);
            Assert.AreEqual("100000000,0 G", str);
        }
    }
}
