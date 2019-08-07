namespace Timelapse.Data.SQLite.FileData
{
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Data.SQLite;
    using Timelapse.Data.FileData;

    /// <summary>
    /// Store that manages file related data in SQLite.
    /// </summary>
    public class SQLiteFileDataStore : FileDataStore
    {
        /// <summary>
        /// Defines the datbase connection string to use.
        /// </summary>
        private readonly string connectionString = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="SQLiteFileDataStore"/> class.
        /// </summary>
        /// <param name="database">The database to manage data commands.</param>
        public SQLiteFileDataStore(ITimelapseDatabase database, string connectionString)
            : base(database)
        {
            this.connectionString = connectionString;
        }

        /// <summary>
        /// Creates a <see cref="DbCommand"/> instance to use for inserting data.
        /// </summary>
        /// <param name="models">The collection of <see cref="FileModel"/> instances to insert into the data store.</param>
        /// <returns>A database command instance.</returns>
        protected override DbCommand CreateInsertCommand(List<FileModel> models)
        {
            SQLiteCommand command = new SQLiteCommand();

            // TODO: use the collection of models to generate a DbCommand once the FileModel is defined.
            throw new System.NotImplementedException("'FileModel' needs to be defined before this method can be used.");
        }
    }
}