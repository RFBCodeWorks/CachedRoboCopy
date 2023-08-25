using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using RoboSharp;
using RoboSharp.Extensions;
using RoboSharp.Interfaces;
using RoboSharp.Results;
using RoboSharp.EventArgObjects;
using System.Threading.Tasks;
using System.Threading;

namespace RFBCodeWorks.RoboSharpExtensions
{
    /// <summary>
    /// Use this IRoboCommand when Moving files from one directory to another when the Source and Destination are on the same root path.<br/>
    /// <br/> Note: If the source and destination do not have the same root path, will use a standard RoboCommand instead.
    /// <br/> Utilizes File.Move() to facilitate the movement of files on the same drive.
    /// </summary>
    public class RoboMover : AbstractCustomIRoboCommand
    {
        /// <inheritdoc/>
        public RoboMover() : base()
        {
            Evaluator = new PairEvaluator(this);
        }

        /// <inheritdoc/>
        public RoboMover(
            CopyOptions copyOptions = null, 
            LoggingOptions loggingOptions = null,
            RetryOptions retryOptions = null,
            SelectionOptions selectionOptions = null,
            RoboSharpConfiguration configuration = null
             ) : base(copyOptions, loggingOptions, retryOptions, selectionOptions, configuration)
        {
            Evaluator = new PairEvaluator(this);
        }

        /// <inheritdoc/>
        public RoboMover(
            string source, string destination,
            CopyOptions.CopyActionFlags copyActionFlags = CopyOptions.CopyActionFlags.Default,
            SelectionOptions.SelectionFlags selectionFlags = SelectionOptions.SelectionFlags.Default,
            LoggingOptions.LoggingActionFlags loggingFlags = LoggingOptions.LoggingActionFlags.RoboSharpDefault
             ) : base(source, destination, copyActionFlags, selectionFlags, loggingFlags)
        {
            Evaluator = new PairEvaluator(this);
        }

        /// <summary>
        /// Create a RoboMover object that clones the options of the input IRoboCommand
        /// </summary>
        /// <param name="cmd">The IRoboCommand to convert to a RoboMover object</param>
        public RoboMover(IRoboCommand cmd)
            : this(cmd.CopyOptions?.Clone(), cmd.LoggingOptions?.Clone(), cmd.RetryOptions?.Clone(), cmd.SelectionOptions?.Clone(), cmd.Configuration?.Clone())
        {

        }

        private RoboCommand standardCommand;
        private Task runningTask;
        private CancellationTokenSource cancelRequest;
        private readonly PairEvaluator Evaluator;
        ResultsBuilder resultsBuilder;

        /// <inheritdoc/>
        public override async Task Start(string domain = "", string username = "", string password = "")
        {
            if (IsRunning) throw new InvalidOperationException("Cannot execute Start command - Process has already started.");
            IsRunning = true;
            
            // Validate Source & Destination - should be able to create directory objects
            DirectoryInfo source = null; DirectoryInfo dest = null;
            try { source = new(CopyOptions.Source); }
            catch (Exception e) 
            { 
                RaiseOnCommandError("CopyOptions.Source is invalid.", e);
                IsRunning = false;
            }
            try { dest = new(CopyOptions.Destination); }
            catch (Exception e)
            {
                RaiseOnCommandError("CopyOptions.Destination is invalid.", e);
                IsRunning = false;
            }
            if (!IsRunning) return;

            cancelRequest = new();
            bool onSameDrive = source.Root.FullName.Equals(dest.Root.FullName, StringComparison.InvariantCultureIgnoreCase);
            bool isMoving = CopyOptions.MoveFiles | CopyOptions.MoveFilesAndDirectories;
            RoboCopyResults results;

            if (!isMoving | !onSameDrive)
            {
                standardCommand = new RoboCommand(Name, source.FullName, dest.FullName, StopIfDisposing, Configuration, CopyOptions, SelectionOptions, RetryOptions, LoggingOptions, JobOptions);
                standardCommand.OnCommandCompleted += StandardCommand_OnCommandCompleted;
                standardCommand.OnCommandError += StandardCommand_OnCommandError;
                standardCommand.OnCopyProgressChanged += StandardCommand_OnCopyProgressChanged;
                standardCommand.OnError += StandardCommand_OnError;
                standardCommand.OnFileProcessed += StandardCommand_OnFileProcessed;
                standardCommand.OnProgressEstimatorCreated += StandardCommand_OnProgressEstimatorCreated;

                runningTask = standardCommand.Start(domain, username, password).ContinueWith(t =>
               {
                   standardCommand.OnCommandCompleted -= StandardCommand_OnCommandCompleted;
                   standardCommand.OnCommandError -= StandardCommand_OnCommandError;
                   standardCommand.OnCopyProgressChanged -= StandardCommand_OnCopyProgressChanged;
                   standardCommand.OnError -= StandardCommand_OnError;
                   standardCommand.OnFileProcessed -= StandardCommand_OnFileProcessed;
                   standardCommand.OnProgressEstimatorCreated -= StandardCommand_OnProgressEstimatorCreated;
               });
                await runningTask;
                results = standardCommand.GetResults();
            }
            else
            {
                // Move the files
                resultsBuilder = new ResultsBuilder(this);
                base.IProgressEstimator = resultsBuilder.ProgressEstimator;

                resultsBuilder.AddSystemMessage("---------------------------");
                resultsBuilder.AddSystemMessage("--  RoboMover Operation  --");
                resultsBuilder.AddSystemMessage("-- Source       :" + source.FullName);
                resultsBuilder.AddSystemMessage("-- Destination  :" + dest.FullName);
                resultsBuilder.AddSystemMessage("---------------------------");
                resultsBuilder.AddSystemMessage("");
                runningTask = ProcessDirectory(new DirectoryPair(source, dest));
                await runningTask;
                results = resultsBuilder.GetResults();
            }

            RaiseOnCommandCompleted(results);
            if (LoggingOptions.ListOnly)
                ListOnlyResults = results;
            else
                RunResults = results;

            standardCommand = null;
            runningTask = null;
            cancelRequest = null;
            IsRunning = false;
        }

