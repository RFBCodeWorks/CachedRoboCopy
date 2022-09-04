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
            bool copySuccess = false; ;
            copier.CopyCompleted += (o, e) => copySuccess = true;
            copier.Copy().Wait();
            Assert.IsTrue(copySuccess);
        }

        //[TestMethod()]
        //public void UpdateTest()
        //{
        //    throw new NotImplementedException();
        //}

        //[TestMethod()]
        //public void Copy_ExcludeNewerTest()
        //{
        //    throw new NotImplementedException();
        //}

        //[TestMethod()]
        //public void Copy_ExcludeOlderTest()
        //{
        //    throw new NotImplementedException();
        //}

        //[TestMethod()]
        //public void MoveTest()
        //{
        //    throw new NotImplementedException();
        //}
    }
}