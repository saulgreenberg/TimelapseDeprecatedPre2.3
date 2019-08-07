namespace Timelapse.Data
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An abstraction around a database for managing timelapse related data.
    /// </summary>
    public interface ITimelapseDatabase
    {
        /// <summary>
        /// Executes the specified command.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        int ExecuteNonQuery(DbCommand command);

        /// <summary>
        /// Executes the specified command.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="cancellationToken">The token used to signal task cancellation.</param>
        /// <returns>A <see cref="Task"/>.</returns>
        Task<int> ExecuteNonQueryAsync(DbCommand command, CancellationToken cancellationToken = default(CancellationToken));
    }
}
