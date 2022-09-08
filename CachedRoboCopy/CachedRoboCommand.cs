using RoboSharp;
using RoboSharp.Interfaces;
using RoboSharp.Results;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RoboSharp.Extensions;
using System.Collections.Concurrent;

namespace RFBCodeWorks.CachedRoboCopy
{
    /// <summary>
    /// Iterate through the directories to move the directories and files quicker than RoboCopy does
    /// </summary>
    public class CachedRoboCommand : RoboSharp.Extensions.AbstractCustomIRoboCommand
    {

        #region < Constructors >

        /// <summary>
        /// Create a new CachedRoboCommand
        /// </summary>
        public CachedRoboCommand() : base() { }
        
        /// <summary>
        /// Create a new CachedRoboCommand
        /// </summary>
        /// <inheritdoc cref="AbstractCustomIRoboCommand.AbstractCustomIRoboCommand(string, string, CopyOptions.CopyActionFlags, SelectionOptions.SelectionFlags, LoggingOptions.LoggingActionFlags)"/>
        public CachedRoboCommand(string source, string destination, 
            CopyOptions.CopyActionFlags copyActionFlags = CopyOptions.CopyActionFlags.Default,
            SelectionOptions.SelectionFlags selectionFlags = SelectionOptions.SelectionFlags.Default,
            LoggingOptions.LoggingActionFlags loggingFlags = LoggingOptions.LoggingActionFlags.RoboSharpDefault
            ) : base(source, destination, copyActionFlags, selectionFlags, loggingFlags) 
        { 

        }

        #endregion

        private CancellationTokenSource CancellationTokenSource { get; set; }
        
        private DirectoryCopier TopLevelDirectory { get; set; }

        //ConcurrentQueue<FileCopier> CopiersInQueue;
        //ConcurrentQueue<Action> ReadTasks;
        //ConcurrentQueue<Action> WriteTasks;
        private ResultsBuilder resultsBuilder;
        //private List<FileCopier> fileCopiers;
        private CopyQueue CopyQueue;

