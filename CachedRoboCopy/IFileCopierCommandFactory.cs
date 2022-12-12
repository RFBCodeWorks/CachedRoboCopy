using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RFBCodeWorks.RoboSharpExtensions
{
    /// <summary>
    /// Factory that creates <see cref="FileCopierCommand"/> objects
    /// </summary>
    public interface IFileCopierCommandFactory
    {
        /// <inheritdoc cref="IFileCopierFactory"/>
        IFileCopierFactory FileCopierFactory { get; }

        /// <summary>
        /// Create a new <see cref="FileCopierCommand"/> object that will facilitate copying loose source files into their respective destinations
        /// </summary>
        /// <returns>A new <see cref="FileCopierCommand"/></returns>
        FileCopierCommand CreateFileCopierCommand();

        /// <inheritdoc cref="CreateFileCopierCommand()"/>
        FileCopierCommand CreateFileCopierCommand(params IFileCopier[] copiers);

        /// <inheritdoc cref="CreateFileCopierCommand()"/>
        FileCopierCommand CreateFileCopierCommand(IEnumerable<IFileCopier> copiers);
    }

    /// <inheritdoc cref="IFileCopierCommandFactory"/>
    public class FileCopierCommandFactory : IFileCopierCommandFactory
    {
        /// <inheritdoc/>
        public virtual IFileCopierFactory FileCopierFactory
        {
            get => fileCopierFactory ?? RoboSharpExtensions.FileCopier.Factory;
            init => fileCopierFactory = value;
        }
        private IFileCopierFactory fileCopierFactory;

        /// <inheritdoc/>
        public virtual FileCopierCommand CreateFileCopierCommand()
        {
            return new FileCopierCommand()
            {
                FileCopierFactory = FileCopierFactory
            };
        }

        /// <inheritdoc/>
        public virtual FileCopierCommand CreateFileCopierCommand(params IFileCopier[] copiers)
        {
            var copier = CreateFileCopierCommand();
            copier.AddCommand(copiers);
            return copier;
        }

        /// <inheritdoc/>
        public virtual FileCopierCommand CreateFileCopierCommand(IEnumerable<IFileCopier> copiers)
        {
            var copier = CreateFileCopierCommand();
            copier.AddCommand(copiers);
            return copier;
        }
    }
}
