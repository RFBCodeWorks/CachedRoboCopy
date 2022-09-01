using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFBCodeWorks.CachedRoboCopy;
using RoboSharp;
using RoboSharp.Tests;
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
        [DataRow( data1: new object[] { CopyActionFlags.Default, SelectionFlags.Default, LoggingActionFlags.IncludeFullPathNames }, DisplayName = "Defaults")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectories, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Subdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectoriesIncludingEmpty, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "EmptySubdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.Mirror, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "EmptySubdirectories")]
        public void CopyTest(object[] flags) //CopyActionFlags copyAction, SelectionFlags selectionFlags, LoggingActionFlags loggingAction
        {
            CopyActionFlags copyAction = (CopyActionFlags)flags[0];
            SelectionFlags selectionFlags = (SelectionFlags)flags[1]; 
            LoggingActionFlags loggingAction = (LoggingActionFlags)flags[2];

            var rc = TestPrep.GetRoboCommand(false, copyAction, selectionFlags, loggingAction);
            var crc = TestPrep.GetCachedRoboCopy(rc);
            rc.LoggingOptions.ListOnly = true;
            var results1 = TestPrep.RunTests(rc, crc, false).Result;
            AssertResults(results1, rc.LoggingOptions.ListOnly);

            rc.LoggingOptions.ListOnly = false;
            var results2 = TestPrep.RunTests(rc, crc, true).Result;
            AssertResults(results2, rc.LoggingOptions.ListOnly);
        }

        private void AssertResults(RoboSharpTestResults[] results, bool ListOnly)
        {
            Console.WriteLine($"Is List Only: {ListOnly}");
            Console.WriteLine($"RoboCopy Completion Time: {results[0].Results.TimeSpan.TotalMilliseconds} ms");
            Console.WriteLine($"CachedRoboCopy Completion Time: {results[1].Results.TimeSpan.TotalMilliseconds} ms");

            try
            {
                //Files
                Console.Write("Evaluating File Stats...");
                Assert.AreEqual(results[0].Results.FilesStatistic.Total, results[1].Results.FilesStatistic.Total);
                Assert.AreEqual(results[0].Results.FilesStatistic.Copied, results[1].Results.FilesStatistic.Copied);
                Assert.AreEqual(results[0].Results.FilesStatistic.Skipped, results[1].Results.FilesStatistic.Skipped);
                Assert.AreEqual(results[0].Results.FilesStatistic.Extras, results[1].Results.FilesStatistic.Extras);
                Console.WriteLine("OK");

                //Bytes
                Console.Write("Evaluating Byte Stats...");
                Assert.AreEqual(results[0].Results.BytesStatistic.Total, results[1].Results.BytesStatistic.Total);
                Assert.AreEqual(results[0].Results.BytesStatistic.Copied, results[1].Results.BytesStatistic.Copied);
                Assert.AreEqual(results[0].Results.BytesStatistic.Skipped, results[1].Results.BytesStatistic.Skipped);
                Assert.AreEqual(results[0].Results.BytesStatistic.Extras, results[1].Results.BytesStatistic.Extras);
                Console.WriteLine("OK");

                //Directories
                Console.Write("Evaluating Directory Stats...");
                Assert.AreEqual(results[0].Results.DirectoriesStatistic.Total, results[1].Results.DirectoriesStatistic.Total);
                Assert.AreEqual(results[0].Results.DirectoriesStatistic.Copied, results[1].Results.DirectoriesStatistic.Copied);
                Assert.AreEqual(results[0].Results.DirectoriesStatistic.Skipped, results[1].Results.DirectoriesStatistic.Skipped);
                Assert.AreEqual(results[0].Results.DirectoriesStatistic.Extras, results[1].Results.DirectoriesStatistic.Extras);
                Console.WriteLine("OK");
            }catch(Exception e)
            {
                Console.WriteLine("FAIL");
                Console.WriteLine("");
                Console.WriteLine("-----------------------------");
                Console.WriteLine("RoboCopy Results:");
                foreach (string s in results[0].Results.LogLines)
                    Console.WriteLine(s);

                Console.WriteLine("-----------------------------");
                Console.WriteLine("");
                Console.WriteLine("CachedRoboCopy Results:");
                foreach (string s in results[1].Results.LogLines)
                    Console.WriteLine(s);
                
                Console.WriteLine("-----------------------------");
                Console.WriteLine($"Error: {e.Message}");
                Console.WriteLine("-----------------------------");
                throw e;
            }
        }

    }
}