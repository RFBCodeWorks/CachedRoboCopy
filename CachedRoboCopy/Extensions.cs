using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.IO;

namespace RFBCodeWorks.RoboSharpExtensions
{
    internal static class MiscExtensions
    {
        #region < WhenAll >

        /// <summary>
        /// Cancellable WhenAll function
        /// </summary>
        /// <inheritdoc cref="Task.WhenAll(IEnumerable{Task})"/>
        public static Task WhenAll(this IEnumerable<Task> tasks, CancellationToken token, int milliseconds = 50)
        {
            return Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && tasks.Any(t => t.Status < TaskStatus.RanToCompletion))
                {
                    await Task.Delay(milliseconds);
                }
            });
        }

        /// <inheritdoc cref="Task.WhenAll(IEnumerable{Task})"/>
        public static Task WhenAll(this IEnumerable<Task> tasks) => Task.WhenAll(tasks);

        #endregion

        /// <summary>
        /// Check if the directory has any files
        /// </summary>
        /// <param name="dir"></param>
        /// <returns>TRUE if <see cref="DirectoryInfo.EnumerateFiles()"/> has atleast 1 file</returns>
        public static bool HasFiles(this DirectoryInfo dir) => dir.Exists && dir.EnumerateFiles().Any();

        /// <summary>
        /// Check if the directory has any subdirectories
        /// </summary>
        /// <param name="dir"></param>
        /// <returns>TRUE if <see cref="DirectoryInfo.EnumerateDirectories()"/> has atleast 1 file</returns>
        public static bool HasSubDirectories(this DirectoryInfo dir) => dir.Exists && dir.EnumerateDirectories().Any();

        /// <summary>
        /// Check if the directory has any files or subdirectories
        /// </summary>
        /// <param name="dir"></param>
        /// <returns>TRUE HasFiles and HasSubdirectories are both false (meaning there are no children). FALSE is any files/subdirectories exist.</returns>
        public static bool IsEmpty(this DirectoryInfo dir) => !dir.HasFiles() && !dir.HasSubDirectories();
    }
}
