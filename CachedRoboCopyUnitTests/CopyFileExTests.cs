using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using RFBCodeWorks.RoboSharpExtensions.Tests;

namespace RFBCodeWorks.RoboSharpExtensions.CopyFileEx.Tests
{
    [TestClass()]
    public class CopyFileExTests
    {

        string SourcePath => Path.Combine(TestPrep.CopyFileExTestSourcePath, @"TestFileSource.txt");
        string DestPath => Path.Combine(TestPrep.SourceDirPath, @"TestFileDest.txt");

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


        [TestMethod()]
        public void CopySuccessTest()
        {
            bool pbCancel = false;
            bool result = FileCopyEx.CopyFile(SourcePath, DestPath, null, ref pbCancel, CopyFileFlags.COPY_FILE_RESTARTABLE);
            Assert.IsTrue(result);
        }

        [TestMethod()]
        public void CancelTest1()
        {
            bool pbCancel = true;
            bool result = FileCopyEx.CopyFile(SourcePath, DestPath, null, ref pbCancel, CopyFileFlags.COPY_FILE_RESTARTABLE);
            Assert.IsFalse(result);
        }

        [TestMethod()]
        public void CancelTest2()
        {
            bool pbCancel = false;
            bool result = FileCopyEx.CopyFile(SourcePath, DestPath, new FileCopyEx.CopyProgressRoutine((a,b,c,d,e,f,g,h,i) => CopyProgressResult.PROGRESS_CANCEL), ref pbCancel, CopyFileFlags.COPY_FILE_RESTARTABLE);
            Assert.IsFalse(result);
        }

        [TestMethod()]
        public void SourceIsLockedTest()
        {
            bool pbCancel = false;
            using (var writer = File.OpenWrite(SourcePath))
            {
                writer.Lock(0, 10000); // Lock the file to have the copy fail to verify that a false result is returned due to failure
                Assert.ThrowsException<IOException>(CopySuccessTest);
                WriteException(CopySuccessTest); //write the result
                writer.Unlock(0, 10000);
            }
        }

        [TestMethod()]
        public void DestIsLockedTest()
        {
            bool pbCancel = false;
            Directory.CreateDirectory(TestPrep.DestDirPath);
            using (var writer = File.OpenWrite(DestPath))
            {
                writer.Lock(0, 10000); // Lock the file to have the copy fail to verify that a false result is returned due to failure
                void Copy() => FileCopyEx.CopyFile(SourcePath, DestPath, null, ref pbCancel, CopyFileFlags.COPY_FILE_RESTARTABLE);
                Assert.ThrowsException<IOException>(Copy);
                WriteException(Copy); //write the result
                writer.Unlock(0, 10000);
            }
        }

        void WriteException(Action action)
        {
            try
            {
                action();
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        }

        [TestMethod()]
        public void CopyFileIfMissingTest()
        {
            CopySuccessTest();
            bool pbCancel = false;
            bool result = FileCopyEx.CopyFileIfMissing(SourcePath, DestPath, null, ref pbCancel);
            Assert.IsFalse(result); // Returns FALSE is the file already exists, since no copy operation was performed
        }
    }
}