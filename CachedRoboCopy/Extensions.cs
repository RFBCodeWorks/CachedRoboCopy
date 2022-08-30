using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace RFBCodeWorks.CachedRoboCopy
{
    static class Extensions
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

        #endregion)
    }
}
