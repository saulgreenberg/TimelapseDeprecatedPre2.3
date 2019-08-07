namespace Timelapse.Data
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Timelapse.Common;

    /// <summary>
    /// An abstraction around a database for managing timelapse related data.
    /// </summary>
    public abstract class TimelapseDatabase : ITimelapseDatabase
    {
        /// <summary>
        /// The database connection string.
        /// </summary>
        protected readonly string connectionString = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimelapseDatabase"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string to use.</param>
        public TimelapseDatabase(string connectionString)
        {
            this.connectionString = connectionString;
        }

        /// <summary>
        /// Executes the specified command.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        public virtual int ExecuteNonQuery(DbCommand command)
        {
            if (command == null) { throw new ArgumentNullException(nameof(DbCommand)); }

            try
            {
                using (DbConnection connection = this.GetDbConnection())
                {
                    connection.Open();
                    command.Connection = connection;
                    return command.ExecuteNonQuery();
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage($"Failure near executing statement '{command.CommandText}' In ExecuteCommand. {exception.ToString()}");
                return 0;
            }
        }

        /// <summary>
        /// Executes the specified command.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="cancellationToken">The token used to signal task cancellation.</param>
        /// <returns>A <see cref="Task"/>.</returns>
        public virtual Task<int> ExecuteNonQueryAsync(DbCommand command, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (command == null) { throw new ArgumentNullException(nameof(DbCommand)); }

            try
            {
                using (DbConnection connection = this.GetDbConnection())
                {
                    connection.Open();
                    command.Connection = connection;
                    return command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage($"Failure near executing statement '{command.CommandText}' In ExecuteCommand. {exception.ToString()}");
                return Task<int>.FromResult(0);
            }
        }

        /// <summary>
        /// Gets a <see cref="DbConnection"/> to use for executing database commands.
        /// </summary>
        /// <returns>A database connection instance.</returns>
        protected abstract DbConnection GetDbConnection();
    }
}