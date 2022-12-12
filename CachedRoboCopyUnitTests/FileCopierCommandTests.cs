using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RFBCodeWorks.RoboSharpExtensions.Tests
{
    [TestClass()]
    public class FileCopierCommandTests
    {

        string SourcePath => Path.Combine(TestPrep.CopyFileExTestSourcePath, @"TestFileSource.txt");
        string DestPath => Path.Combine(TestPrep.DestDirPath, @"TestFileDest.txt");

        [TestInitialize]
        public void TestInit()
        {
            Directory.CreateDirectory(TestPrep.SourceDirPath);
            File.WriteAllText(SourcePath, "THIS IS A FUN FILE");
            Command = new FileCopierCommand(new FileCopier(SourcePath, DestPath));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            TestPrep.CleanDestination();
        }

        [TestMethod()]
        public void FileCopierCommandTest()
        {
            var fc = new FileCopierCommand();
            Assert.IsNotNull(fc);
        }

        FileCopierCommand Command;

        [TestMethod()]
        public void FileCopierCommandTest1()
        {
            var fc = Command;
            Assert.IsNotNull(fc);
            Assert.AreEqual(1, fc.Count());
        }

        [TestMethod()]
        public void DisposeTest()
        {
            Command.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => Command.Start());
        }

        [TestMethod()]
        public void GetResultsTest()
        {
            Command.Start().Wait();
            var results = Command.GetResults();
            Assert.IsNotNull(results, "Results object is null :(");
            Assert.IsInstanceOfType(results, typeof(RoboSharp.Results.RoboCopyResults));
            Assert.AreEqual(1, results.FilesStatistic.Copied);
            Console.WriteLine(results.LogLines);
        }

        [TestMethod()]
        public void StandardEventTests()
        {
            bool CommandComplete = false, CopyProg = false, FileProcessed = false, est = false;

            Command.OnCommandCompleted += (O, E) => EventAcknowledge("Command Completed", ref CommandComplete);
            Command.OnCopyProgressChanged += (O, E) => EventAcknowledge("CopyProgress", ref CopyProg);
            Command.OnFileProcessed += (O, E) => EventAcknowledge("FileProcessed", ref FileProcessed);
            Command.OnProgressEstimatorCreated += (O, E) => EventAcknowledge("EstimatorCreated", ref est);
            Command.Start().Wait();

            Assert.IsTrue(est, "ProgressEstimator event not raised!");
            Assert.IsTrue(FileProcessed, "File Processed event not raised!");
            Assert.IsTrue(CopyProg, "CopyProgress event not raised!");
            Assert.IsTrue(CommandComplete, "CommandComplete event not raised!");    
        }

        void EventAcknowledge(string eventName, ref bool b)
        {
            b = true;
            Console.WriteLine(eventName + " Event Raised!");
        }

        [TestMethod()]
        public void ErrorEventTest()
        {
            bool Error = false;
            Command.OnError += (O, E) => EventAcknowledge("Error Event Raised", ref Error);
            Directory.CreateDirectory(TestPrep.DestDirPath);
            using (var stream = File.OpenWrite(DestPath))
            {
                Command.Start().Wait();
            }
            Assert.IsTrue(Error, "OnError event not raised!");
        }
    }
}


