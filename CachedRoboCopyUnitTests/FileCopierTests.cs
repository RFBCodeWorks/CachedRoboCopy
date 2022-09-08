using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RoboSharp.Extensions;
using RFBCodeWorks.CachedRoboCopy;

namespace RFBCodeWorks.CachedRoboCopy.Tests
{
    [TestClass()]
    public class FileCopierTests
    {

        string SourcePath => Path.Combine(TestPrep.CopyFileExTestSourcePath, @"TestFileSource.txt");
        string DestPath => Path.Combine(TestPrep.DestDirPath, @"TestFileDest.txt");

        [TestInitialize]
        public void TestInit()
        {
            TestPrep.CleanDestination();
            Directory.CreateDirectory(TestPrep.SourceDirPath);
            File.WriteAllText(SourcePath, "THIS IS A FUN FILE");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            TestPrep.CleanDestination();
        }

        private FileCopier GetFileCopier() => new FileCopier(SourcePath, DestPath);


        [TestMethod()]
        public void CopyTest()
        {
            var copier = GetFileCopier();
            bool copySuccess = false, progress = false;
            var failMessage = "";
            copier.CopyCompleted += (o, e) => copySuccess = true;
            copier.CopyProgressUpdated += (o, e) => progress = true;
            copier.CopyFailed += (o, e) => failMessage = e.Error;
            bool result = copier.Copy().Result;
            Assert.IsTrue(result, "File Copy Failed! --> " + failMessage);
            Assert.IsTrue(copySuccess, "CopyCompleted event was not raised");
            Assert.IsTrue(progress, "CopyProgressUpdated event was not raised");
            Assert.IsTrue(copier.Source.Exists, "Source does not exist");
            Assert.IsTrue(copier.Destination.Exists, "Destination does not exist");
            Assert.AreEqual(100, copier.Progress, "Progress not equal to 100%");
        }

        [TestMethod()]
        public void MoveTest()
        {
            var copier = GetFileCopier();
            bool copySuccess = false, progress = false;
            var failMessage = "";
            copier.CopyCompleted += (o, e) => copySuccess = true;
            copier.CopyProgressUpdated += (o, e) => progress = true;
            copier.CopyFailed += (o, e) => failMessage = e.Error;
            bool result = copier.Move().Result;
            Assert.IsTrue(result, "File Copy Failed! --> " + failMessage);
            Assert.IsTrue(copySuccess, "CopyCompleted event was not raised");
            Assert.IsTrue(progress, "CopyProgressUpdated event was not raised");
            Assert.IsFalse(copier.Source.Exists, "Source still exists");
            Assert.IsTrue(copier.Destination.Exists, "Destination does not exist");
            Assert.AreEqual(100, copier.Progress, "Progress not equal to 100%");
        }

        [TestMethod()]
        public void ErrorTest()
        {
            var copier = GetFileCopier();
            bool copySuccess = false, progress = false;
            var failMessage = "";
            Exception err = null;
            copier.CopyCompleted += (o, e) => copySuccess = true;
            copier.CopyProgressUpdated += (o, e) => progress = true;
            copier.CopyFailed += (o, e) =>
            {
                failMessage = e.Error;
                err = e.Exception;
            };
            bool result = true;
            copier.Destination.Directory.Create();

            using (var stream = copier.Destination.OpenWrite())
            {
                result = copier.Copy(true).Result;
            }
            Assert.IsFalse(result, "Copier returned true when copy should have failed");
            Assert.AreNotEqual("", failMessage, "CopyFailed message is empty!");
            Assert.IsNotNull(err, "Exception Data Not Reported!");

        }
    }
}