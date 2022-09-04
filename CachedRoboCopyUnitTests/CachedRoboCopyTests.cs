using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFBCodeWorks.CachedRoboCopy;
using RoboSharp;
using RoboSharp.Interfaces;
using RoboSharp.Tests;
using System;
using System.Collections.Generic;
using System.IO;
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
        [DataRow( data1: new object[] { CopyActionFlags.Default, SelectionFlags.Default, LoggingActionFlags.Default}, DisplayName = "Defaults")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectories, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Subdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectoriesIncludingEmpty, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "EmptySubdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.Mirror, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Mirror")]
        public void CopyTest(object[] flags) //CopyActionFlags copyAction, SelectionFlags selectionFlags, LoggingActionFlags loggingAction
        {
            TestPrep.CleanDestination();
            CopyActionFlags copyAction = (CopyActionFlags)flags[0];
            SelectionFlags selectionFlags = (SelectionFlags)flags[1]; 
            LoggingActionFlags loggingAction = (LoggingActionFlags)flags[2];
            if (copyAction != CopyActionFlags.Default) return;
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
            Console.Write("---------------------------------------------------");
            Console.WriteLine($"Is List Only: {ListOnly}");
            Console.WriteLine($"RoboCopy Completion Time: {results[0].Results.TimeSpan.TotalMilliseconds} ms");
            Console.WriteLine($"CachedRoboCopy Completion Time: {results[1].Results.TimeSpan.TotalMilliseconds} ms");
            IStatistic RCStat = null, CRCStat = null;
            string evalSection = "";
            
            try
            {
                //Files
                //Console.Write("Evaluating File Stats...");
                AssertStat(results[0].Results.FilesStatistic, results[1].Results.FilesStatistic, "Files");
                //Console.WriteLine("OK");

                //Bytes
                //Console.Write("Evaluating Byte Stats...");
                AssertStat(results[0].Results.BytesStatistic, results[1].Results.BytesStatistic, "Bytes");
                //Console.WriteLine("OK");

                //Directories
                //Console.Write("Evaluating Directory Stats...");
                AssertStat(results[0].Results.DirectoriesStatistic, results[1].Results.DirectoriesStatistic, "Directory");
                //Console.WriteLine("OK");

                Console.WriteLine("Test Passed.");
                //Console.WriteLine("RoboCopy Results:");
                //Console.WriteLine($"{results[0].Results.DirectoriesStatistic}");
                //Console.WriteLine($"{results[0].Results.BytesStatistic}");
                //Console.WriteLine($"{results[0].Results.FilesStatistic}");
                //Console.WriteLine("-----------------------------");
                //Console.WriteLine("CachedRoboCopy Results:");
                //Console.WriteLine($"{results[1].Results.DirectoriesStatistic}");
                //Console.WriteLine($"{results[1].Results.BytesStatistic}");
                //Console.WriteLine($"{results[1].Results.FilesStatistic}");
                //Console.WriteLine("-----------------------------");

                void AssertStat(IStatistic rcStat, IStatistic crcSTat, string eval)
                {
                    RCStat = rcStat;
                    CRCStat = crcSTat;
                    evalSection = eval;
                    Assert.AreEqual(RCStat.Total, CRCStat.Total, "Stat Category: TOTAL");
                    Assert.AreEqual(RCStat.Copied, CRCStat.Copied, "Stat Category: COPIED");
                    Assert.AreEqual(RCStat.Skipped, CRCStat.Skipped, "Stat Category: SKIPPED");
                    Assert.AreEqual(RCStat.Extras, CRCStat.Extras, "Stat Category: EXTRAS");
                }

            }catch(Exception e)
            {
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
                throw new AssertFailedException(e.Message +
                    $"\nIs List Only: {ListOnly}" +
                    $"\n{evalSection} Stats: \n"+
                    $"RoboCopy Results: {RCStat}\n" +
                    $"CachedRC Results: {CRCStat}" +
                    (e.GetType() == typeof(AssertFailedException) ? "" :  $" \nStackTrace: \n{e.StackTrace}"));
            }
        }

        [TestMethod()]
        [DataRow(data1: new object[] { CopyActionFlags.Default, SelectionFlags.Default, LoggingActionFlags.ReportExtraFiles }, DisplayName = "Defaults")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectories, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Subdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectoriesIncludingEmpty, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "EmptySubdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.Mirror, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Mirror")]
        public void FileInclusionTest(object[] flags) //CopyActionFlags copyAction, SelectionFlags selectionFlags, LoggingActionFlags loggingAction
        {
            CopyActionFlags copyAction = (CopyActionFlags)flags[0];
            SelectionFlags selectionFlags = (SelectionFlags)flags[1];
            LoggingActionFlags loggingAction = (LoggingActionFlags)flags[2] | LoggingActionFlags.ReportExtraFiles;

            var rc = TestPrep.GetRoboCommand(false, copyAction, selectionFlags, loggingAction);
            var crc = TestPrep.GetCachedRoboCopy(rc);
            rc.LoggingOptions.ListOnly = true;
            rc.CopyOptions.FileFilter = new string[] { "*.txt" };
            var results1 = TestPrep.RunTests(rc, crc, false).Result;
            AssertResults(results1, rc.LoggingOptions.ListOnly);

            crc.LoggingOptions.ListOnly = false;
            var results2 = TestPrep.RunTests(rc, crc, true).Result;
            AssertResults(results2, rc.LoggingOptions.ListOnly);
        }

        [TestMethod()]
        [DataRow(data1: new object[] { CopyActionFlags.Default, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Defaults")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectories, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Subdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectoriesIncludingEmpty, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "EmptySubdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.Mirror, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Mirror")]
        public void FileExclusionTest(object[] flags) //CopyActionFlags copyAction, SelectionFlags selectionFlags, LoggingActionFlags loggingAction
        {
            CopyActionFlags copyAction = (CopyActionFlags)flags[0];
            SelectionFlags selectionFlags = (SelectionFlags)flags[1];
            LoggingActionFlags loggingAction = (LoggingActionFlags)flags[2];

            var rc = TestPrep.GetRoboCommand(false, copyAction, selectionFlags, loggingAction);
            var crc = TestPrep.GetCachedRoboCopy(rc);
            rc.LoggingOptions.ListOnly = true;
            rc.SelectionOptions.ExcludedFiles.Add("*.txt");
            var results1 = TestPrep.RunTests(rc, crc, false).Result;
            AssertResults(results1, rc.LoggingOptions.ListOnly);

            rc.LoggingOptions.ListOnly = false;
            var results2 = TestPrep.RunTests(rc, crc, true).Result;
            AssertResults(results2, rc.LoggingOptions.ListOnly);
        }


        [TestMethod()]
        [DataRow(data1: new object[] { CopyActionFlags.Default, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Defaults")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectories, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Subdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectoriesIncludingEmpty, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "EmptySubdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.Mirror, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Mirror")]
        public void ExtraFileTest(object[] flags) //CopyActionFlags copyAction, SelectionFlags selectionFlags, LoggingActionFlags loggingAction
        {
            CopyActionFlags copyAction = (CopyActionFlags)flags[0];
            SelectionFlags selectionFlags = (SelectionFlags)flags[1];
            LoggingActionFlags loggingAction = (LoggingActionFlags)flags[2];

            var rc = TestPrep.GetRoboCommand(false, copyAction, selectionFlags, loggingAction);
            var crc = TestPrep.GetCachedRoboCopy(rc);
            rc.LoggingOptions.ListOnly = true;
            
            var results1 = TestPrep.RunTests(rc, crc, false, CreateFile).Result;
            AssertResults(results1, rc.LoggingOptions.ListOnly);

            rc.LoggingOptions.ListOnly = false;
            var results2 = TestPrep.RunTests(rc, crc, true, CreateFile).Result;
            AssertResults(results2, rc.LoggingOptions.ListOnly);
            TestPrep.CleanDestination();
            void CreateFile()
            {
                Directory.CreateDirectory(TestPrep.DestDirPath);
                string path = Path.Combine(TestPrep.DestDirPath, "ExtraFileTest.txt");
                if (!File.Exists(path))
                    File.WriteAllText(path, "This is an extra file");
            }
        }

        [TestMethod()]
        [DataRow(data1: new object[] { CopyActionFlags.Default, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Defaults")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectories, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Subdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.CopySubdirectoriesIncludingEmpty, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "EmptySubdirectories")]
        [DataRow(data1: new object[] { CopyActionFlags.Mirror, SelectionFlags.Default, LoggingActionFlags.Default }, DisplayName = "Mirror")]
        public void SameFileTest(object[] flags) //CopyActionFlags copyAction, SelectionFlags selectionFlags, LoggingActionFlags loggingAction
        {
            CopyActionFlags copyAction = (CopyActionFlags)flags[0];
            SelectionFlags selectionFlags = (SelectionFlags)flags[1];
            LoggingActionFlags loggingAction = (LoggingActionFlags)flags[2];

            var rc = TestPrep.GetRoboCommand(false, copyAction, selectionFlags, loggingAction);
            var crc = TestPrep.GetCachedRoboCopy(rc);
            rc.LoggingOptions.ListOnly = true;

            var results1 = TestPrep.RunTests(rc, crc, false, CreateFile).Result;
            AssertResults(results1, rc.LoggingOptions.ListOnly);

            rc.LoggingOptions.ListOnly = false;
            var results2 = TestPrep.RunTests(rc, crc, true, CreateFile).Result;
            AssertResults(results2, rc.LoggingOptions.ListOnly);
            TestPrep.CleanDestination();

            void CreateFile()
            {
                Directory.CreateDirectory(TestPrep.DestDirPath);
                string fn = "1024_Bytes.txt";
                string dest = Path.Combine(TestPrep.DestDirPath, fn);
                if (!File.Exists(dest))
                    File.Copy(Path.Combine(TestPrep.SourceDirPath, fn), dest);
            }
        }


    }
}