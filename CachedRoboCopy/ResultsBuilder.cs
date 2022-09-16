﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RoboSharp;
using RoboSharp.Extensions;
using RoboSharp.Interfaces;
using RoboSharp.Results;
using RoboSharp.EventArgObjects;
using static RoboSharp.Results.ProgressEstimator;

namespace RFBCodeWorks.RoboSharpExtensions
{
    /// <summary>
    /// ResultsBuilder object for custom IRoboCommand implementations
    /// </summary>
    public class ResultsBuilder
    {
        #region < Constructor >

        /// <summary>
        /// Create a new  results builder
        /// </summary>
        /// <param name="cmd"></param>
        public ResultsBuilder(IRoboCommand cmd)
        {
            ProgressEstimator = new ProgressEstimator(cmd);
            StartTime = DateTime.Now;
            Command = cmd;
            CreateHeader();
        }

        #endregion

        #region < Properties >

        /// <summary>
        /// The associated <see cref="IRoboCommand"/> object
        /// </summary>
        protected IRoboCommand Command { get; }
        private ProgressEstimator ProgressEstimatorField;
        
        /// <summary>
        /// The private collection of log lines
        /// </summary>
        protected List<string> LogLines { get; } = new();

        /// <summary>
        /// Gets an array of all the log lines currently logged
        /// </summary>
        public string[] CurrentLogLines => LogLines.ToArray();

        /// <summary>
        /// Used to calculate the average speed, and is supplied to the results object when getting results.
        /// </summary>
        public AverageSpeedStatistic AverageSpeed { get; } = new();

        /// <summary>
        /// The time the ResultsBuilder was instantiated
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// End Time is set when the summary is created.
        /// </summary>
        public DateTime EndTime { get; protected set; }

        /// <summary>
        /// Flag to prevent writing the summary to the log multiple times
        /// </summary>
        protected bool IsSummaryCreated { get; set; }

        /// <summary>
        /// The ProgressEstimator object that will be used to calculate the statistics objects
        /// </summary>
        public ProgressEstimator ProgressEstimator
        {
            get { return ProgressEstimatorField; }
            set { ProgressEstimatorField = value; }
        }

        #endregion

        #region < Add Files >

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        public virtual void AddFile(ProcessedFileInfo file)
        {
            ProgressEstimator.AddFile(file);
            if (Command.LoggingOptions.ListOnly) LogFileInfo(file);
        }

        /// <summary>
        /// Adds the file to the ProgressEstimator, then sets that the copy operation is started
        /// </summary>
        /// <param name="file"></param>
        public virtual void SetCopyOpStarted(ProcessedFileInfo file)
        {
            ProgressEstimator.AddFile(file);
            ProgressEstimator.SetCopyOpStarted();
        }

        /// <summary>
        /// Mark an file as Copied
        /// </summary>
        /// <param name="file"></param>
        public virtual void AddFileCopied(ProcessedFileInfo file)
        {
            ProgressEstimator.AddFileCopied(file);
            LogFileInfo(file, " -- OK");
        }

        /// <summary>
        /// Mark an file as SKIPPED
        /// </summary>
        /// <param name="file"></param>
        public virtual void AddFileSkipped(ProcessedFileInfo file)
        {
            ProgressEstimator.AddFileSkipped(file);
            LogFileInfo(file, " -- Skipped");
        }

        /// <summary>
        /// Mark an file as FAILED
        /// </summary>
        public virtual void AddFileFailed(ProcessedFileInfo file)
        {
            ProgressEstimator.AddFileFailed(file);
            LogFileInfo(file, " -- FAILED");
        }

        /// <summary>
        /// Mark an file as PURGED
        /// </summary>
        /// <param name="file"></param>
        public virtual void AddFilePurged(ProcessedFileInfo file)
        {
            ProgressEstimator.AddFile(file);
            LogFileInfo(file, " -- Purged");
        }

        /// <summary>
        /// Write the <paramref name="file"/> and <paramref name="suffix"/> to the logs
        /// </summary>
        /// <param name="file"></param>
        /// <param name="suffix"></param>
        protected virtual void LogFileInfo(ProcessedFileInfo file, string suffix = "")
        {
            //Check to log the directory listing
            if (!Command.LoggingOptions.NoFileList)
                WriteToLogs(file.ToString(Command.LoggingOptions) + suffix);
        }