        /// <inheritdoc/>
        public override Task Start(string domain = "", string username = "", string password = "")
        {
            if (IsRunning) throw new Exception("Cannot start copier - copier is already running!");
            IsRunning = true;
            IsCancelled = false;
            IsPaused = false;
            CancellationTokenSource = new();
            bool listOnly = LoggingOptions.ListOnly;
            var evaluator = new PairEvaluator(this);

            //Setup the Instance
            try
            {
                //Setup the TopLevelDirectory
                if (TopLevelDirectory is null || CopyOptions.Source != TopLevelDirectory.Source.FullName | CopyOptions.Destination != TopLevelDirectory.Destination.FullName)
                {
                    TopLevelDirectory = new DirectoryCopier(CopyOptions.Source, CopyOptions.Destination, evaluator);
                    SetTopLevelDirInfo();
                    TopLevelDirectory.RoboSharpInfo = new ProcessedFileInfo(TopLevelDirectory.Source, Configuration, TopLevelDirectory.DirectoryClass);
                    TopLevelDirectory.ShouldExclude_JunctionDirectory = false;
                    TopLevelDirectory.ShouldExclude_NamedDirectory = false;
                }
                else if (listOnly)
                {
                    TopLevelDirectory.Refresh();
                    SetTopLevelDirInfo();
                }
                
                //Setup the copy objects
                if (!listOnly)
                {
                    TopLevelDirectory.Destination.Create();
                    TopLevelDirectory.Destination.Refresh();
                    TopLevelDirectory.RoboSharpInfo?.SetDirectoryClass(DirectoryClasses.ExistingDir, this.Configuration);
                    
                    CopyQueue = new CopyQueue(this, this.CancellationTokenSource.Token) { SetAttributesAction = evaluator.ApplyAttributes };
                }

                void SetTopLevelDirInfo()
                {
                    if (!TopLevelDirectory.Destination.Exists)
                        TopLevelDirectory.DirectoryClass = DirectoryClasses.NewDir;
                    else
                        TopLevelDirectory.DirectoryClass = DirectoryClasses.ExistingDir;
                    TopLevelDirectory.RoboSharpInfo?.SetDirectoryClass(TopLevelDirectory.DirectoryClass, this.Configuration);
                }
            }
            catch(Exception e)
            {
                RaiseOnCommandError(e);
                IsRunning = false;
                CancellationTokenSource.Cancel();
                CancellationTokenSource.Dispose();
                CancellationTokenSource = null;
                return Task.CompletedTask;
            }

            
            resultsBuilder = new ResultsBuilder(this);
            base.IProgressEstimator = resultsBuilder.ProgressEstimator;
            RaiseOnProgressEstimatorCreated(base.IProgressEstimator);

            if (CopyOptions.MultiThreadedCopiesCount > 0)
            {
                LoggingOptions.NoDirectoryList = true;
                LoggingOptions.IncludeFullPathNames = true;

                // If multithreading is enabled, RoboCopy adds 1 to the COPIED segment even if the root directory doesn't need to be created. Without this, results unit tests fail.
                resultsBuilder.ProgressEstimator.AddDir(TopLevelDirectory.RoboSharpInfo);
                resultsBuilder.ProgressEstimator.AddDirCopied(TopLevelDirectory.RoboSharpInfo);
            }

            bool mirror = CopyOptions.Mirror;
            bool includeEmptyDirs = mirror | CopyOptions.CopySubdirectoriesIncludingEmpty;
            bool includeSubDirs = includeEmptyDirs | CopyOptions.CopySubdirectories;
            bool move = !mirror && (CopyOptions.MoveFiles | CopyOptions.MoveFilesAndDirectories);
            bool purgeExtras = !SelectionOptions.ExcludeExtra && (mirror | CopyOptions.Purge);
            bool verbose = LoggingOptions.VerboseOutput;
            bool logExtras = verbose || !SelectionOptions.ExcludeExtra | LoggingOptions.ReportExtraFiles;
            bool logSkipped = verbose;

            
            var RunTask = Task.Factory.StartNew(async () =>
           {
               
               await Dig(TopLevelDirectory);
               CopyQueue?.FinalizeQueue();
               await WaitCopyComplete();

               return;

               async Task Dig(DirectoryCopier dir, int currentDepth = 0)
               {
                   if (CancellationTokenSource.IsCancellationRequested) return;

                   RaiseDirProcessed(dir);
                   if (!listOnly)
                   {
                       if (!dir.Destination.Exists)
                       {
                           dir.Destination.Create();
                           resultsBuilder.ProgressEstimator.AddDirCopied(dir.RoboSharpInfo);
                       }
                   }

                   //Process all files in this directory
                   foreach (var f in dir.Files)
                   {
                       if (CancellationTokenSource.IsCancellationRequested) break;

                       //Generate the ProcessedFileInfo objects
                       if (f.RoboSharpFileInfo is null)
                       {
                           bool shouldCopy = evaluator.ShouldCopyFile(f, out var info) && evaluator.ShouldIncludeFileName(f);
                           f.RoboSharpFileInfo = info;
                           f.ShouldCopy = shouldCopy;
                           f.RoboSharpDirectoryInfo = dir.RoboSharpInfo;
                       }

                       //Special handling for Extra files ( Purge or ignore )
                       if (f.IsExtra())
                       {
                           if (logExtras & !purgeExtras)
                           {
                               resultsBuilder.AddFile(f.RoboSharpFileInfo);
                               RaiseFileProcessed(f);
                           }
                           if (purgeExtras)
                           {
                               f.Destination.Delete();
                               resultsBuilder.AddFilePurged(f.RoboSharpFileInfo);
                           }
                       }
                       else if (f.IsLonely() && SelectionOptions.ExcludeLonely)
                       {
                           /* Do Nothing - Lonely files are being ignored */
                       }
                       //Copy or Move
                       else
                       {
                           if (!f.ShouldCopy)
                           {
                               if (logSkipped)
                                   resultsBuilder.AddFileSkipped(f.RoboSharpFileInfo);
                           }
                           else
                           {
                               RaiseFileProcessed(f); // Log File Copied
                               if (listOnly)
                               {
                                   resultsBuilder.AddFileCopied(f.RoboSharpFileInfo);
                               }
                               else
                               {
                                   if (CopyOptions.CreateDirectoryAndFileTree)
                                   {
                                       if (!f.Destination.Exists) f.Destination.Create().Close(); // This should create a 0-length file at this location
                                       resultsBuilder.AddFileCopied(f.RoboSharpFileInfo);
                                   }
                                   else
                                   {
                                       f.CopyCompleted += FileCopier_CopyCompleted;
                                       f.CopyFailed += FileCopier_CopyFailed;
                                       f.CopyProgressUpdated += FileCopier_CopyProgressUpdated;
                                       CopyQueue?.AddToQueue(f);
                                   }
                               }
                           }
                       }

                       //Wait for files to Complete copying
                       while (ShouldWait())
                           await Task.Delay(20);
                   }

                   #region < File Copier Helper Routines >

                   bool ShouldWait()
                   {
                       if (CancellationTokenSource.IsCancellationRequested) return false;
                       if (IsPaused) return true;
                       if (CopyOptions.MultiThreadedCopiesCount >= 1) return false; // just queue them all up!
                       //if (fileCopiers.Any() && fileCopiers.Count() >= CopyOptions.MultiThreadedCopiesCount) return true;
                       if ((CopyQueue?.AnyInProgress ?? false) && CopyOptions.MultiThreadedCopiesCount < 1) return true;
                       return false;
                   }

                   #endregion

                   // Wait for all files to finish copying before digging further into the directory tree
                   await WaitCopyComplete();
                   
                   // Process the Directories
                   if (CanDigDeeper())
                   {
                       foreach (var d in dir.SubDirectories)
                       {
                           if (CancellationTokenSource.IsCancellationRequested) break;
                           if (d is null | d == dir) continue;

                           // Get the ProcessedFileInfo object
                           if (d.RoboSharpInfo is null)
                           {
                               evaluator.ShouldCopyDir(d, out var info, out var @class, out bool Exjunct, out bool ExNamed);
                               d.RoboSharpInfo = info;
                               d.DirectoryClass = @class;
                               d.ShouldExclude_JunctionDirectory = Exjunct;
                               d.ShouldExclude_NamedDirectory = ExNamed;
                           }

                           // If the directory doens't exist in the source
                           if (d.IsExtra)
                           {
                               if (logExtras)
                               {
                                   RaiseDirProcessed(d);
                               }
                               if (purgeExtras)
                               {
                                   await d.Purge(RetryOptions);
                               }
                               //else if (false)  // If not purging, decide if we need to dig into the extra subdirectories
                               //{
                               //    await Dig(d, currentDepth + 1);
                               //}
                           }
                           else if (d.ShouldExclude_NamedDirectory | d.ShouldExclude_JunctionDirectory)
                           {
                               if (verbose)
                                   RaiseDirProcessed(d);
                           }
                           else if (d.IsLonely && SelectionOptions.ExcludeLonely) //Lonely directories are ignored under this condition - don't even log them
                           {
                               if (verbose)
                                   RaiseDirProcessed(d);
                           }
                           else // Check to dig into the directory
                           {
                               await Dig(d, currentDepth + 1);

                               if (move && !listOnly && d.Source.Exists)
                               {
                                   if (d.Source.IsEmpty())
                                       d.Source.Delete();
                               }
                           }
                       }
                   }

                   #region < Dir Copier Helper Routines >

                   //Adds the file to the results builder and raises the event
                   void RaiseDirProcessed(DirectoryCopier d)
                   {
                       resultsBuilder.AddDir(d.RoboSharpInfo);
                       RaiseOnFileProcessed(d.RoboSharpInfo);
                   }

                   bool CanDigDeeper()
                   {
                       if (CopyOptions.Depth <= 0) return includeSubDirs;
                       return currentDepth <= CopyOptions.Depth;
                   }

                   #endregion
               }

               //Await all copy tasks to finish running before moving to the next directory
               async Task WaitCopyComplete()
               {
                   while (true)
                   {
                       if (CancellationTokenSource.IsCancellationRequested)
                       {
                           CopyQueue?.Cancel();
                       }
                       else
                       {
                           if (CopyQueue?.AnyInProgress ?? false)
                               await Task.Delay(50);
                           else
                               break;
                       }
                   }
               }

           }, TaskCreationOptions.LongRunning).Unwrap();

            // Create the Continuation Task that will be returned to the caller
            var finishTask = RunTask.ContinueWith(async (runTask) =>
            {
                IsRunning = false;
                IsPaused = false;
                CancellationTokenSource?.Cancel();
                CancellationTokenSource?.Dispose();

                //await WaitForThreads();
                await (CopyQueue?.GetAwaiter() ?? Task.CompletedTask);

                if (runTask.IsFaulted)
                {
                    RaiseOnTaskFaulted(runTask.Exception);
                }
                var results = resultsBuilder?.GetResults();
                base.SaveResults(results);
                RaiseOnCommandCompleted(results);
            }).Unwrap();
            return finishTask;

        }

