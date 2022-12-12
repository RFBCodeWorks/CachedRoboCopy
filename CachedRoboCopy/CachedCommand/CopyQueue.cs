using RoboSharp.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RFBCodeWorks.RoboSharpExtensions.CachedCommand
{
    /// <summary>
    /// Class that holds the queue logic for the custom IRoboCommands
    /// </summary>
    internal class CopyQueue
    {

        #region < Constructors >

        public CopyQueue(IRoboCommand cmd, CancellationToken token)
        {
            CancelToken = token;
            Command = cmd;
        }

        #endregion

        #region < Properties >

        IRoboCommand Command { get; }
        public Action<FileInfo> SetAttributesAction { get; set; } = null;

        private CancellationToken CancelToken { get; } = CancellationToken.None;
        private bool IsCommandPaused => Command.IsPaused;
        private bool IsCommandRunning => Command.IsRunning;
        private bool IsCancelled => CancelToken.CanBeCanceled ? CancelToken.IsCancellationRequested : true;

        
        public bool AnyInProgress => !FileCopiers.IsEmpty || ReadTasks.Any() | WriteTasks.Any();
        public bool IsFinalized { get; private set; }

        private object QueueThreadLock = new object();
        private Task queueThread;
        private readonly List<Task> ReadTasks = new();
        private readonly List<Task> WriteTasks = new();
        private readonly ConcurrentQueue<FileCopier> FileCopiers = new();
        private readonly List<FileCopier> RunningCopiers = new();

        #endregion

        #region < Methods >

        /// <summary>
        /// Adds the item to the queue, and if the queue is not started, starts the queue task
        /// </summary>
        /// <param name="copier"></param>
        public void AddToQueue(FileCopier copier)
        {
            FileCopiers.Enqueue(copier);
            lock(QueueThreadLock)
                if (queueThread is null)
                    queueThread = this.CopierQueueThread();
        }

        /// <summary>
        /// Set a flag that declares no more file copiers will be added to the queue
        /// </summary>
        public void FinalizeQueue()
        {
            IsFinalized = true;
        }

        /// <summary>
        /// Gets a task that waits until all items in queue have completed
        /// </summary>
        /// <returns></returns>
        public async Task GetAwaiter()
        {
            if (queueThread != null)
            {
                await queueThread;
                await Task.WhenAll(ReadTasks);
                await Task.WhenAll(WriteTasks);
                while (RunningCopiers.Any())
                    await Task.Delay(50);
            }
        }

        private Task CopierQueueThread()
        {
            return Task.Factory.StartNew(() =>
           {
               bool isMultiThreaded = Command.CopyOptions.MultiThreadedCopiesCount > 0;
               while (!IsCancelled && IsCommandRunning)
               {
                   if (IsCommandPaused |
                       !isMultiThreaded && ReadTasks.Count > 0 ||
                       isMultiThreaded && ReadTasks.Count >= Command.CopyOptions.MultiThreadedCopiesCount
                       )
                   {
                       Thread.Sleep(25);
                   }
                   else if (FileCopiers.TryDequeue(out var copier))
                   {
                       copier.CopyFailed += Copier_CopyFailed;
                       copier.CopyCompleted += Copier_CopyCompleted;

                       lock (RunningCopiers)
                           RunningCopiers.Add(copier);

                       copier.SetStarted();
                       //Read Task
                       var readTask = copier.GetReadTask();
                       lock (ReadTasks)
                           ReadTasks.Add(readTask);
                       _ = readTask.ContinueWith(task =>
                       {
                           lock (ReadTasks)
                               ReadTasks.Remove(task);
                       });

                       //WriteTask
                       Task writeTask;
                       if (!Command.CopyOptions.Mirror && (Command.CopyOptions.MoveFiles | Command.CopyOptions.MoveFilesAndDirectories))
                           writeTask = copier.GetMoveTask(SetAttributesAction);
                       else
                           writeTask = copier.GetWriteTask(SetAttributesAction);
                       lock (WriteTasks)
                           WriteTasks.Add(writeTask);
                       _ = writeTask.ContinueWith(task =>
                       {
                           lock (WriteTasks)
                               WriteTasks.Remove(task);
                       });

                       //Start the tasks
                       readTask.Start();
                       writeTask.Start();
                   }
                   else if (FileCopiers.IsEmpty)
                   {
                       if (IsFinalized)
                           break;
                       Thread.Sleep(10);
                   }
               }
           }, TaskCreationOptions.LongRunning);
        }

        private void Copier_CopyFailed(object sender, FileCopyFailedEventArgs e)
        {
            if (e.WasFailed | e.WasCancelled)
                lock (RunningCopiers)
                    RunningCopiers.Remove(sender as FileCopier);
        }

        private void Copier_CopyCompleted(object sender, FileCopyCompletedEventArgs e)
        {
            lock (RunningCopiers)
                RunningCopiers.Remove(sender as FileCopier);
        }

        public ParallelLoopResult Pause()
        {
            lock(RunningCopiers)
            {
                return Parallel.ForEach(RunningCopiers, f => f.Pause());
            }
        }

        public ParallelLoopResult Resume()
        {
            lock (RunningCopiers)
            {
                return Parallel.ForEach(RunningCopiers, f => f.Resume());
            }
        }

        /// <summary>
        /// Cancel all the active copiers
        /// </summary>
        public ParallelLoopResult Cancel()
        {
            lock (RunningCopiers)
            {
                return Parallel.ForEach(RunningCopiers, f => f.Cancel());
            }
        }

        #endregion

    }
}
