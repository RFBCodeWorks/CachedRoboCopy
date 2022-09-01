using System;
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

namespace RFBCodeWorks.CachedRoboCopy
{
    /// <summary>
    /// ResultsBuilder object for custom IRoboCommand implementations
    /// </summary>
    public class ResultsBuilder
    {
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

        private IRoboCommand Command { get; }
        private ProgressEstimator ProgressEstimatorField;
        
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
        protected DateTime EndTime { get; set; }

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

        /// <summary>
        /// Gets an array of all the log lines currently logged
        /// </summary>
        public string[] CurrentLogLines => LogLines.ToArray();

        private List<string> LogLines { get; } = new();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="isListOperation">TRUE if this is a ListOperation - if true, adds to the log lines, if false does not add to log lines.</param>
        public virtual void AddFile(ProcessedFileInfo file, bool isListOperation)
        {
            ProgressEstimator.AddFile(file, !isListOperation);
            if (isListOperation) LogFileInfo(file);
        }

        /// <summary>
        /// Mark an file as Copied
        /// </summary>
        /// <param name="file"></param>
        public virtual void AddFileCopied(ProcessedFileInfo file)
        {
            ProgressEstimator.AddFileCopied(file);
            LogFileInfo(file, "-- OK");
        }

        /// <summary>
        /// Mark an file as SKIPPED
        /// </summary>
        /// <param name="file"></param>
        public virtual void AddFileSkipped(ProcessedFileInfo file)
        {
            ProgressEstimator.PerformByteCalc(file, WhereToAdd.Skipped);
            LogFileInfo(file, "-- Skipped");
        }

        /// <summary>
        /// Mark an file as PURGED
        /// </summary>
        /// <param name="file"></param>
        public virtual void AddFilePurged(ProcessedFileInfo file)
        {
            ProgressEstimator.PerformByteCalc(file, WhereToAdd.Extra);
            LogFileInfo(file, "-- Purged");
        }

        private void LogFileInfo(ProcessedFileInfo file, string suffix = "")
        {
            //Check to log the directory listing
            if (!Command.LoggingOptions.NoFileList)
                WriteToLogs(file.ToString(Command.LoggingOptions) + suffix);
        }

        /// <summary>
        /// Add a directory to the 
        /// </summary>
        /// <param name="file"></param>
        public void AddDir(ProcessedFileInfo file)
        {
            ProgressEstimator.AddDir(file);
            //Check to log the directory listing
            if (!Command.LoggingOptions.NoDirectoryList)
                WriteToLogs(file.ToString(Command.LoggingOptions));
        }

        /// <summary>
        /// Adds a System Message to the logs
        /// </summary>
        /// <param name="info"></param>
        public void AddSystemMessage(ProcessedFileInfo info) => WriteToLogs(info.FileClass);

        /// <summary>
        /// Adds a System Message to the logs
        /// </summary>
        /// <param name="info"></param>
        public void AddSystemMessage(string info) => WriteToLogs(info);

        const string Divider = "------------------------------------------------------------------------------";

        /// <summary>
        /// Write the header to the log
        /// </summary>
        protected virtual void CreateHeader()
        {
            Command.LoggingOptions.DeleteLogFiles();
            if (!Command.LoggingOptions.NoJobHeader)
            {
                WriteToLogs(Divider);
                WriteToLogs("   RFBCodeWorks.CachedRoboCopy :: \t An alternative to RoboCopy");
                WriteToLogs(Divider);
                WriteToLogs("");
                WriteToLogs($"  Started : {StartTime.ToLongDateString()} {StartTime.ToLongTimeString()}");
                WriteToLogs($"   Source : {Command.CopyOptions.Source}");
                WriteToLogs($"     Dest : {Command.CopyOptions.Source}");
                WriteToLogs("");
                if (Command.CopyOptions.FileFilter.Any())
                    WriteToLogs($"    Files : {String.Concat(Command.CopyOptions.FileFilter.Select(filter => filter + " "))}");
                else
                    WriteToLogs($"    Files : *.*");

                if (Command.SelectionOptions.ExcludedFiles.Any())
                {
                    WriteToLogs($"    Excluded Files : {String.Concat(Command.SelectionOptions.ExcludedFiles.Select(filter => filter + " "))}");
                    WriteToLogs("");
                }

                if (Command.SelectionOptions.ExcludedDirectories.Any())
                {
                    WriteToLogs($"    Excluded Files : {String.Concat(Command.SelectionOptions.ExcludedDirectories.Select(filter => filter + " "))}");
                    WriteToLogs("");
                }

                WriteToLogs("");
                WriteToLogs($"  Options : {Command.CommandOptions}");
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
            string SummaryLine() => string.Format("\t{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", "\t", RightAlign(ColSizes[0],"Total"), RightAlign(ColSizes[1], "Copied"), RightAlign(ColSizes[2], "Skipped"), RightAlign(ColSizes[3], "Mismatch"), RightAlign(ColSizes[4], "FAILED"), RightAlign(ColSizes[5], "Extras"));
            string Tabulator(string name, IStatistic stat) => string.Format("\t{0} : {1}\t{2}\t{3}\t{4}\t{5}\t{6}", name, Align(ColSizes[0], stat.Total), Align(ColSizes[1], stat.Copied), Align(ColSizes[2], stat.Skipped), Align(ColSizes[3], stat.Mismatch), Align(ColSizes[4], stat.Failed), Align(ColSizes[5], stat.Extras));

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
                if (!Command.LoggingOptions.ListOnly) WriteToLogs($"\t{AverageSpeed}");
                
            }
            IsSummaryCreated = true;
        }

        /// <summary>
        /// Get the results
        /// </summary>
        public virtual RoboCopyResults GetResults()
        {
            CreateSummary();
            return ProgressEstimator.GetResults(StartTime, EndTime < StartTime ? DateTime.Now :EndTime, AverageSpeed, LogLines.ToArray());
        }

        /// <summary>
        /// Add the lines to the log lines, and also write it to the output logs
        /// </summary>
        /// <param name="lines"></param>
        protected virtual void WriteToLogs(params string[] lines)
        {
            LogLines.AddRange(lines);
            Command.LoggingOptions.AppendToLogs(lines);
        }

    }
}
