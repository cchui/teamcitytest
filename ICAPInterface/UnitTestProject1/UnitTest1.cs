using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            ICAPInterfaceLib.ICAP intf = new ICAPInterfaceLib.ICAP("10.200.23.84", 1344);

            var r1 = intf.ScanFile(@"ILIDocumentInfoUpdate.sql");

            var r2 = intf.ScanFile(@"icap_whitepaper_v1-01.pdf");

            Assert.IsFalse(r1.Success);
            Assert.IsTrue(r1.Message.Equals("McAfee Web Gateway has blocked the file, because the detected media type (see below) is not allowed."));
            Assert.IsTrue(r2.Success);
        }
    }
}
