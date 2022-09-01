using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFBCodeWorks.CachedRoboCopy;
using RoboSharp;
using System;
using System.Collections.Generic;
using System.Text;
using CopyActionFlags = RoboSharp.CopyOptions.CopyActionFlags;
using LoggingActionFlags = RoboSharp.LoggingOptions.LoggingActionFlags;
using SelectionFlags = RoboSharp.SelectionOptions.SelectionFlags;
namespace RFBCodeWorks.CachedRoboCopy.Tests
{
    [TestClass()]
    public class CachedRoboCopyTests
    {
        [TestMethod()]
        [DataRow(CopyActionFlags.Default, SelectionFlags.Default, LoggingActionFlags.Default, "Defaults")]
        public void StartTest(CopyActionFlags copyAction, SelectionFlags selectionFlags, LoggingActionFlags loggingAction)
        {
            var rc = TestPrep.GetRoboCommand(false, copyAction, selectionFlags, loggingAction);
            var crc = TestPrep.GetCachedRoboCopy(rc);
            rc.LoggingOptions.ListOnly = true;
            var results1 = TestPrep.RunTests(rc, crc, false).Result;
            
            rc.LoggingOptions.ListOnly = false;
            var results2 = TestPrep.RunTests(rc, crc, true).Result;
        }
    }
}