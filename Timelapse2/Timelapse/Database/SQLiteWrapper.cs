using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Detection;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Database
{
    // A wrapper to make it easy to invoke some basic SQLite commands
    // It is NOT a generalized wrapper, as it only handles a few simple things.
    public class SQLiteWrapper
    {
        // A connection string identifying the  database file. Takes the form:
        // "Data Source=filepath" 
        private readonly string connectionString;

        /// <summary>
        /// Constructor: Create a database file if it does not exist, and then create a connection string to that file
        /// If the database file does not exist,iIt will be created
        /// </summary>
        /// <param name="inputFile">the file containing the database</param>
        public SQLiteWrapper(string inputFile)
        {
            if (!File.Exists(inputFile))
            {
                SQLiteConnection.CreateFile(inputFile);
            }
            SQLiteConnectionStringBuilder connectionStringBuilder = new SQLiteConnectionStringBuilder()
            {
                DataSource = inputFile,
                DateTimeKind = DateTimeKind.Utc
            };
            // Enable foreign keys
            connectionStringBuilder.ForeignKeys = true;
            this.connectionString = connectionStringBuilder.ConnectionString;
        }

        /// <summary>
        /// A simplified table creation routine. It expects the column definitions to be supplied
        /// as a column_name, data type key value pair. 
        // The table creation syntax supported is:
        // CREATE TABLE table_name (
        //     column1name datatype,       e.g.,   Id INT PRIMARY KEY OT NULL,
        //     column2name datatype,               NAME TEXT NOT NULL,
        //     ...                                 ...
        //     columnNname datatype);              SALARY REAL);
        /// </summary>
        public void CreateTable(string tableName, List<ColumnDefinition> columnDefinitions)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnDefinitions, nameof(columnDefinitions));

            string query = Sql.CreateTable + tableName + Sql.OpenParenthesis + Environment.NewLine;               // CREATE TABLE <tablename> (
            foreach (ColumnDefinition column in columnDefinitions)
            {
                query += column.ToString() + Sql.Comma + Environment.NewLine;             // "columnname TEXT DEFAULT 'value',\n" or similar
            }
            query = query.Remove(query.Length - Sql.Comma.Length - Environment.NewLine.Length);         // remove last comma / new line and replace with );
            query += Sql.CloseParenthesis + Sql.Semicolon;
            this.ExecuteNonQuery(query);
        }

        #region Indexes: Create or Drop
        // Create an index in table tableName named index name to the column names
        public void CreateIndex(string indexName, string tableName, string columnNames)
        {
            // Form: CREATE INDEX IF NOT EXISTS indexName ON tableName  (column1, column2...);
            string query = Sql.CreateIndex + Sql.IfNotExists + indexName + Sql.On + tableName + Sql.OpenParenthesis + columnNames + Sql.CloseParenthesis;
            this.ExecuteNonQuery(query);
        }

        // Drop an index named indexName if it exists
        public void DropIndex(string indexName)
        {
            // Form: DROP INDEX IF EXISTS indexName 
            string query = Sql.DropIndex + Sql.IfExists + indexName;
            this.ExecuteNonQuery(query);
        }
        #endregion

        // Return a dictionary comprising each column in the schema and its default values (if any)
        public Dictionary<string, string> SchemaGetColumnsAndDefaultValues(string tableName)
        {
            try
            {
                // Open the connection
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();
                    SQLiteDataReader reader = GetSchema(connection, tableName);
                    Dictionary<string, string> columndefaultsDict = new Dictionary<string, string>();
                    while (reader.Read())
                    {
                        columndefaultsDict.Add(reader[1].ToString(), reader[4] != null ? reader[4].ToString() : String.Empty);
                    }
                    return columndefaultsDict;
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure executing getschema in GetColumnAndDefaultValue. {0}", exception.ToString()));
                return null;
            }
        }

        private void DataTableColumns_Changed(object sender, CollectionChangeEventArgs columnChange)
        {
            // DateTime columns default to DataSetDateTime.UnspecifiedLocal, which converts fully qualified DateTimes returned from SQLite to DateTimeKind.Unspecified
            // Since the DATETIME and TIME columns in Timelapse are UTC change this to DataSetDateTime.Utc to get DateTimeKind.Utc.  This must be done before any rows 
            // are added to the table.  This callback is the only way to access the column schema from within DataTable.Load() to make the change.
            DataColumn columnChanged = (DataColumn)columnChange.Element;
            if (columnChanged.DataType == typeof(DateTime))
            {
                columnChanged.DateTimeMode = DataSetDateTime.Utc;
            }
        }

        // Construct each individual query in the form 
        // INSERT INTO table_name
        //      colname1, colname12, ... colnameN VALUES
        //      ('value1', 'value2', ... 'valueN');
        public void Insert(string tableName, List<List<ColumnTuple>> insertionStatements)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(insertionStatements, nameof(insertionStatements));


            List<string> queries = new List<string>();
            foreach (List<ColumnTuple> columnsToUpdate in insertionStatements)
            {
                Debug.Assert(columnsToUpdate != null && columnsToUpdate.Count > 0, "No column updates are specified.");

                string columns = String.Empty;
                string values = String.Empty;
                foreach (ColumnTuple column in columnsToUpdate)
                {
                    columns += String.Format(" {0}" + Sql.Comma, column.Name);      // transform dictionary entries into a string "col1, col2, ... coln"
                    values += String.Format(" {0}" + Sql.Comma, Utilities.QuoteForSql(column.Value));         // transform dictionary entries into a string "'value1', 'value2', ... 'valueN'"
                }
                if (columns.Length > 0)
                {
                    columns = columns.Substring(0, columns.Length - Sql.Comma.Length);     // Remove last comma in the sequence to produce (col1, col2, ... coln)  
                }
                if (values.Length > 0)
                {
                    values = values.Substring(0, values.Length - Sql.Comma.Length);        // Remove last comma in the sequence 
                }

                // Construct the query. The newlines are to format it for pretty printing
                string query = Sql.InsertInto + tableName;               // INSERT INTO table_name
                query += Environment.NewLine;
                query += String.Format("({0}) ", columns);                         // (col1, col2, ... coln)
                query += Environment.NewLine;
                query += Sql.Values;                                     // VALUES
                query += Environment.NewLine;
                query += String.Format("({0}); ", values);                         // ('value1', 'value2', ... 'valueN');
                queries.Add(query);
            }

            // Now try to invoke the batch queries
            this.ExecuteNonQueryWrappedInBeginEnd(queries);
        }

        public DataTable GetDataTableFromSelect(string query)
        {
            DataTable dataTable = new DataTable();
            try
            {
                // Open the connection
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        command.CommandText = query;
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            dataTable.Columns.CollectionChanged += this.DataTableColumns_Changed;
                            dataTable.Load(reader);
                            return dataTable;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure executing query '{0}' in GetDataTableFromSelect. {1}", query, exception.ToString()));
                return dataTable;
            }
        }

        public List<object> GetDistinctValuesInColumn(string tableName, string columnName)
        {
            using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(connection))
                {
                    command.CommandText = String.Format(Sql.SelectDistinct + " {0} " + Sql.From + "{1}", columnName, tableName);
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        List<object> distinctValues = new List<object>();
                        while (reader.Read())
                        {
                            distinctValues.Add(reader[columnName]);
                        }
                        return distinctValues;
                    }
                }
            }
        }

        /// <summary>
        /// Run a generic Select query against the Database, with a single result returned as an object that must be cast. 
        /// </summary>
        /// <param name="query">The SQL to run</param>
        /// <returns>A value containing the single result.</returns>
        private object GetScalarFromSelect(string query)
        {
            try
            {
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                        command.CommandText = query;
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                        return command.ExecuteScalar();
                    }
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure executing query '{0}' in GetObjectFromSelect: {1}", query, exception.ToString()));
                return null;
            }
        }

        /// <summary>
        /// Allows the programmer to interact with the database for purposes other than a query.
        /// </summary>
        /// <param name="statement">The SQL to be run.</param>
        public void ExecuteNonQuery(string statement)
        {
            try
            {
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        command.CommandText = statement;
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure executing statement '{0}'. in ExecuteNonQuery:{1}", statement, exception.ToString()));
            }
        }

        /// <summary>
        /// Given a list of complete queries, wrap up to 500 of them in a BEGIN/END statement so they are all executed in one go for efficiency
        /// Continue for the next up to 500, and so on.
        // BEGIN
        //      query1
        //      query2
        //      ...
        //      queryn
        // END
        /// </summary>
        public void ExecuteNonQueryWrappedInBeginEnd(List<string> statements)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(statements, nameof(statements));

            const int MaxStatementCount = 50000;
            string mostRecentStatement = null;
            try
            {
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();

                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        // Invoke each query in the queries list
                        int rowsUpdated = 0;
                        int statementsInQuery = 0;
                        foreach (string statement in statements)
                        {
                            // capture the most recent statement so it's available for debugging
                            mostRecentStatement = statement;
                            statementsInQuery++;

                            // Insert a BEGIN if we are at the beginning of the count
                            if (statementsInQuery == 1)
                            {
                                command.CommandText = Sql.BeginTransaction;
                                command.ExecuteNonQuery();
                            }

                            command.CommandText = statement;
                            rowsUpdated += command.ExecuteNonQuery();

                            // END
                            if (statementsInQuery >= MaxStatementCount)
                            {
                                command.CommandText = Sql.EndTransaction;
                                rowsUpdated += command.ExecuteNonQuery();
                                statementsInQuery = 0;
                            }
                        }
                        // END
                        if (statementsInQuery != 0)
                        {
                            command.CommandText = Sql.EndTransaction;
                            rowsUpdated += command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure near executing statement '{0}' n ExecuteNonQueryWrappedInBeginEnd. {1}", mostRecentStatement, exception.ToString()));
            }
        }

        /// <summary>
        /// Executes the incoming string as a Single SQL command/>.
        /// </summary>
        /// <param name="commandString">The command to execute.</param>
        public void ExecuteOneNonQueryCommand(string commandString)
        {
            if (string.IsNullOrEmpty(commandString))
            {
                return;
            }
            SQLiteCommand command = new SQLiteCommand
            {
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                CommandText = commandString
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            };

            try
            {
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();
                    command.Connection = connection;
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure near executing statement '{0}' In ExecuteCommand. {1}", command.CommandText, exception.ToString()));
            }
            if (command != null)
            {
                command.Dispose();
            }
        }

        // Trims all the white space from the data held in the list of column_names in table_name
        // Note: this is needed as earlier versions didn't trim the white space from the data. This allows us to trim it in the database after the fact.
        public void TrimWhitespace(string tableName, List<string> columnNames)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnNames, nameof(columnNames));

            List<string> queries = new List<string>();
            foreach (string columnName in columnNames)
            {
                string query = Sql.Update + tableName + Sql.Set + columnName + " = " + Sql.Trim + Sql.OpenParenthesis + columnName + Sql.CloseParenthesis + Sql.Semicolon; // Form: UPDATE tablename SET columname = TRIM(columnname);
                queries.Add(query);
            }
            this.ExecuteNonQueryWrappedInBeginEnd(queries);
        }

        // Set all rows in a given column to a single value
        public void SetColumnToACommonValue(string tableName, string columnName, string value)
        {
            string query = Sql.Update + tableName + Sql.Set + columnName + Sql.Equal + Utilities.QuoteForSql(value);
            this.ExecuteNonQuery(query);
        }

        // Convert all nulls in the list of column_names in table_name
        // Note: this is needed as a prior version did not always set the defaults for the data, which meant that they may have introduced null values. 
        // As I don't handle nulls well, its possible that this could introduce crashes.
        public void ChangeNullToEmptyString(string tableName, List<string> columnNames)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnNames, nameof(columnNames));

            List<string> queries = new List<string>();
            foreach (string columnName in columnNames)
            {
                string query = Sql.Update + tableName + Sql.Set + columnName + " = '' " + Sql.Where + columnName + Sql.IsNull + Sql.Semicolon; // Form: UPDATE tablename SET columname = '' WHERE columnname IS NULL;
                queries.Add(query);
            }
            this.ExecuteNonQueryWrappedInBeginEnd(queries);
        }

        public void Update(string tableName, List<ColumnTuplesWithWhere> updateQueryList)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(updateQueryList, nameof(updateQueryList));

            List<string> queries = new List<string>();
            foreach (ColumnTuplesWithWhere updateQuery in updateQueryList)
            {
                string query = CreateUpdateQuery(tableName, updateQuery);
                if (String.IsNullOrEmpty(query))
                {
                    continue; // skip non-queries
                }
                queries.Add(query);
            }
            this.ExecuteNonQueryWrappedInBeginEnd(queries);
        }

        /// <summary>
        /// Update specific rows in the DB as specified in the where clause.
        /// </summary>
        /// <param name="tableName">The table to update.</param>
        /// <param name="columnsToUpdate">The column names and their new values.</param>
        // UPDATE table_name SET 
        // colname1 = value1, 
        // colname2 = value2,
        // ...
        // colnameN = valueN
        // WHERE
        // <condition> e.g., ID=1;
        public void Update(string tableName, ColumnTuplesWithWhere columnsToUpdate)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnsToUpdate, nameof(columnsToUpdate));

            string query = CreateUpdateQuery(tableName, columnsToUpdate);
            this.ExecuteNonQuery(query);
        }

        // UPDATE table_name SET 
        // columnname = value, 
        public void Update(string tableName, ColumnTuple columnToUpdate)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnToUpdate, nameof(columnToUpdate));

            string query = Sql.Update + tableName + Sql.Set;
            query += String.Format(" {0} = {1}", columnToUpdate.Name, Utilities.QuoteForSql(columnToUpdate.Value));
            this.ExecuteNonQuery(query);
        }

        // Return a single update query as a string
        private static string CreateUpdateQuery(string tableName, ColumnTuplesWithWhere columnsToUpdate)
        {
            if (columnsToUpdate.Columns.Count < 1)
            {
                return String.Empty;
            }
            // UPDATE tableName SET 
            // colname1 = value1, 
            // colname2 = value2,
            // ...
            // colnameN = valueN
            // WHERE
            // <condition> e.g., ID=1;
            string query = Sql.Update + tableName + Sql.Set;
            if (columnsToUpdate.Columns.Count < 0)
            {
                return String.Empty;     // No data, so nothing to update. This isn't really an error, so...
            }

            // column_name = 'value'
            foreach (ColumnTuple column in columnsToUpdate.Columns)
            {
                // we have to cater to different formats for integers, NULLS and strings...
                if (column.Value == null)
                {
                    query += String.Format(" {0} = {1}{2}", column.Name.ToString(), Sql.Null, Sql.Comma);
                }
                else
                {
                    query += String.Format(" {0} = {1}{2}", column.Name, Utilities.QuoteForSql(column.Value), Sql.Comma);
                }
            }
            query = query.Substring(0, query.Length - Sql.Comma.Length); // Remove the last comma

            if (String.IsNullOrWhiteSpace(columnsToUpdate.Where) == false)
            {
                query += Sql.Where;
                query += columnsToUpdate.Where;
            }
            query += Sql.Semicolon;
            return query;
        }

        /// <summary>
        /// Retrieve a count of items from the DB. Select statement must be of the form "Select Count(*) FROM "
        /// </summary>
        /// <param name="query">The query to run.</param>
        /// <returns>The number of items selected.</returns>
        public int GetCountFromSelect(string query)
        {
            return Convert.ToInt32(this.GetScalarFromSelect(query));
        }

        // The EXISTS query should be in the form of 
        // e.g., Select EXISTS  ( SELECT 1 FROM DataTable WHERE DeleteFlag='true')
        // which returns a 1 (true) or a 0 (false) if any matching row exists.
        // That result is transformed into a boolean true/false
        // The performance of this query depends upon how many rows in the table has to be searched
        // before the first exists appears. If there are no matching rows, the performance is more or
        // less equivalent to COUNT as it has to go through every row. 
        public bool GetExists(string query)
        {
            return (Convert.ToInt32(this.GetScalarFromSelect(query)) == 1);
        }

        // This method will create a column in a table of type TEXT, where it is added to its end
        // It assumes that the value, if not empty, should be treated as the default value for that column
        public void AddColumnToEndOfTable(string tableName, ColumnDefinition columnDefinition)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnDefinition, nameof(columnDefinition));

            this.ExecuteNonQuery(Sql.AlterTable + tableName + Sql.AddColumn + columnDefinition.ToString());
        }

        /// <summary>delete specific rows from the DB where...</summary>
        /// <param name="tableName">The table from which to delete.</param>
        /// <param name="where">The where clause for the delete.</param>
        public void DeleteRows(string tableName, string where)
        {
            // DELETE FROM table_name WHERE where
            string query = Sql.DeleteFrom + tableName;        // DELETE FROM table_name
            if (!String.IsNullOrWhiteSpace(where))
            {
                // Add the WHERE clause only when where is not empty
                query += Sql.Where;                   // WHERE
                query += where;                                 // where
            }
            this.ExecuteNonQuery(query);
        }

        /// <summary>
        /// Delete one or more rows from the DB, where each row is specified in the list of where clauses ..
        /// </summary>
        /// <param name="tableName">The table from which to delete</param>
        /// <param name="whereClauses">The where clauses for the row to delete (e.g., ID=1 ID=3 etc</param>
        public void Delete(string tableName, List<string> whereClauses)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(whereClauses, nameof(whereClauses));

            List<string> queries = new List<string>();                      // A list of SQL queries

            // Construct a list containing queries of the form DELETE FROM table_name WHERE where
            foreach (string whereClause in whereClauses)
            {
                // Add the WHERE clause only when uts is not empty
                if (!String.IsNullOrEmpty(whereClause.Trim()))
                {                                                            // Construct each query statement
                    string query = Sql.DeleteFrom + tableName;     // DELETE FROM tablename
                    query += Sql.Where;                            // DELETE FROM tablename WHERE
                    query += whereClause;                                    // DELETE FROM tablename WHERE whereClause
                    query += "; ";                                           // DELETE FROM tablename WHERE whereClause;
                    queries.Add(query);
                }
            }
            // Now try to invoke the batch queries
            if (queries.Count > 0)
            {
                this.ExecuteNonQueryWrappedInBeginEnd(queries);
            }
        }

        #region Schema and Column Changes: Replace Schema, IsColumnInTable / Add / Delete / Rename / 
        public void ReplaceTableSchemaWithNewColumnDefinitionsSchema(string sourceTable, List<ColumnDefinition> columnDefinitions)
        {
            string destTable = "TempTable";
            try
            {
                // Create an empty table with the schema based on columnDefinitions
                this.CreateTable(destTable, columnDefinitions);
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();

                    // copy the contents from the source table to the destination table
                    CopyAllValuesFromTable(connection, destTable, sourceTable, destTable);

                    // delete the source table, and rename the destination table so it is the same as the source table
                    DropTable(connection, sourceTable);
                    RenameTable(connection, destTable, sourceTable);
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure in CopyTableContentsToEmptyTable. {0}", exception.ToString()));
                throw;
            }
        }

        public bool IsColumnInTable(string sourceTable, string currentColumnName)
        {
            try
            {
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();
                    List<string> currentColumnNames = GetColumnNamesAsList(connection, sourceTable);
                    return currentColumnNames.Contains(currentColumnName);
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure in ColumnExists. {0}", exception.ToString()));
                return false;
            }
        }

        /// <summary>
        /// Add a column to the table named sourceTable at position columnNumber using the provided columnDefinition
        /// The value in columnDefinition is assumed to be the desired default value
        /// </summary>
        public void AddColumnToTable(string tableName, int columnNumber, ColumnDefinition columnDefinition)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnDefinition, nameof(columnDefinition));

            try
            {
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();

                    // Some basic error checking to make sure we can do the operation
                    List<string> columnNames = GetColumnNamesAsList(connection, tableName);

                    // Check if a column named Name already exists in the source Table. If so, abort as we cannot add duplicate column names
                    if (columnNames.Contains(columnDefinition.Name))
                    {
                        throw new ArgumentException(String.Format("Column '{0}' is already present in table '{1}'.", columnDefinition.Name, tableName), nameof(columnDefinition));
                    }

                    // If columnNumber would result in the column being inserted at the end of the table, then use the more efficient method to do so.
                    if (columnNumber >= columnNames.Count)
                    {
                        this.AddColumnToEndOfTable(tableName, columnDefinition);
                        return;
                    }

                    // We need to add a column elsewhere than the end. This requires us to 
                    // create a new schema, create a new table from that schema, copy data over to it, remove the old table
                    // and rename the new table to the name of the old one.

                    // Get a schema definition identical to the schema in the existing table, 
                    // but with a new column definition of type TEXT added at the given position, where the value is assumed to be the default value
                    string newSchema = InsertColumnInSchema(connection, tableName, columnNumber, columnDefinition);

                    // Create a new table 
                    string destTable = tableName + "NEW";
                    string sql = Sql.CreateTable + destTable + Sql.OpenParenthesis + newSchema + Sql.CloseParenthesis;
                    using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // Copy the old table's contents to the new table
                    CopyAllValuesFromTable(connection, tableName, tableName, destTable);

                    // Now drop the source table and rename the destination table to that of the source table
                    DropTable(connection, tableName);

                    // Rename the table
                    RenameTable(connection, destTable, tableName);
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure in AddColumn. {0}", exception.ToString()));
                throw;
            }
        }

        public bool DeleteColumn(string sourceTable, string columnName)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnName, nameof(columnName));

            try
            {
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();
                    // Some basic error checking to make sure we can do the operation
                    if (String.IsNullOrEmpty(columnName.Trim()))
                    {
                        return false;  // The provided column names= is an empty string
                    }
                    List<string> columnNames = GetColumnNamesAsList(connection, sourceTable);
                    if (!columnNames.Contains(columnName))
                    {
                        return false; // There is no column called columnName in the source Table, so we can't delete ti
                    }

                    // Get a schema definition identical to the schema in the existing table, 
                    // but with the column named columnName deleted from it
                    string newSchema = RemoveColumnFromSchema(connection, sourceTable, columnName);

                    // Create a new table 
                    string destTable = sourceTable + "NEW";
                    string sql = Sql.CreateTable + destTable + Sql.OpenParenthesis + newSchema + Sql.CloseParenthesis;
                    using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // Copy the old table's contents to the new table
                    CopyAllValuesFromTable(connection, destTable, sourceTable, destTable);

                    // Now drop the source table and rename the destination table to that of the source table
                    DropTable(connection, sourceTable);

                    // Rename the table
                    RenameTable(connection, destTable, sourceTable);
                    return true;
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure in DeleteColumn. {0}", exception.ToString()));
                throw;
            }
        }

        public void RenameColumn(string sourceTable, string currentColumnName, string newColumnName)
        {
            // Some basic error checking to make sure we can do the operation
            if (String.IsNullOrWhiteSpace(currentColumnName))
            {
                throw new ArgumentOutOfRangeException(nameof(currentColumnName));
            }
            if (String.IsNullOrWhiteSpace(newColumnName))
            {
                throw new ArgumentOutOfRangeException(nameof(newColumnName));
            }

            try
            {
                using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
                {
                    connection.Open();
                    List<string> currentColumnNames = GetColumnNamesAsList(connection, sourceTable);
                    if (currentColumnNames.Contains(currentColumnName) == false)
                    {
                        throw new ArgumentException(String.Format("No column named '{0}' exists to rename.", currentColumnName), nameof(currentColumnName));
                    }
                    if (currentColumnNames.Contains(newColumnName) == true)
                    {
                        throw new ArgumentException(String.Format("Column '{0}' is already in use.", newColumnName), nameof(newColumnName));
                    }

                    string newSchema = CloneSchemaButRenameColumn(connection, sourceTable, currentColumnName, newColumnName);

                    // Create a new table 
                    string destTable = sourceTable + "NEW";
                    string sql = Sql.CreateTable + destTable + Sql.OpenParenthesis + newSchema + Sql.CloseParenthesis;
                    using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // Copy the old table's contents to the new table
                    CopyAllValuesBetweenTables(connection, sourceTable, destTable, sourceTable, destTable);

                    // Now drop the source table and rename the destination table to that of the source table
                    DropTable(connection, sourceTable);

                    // Rename the table
                    RenameTable(connection, destTable, sourceTable);
                }
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Failure in RenameColumn. {0}", exception.ToString()));
                throw;
            }
        }



        private static void AddColumnToEndOfTable(SQLiteConnection connection, string tableName, string columnDefinition)
        {
            string sql = Sql.AlterTable + tableName + Sql.AddColumn + columnDefinition;
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }
        #endregion

        /// <summary>
        /// Copy all the values from the source table into the destination table. Assumes that both tables are populated with identically-named columns
        /// </summary>
        private static void CopyAllValuesFromTable(SQLiteConnection connection, string schemaFromTable, string dataSourceTable, string dataDestinationTable)
        {
            string commaSeparatedColumns = GetColumnNamesAsString(connection, schemaFromTable);
            string sql = Sql.InsertInto + dataDestinationTable + Sql.OpenParenthesis + commaSeparatedColumns + Sql.CloseParenthesis + Sql.Select + commaSeparatedColumns + Sql.From + dataSourceTable;

            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Copy all the values from the source table into the destination table. Assumes that both tables are populated with identically-named columns
        /// </summary>
        private static void CopyAllValuesBetweenTables(SQLiteConnection connection, string schemaFromSourceTable, string schemaFromDestinationTable, string dataSourceTable, string dataDestinationTable)
        {
            string commaSeparatedColumnsSource = GetColumnNamesAsString(connection, schemaFromSourceTable);
            string commaSeparatedColumnsDestination = GetColumnNamesAsString(connection, schemaFromDestinationTable);
            string sql = Sql.InsertInto + dataDestinationTable + Sql.OpenParenthesis + commaSeparatedColumnsDestination + Sql.CloseParenthesis + Sql.Select + commaSeparatedColumnsSource + Sql.From + dataSourceTable;

            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Drop the database table 'tableName' from the connected database.
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param>
        /// <param name="tableName">the name of the table</param>
        private static void DropTable(SQLiteConnection connection, string tableName)
        {
            // Turn foreign keys oof, do the operaton, then turn it backon. 
            // This is because if we drop a table that has foreign keys in it, we need to make sure foreign keys are off
            // as otherwise it will delete the foreign key table contents.
            SQLiteWrapper.SetPragmaForeignKeys(connection, false);

            // Drop the table
            string sql = Sql.DropTable + tableName;
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }

            SQLiteWrapper.SetPragmaForeignKeys(connection, true);
        }



        public void DropTable(string tableName)
        {
            using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
            {
                connection.Open();
                DropTable(connection, tableName);
            }
        }

        private static List<string> GetColumnDefinitions(SQLiteConnection connection, string tableName)
        {
            using (SQLiteDataReader reader = GetSchema(connection, tableName))
            {
                List<string> columnDefinitions = new List<string>();
                while (reader.Read())
                {
                    string existingColumnDefinition = String.Empty;
                    for (int field = 0; field < reader.FieldCount; field++)
                    {
                        switch (field)
                        {
                            case 0:  // cid (Column Index)
                                break;
                            case 1:  // name (Column Name)
                            case 2:  // type (Column type)
                                existingColumnDefinition += reader[field].ToString() + " ";
                                break;
                            case 3:  // notnull (Column has a NOT NULL constraint)
                                if (reader[field].ToString() != "0")
                                {
                                    existingColumnDefinition += Sql.NotNull;
                                }
                                break;
                            case 4:  // dflt_value (Column has a default value)
                                string s = reader[field].ToString();
                                if (!String.IsNullOrEmpty(s))
                                {
                                    existingColumnDefinition += Sql.Default + reader[field].ToString() + " ";
                                }
                                break;
                            case 5:  // pk (Column is part of the primary key)
                                if (reader[field].ToString() != "0")
                                {
                                    existingColumnDefinition += Sql.PrimaryKey;
                                }
                                break;
                            default:
                                // This should never happen
                                // But if it does, we just ignore it
                                System.Diagnostics.Debug.Print("Unknown Field: " + field.ToString());
                                break;
                        }
                    }
                    existingColumnDefinition = existingColumnDefinition.TrimEnd(' ');
                    columnDefinitions.Add(existingColumnDefinition);
                }
                return columnDefinitions;
            }
        }

        /// <summary>
        /// Return a list of all the column names in the  table named 'tableName' from the connected database.
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param>
        /// <param name="tableName">the name of the table</param>
        /// <returns>a list of all the column names in the  table</returns>
        private static List<string> GetColumnNamesAsList(SQLiteConnection connection, string tableName)
        {
            SQLiteDataReader reader = GetSchema(connection, tableName);
            List<string> columnNames = new List<string>();
            while (reader.Read())
            {
                columnNames.Add(reader[1].ToString());
            }
            return columnNames;
        }

        /// <summary>
        /// Return a comma separated string of all the column names in the  table named 'tableName' from the connected database.
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param>
        /// <param name="tableName">the name of the table</param>
        /// <returns>a comma separated string of all the column names in the table</returns>
        private static string GetColumnNamesAsString(SQLiteConnection connection, string tableName)
        {
            return String.Join(", ", GetColumnNamesAsList(connection, tableName));
        }

        /// <summary>
        /// Get the Schema for a simple database table 'tableName' from the connected database.
        /// For each column, it can retrieve schema settings including:
        ///     Name, Type, If its the Primary Key, Constraints including its Default Value (if any) and Not Null 
        /// However other constraints that may be set in the table schema are NOT returned, including:
        ///     UNIQUE, CHECK, FOREIGN KEYS, AUTOINCREMENT 
        /// If you use those, the schema may either ignore them or return odd values. So check it!
        /// Usage example: SQLiteDataReader reader = this.GetSchema(connection, "tableName");
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param>
        /// <param name="tableName">the  name of the table</param>
        /// <returns>
        /// The schema as a SQLiteDataReader.To examine it, do a while loop over reader.Read() to read a column at a time after every read
        /// access the column's attributes, where 
        /// -reader[0] is column number (e.g., 0)
        /// -reader[1] is column name (e.g., Employee)
        /// -reader[2] is type (e.g., STRING)
        /// -reader[3] to [5] also returns values, but not yet sure what they stand for.. maybe 'Primary Key Autoincrement'?
        /// </returns>
        private static SQLiteDataReader GetSchema(SQLiteConnection connection, string tableName)
        {
            string sql = Sql.PragmaTableInfo + Sql.OpenParenthesis + tableName + Sql.CloseParenthesis; // Syntax is: PRAGMA TABLE_INFO (tableName)
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                return command.ExecuteReader();
            }
        }

        /// <summary>
        /// Add a column definition into the provided schema at the given column location
        /// </summary>
        private static string InsertColumnInSchema(SQLiteConnection connection, string tableName, int newColumnNumber, ColumnDefinition newColumn)
        {
            List<string> columnDefinitions = GetColumnDefinitions(connection, tableName);
            columnDefinitions.Insert(newColumnNumber, newColumn.ToString());
            return String.Join(", ", columnDefinitions);
        }

        /// <summary>
        /// Create a schema cloned from tableName, except with the column definition for columnName deleted
        /// </summary>
        private static string RemoveColumnFromSchema(SQLiteConnection connection, string tableName, string columnName)
        {
            List<string> columnDefinitions = GetColumnDefinitions(connection, tableName);
            int columnToRemove = -1;
            for (int column = 0; column < columnDefinitions.Count; ++column)
            {
                string columnDefinition = columnDefinitions[column];
                if (columnDefinition.StartsWith(columnName))
                {
                    columnToRemove = column;
                    break;
                }
            }
            if (columnToRemove == -1)
            {
                throw new ArgumentOutOfRangeException(String.Format("Column '{0}' not found in table '{1}'.", columnName, tableName));
            }

            columnDefinitions.RemoveAt(columnToRemove);
            return String.Join(", ", columnDefinitions);
        }

        /// <summary>
        /// Create a schema cloned from tableName, except with the column definition for columnName deleted
        /// </summary>
        private static string CloneSchemaButRenameColumn(SQLiteConnection connection, string tableName, string existingColumnName, string newColumnName)
        {
            string newSchema = String.Empty;
            SQLiteDataReader reader = GetSchema(connection, tableName);
            while (reader.Read())
            {
                string existingColumnDefinition = String.Empty;

                // Copy the existing column definition unless its the column named columnNam
                for (int field = 0; field < reader.FieldCount; field++)
                {
                    switch (field)
                    {
                        case 0:  // cid (Column Index)
                            break;
                        case 1:  // name (Column Name)
                            // Rename the column if it is the one to be renamed
                            existingColumnDefinition += (reader[1].ToString() == existingColumnName) ? newColumnName : reader[1].ToString();
                            existingColumnDefinition += " ";
                            break;
                        case 2:  // type (Column type)
                            existingColumnDefinition += reader[field].ToString() + " ";
                            break;
                        case 3:  // notnull (Column has a NOT NULL constraint)
                            if (reader[field].ToString() != "0")
                            {
                                existingColumnDefinition += Sql.NotNull;
                            }
                            break;
                        case 4:  // dflt_value (Column has a default value)
                            if (String.IsNullOrEmpty(reader[field].ToString()))
                            {
                                existingColumnDefinition += Sql.Default + Utilities.QuoteForSql(reader[field].ToString()) + " ";
                            }
                            break;
                        case 5:  // pk (Column is part of the primary key)
                            if (reader[field].ToString() != "0")
                            {
                                existingColumnDefinition += Sql.PrimaryKey;
                            }
                            break;
                    }
                }
                existingColumnDefinition = existingColumnDefinition.TrimEnd(' ');
                newSchema += existingColumnDefinition + ", ";
            }
            newSchema = newSchema.TrimEnd(',', ' '); // remove last comma
            return newSchema;
        }

        /// <summary>
        /// Rename the database table named 'tableName' to 'new_tableName'  
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param>
        /// <param name="tableName">the current name of the existing table</param> 
        /// <param name="newTableName">the new name of the table</param> 
        private static void RenameTable(SQLiteConnection connection, string tableName, string newTableName)
        {
            string sql = Sql.AlterTable + tableName + Sql.RenameTo + newTableName;
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        #region Utilities
        // Open the SQLite connection
        // Note that the 2nd argument is ParseViaFramework. This is included to resolve an issue that occurs
        // when users try to open a network file on some VPNs, eg., Cisco VPN and perhaps other network file systems
        // Its an obscur bug and solution reported by others: sqlite doesn't really document that argument very well. But it seems to fix it.
        private static SQLiteConnection GetNewSqliteConnection(string connectionString)
        {
            return new SQLiteConnection(connectionString, true);
        }
        #endregion

        #region DetectionTable - related routines, put in normal places.
        public void ClearRowsInTables(List<string> tables)
        {
            if (tables == null || tables.Count == 0)
            {
                return;
            }
            string queries = String.Empty;                      // A list of SQL queries

            // Turn pragma foreign_key off before the delete, as otherwise it takes forever on largish tables
            // Notice that we do not wrap this in a begin / end, as the pragma does not work within that.
            queries += Sql.PragmaForeignKeysOff + "; ";

            // Construct a list containing queries of the form DELETE FROM table_name
            foreach (string table in tables)
            {
                string query = Sql.DeleteFrom + table;     // DELETE FROM tablename
                query += "; ";
                queries += query;
            }

            // Now turn pragma foreign_key on again after the delete
            queries += Sql.PragmaForeignKeysOn + ";";

            // Invoke the batched queries
            this.ExecuteNonQuery(queries);
        }

        public bool TableExists(string tableName)
        {
            // DETECTIONS: Move statements into constants
            string query = String.Format("SELECT name FROM sqlite_master WHERE type = 'table' AND name = '{0}'; ", tableName);
            DataTable datatable = this.GetDataTableFromSelect(query);
            bool rowsExist = datatable.Rows.Count != 0;
            if (datatable != null)
            {
                datatable.Dispose();
            }
            return rowsExist;
        }

        public bool TableExistsAndNotEmpty(string tableName)
        {
            // DETECTIONS: Move statements into constants
            string query = String.Format("SELECT name FROM sqlite_master WHERE type = 'table' AND name = '{0}'; ", tableName);
            using (DataTable datatable = this.GetDataTableFromSelect(query))
            {
                if (datatable.Rows.Count == 0)
                {
                    return false;
                }
                query = String.Format("SELECT COUNT(*)_ FROM {0}", tableName);
                return this.GetCountFromSelect(query) != 0;
            }
        }
        #endregion

        #region Unused methods
#pragma warning disable IDE0051 // Remove unused private members
        /// <summary>
        /// CURRENTLY UNUSED
        /// Add a column to the end of the database table 
        /// This does NOT require the table to be cloned.
        /// Note: Some of the AddColumnToEndOfTable methods are currently not referenced, but may be handy in the future.
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param> 
        /// <param name="tableName">the name of the  table</param> 
        /// <param name="name">the name of the new column</param> 
        /// <param name="type">the type of the new column</param> 
        private static void AddColumnToEndOfTable(SQLiteConnection connection, string tableName, string name, string type)
        {
            string columnDefinition = name + " " + type;
            AddColumnToEndOfTable(connection, tableName, columnDefinition);
        }

        /// <summary>
        /// Add a column to the end of the database table. 
        /// This does NOT require the table to be cloned.
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param> 
        /// <param name="tableName">the name of the  table</param> 
        /// <param name="name">the name of the new column</param> 
        /// <param name="type">the type of the new column</param> 
        /// <param name="otherOptions">space-separated options such as PRIMARY KEY AUTOINCREMENT, NULL or NOT NULL etc</param>
        private static void AddColumnToEndOfTable(SQLiteConnection connection, string tableName, string name, string type, string otherOptions)
        {
            string columnDefinition = name + " " + type;
            if (String.IsNullOrEmpty(otherOptions))
            {
                columnDefinition += " " + otherOptions;
            }
            AddColumnToEndOfTable(connection, tableName, columnDefinition);
        }
#pragma warning restore IDE0051 // Remove unused private members
        #endregion

        #region Merge Databases
        public async static Task<bool> TryMergeDatabasesAsync(string tdbFile, List<string> ddbFiles)
        {
            // Set up a progress handler that will update the progress bar
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                // Update the progress bar
                SQLiteWrapper.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler as IProgress<ProgressBarArguments>;

            if (ddbFiles?.Count < 1)
            {
                System.Diagnostics.Debug.Print("Two files minimum needed");
                return false;
            }

            string rootFolderPath = Path.GetDirectoryName(tdbFile);
            string mergedDDBPath = Path.Combine(rootFolderPath, "project.ddb");
            string rootFolder = rootFolderPath.Split(Path.DirectorySeparatorChar).Last();
            await Task.Run(() =>
            {
                // Update the progress bar
                progress.Report(new ProgressBarArguments((int)(1 / (double)ddbFiles.Count * 100.0), String.Format("Merging 1/{0} databases. Please wait...", ddbFiles.Count), false, false));
                Thread.Sleep(250); // Allows the UI thread to update plus makes the progress bar readable

                // Create and open the main ddb file in the root folder as a copy of the first database we find. 
                // We will then add to that file.
                if (File.Exists(mergedDDBPath))
                {
                    File.Delete(mergedDDBPath);
                }
                File.Copy(ddbFiles[0], mergedDDBPath);
            }).ConfigureAwait(true);
          
            SQLiteWrapper mergedDDB = new SQLiteWrapper(mergedDDBPath);

            // Get a sample relative path from the datatable, which we will use to figure out the prefix to add to the relative from the current root
            // Form:  SELECT RelativePath FROM DataTable LIMIT 1"))
            string pathPrefixToAdd;
            string query = Sql.Select + Constant.DatabaseColumn.RelativePath + Sql.From + Constant.DBTables.FileData + Sql.LimitOne;
            using (DataTable dt = mergedDDB.GetDataTableFromSelect(query))
            {
                if (dt.Rows.Count == 0)
                {
                    // No rows in the main database table. Abort
                    // But note that this could be possible if the first db has nothing in it, where we really should continue... But later.
                    System.Diagnostics.Debug.Print("No rows in table!");
                    return false;
                }

                // Correct the relative path. Find the extra path to the ddb File path from the .tdb File path.
                // then append it onto the relative paths of the ddb file rows
                // SQL Form: UPDATE DataTable SET RelativePath = ("pathPrefixToAdd\" || RelativePath)
                pathPrefixToAdd = GetSubPathPrefix(ddbFiles[0], rootFolderPath);
                pathPrefixToAdd = pathPrefixToAdd.TrimEnd(Path.DirectorySeparatorChar);
                if (!String.IsNullOrEmpty(pathPrefixToAdd))
                {
                    mergedDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.FileData + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal + Utilities.QuoteForSql(pathPrefixToAdd) + Sql.Concatenate + Constant.DatabaseColumn.RelativePath);
                }
            }

            for (int i = 1; i < ddbFiles.Count; i++)
            {
                await Task.Run(() =>
                {
                    string message = String.Format("Merging {0}/{1} databases. Please wait...", i + 1, ddbFiles.Count);
                    progress.Report(new ProgressBarArguments((int) ((i + 1) / (double) ddbFiles.Count * 100.0), message, false, false));
                    Thread.Sleep(250); // Allows the UI thread to update plus makes the progress bar readable
                    pathPrefixToAdd = GetSubPathPrefix(ddbFiles[i], rootFolderPath);
                    pathPrefixToAdd = pathPrefixToAdd.TrimEnd(Path.DirectorySeparatorChar);
                    SQLiteWrapper.MergeIntoDDB(mergedDDB, ddbFiles[i], pathPrefixToAdd);
                }).ConfigureAwait(true);
            }
            // After the merged database is constructed, set the Folder column to the current root folder
            if (!String.IsNullOrEmpty(rootFolder))
            {
                mergedDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.FileData + Sql.Set + Constant.DatabaseColumn.Folder + Sql.Equal + Utilities.QuoteForSql(rootFolder));
            }

            // After the merged database is constructed, reset fields in the ImageSetTable to the defaults i.e., first row, selection all, 
            if (!String.IsNullOrEmpty(rootFolder))
            {
                mergedDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.MostRecentFileID + Sql.Equal + "1");
                mergedDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.Selection + Sql.Equal + ((int) FileSelectionEnum.All).ToString());
                mergedDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.SortTerms + Sql.Equal + Utilities.QuoteForSql(Constant.DatabaseValues.DefaultSortTerms));
            }
            return true;
        }

        private static bool MergeIntoDDB(SQLiteWrapper mergedDDB, string toMergeDDB, string pathPrefixToAdd)
        {
            string attachedDB = "attachedDB";
            string tempDataTable = "tempDataTable";
            string tempDetectionsTable = "tempDetectionsTable";

            // Calculate an ID offset (the current max Id), where we will be adding that to all Ids in the ddbFile to merge. 
            // This will guarantee that there are no duplicate primary keys 
            int offsetId = mergedDDB.GetCountFromSelect("Select Max(Id) from DataTable");

            // Create the first part of the query to:
            // - Attach the ddbFile
            // - Create a temporary DataTable mirroring the one in the toMergeDDB (so updates to that don't affect the original ddb)
            // - Update the DataTable with the modified Ids
            // - Update the DataTable with the path prefix
            // - Insert the DataTable  into the main db's DataTable
            // Form: ATTACH DATABASE 'toMergeDDB' AS attachedDB; 
            //       CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM attachedDB.DataTable;
            //       UPDATE tempDataTable SET Id = (offsetID + tempDataTable.Id);
            //       UPDATE TempDataTable SET RelativePath = ("PrefixPath\" || RelativePath)
            //       INSERT INTO DataTable SELECT * FROM tempDataTable;
            string query = Sql.BeginTransaction + Sql.Semicolon;
            query += Sql.AttachDatabase + Utilities.QuoteForSql(toMergeDDB) + Sql.As + attachedDB + Sql.Semicolon;
            query += Sql.CreateTemporaryTable + tempDataTable + Sql.As + Sql.SelectStarFrom + attachedDB + Sql.Dot + Constant.DBTables.FileData + Sql.Semicolon;
            query += Sql.Update + tempDataTable + Sql.Set + Constant.DatabaseColumn.ID + Sql.Equal + Sql.OpenParenthesis + offsetId + Sql.Plus + tempDataTable + Sql.Dot + Constant.DatabaseColumn.ID + Sql.CloseParenthesis + Sql.Semicolon;
            query += Sql.Update + tempDataTable + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal + Utilities.QuoteForSql(pathPrefixToAdd) + Sql.Concatenate + Constant.DatabaseColumn.RelativePath + ";";
            query += Sql.InsertInto + Constant.DBTables.FileData + Sql.SelectStarFrom + tempDataTable + Sql.Semicolon;

            // Now we need to see if we have to handle detection table updates.
            // Check to see if the main DB file and the toMerge DB file each have a Detections table.
            bool dbToMergeDetectionsExists = CheckDBForDetectionsTable(toMergeDDB);
            bool mergedDDBDetectionsExists = mergedDDB.TableExists(Constant.DBTables.Detections);

            // Create the second part of the query only if the toMergeDDB contains a detections table
            // (as otherwise we don't have to update the detection table in the main ddb.
            // - Create a temporary Detections table mirroring the one in the toMergeDDB (so updates to that don't affect the original ddb)
            // - Update the Detections Table with both the modified Ids and detectionIDs
            // - Insert the Detections Table into the main db's Detections Table
            // Form: CREATE TEMPORARY TABLE tempDetectionsTable AS SELECT * FROM attachedDB.Detections;
            //       UPDATE TempDetectionsTable SET Id = (offsetId + TempDetectionsTable.Id);
            //       UPDATE TempDetectionsTable SET DetectionID = (offsetDetectionId + TempDetectionsTable.DetectionId);
            //       INSERT INTO Detections SELECT * FROM TempDetectionsTable;"
            if (dbToMergeDetectionsExists)
            {
                // The database to merge in has detections, so the SQL query also updates the Detections table.
                // Calculate an offset (the max DetectionIDs), where we will be adding that to all detectionIds in the ddbFile to merge. 
                // However, the offeset should be 0 if there are no detections in the main DB, 
                // as we will be creating the detection table and then just adding to it.
                int offsetDetectionId = (mergedDDBDetectionsExists) ? mergedDDB.GetCountFromSelect("Select Max(detectionId) from Detections") : 0;
                query += Sql.CreateTemporaryTable + tempDetectionsTable + Sql.As + Sql.SelectStarFrom + attachedDB + Sql.Dot + Constant.DBTables.Detections + Sql.Semicolon;
                query += Sql.Update + tempDetectionsTable + Sql.Set + Constant.DatabaseColumn.ID + Sql.Equal + Sql.OpenParenthesis + offsetId + Sql.Plus + tempDetectionsTable + Sql.Dot + Constant.DatabaseColumn.ID + Sql.CloseParenthesis + Sql.Semicolon;
                query += Sql.Update + tempDetectionsTable + Sql.Set + Constant.DetectionColumns.DetectionID + Sql.Equal + Sql.OpenParenthesis + offsetDetectionId + Sql.Plus + tempDetectionsTable + Sql.Dot + Constant.DetectionColumns.DetectionID + Sql.CloseParenthesis + Sql.Semicolon;
                query += Sql.InsertInto + Constant.DBTables.Detections + Sql.SelectStarFrom + tempDetectionsTable + Sql.Semicolon;
            }
            query += Sql.EndTransaction + Sql.Semicolon;

            // Execute the query. 
            using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(mergedDDB.connectionString))
            {
                connection.Open();

                // If the main database doesn't have detections, but the database to merge into it does,
                // then we have to create the detection tables to the main database.
                SQLiteWrapper tempDDB = new SQLiteWrapper(toMergeDDB);
                if (mergedDDBDetectionsExists == false && dbToMergeDetectionsExists)
                {
                    DetectionDatabases.CreateOrRecreateTablesAndColumns(mergedDDB);
                }

                // Merge the database into the main database 
                // I had thought that we would have to defer foreign keys as otherwise it wont allow us to update the primary key ids,
                // but this doesn't seem to be the case
                //SQLiteWrapper.DeferForeignKeys(connection, true);
                mergedDDB.ExecuteOneNonQueryCommand(query);
            }
            return true;
        }

        private static string GetSubPathPrefix(string fullPath, string fullPathPrefix)
        {
            string subPathPrefix = Path.GetDirectoryName(fullPath).Replace(fullPathPrefix + "\\", "");
            if (!String.IsNullOrEmpty(subPathPrefix))
            {
                subPathPrefix += "\\";
            }
            return subPathPrefix;
        }

        static private void UpdateProgressBar(BusyCancelIndicator busyCancelIndicator, int percent, string message, bool isCancelEnabled, bool isIndeterminate)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Code to run on the GUI thread.
                // Check the arguments for null 
                ThrowIf.IsNullArgument(busyCancelIndicator, nameof(busyCancelIndicator));

                // Set it as a progressive or indeterminate bar
                busyCancelIndicator.IsIndeterminate = isIndeterminate;

                // Set the progress bar position (only visible if determinate)
                busyCancelIndicator.Percent = percent;

                // Update the text message
                busyCancelIndicator.Message = message;

                // Update the cancel button to reflect the cancelEnabled argument
                busyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
                busyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Processing detections...";
            });
        }
        #endregion 

        private static bool CheckDBForDetectionsTable(string dbPath)
        {
            SQLiteWrapper db = new SQLiteWrapper(dbPath);
            return db.TableExists("Detections");
        }

        #region Pragmas
        // PRAGMA Turn foreign keys on or off. 
        // For example, if we drop a table that has foreign keys in it, we need to make sure foreign keys are off
        // as otherwise it will delete the foreign key table contents.
        private static void SetPragmaForeignKeys(SQLiteConnection connection, bool state)
        {
            // Syntax is: PRAGMA foreign_keys = OFF;
            // Syntax is: PRAGMA foreign_keys = On;
            string sql = "PRAGMA foreign_keys = ";
            sql += state ? "ON;" : "Off;";
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        // PRAGMA Defer foreign keys. 
        private static void SetPragmaDeferForeignKeys(SQLiteConnection connection, bool state)
        {
            // Syntax is: defer_foreign_keys = 1; True
            // Syntax is: defer_foreign_keys = 0; False
            string sql = "PRAGMA defer_foreign_keys = ";
            sql += state ? "1;" : "0;";
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }
        #endregion
    }
}