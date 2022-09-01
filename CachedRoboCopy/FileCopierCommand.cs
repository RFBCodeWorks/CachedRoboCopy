using RoboSharp;
using RoboSharp.Interfaces;
using RoboSharp.Results;
using System;
using System.Collections;
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
    /// This is a list of <see cref="FileCopier"/> objects that provides for the <see cref="IRoboCommand"/> interface.
    /// </summary>
    /// <remarks>
    /// IRoboCommand is partially implemented. 
    /// <br/> - CopyOptions is partially Implemented
    /// <br/> - RetryOptions and LoggingOptions are implemented.
    /// <br/> - SelectionOptions is not implemented because the list of files to copy/move are explicitly passed in.
    /// <br/> - JobOptions is not implemented.
    /// </remarks>
    public class FileCopierCommand : RoboSharp.Extensions.AbstractCustomIRoboCommand, INotifyPropertyChanged, IRoboCommand, IEnumerable<FileCopier>, IDisposable
    {
        /// <summary>
        /// Create a new FileCopierCommand
        /// </summary>
        public FileCopierCommand() : base() 
        {
            base.CopyOptions.MultiThreadedCopiesCount = 1;
        }

        /// <summary>
        /// Create a new FileCopierCommand with the provided copiers
        /// </summary>
        /// <param name="copiers"></param>
        public FileCopierCommand(params FileCopier[] copiers) : base()
        {
            FileCopiers.AddRange(copiers);
            base.CopyOptions.MultiThreadedCopiesCount = 1;
        }

        #region < Properties >

        /// <summary>
        /// The FileCopier objects that get run with this Start method is called
        /// </summary>
        public ObservableList<FileCopier> FileCopiers { get; } = new ObservableList<FileCopier>();


        /// <summary>
        /// Not fully implemented. Relevant options include:
        /// <br/> - MultiThreadedCopiesCount (Number of files that can copy at once)
        /// <br/> - Mirror ( Forces to copy files, ignored MoveFiles option)
        /// <br/> - MoveFiles ( Enable Moving instead of Copying )
        /// <br/> - Exclude Newer / Exclude Older
        /// </summary>
        /// <inheritdoc/>
        new public CopyOptions CopyOptions
        {
            get => base.CopyOptions; set => base.CopyOptions = value;
        }

        /// <summary>
        /// Not Implemented
        /// </summary>
        new public SelectionOptions SelectionOptions
        {
            get => base.SelectionOptions; set => base.SelectionOptions = value;
        }

        /// <summary>
        /// Not Fully Implemented. Relevant options include:
        /// <br/> - 
        /// </summary>
        /// <inheritdoc/>
        new public RetryOptions RetryOptions
        {
            get => base.RetryOptions; set => base.RetryOptions = value;
        } 

        /// <summary
        /// >Not Fully Implemented. Relevant options include:
        /// <br/> - 
        /// </summary>
        /// <inheritdoc/>
        new public LoggingOptions LoggingOptions
        {
            get => base.LoggingOptions; set => base.LoggingOptions = value;
        }

        #endregion

        private int CopyOperationsActive => this.Where(o => o.IsCopying).Count();
        private CancellationTokenSource CancellationSource { get; set; }
        private bool disposedValue;

        /// <summary>
        /// Add the copiers to the list
        /// </summary>
        /// <param name="copier"></param>
        public void AddCommand(params FileCopier[] copier) => this.FileCopiers.AddRange(copier);


        /// <inheritdoc/>
        public override Task Start(string domain = "", string username = "", string password = "")
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileCopierCommand));
            if (IsRunning) throw new Exception("Already Running!");
            IsCancelled = false;
            IsRunning = true;
            CancellationSource = new CancellationTokenSource();
            var resultsBuilder = new ResultsBuilder(this);
            RaiseOnProgressEstimatorCreated(resultsBuilder.ProgressEstimator);
            

            var moveOp = Task.Run(async () =>
            {
                List<Task> queue = new List<Task>();
                Task copyTask = null;
                var evaluator = new RoboSharp.Extensions.SourceDestinationEvaluator(this);
                foreach (FileCopier copier in this)
                {
                    #region < Setup the Events and Continuation Task >

                    copier.FileCopyProgressUpdated += CopierCopyProgressUpdated;
                    copier.FileCopyCompleted += CopierCompleted;
                    copier.FileCopyFailed += CopierFailed;
                    
                    Task CopierContinuation(Task copyTask)
                    {
                        copier.FileCopyProgressUpdated -= CopierCopyProgressUpdated;
                        copier.FileCopyCompleted -= CopierCompleted;
                        copier.FileCopyFailed -= CopierFailed;
                        return Task.CompletedTask;
                    }

                    #endregion

                    #region < Start the Copy Tasks >

                    copier.Destination.Refresh();
                    evaluator.ShouldCopyFile(copier, out var info);
                    copier.RoboSharpFileInfo = info;
                    copier.RoboSharpDirectoryInfo = new ProcessedFileInfo(Path.GetDirectoryName(copier.Source.FullName), FileClassType.NewDir, this.Configuration.GetDirectoryClass(DirectoryClasses.ExistingDir), 1);

                    resultsBuilder.AddFile(copier.RoboSharpFileInfo, LoggingOptions.ListOnly);

                    //Check if it can copy, or if there is a need to copy.
                    bool canCopy = !copier.IsExtra() && (copier.IsLonely() || !(copier.IsSameDate() && copier.Source.Length == copier.Destination.Length));
                    copier.ShouldCopy = canCopy;

                    if (canCopy)
                    {
                        resultsBuilder.ProgressEstimator.SetCopyOpStarted();
                        if (!CopyOptions.Mirror && (CopyOptions.MoveFiles | CopyOptions.MoveFilesAndDirectories))
                            copyTask = copier.Move(RetryOptions);
                        else
                            copyTask = copier.Copy(RetryOptions);
                    }
                    else
                        copyTask = Task.FromResult(false);

                    //Add the task to the list
                    Task continueTask = copyTask.ContinueWith(CopierContinuation);
                    queue.Add(continueTask);

                    #endregion

                    #region < Wait for all items to proceed >

                    bool wasPaused = false;
                    while (copier != this.Last() && (CopyOperationsActive >= CopyOptions.MultiThreadedCopiesCount | IsPaused))
                    {
                        if (CancellationSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }
                        else if (IsPaused)
                        {
                            wasPaused = true;
                        }
                        else if (wasPaused)
                        {
                            wasPaused = false;
                        }

                        await Task.Delay(100);
                    }

                    #endregion
                }

                await Task.WhenAll(queue);
                
            }, CancellationSource.Token);
            
            return moveOp.ContinueWith(MoveOpContinuation);

            Task MoveOpContinuation(Task moveOp)
            {
                IsCancelled = CancellationSource?.IsCancellationRequested ?? true;
                IsRunning = false;
                CancellationSource = null;
                return Task.CompletedTask;
            }

            // Copier Events

            #region < Copier Events >

            void CopierCopyProgressUpdated(object sender, FileCopyProgressUpdatedEventArgs e)
            {
                RaiseOnCopyProgressChanged(e.Progress, e.RoboSharpFileInfo, e.RoboSharpDirInfo);
            }

            void CopierCompleted(object sender, FileCopyCompletedEventArgs e)
            {
                resultsBuilder.AddFileCopied(e.RoboSharpFileInfo);
                resultsBuilder.AverageSpeed.Average(e.Speed);
            }

            void CopierFailed(object sender, FileCopyFailedEventArgs e)
            {
                if (e.WasCancelled)
                    resultsBuilder.AddSystemMessage($@"Copy Operation Cancelled --> {e.Destination.FullName}");
                //else if (e.WasSkipped)
                //    LogLines.Add($@"Copy Operation Skipped ( {e.Error} )--> {e.Destination.FullName}");
                else
                    resultsBuilder.AddSystemMessage($@"Copy Operation Failed --> {e.Destination.FullName}");
            }

            #endregion
        }

        /// <inheritdoc/>
        public override void Stop()
        {
            if (IsRunning && !IsCancelled)
            {
                foreach (var c in FileCopiers)
                    c.Cancel();
                this.CancellationSource?.Cancel();
                IsCancelled = true;
            }
        }


        #region < IEnumerable >

        /// <inheritdoc/>
        public IEnumerator<FileCopier> GetEnumerator()
        {
            return ((IEnumerable<FileCopier>)FileCopiers).GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)FileCopiers).GetEnumerator();
        }

        #endregion

        #region < IDisposable >

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    if (StopIfDisposing && !(CancellationSource?.IsCancellationRequested ?? true))
                        CancellationSource?.Cancel();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~FileCopierCommand()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        /// <inheritdoc/>
        public override void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

}
