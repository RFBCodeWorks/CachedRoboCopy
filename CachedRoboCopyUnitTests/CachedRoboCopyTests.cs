using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFBCodeWorks.CachedRoboCopy;
using System;
using System.Collections.Generic;
using System.Text;

namespace RFBCodeWorks.CachedRoboCopy.Tests
{
    [TestClass()]
    public class CachedRoboCopyTests
    {
        [TestMethod()]
        public void StartTest()
        {
            var rc = TestPrep.GetRoboCommand(false, RoboSharp.CopyOptions.CopyActionFlags.Default, RoboSharp.SelectionOptions.SelectionFlags.Default);
            var crc = TestPrep.GetCachedRoboCopy(rc);
            rc.LoggingOptions.ListOnly = true;
            var results1 = TestPrep.RunTests(rc, crc, false).Result;
            
            rc.LoggingOptions.ListOnly = false;
            var results2 = TestPrep.RunTests(rc, crc, true).Result;
        }
    }
}