        #endregion

        #region < Add Dirs >

        private ProcessedFileInfo LastDir;


        /// <summary>
        /// Add a directory to the log and progressEstimator
        /// </summary>
        public virtual void AddDir(ProcessedFileInfo dir)
        {
            LastDir = dir;
            ProgressEstimator.AddDir(dir);
            //Check to log the directory listing
            if (!Command.LoggingOptions.NoDirectoryList)
                WriteToLogs(dir.ToString(Command.LoggingOptions));
        }

        #endregion

        #region < Add System Message >

        /// <summary>
        /// Adds a System Message to the logs
        /// </summary>
        /// <param name="info"></param>
        public virtual void AddSystemMessage(ProcessedFileInfo info) => WriteToLogs(info.FileClass);

        /// <summary>
        /// Adds a System Message to the logs
        /// </summary>
        /// <param name="info"></param>
        public virtual void AddSystemMessage(string info) => WriteToLogs(info);

        #endregion

        #region < Create Header / Summary >

        /// <summary>
        /// Divider string that can be used
        /// </summary>
        protected const string Divider = "------------------------------------------------------------------------------";

        /// <summary>
        /// RoboCopy uses padding of 9 on the header to align column details, such as the 'Started' time and 'Source' string
        /// </summary>
        /// <param name="RowName"></param>
        /// <returns>A padded string</returns>
        protected string PadHeader(string RowName) => RowName.PadLeft(9);

        /// <summary>
        /// Write the header to the log
        /// </summary>
        protected virtual void CreateHeader()
        {
            Command.LoggingOptions.DeleteLogFiles();
            if (!Command.LoggingOptions.NoJobHeader)
            {
                
                WriteToLogs(Divider);
                WriteToLogs("   RFBCodeWorks.RoboSharpExtensions :: \t An alternative to RoboCopy");
                WriteToLogs(Divider);
                WriteToLogs("");
                WriteToLogs($"{PadHeader("Started")} : {StartTime.ToLongDateString()} {StartTime.ToLongTimeString()}");
                WriteToLogs($"{PadHeader("Source")} : {Command.CopyOptions.Source}");
                WriteToLogs($"{PadHeader("Dest")} : {Command.CopyOptions.Destination}");
                WriteToLogs("");
                if (Command.CopyOptions.FileFilter.Any())
                    WriteToLogs($"{PadHeader("Files")} : {String.Concat(Command.CopyOptions.FileFilter.Select(filter => filter + " "))}");
                else
                    WriteToLogs($"{PadHeader("Files")} : *.*");
                WriteToLogs("");
                
                if (Command.SelectionOptions.ExcludedFiles.Any())
                {
                    WriteToLogs($"{PadHeader("Exc Files")} : {String.Concat(Command.SelectionOptions.ExcludedFiles.Select(filter => filter + " "))}");
                    WriteToLogs("");
                }

                if (Command.SelectionOptions.ExcludedDirectories.Any())
                {
                    WriteToLogs($"{PadHeader("Exc Dirs")} : {String.Concat(Command.SelectionOptions.ExcludedDirectories.Select(filter => filter + " "))}");
                    WriteToLogs("");
                }

                WriteToLogs("");
                WriteToLogs($"{PadHeader("Options")} : {Command.CommandOptions}");
                WriteToLogs("");
                WriteToLogs(Divider);
                WriteToLogs("");
            }
        }