        //Adds the file to the results builder and raises the event
        void RaiseFileProcessed(FileCopier f)
        {
            base.RaiseOnFileProcessed(f.RoboSharpFileInfo);
        }

        void FileCopier_CopyCompleted(object sender, FileCopyCompletedEventArgs e)
        {
            resultsBuilder.AverageSpeed.Average(e.Speed);
            resultsBuilder.AddFileCopied(e.RoboSharpFileInfo);
            var copier = sender as FileCopier;
            copier.CopyProgressUpdated -= FileCopier_CopyProgressUpdated;
            copier.CopyFailed -= FileCopier_CopyFailed;
            copier.CopyCompleted -= FileCopier_CopyCompleted;
        }

        void FileCopier_CopyFailed(object sender, FileCopyFailedEventArgs e)
        {
            if (e.WasFailed)
            {
                resultsBuilder.AddSystemMessage($"{e.Source} -- FAILED");
                RaiseOnError(new RoboSharp.ErrorEventArgs(e.Exception, e.Destination.FullName, DateTime.Now));
            }
            else if (e.WasCancelled)
                resultsBuilder.AddSystemMessage($"{e.Source} -- CANCELLED");
        }

        private void FileCopier_CopyProgressUpdated(object sender, FileCopyProgressUpdatedEventArgs e)
        {
            RaiseOnCopyProgressChanged(e.Progress, e.RoboSharpFileInfo, e.RoboSharpDirInfo);
        }

        /// <inheritdoc/>
        public override void Stop()
        {
            if (IsRunning && !IsCancelled)
            {
                CancellationTokenSource?.Cancel();
                IsCancelled = true;
                CopyQueue?.Cancel();
            }
        }

        /// <inheritdoc/>
        public override void Pause()
        {
            if (IsRunning && !IsPaused)
                IsPaused = true;
            CopyQueue?.Pause();
        }

        /// <inheritdoc/>
        public override void Resume()
        {
            if (IsRunning && IsPaused)
                IsPaused = false;
            CopyQueue?.Resume();
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            Stop();
        }
    }
}
