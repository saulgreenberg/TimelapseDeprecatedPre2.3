namespace Timelapse.Data.FileData
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Store that manages file related data.
    /// </summary>
    public abstract class FileDataStore : IFileDataStore
    {
        /// <summary>
        /// The database abstraction used to manage data.
        /// </summary>
        protected ITimelapseDatabase Database = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileDataStore"/> class.
        /// </summary>
        /// <param name="database">The database to manage data commands.</param>
        public FileDataStore(ITimelapseDatabase database)
        {
            // Database is required, throw an exception if it is not defined.
            this.Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Inserts the specified files into the data store.
        /// </summary>
        /// <param name="models">The models to insert into the data store.</param>
        /// <returns>The number of records inserted.</returns>
        public int InsertFiles(List<FileModel> models)
        {
            DbCommand command = this.CreateInsertCommand(models);
            return this.Database.ExecuteNonQuery(command);
        }

        /// <summary>
        /// Inserts the specified files into the data store.
        /// </summary>
        /// <param name="models">The models to insert into the data store.</param>
        /// <param name="cancellationToken">The token used to track task cancellation.</param>
        /// <returns>A <see cref="Task"/>.</returns>
        public Task<int> InsertFilesAsync(List<FileModel> models, CancellationToken cancellationToken = default(CancellationToken))
        {
            DbCommand command = this.CreateInsertCommand(models);
            return this.Database.ExecuteNonQueryAsync(command, cancellationToken);
        }

        /// <summary>
        /// Creates a <see cref="DbCommand"/> instance to use for inserting data.
        /// </summary>
        /// <param name="models">The collection of <see cref="FileModel"/> instances to insert into the data store.</param>
        /// <returns>A database command instance.</returns>
        protected abstract DbCommand CreateInsertCommand(List<FileModel> models);
    }
}