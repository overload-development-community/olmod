using GameMod;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OLModUnitTest
{
    [TestClass]
    public class DownloadLevelUnitTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            bool downloadComplete = false;
            var algorithm = new MPDownloadLevelAlgorithm
            {
                _downloadFailed = () => downloadComplete = true
            };
            var iterator = algorithm.DoGetLevel("testlevel.mp");
            while (iterator.MoveNext()) ;
            Assert.IsTrue(downloadComplete);
        }
    }
}
