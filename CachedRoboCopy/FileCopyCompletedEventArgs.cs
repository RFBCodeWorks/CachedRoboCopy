﻿using RoboSharp;
using RoboSharp.Extensions;
using RoboSharp.Results;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RFBCodeWorks.RoboSharpExtensions
{
    /// <summary>
    /// Occurs when FileCopy completes copying - Occurrs after CopyProgress 100 occurrs
    /// </summary>
    public class FileCopyCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePair"></param>
        /// <param name="destInfo">ProcessedFileInfo object about the destination file</param>
        /// <param name="sourceDirInfo">ProcessedFileInfo object about the Source Directory - Compatibility for RoboSharp</param>
        /// <param name="Start"/>
        /// <param name="End"/>
        public FileCopyCompletedEventArgs(IFilePair filePair, DateTime Start, DateTime End, ProcessedFileInfo destInfo, ProcessedFileInfo sourceDirInfo)
        {
            if (End < Start) throw new ArgumentException("End Time cannot be less than Start Time", nameof(End));
            SourceFileInfo = filePair?.Source ?? throw new ArgumentNullException(nameof(filePair.Source));
            DestinationFileInfo = filePair?.Destination ?? throw new ArgumentNullException(nameof(filePair.Destination));
            
            StartTime = Start;
            EndTime = End;
            TimeSpan = EndTime - StartTime;
            RoboSharpFileInfo = destInfo;
            SourceDirInfo = sourceDirInfo;
            Speed = new SpeedStatistic(filePair.GetFileLength(), TimeSpan);
        }

        /// <summary>
        /// Property to allow easy integration into RoboSharp
        /// </summary>
        public ProcessedFileInfo RoboSharpFileInfo { get; }

        internal ProcessedFileInfo SourceDirInfo;

        /// <summary>
        /// The Source FileInfo
        /// </summary>
        public FileInfo SourceFileInfo { get; }

        /// <summary>
        /// The Destination FileInfo
        /// </summary>
        public FileInfo DestinationFileInfo { get; }

        /// <summary> </summary>
        public DateTime StartTime { get; }

        /// <summary> </summary>
        public DateTime EndTime { get; }

        /// <summary> </summary>
        public TimeSpan TimeSpan { get; }

        /// <summary>  </summary>
        public SpeedStatistic Speed { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public RoboSharp.CopyProgressEventArgs ToRoboSharpCopyProgressEventArgs()
        {
            return new CopyProgressEventArgs(100, RoboSharpFileInfo, SourceDirInfo);
        }
    }
}
