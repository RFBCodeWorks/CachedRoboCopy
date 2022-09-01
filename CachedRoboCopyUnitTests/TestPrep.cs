using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoboSharp;
using RoboSharp.Interfaces;
using RoboSharp.Tests;

namespace RFBCodeWorks.CachedRoboCopy.Tests
{
    public static class TestPrep
    {

        /// <inheritdoc cref="Test_Setup.ClearOutTestDestination"/>
        public static void CleanDestination()
        {
            Test_Setup.ClearOutTestDestination();
        }

        /// <inheritdoc cref="Test_Setup.WriteLogLines(RoboSharp.Results.RoboCopyResults, bool)"/>
        public static void WriteLogLines(RoboSharp.Results.RoboCopyResults results, bool summaryOnly = false)
        {
            Test_Setup.WriteLogLines(results, summaryOnly);
        }

        /// <inheritdoc cref="Test_Setup.GenerateCommand(bool, bool)"/>
        public static RoboCommand GetRoboCommand(bool useLargerFileSet, CopyOptions.CopyActionFlags copyActionFlags, SelectionOptions.SelectionFlags selectionFlags, LoggingOptions.LoggingActionFlags loggingAction)
        {
            var cmd = Test_Setup.GenerateCommand(useLargerFileSet, false);
            cmd.CopyOptions.ApplyActionFlags(copyActionFlags);
            cmd.SelectionOptions.ApplySelectionFlags(selectionFlags);
            cmd.LoggingOptions.ApplyLoggingFlags(loggingAction);
            return cmd;
        }

        /// <summary>
        /// Generate a new CachedRoboCommand
        /// </summary>
        /// <param name="useLargerFileSet"></param>
        /// <returns></returns>
        public static CachedRoboCommand GetCachedRoboCopy(RoboCommand rc)
        {
            var cmd = new CachedRoboCommand();
            cmd.CopyOptions = rc.CopyOptions;
            cmd.SelectionOptions = rc.SelectionOptions;
            cmd.LoggingOptions = rc.LoggingOptions;
            cmd.RetryOptions = rc.RetryOptions;
            return cmd;
        }


        public static async Task<RoboSharpTestResults[]> RunTests(RoboCommand roboCommand, CachedRoboCommand cachedRoboCommand, bool CleanBetweenRuns)
        {
            var results = new List<RoboSharpTestResults>();
            results.Add(await Test_Setup.RunTest(roboCommand));
            if (CleanBetweenRuns) CleanDestination();
            results.Add(await Test_Setup.RunTest(cachedRoboCommand));
            Assert.AreEqual(results[0].Results.FilesStatistic.Total, results[1].Results.FilesStatistic.Total);
            return results.ToArray();
        }

        public static Task<RoboSharpTestResults> RunTest(IRoboCommand roboCommand)
        {
            return Test_Setup.RunTest(roboCommand);
        }

    }
}
