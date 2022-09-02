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

        /// <inheritdoc/>
        public override Task Start(string domain = "", string username = "", string password = "")
        {
            if (IsRunning) throw new Exception("Cannot start copier - copier is already running!");
            IsRunning = true;
            IsCancelled = false;
            IsPaused = false;
            bool listOnly = LoggingOptions.ListOnly;
            var evaluator = new PairEvaluator(this);

            //Setup the Instance
            try
            {
                if (TopLevelDirectory is null || CopyOptions.Source != TopLevelDirectory.Source.FullName | CopyOptions.Destination != TopLevelDirectory.Destination.FullName)
                {
                    TopLevelDirectory = new DirectoryCopier(CopyOptions.Source, CopyOptions.Destination);
                    if (TopLevelDirectory.Destination.Exists)
                        TopLevelDirectory.DirectoryClass = DirectoryClasses.ExistingDir;
                    else
                        TopLevelDirectory.DirectoryClass = DirectoryClasses.NewDir;

                    TopLevelDirectory.RoboSharpInfo = new ProcessedFileInfo(TopLevelDirectory.Source, Configuration, TopLevelDirectory.DirectoryClass);
                    TopLevelDirectory.ShouldExclude_JunctionDirectory = false;
                    TopLevelDirectory.ShouldExclude_NamedDirectory = false;
                }
                else if (listOnly)
                {
                    TopLevelDirectory.Refresh();
                }
            }
            catch(Exception e)
            {
                RaiseOnCommandError(e);
                IsRunning = false;
                return Task.CompletedTask;
            }

            CancellationTokenSource = new();
            var resultsBuilder = new ResultsBuilder(this);
            base.IProgressEstimator = resultsBuilder.ProgressEstimator;
            RaiseOnProgressEstimatorCreated(base.IProgressEstimator);
            
            bool mirror = CopyOptions.Mirror;
            bool includeEmptyDirs = mirror | CopyOptions.CopySubdirectoriesIncludingEmpty;
            bool includeSubDirs = includeEmptyDirs | CopyOptions.CopySubdirectories;
            bool move = !mirror && (CopyOptions.MoveFiles | CopyOptions.MoveFilesAndDirectories);
            bool purgeExtras = !SelectionOptions.ExcludeExtra && (mirror | CopyOptions.Purge);
            bool verbose = LoggingOptions.VerboseOutput;
            bool logExtras = verbose || !SelectionOptions.ExcludeExtra | LoggingOptions.ReportExtraFiles;
            bool logSkipped = verbose | LoggingOptions.ReportExtraFiles;

            var RunTask = Task.Factory.StartNew(async () =>
           {
               List<Task> CopyTasks = new();
               List<FileCopier> fileCopiers = new();

               await Dig(TopLevelDirectory);
               await WaitCopyComplete();
               return;

               async Task Dig(DirectoryCopier dir, int currentDepth = 0)
               {
                   if (CancellationTokenSource.IsCancellationRequested) return;

                   RaiseDirProcessed(dir);
                   if (!listOnly) dir.Destination.Create();

                   //Process all files in this directory
                   foreach (var f in evaluator.FilterFilePairs(dir.Files))
                   {
                       if (CancellationTokenSource.IsCancellationRequested) break;

                       //Generate the ProcessedFileInfo objects
                       if (f.RoboSharpFileInfo is null)
                       {
                           bool shouldCopy = evaluator.ShouldCopyFile(f, out var info);
                           f.RoboSharpFileInfo = info;
                           f.ShouldCopy = shouldCopy;
                           f.RoboSharpDirectoryInfo = dir.RoboSharpInfo;
                       }

                       //Special handling for Extra files ( Purge or ignore )
                       if (f.IsExtra())
                       {
                           if (logExtras & !purgeExtras)
                           {
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
                                       fileCopiers.Add(f);
                                       f.CopyProgressUpdated += FileCopyProgressUpdated;
                                       f.CopyFailed += FileCopyFailed;
                                       f.CopyCompleted += FileCopyCompleted;
                                       resultsBuilder.ProgressEstimator.SetCopyOpStarted(); // Mark as starting the copy operation
                                       Task copyTask;
                                       if (move)
                                           copyTask = f.Move(RetryOptions, evaluator.ApplyAttributes);
                                       else
                                           copyTask = f.Copy(RetryOptions, evaluator.ApplyAttributes);

                                       Task continuation = copyTask.ContinueWith(t =>
                                       {
                                           f.CopyProgressUpdated -= FileCopyProgressUpdated;
                                           f.CopyFailed -= FileCopyFailed;
                                           f.CopyCompleted -= FileCopyCompleted;
                                       });
                                       CopyTasks.Add(continuation);
                                   }
                               }
                           }
                       }

                       //Wait for files to Complete copying
                       while (ShouldWait())
                           await Task.Delay(100);
                   }

                   #region < File Copier Helper Routines >

                   //Adds the file to the results builder and raises the event
                   void RaiseFileProcessed(FileCopier f)
                   {
                       resultsBuilder.AddFile(f.RoboSharpFileInfo);
                       RaiseOnFileProcessed(f.RoboSharpFileInfo);
                   }

                   void FileCopyCompleted(object sender, FileCopyCompletedEventArgs e)
                   {
                       resultsBuilder.AverageSpeed.Average(e.Speed);
                       resultsBuilder.AddFileCopied(e.RoboSharpFileInfo);
                   }
                   void FileCopyFailed(object sender, FileCopyFailedEventArgs e)
                   {
                       if (e.WasFailed)
                       {
                           resultsBuilder.AddSystemMessage($"{e.Source} -- FAILED");
                           RaiseOnError(new RoboSharp.ErrorEventArgs(e.Exception, e.Destination.FullName, DateTime.Now));
                       }
                       else if (e.WasCancelled)
                           resultsBuilder.AddSystemMessage($"{e.Source} -- CANCELLED");
                   }

                   bool ShouldWait()
                   {
                       if (CancellationTokenSource.IsCancellationRequested) return false;
                       if (IsPaused) return true;
                       if (CopyOptions.MultiThreadedCopiesCount <= 0) return false;
                       if (CopyTasks.Any() && CopyTasks.Where(t => t.Status < TaskStatus.RanToCompletion).Count() >= CopyOptions.MultiThreadedCopiesCount) return true;
                       return false;
                   }

                   #endregion

                   // Wait for all files to finish copying before digging further into the directory tree
                   await WaitCopyComplete();
                   CopyTasks.Clear(); // Clear the buffer since all have finished copying

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
                   await CopyTasks.WhenAll(CancellationTokenSource.Token);
                   if (CancellationTokenSource.IsCancellationRequested)
                   {

                       foreach (var f in fileCopiers.Where(o => o.IsCopying))
                           f.Cancel();
                       await CopyTasks.WhenAll();
                   }
               }

           }, TaskCreationOptions.LongRunning).Unwrap();

            // Create the Continuation Task that will be returned to the caller
            var finishTask = RunTask.ContinueWith((runTask) =>
            {
                IsRunning = false;
                IsPaused = false;
                CancellationTokenSource?.Cancel();
                CancellationTokenSource?.Dispose();

                if (runTask.IsFaulted)
                {
                    RaiseOnTaskFaulted(runTask.Exception);
                }
                var results = resultsBuilder?.GetResults();
                base.SaveResults(results);
                RaiseOnCommandCompleted(results);
            });
            return finishTask;

        }



        private void FileCopyProgressUpdated(object sender, FileCopyProgressUpdatedEventArgs e)
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
            }
        }

        /// <inheritdoc/>
        public override void Pause()
        {
            if (IsRunning && !IsPaused)
                IsPaused = true;
        }

        /// <inheritdoc/>
        public override void Resume()
        {
            if (IsRunning && IsPaused)
                IsPaused = false;
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            Stop();
        }
    }
}
