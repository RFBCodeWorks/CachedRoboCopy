using RoboSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RFBCodeWorks.CachedRoboCopy
{
    /// <summary>
    /// Args for FileCopier
    /// </summary>
    public class FileCopyProgressUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="sourceFile"></param>
        /// <param name="destinationFile"></param>
        /// <param name="destInfo">ProcessedFileInfo object about the destination file</param>
        /// <param name="sourceDirInfo">ProcessedFileInfo object about the Source Directory - Compatibility for RoboSharp</param>
        public FileCopyProgressUpdatedEventArgs(double progress, FileInfo sourceFile, FileInfo destinationFile, ProcessedFileInfo destInfo, ProcessedFileInfo sourceDirInfo)
        {
            Progress = progress;
            SourceFileInfo= sourceFile;
            DestinationFileInfo = destinationFile;
            RoboSharpFileInfo = destInfo ?? new ProcessedFileInfo() { FileClass = "", FileClassType = FileClassType.File , Name = DestinationFileInfo.Name, Size = SourceFileInfo.Length };
            RoboSharpDirInfo = sourceDirInfo ?? new ProcessedFileInfo() { FileClass = "", FileClassType = FileClassType.NewDir, Name = Path.GetFileName(SourceFileInfo.DirectoryName), Size = 1 };
            //if (destInfo is null) destInfo = RoboSharpFileInfo;
            //if (sourceDirInfo is null) sourceDirInfo = SourceDirInfo;
        }

        /// <summary>
        /// The current progress of the file being copied
        /// </summary>
        public double Progress { get; }

        /// <summary>
        /// Property to allow easy integration into RoboSharp
        /// </summary>
        public ProcessedFileInfo RoboSharpFileInfo { get; }

        /// <summary>
        /// The RoboSharp ProcessedFileInfo object
        /// </summary>
        public  ProcessedFileInfo RoboSharpDirInfo { get; }

        /// <summary>
        /// The Source FileInfo
        /// </summary>
        public FileInfo SourceFileInfo { get; }

        /// <summary>
        /// The Destination FileInfo
        /// </summary>
        public FileInfo DestinationFileInfo{ get; }

    }
}
