namespace Timelapse.Data.SQLite
{
    using System.Data.Common;
    using System.Data.SQLite;

    /// <summary>
    /// An abstraction around a database for managing timelapse related data in a SQLite database.
    /// </summary>
    public class SQLiteDatabase : TimelapseDatabase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SQLiteDatabase"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string to use.</param>
        public SQLiteDatabase(string connectionString)
            : base(connectionString)
        { }

        /// <summary>
        /// Gets a <see cref="DbConnection"/> to use for executing database commands.
        /// </summary>
        /// <returns>A database connection instance.</returns>
        protected override DbConnection GetDbConnection()
        {
            // Note that the 2nd argument is ParseViaFramework. This is included to resolve an issue that occurs when users try to open a network file on some VPNs, eg., Cisco VPN and perhaps other network file systems Its an obscur bug and solution reported by others: sqlite doesn't really document
            // that argument very well. But it seems to fix it.
            return new SQLiteConnection(this.connectionString, true);
        }
    }
}