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
    public class CachedRoboCopy : RoboSharp.Extensions.AbstractCustomIRoboCommand
    {

        private CancellationTokenSource CancellationTokenSource { get; set; }
        private bool HasBeenListed { get; set; }
        
        /// <summary>
        /// ONLY FOR MOVING ENTIRE DIRECTORIES THAT DON'T EXIST IN THE DESTINATION
        /// </summary>
        private List<DirectoryCopier> DirCopiers { get; } = new();

        private List<DirectoryCopier> DirInfos { get; } = new();
        /// <summary>
        /// Used for all Copy/Move operations that may or may not exist in the destination
        /// </summary>
        private IEnumerable<FileCopier> FileCopiers { get; set; }
        
        private ProcessedFileInfo LastDirectoryInfo { get; set; }
        private DirectoryCopier TopLevelDirectory { get; set; }

        /// <inheritdoc/>
        public override Task Start(string domain = "", string username = "", string password = "")
        {
            if (IsRunning) throw new Exception("Cannot start copier - copier is already running!");
            IsRunning = true;
            IsCancelled = false;
            IsPaused = false;
            bool listOnly = LoggingOptions.ListOnly;
            var evaluator = new RoboSharp.Extensions.SourceDestinationEvaluator(this);

            //Setup the Instance
            try
            {
                if (TopLevelDirectory is null || CopyOptions.Source != TopLevelDirectory.Source.FullName | CopyOptions.Destination != TopLevelDirectory.Destination.FullName)
                {
                    TopLevelDirectory = new DirectoryCopier(CopyOptions.Source, CopyOptions.Destination);
                    evaluator.ShouldCopyDir(TopLevelDirectory, out var info);
                    TopLevelDirectory.RoboSharpInfo = info;
                    HasBeenListed = false;
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
            bool move = !mirror && (CopyOptions.MoveFiles | CopyOptions.MoveFilesAndDirectories);
            bool verbose = LoggingOptions.VerboseOutput;

            var RunTask = new Task(async () =>
           {
               //Generate the IEnumerables
               if (!HasBeenListed | listOnly)
               {
                   // Must get all the copiers
                   DirCopiers.Clear();
                   DirInfos.Clear();
                   FileCopiers = new FileCopier[] { };

                   GetAllCopiers(TopLevelDirectory);
                   HasBeenListed = true;

                   void GetAllCopiers(DirectoryCopier dir, int currentDepth = 0)
                   {
                       bool shouldCopyDir = IsLessThanMaxDepth(currentDepth) & evaluator.ShouldCopyDir(dir, out var info);
                       DirInfos.Add(dir);
                       dir.RoboSharpInfo = info;
                       if (shouldCopyDir)
                       {
                           if (!verbose && dir.IsLocatedOnSameDrive() && move && !dir.Destination.Exists)
                           {
                               DirCopiers.Add(dir);
                           }
                           else
                           {
                               FileCopiers.Concat(TopLevelDirectory.GetFileCopiersEnumerable());
                               foreach (var d in dir.GetDirectoryCopiersEnumerable())
                               {
                                   if (CancellationTokenSource.IsCancellationRequested) break;
                                   GetAllCopiers(d);
                               }
                           }
                       }
                   }
                   bool IsLessThanMaxDepth(int depth)
                   {
                       if (CopyOptions.Depth <= 0) return true;
                       return depth <= CopyOptions.Depth;
                   }
               }

               List<Task> CopyTasks = new();

               foreach (var d in DirCopiers)
               {
                   if (CancellationTokenSource.IsCancellationRequested) break;
                   resultsBuilder.AddDir(d.RoboSharpInfo);
                   if (!listOnly && move)
                   {
                       CopyTasks.Add(Task.Run(() => d.Source.MoveTo(d.Destination.FullName)));
                       //Wait Completion
                       if (ShouldWait()) await Task.Delay(100);
                   }
               }
               await CopyTasks.WhenAll(CancellationTokenSource.Token);

               // Loop through the copiers
               foreach (var f in FileCopiers)
               {
                   if (CancellationTokenSource.IsCancellationRequested) break;

                   if (f.RoboSharpFileInfo is null)
                   {
                       bool shouldCopy = evaluator.ShouldCopyFile(f, out var info);
                       f.RoboSharpFileInfo = info;
                       f.ShouldCopy = shouldCopy;
                       f.RoboSharpDirectoryInfo = DirInfos.FirstOrDefault(o => o.Source.FullName == Path.GetDirectoryName(f.Source.FullName))?.RoboSharpInfo;
                   }
                   if (LastDirectoryInfo != f.RoboSharpDirectoryInfo)
                   {
                       resultsBuilder.AddDir(f.RoboSharpDirectoryInfo);
                       LastDirectoryInfo = f.RoboSharpDirectoryInfo;
                   }
                   resultsBuilder.AddFile(f.RoboSharpFileInfo, listOnly);
                   RaiseOnFileProcessed(f.RoboSharpFileInfo);

                   //Copy or Move
                   if (!listOnly)
                   {
                       if (f.IsExtra())
                       {
                           if (mirror | CopyOptions.Purge)
                           {
                               f.Destination.Delete();
                               resultsBuilder.AddFilePurged(f.RoboSharpFileInfo);
                           }
                       }
                       else if (!f.ShouldCopy)
                       {
                           resultsBuilder.AddFileSkipped(f.RoboSharpFileInfo);
                       }
                       else if (f.ShouldCopy)
                       {
                           f.FileCopyProgressUpdated += FileCopyProgressUpdated;
                           f.FileCopyFailed += FileCopyFailed;
                           f.FileCopyCompleted += FileCopyCompleted;
                           resultsBuilder.ProgressEstimator.SetCopyOpStarted();
                           Task copyTask;
                           if (move)
                               copyTask = f.Move(true);
                           else
                               copyTask = f.Copy(true);

                           Task continuation = copyTask.ContinueWith(t =>
                           {
                               f.FileCopyProgressUpdated -= FileCopyProgressUpdated;
                               f.FileCopyFailed -= FileCopyFailed;
                               f.FileCopyCompleted -= FileCopyCompleted;
                           });
                           CopyTasks.Add(continuation);
                       }
                   }

                   //Wait Completion
                   if (ShouldWait())
                       await Task.Delay(100);
               }

               if (CancellationTokenSource.IsCancellationRequested)
               {
                   //foreach (var d in DirCopiers)
                       //d.Cancel();
                   foreach (var f in FileCopiers.Where( o => o.IsCopying))
                       f.Cancel();
               }

               await CopyTasks.WhenAll();

               // Private Methods
               bool ShouldWait()
               {
                   if (CancellationTokenSource.IsCancellationRequested) return false;
                   if (CopyTasks.Where(t => t.Status < TaskStatus.RanToCompletion).Count() >= CopyOptions.MultiThreadedCopiesCount) return true;
                   return false;
               }
               void FileCopyCompleted(object sender, FileCopyCompletedEventArgs e)
               {
                   resultsBuilder.AddFileCopied(e.RoboSharpFileInfo);
               }
               void FileCopyFailed(object sender, FileCopyFailedEventArgs e)
               {
                   if (e.WasFailed)
                       resultsBuilder.AddSystemMessage($"{e.Source} -- FAILED");
                   else if (e.WasCancelled)
                       resultsBuilder.AddSystemMessage($"{e.Source} -- CANCELLED");
               }

           }, TaskCreationOptions.LongRunning);

            var finishTask = RunTask.ContinueWith((runTask) =>
            {
                IsRunning = false;
                IsPaused = false;
                CancellationTokenSource?.Dispose();

                if (runTask.IsFaulted)
                {
                    RaiseOnTaskFaulted(runTask.Exception);
                }
                base.SaveResults(resultsBuilder?.GetResults());
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