        private async Task ProcessDirectory(DirectoryPair directoryPair)
        {

            ProcessedFileInfo pInfo;
            var filePairs = directoryPair.EnumerateFilePairs(FileCopierFactory.DefaultFactory.CreateFileCopier);

            // Files
            foreach (var file in filePairs)
            {
                if (cancelRequest.IsCancellationRequested) break;
                bool shouldMove = Evaluator.ShouldCopyFile(file, out pInfo);

                file.RoboSharpFileInfo = pInfo;
                file.RetryOptions = this.RetryOptions;
                base.RaiseOnFileProcessed(pInfo);
                resultsBuilder.AddFile(pInfo);
                
                if (shouldMove)
                {
                    if (!LoggingOptions.ListOnly)
                    {
                        resultsBuilder.SetCopyOpStarted(pInfo);
                        file.CopyFailed += File_CopyFailed;
                        await file.Move(true).ContinueWith(t =>
                       {
                           file.CopyFailed -= File_CopyFailed;
                       });
                    }
                    resultsBuilder.AddFileCopied(pInfo);
                }
                else if (file.IsExtra())
                {
                    if (CopyOptions.Purge && !LoggingOptions.ListOnly)
                    {
                        file.Destination.Delete();
                        resultsBuilder.AddFilePurged(pInfo);
                    }
                    else { 
                        resultsBuilder.AddFileSkipped(pInfo); 
                    }
                }
                else
                {
                    resultsBuilder.AddFileSkipped(pInfo);
                }
            }

            // Iterate through dirs
            foreach(var dir in directoryPair.GetDirectoryPairs(DirectoryPair.CreatePair))
            {
                if (cancelRequest.IsCancellationRequested) break;
                bool processDir = Evaluator.ShouldCopyDir(dir, out pInfo, out _, out _, out _);
                dir.ProcessedFileInfo = pInfo;
                dir.ProcessedFileInfo.Size = dir.Source.GetFileSystemInfos().Length;
                resultsBuilder.AddDir(pInfo);
                RaiseOnFileProcessed(pInfo);
                if (processDir)
                {
                    await ProcessDirectory(dir);
                }
            }

            if (CopyOptions.MoveFilesAndDirectories && directoryPair.Source.IsEmpty())
            {
                directoryPair.Source.Delete();
            }
        }

        /// <inheritdoc/>
        public override void Stop()
        {
            if (!IsRunning) return;
            try
            {
                cancelRequest?.Cancel();
            }
            finally
            {
                standardCommand?.Stop();
            }
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            standardCommand?.Dispose();
        }

        #region < Event Handlers >

        private void File_CopyFailed(object sender, FileCopyFailedEventArgs e)
        {

        }

        private void StandardCommand_OnProgressEstimatorCreated(IRoboCommand sender, ProgressEstimatorCreatedEventArgs e)
        {
            this.IProgressEstimator = e.ResultsEstimate;
            //base.RaiseOnProgressEstimatorCreated(e.ResultsEstimate);
        }

        private void StandardCommand_OnFileProcessed(IRoboCommand sender, FileProcessedEventArgs e)
        {
            base.RaiseOnFileProcessed(e.ProcessedFile);
        }

        private void StandardCommand_OnError(IRoboCommand sender, RoboSharp.ErrorEventArgs e)
        {
            base.RaiseOnError(e);
        }

        private void StandardCommand_OnCopyProgressChanged(IRoboCommand sender, CopyProgressEventArgs e)
        {
            base.RaiseOnCopyProgressChanged(e);
        }

        private void StandardCommand_OnCommandError(IRoboCommand sender, CommandErrorEventArgs e)
        {
            base.RaiseOnCommandError(e);
        }

        private void StandardCommand_OnCommandCompleted(IRoboCommand sender, RoboCommandCompletedEventArgs e)
        {
            //base.RaiseOnCommandCompleted(e);
        }

        #endregion

    }
}