        /// <summary>
        /// Write the summary to the log
        /// </summary>
        protected virtual void CreateSummary()
        {
            int[] GetColumnSizes() 
            {
                var sizes = new List<int>();
                int GetColumnSize(string name, long bytes, long files, long dirs)
                {
                    int GetLargerValue(int length1, int length2) => length1 > length2 ? length1 : length2;
                    int length = GetLargerValue(name.Length, bytes.ToString().Length);
                    length = GetLargerValue(length, files.ToString().Length);
                    return GetLargerValue(length, dirs.ToString().Length);
                }
                sizes.Add(GetColumnSize("Total", ProgressEstimator.BytesStatistic.Total, ProgressEstimator.FilesStatistic.Total, ProgressEstimator.DirectoriesStatistic.Total));
                sizes.Add(GetColumnSize("Copied", ProgressEstimator.BytesStatistic.Copied, ProgressEstimator.FilesStatistic.Copied, ProgressEstimator.DirectoriesStatistic.Copied));
                sizes.Add(GetColumnSize("Skipped", ProgressEstimator.BytesStatistic.Skipped, ProgressEstimator.FilesStatistic.Skipped, ProgressEstimator.DirectoriesStatistic.Skipped));
                sizes.Add(GetColumnSize("Mismatch", ProgressEstimator.BytesStatistic.Mismatch, ProgressEstimator.FilesStatistic.Mismatch, ProgressEstimator.DirectoriesStatistic.Mismatch));
                sizes.Add(GetColumnSize("Failed", ProgressEstimator.BytesStatistic.Failed, ProgressEstimator.FilesStatistic.Failed, ProgressEstimator.DirectoriesStatistic.Failed));
                sizes.Add(GetColumnSize("Extras", ProgressEstimator.BytesStatistic.Extras, ProgressEstimator.FilesStatistic.Extras, ProgressEstimator.DirectoriesStatistic.Extras));
                return sizes.ToArray();
            }
            string RightAlign(int columnSize, string value)
            {
                return value.PadLeft(columnSize);
            }
            string Align(int columnSize, long value) => RightAlign(columnSize, value.ToString());

            int[] ColSizes = GetColumnSizes();
            string SummaryLine() => string.Format("    {0}{1}\t{2}\t{3}\t{4}\t{5}\t{6}", PadHeader(""), RightAlign(ColSizes[0],"Total"), RightAlign(ColSizes[1], "Copied"), RightAlign(ColSizes[2], "Skipped"), RightAlign(ColSizes[3], "Mismatch"), RightAlign(ColSizes[4], "FAILED"), RightAlign(ColSizes[5], "Extras"));
            string Tabulator(string name, IStatistic stat) => string.Format("{0} : {1}\t{2}\t{3}\t{4}\t{5}\t{6}", PadHeader(name), Align(ColSizes[0], stat.Total), Align(ColSizes[1], stat.Copied), Align(ColSizes[2], stat.Skipped), Align(ColSizes[3], stat.Mismatch), Align(ColSizes[4], stat.Failed), Align(ColSizes[5], stat.Extras));

            if (IsSummaryCreated) return;
            EndTime = DateTime.Now;

            if (!Command.LoggingOptions.NoJobSummary)
            {
                ProgressEstimator.FinalizeResults();
                WriteToLogs("");
                WriteToLogs(Divider);
                WriteToLogs("");
                WriteToLogs(SummaryLine());
                WriteToLogs(Tabulator(" Dirs", ProgressEstimator.DirectoriesStatistic));
                WriteToLogs(Tabulator("Files", ProgressEstimator.FilesStatistic));
                WriteToLogs(Tabulator("Bytes", ProgressEstimator.BytesStatistic));
                WriteToLogs("");
                WriteToLogs($"\tEnded : {EndTime.ToLongDateString()} {EndTime.ToLongTimeString()}");
                TimeSpan totalTime = EndTime - StartTime;
                WriteToLogs($"\tTotal Time: {totalTime.Hours} hours, {totalTime.Minutes} minutes, {totalTime.Seconds}.{totalTime.Milliseconds} seconds");
                if (!Command.LoggingOptions.ListOnly)
                {
                    WriteToLogs("");
                    WriteToLogs($"\tSpeed: {AverageSpeed.GetBytesPerSecond()}");
                    WriteToLogs($"\tSpeed: { AverageSpeed.GetMegaBytesPerMin()}");
                }
                WriteToLogs("");
                WriteToLogs(Divider);
                WriteToLogs("");

            }
            IsSummaryCreated = true;
        }

        #endregion

        #region < Get Results / Write to Logs >

        /// <summary>
        /// Add the lines to the log lines, and also write it to the output logs
        /// </summary>
        /// <param name="lines"></param>
        protected virtual void WriteToLogs(params string[] lines)
        {
            lock (LogLines)
            {
                LogLines.AddRange(lines);
                Command.LoggingOptions.AppendToLogs(lines);
            }
        }

        /// <summary>
        /// Get the results
        /// </summary>
        public virtual RoboCopyResults GetResults()
        {
            CreateSummary();
            return ProgressEstimator.GetResults(StartTime, EndTime < StartTime ? DateTime.Now :EndTime, AverageSpeed, LogLines.ToArray());
        }

        #endregion

    }
}
