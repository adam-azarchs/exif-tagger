using Microsoft.VisualStudio.TestTools.UnitTesting;
using PhotoTagger.Imaging;

namespace PhotoTagger.Tests {
    [TestClass()]
    public class GpsLocationTests {
        [TestMethod()]
        public void TryParseTest() {
            Assert.IsTrue(GpsLocation.TryParse("10.2,45.238", out GpsLocation? result));
            Assert.AreEqual("10.2, 45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2, 45.016666666", out result));
            Assert.AreEqual("10.2, 45.016666666", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2N,45.238", out result));
            Assert.AreEqual("10.2, 45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2,45.238fE", out result));
            Assert.AreEqual("10.2, 45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.256456456894N,45.238E", out result));
            Assert.AreEqual("10.256456456894, 45.238", result?.ToString());

            Assert.IsTrue(GpsLocation.TryParse("-10.2,45.238", out result));
            Assert.AreEqual("-10.2, 45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("-10.2, 45", out result));
            Assert.AreEqual("-10.2, 45", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2S,45.0166666667", out result));
            Assert.AreEqual("-10.2, 45.0166666667", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("-10.2,45.238E", out result));
            Assert.AreEqual("-10.2, 45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2S,45.238E", out result));
            Assert.AreEqual("-10.2, 45.238", result?.ToString());

            Assert.IsTrue(GpsLocation.TryParse("10.2,-45.238", out result));
            Assert.AreEqual("10.2, -45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2, -45.238", out result));
            Assert.AreEqual("10.2, -45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2N,-45.238", out result));
            Assert.AreEqual("10.2, -45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2,45.238W", out result));
            Assert.AreEqual("10.2, -45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2N,45.238W", out result));
            Assert.AreEqual("10.2, -45.238", result?.ToString());

            Assert.IsTrue(GpsLocation.TryParse("-10.2,-45.238", out result));
            Assert.AreEqual("-10.2, -45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("-10.2, -45.238", out result));
            Assert.AreEqual("-10.2, -45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2S,-45.238", out result));
            Assert.AreEqual("-10.2, -45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("-10.2,45.238W", out result));
            Assert.AreEqual("-10.2, -45.238", result?.ToString());
            Assert.IsTrue(GpsLocation.TryParse("10.2S,45.238W", out result));
            Assert.AreEqual("-10.2, -45.238", result?.ToString());
        }
    }
}
