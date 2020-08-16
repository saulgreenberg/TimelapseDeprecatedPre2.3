using System;

namespace Timelapse
{
    // Create SQL commands using constants rather than typing the SQL keywords. 
    // This really helps avoid typos, bugs due to spacing such as not having spaces inbetween keywords, etc.
    public static class Sql
    {
        public const string AddColumn = " ADD COLUMN ";
        public const string AlterTable = " ALTER TABLE ";
        public const string And = " AND ";
        public const string As = " AS ";
        public const string AsInteger = " AS INTEGER ";
        public const string AsReal = " AS REAL ";
        public const string Ascending = " ASC ";
        public const string AttachDatabase = " ATTACH DATABASE ";
        public const string Concatenate = " || ";
        public const string Descending = " DESC ";
        public const string Dot = ".";
        public const string DotStar = Sql.Dot + Sql.Star;
        public const string BeginTransaction = " BEGIN TRANSACTION ";
        public const string BeginTransactionSemiColon = Sql.BeginTransaction + Sql.Semicolon;
        public const string Between = " BETWEEN ";
        public const string CaseWhen = " CASE WHEN ";
        public const string Cast = " CAST ";
        public const string CreateIndex = " CREATE INDEX ";
        public const string CreateTable = " CREATE TABLE ";
        public const string CreateTemporaryTable = " CREATE TEMPORARY TABLE ";
        public const string CreateUniqueIndex = " CREATE UNIQUE INDEX ";
        public const string CreationStringPrimaryKey = "INTEGER PRIMARY KEY AUTOINCREMENT";
        public const string CloseParenthesis = " ) ";
        public const string CollateNocase = " COLLATE NOCASE ";
        public const string Comma = ", ";
        public const string DataSource = "Data Source=";
        public const string Default = " DEFAULT ";
        public const string DeleteFrom = "DELETE FROM ";
        public const string Do = " DO ";
        public const string DoUpdate = " Do UPDATE ";
        public const string DropIndex = " DROP INDEX ";
        public const string DropTable = " DROP TABLE ";
        public const string Else = " ELSE ";
        public const string End = " END ";
        public const string EndTransaction = " END TRANSACTION ";
        public const string EndTransactionSemiColon = Sql.EndTransaction + Sql.Semicolon;
        public const string Equal = " = ";
        public const string EqualsCaseID = " = CASE Id";
        public const string From = " FROM ";
        public const string GreaterThanEqual = " >= ";
        public const string GreaterThan = " > ";
        public const string GroupBy = " GROUP BY ";
        public const string Having = " HAVING ";
        public const string IfNotExists = " IF NOT EXISTS ";
        public const string IfExists = " IF EXISTS ";
        public const string In = " In ";
        public const string InnerJoin = " INNER JOIN ";
        public const string InsertInto = " INSERT INTO ";
        public const string InsertOrReplaceInto = " INSERT OR REPLACE INTO ";
        public const string IntegerType = " INTEGER ";
        public const string IsNull = " IS NULL ";
        public const string LeftJoin = " LEFT JOIN ";
        public const string LessThanEqual = " <= ";
        public const string LessThan = " < ";
        public const string Limit = " LIMIT ";
        public const string LimitOne = Limit + " 1 ";
        public const string Max = " MAX ";
        public const string Name = " NAME ";
        public const string NameFromSqliteMaster = " NAME FROM SQLITE_MASTER ";
        public const string NotNull = " NOT NULL ";
        public const string Null = " NULL ";
        public const string NullAs = Null + " " + As;
        public const string Ok = "ok";
        public const string On = " ON ";
        public const string OnConflict = " ON CONFLICT ";
        public const string OpenParenthesis = " ( ";
        public const string Or = " OR ";
        public const string OrderBy = " ORDER BY ";
        public const string Plus = " + ";
        public const string PragmaTableInfo = " PRAGMA TABLE_INFO ";
        public const string PragmaSetForeignKeys = " PRAGMA foreign_keys=1 ";
        public const string PragmaForeignKeysOff = " PRAGMA foreign_keys = OFF ";
        public const string PragmaForeignKeysOn = " PRAGMA foreign_keys = ON ";
        public const string PragmaQuickCheck = "PRAGMA QUICK_CHECK ";
        public const string PrimaryKey = " PRIMARY KEY ";
        public const string RenameTo = " RENAME TO ";
        public const string QuotedEmptyString = " '' ";
        public const string Select = " SELECT ";
        public const string SelectDistinct = " SELECT DISTINCT ";
        public const string SelectOne = " SELECT 1 ";
        public const string SelectStar = Sql.Select + Sql.Star; // SELECT * "
        public const string SelectStarFrom = Sql.SelectStar + Sql.From; // SELECT * FROM "

        public const string SelectCount = " SELECT COUNT ";
        public const string SelectDistinctCount = " SELECT DISTINCT COUNT ";
        public const string SelectCountStarFrom = Sql.SelectCount + Sql.OpenParenthesis + Sql.Star + Sql.CloseParenthesis + Sql.From;
        public const string SelectDistinctCountStarFrom = Sql.SelectDistinctCount + Sql.OpenParenthesis + Sql.Star + Sql.CloseParenthesis + Sql.From;
        public const string SelectExists = " SELECT EXISTS ";
        public const string SelectNameFromSqliteMasterWhereTypeEqualTableAndNameEquals = " SELECT name FROM sqlite_master WHERE TYPE = 'table' AND name = ";
        public const string Semicolon = " ; ";
        public const string Set = " SET ";
        public const string Star = "*";
        public const string StringType = " STRING ";
        public const string MasterTableList = "sqlite_master";
        public const string Real = " REAL ";
        public const string Text = "TEXT";
        public const string Then = " THEN ";
        public const string Trim = " TRIM ";
        public const string True = " TRUE ";
        public const string TypeEqualsTable = " TYPE='table' ";
        public const string UnionAll = " UNION ALL";
        public const string Update = " UPDATE ";
        public const string Values = " VALUES ";
        public const string When = " WHEN ";
        public const string Where = " WHERE ";
        public const string WhereIDIn = Where + "Id IN ";
        public const string WhereIDNotIn = Where + " Id NOT IN ";
        public const string WhereIDEquals = Where + " Id " + Equal;

        /// <summary>
        /// Format the passed value for use as string value in a SQL statement or query.
        /// Nulls are quoted as empty strings
        /// </summary>
        public static string Quote(string value)
        {
            // promote null values to empty strings
            return (value == null)
                ? "''"
                : "'" + value.Replace("'", "''") + "'";
        }
    }

    /// <summary>
    /// Instead of having lots of long SQL phrase fragments constructed in various files, we construct and collect them here
    /// </summary>
    public static class SqlPhrase
    {
        /// <summary>
        /// Sql Phrase - Create partial query to return all missing detections
        /// </summary>
        ///  <param name="useCountForm">If true, return a SELECT COUNT vs a SELECT from</param>
        /// <returns> 
        /// Count Form:  SELECT COUNT  ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
        /// Select Form: SELECT DataTable.*             FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL</returns>
        public static string SelectMissingDetections(bool useCountForm)
        {
            string phrase = useCountForm
                ? Sql.SelectCount + Sql.OpenParenthesis + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + Sql.CloseParenthesis
                : Sql.Select + Constant.DBTables.FileData + Sql.DotStar;
            return phrase + Sql.From + Constant.DBTables.FileData +
                Sql.LeftJoin + Constant.DBTables.Detections +
                Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID +
                Sql.Equal + Constant.DBTables.Detections + Sql.Dot + Constant.DatabaseColumn.ID +
                Sql.Where + Constant.DBTables.Detections + Sql.Dot + Constant.DatabaseColumn.ID + Sql.IsNull;
        }

        /// <summary>
        /// Sql Phrase - Create partial query to return detections
        /// </summary>
        /// <param name="useCountForm">If true, form is SELECT COUNT vs SELECT</param>
        /// <returns>
        /// Count Form:  SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
        /// Select Form: SELECT DataTable.*                     FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
        /// </returns>
        public static string SelectDetections(bool useCountForm)
        {
            string phrase = useCountForm
                ? Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectStar
                : Sql.Select + Constant.DBTables.FileData + Sql.DotStar ;
            return phrase + Sql.From + Constant.DBTables.Detections + Sql.InnerJoin + Constant.DBTables.FileData +
                    Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + Sql.Equal + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="useCountForm"></param>
        /// <returns>
        /// Count Form:  Select COUNT  ( * )  FROM  (SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
        /// Select Form: SELECT                                      DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
        /// 
        /// </returns>
        public static string SelectClassifications(bool useCountForm)
        {
            string phrase = useCountForm
                ? Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectDistinct
                : Sql.SelectDistinct;
           //     : Sql.SelectDistinct + Constant.DBTables.Classifications + Sql.Dot + Constant.ClassificationColumns.Conf + Sql.Comma;
            phrase += Constant.DBTables.FileData + Sql.DotStar + Sql.From + Constant.DBTables.Classifications +
                    Sql.InnerJoin + Constant.DBTables.FileData + Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + 
                    Sql.Equal + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID;
            // and now append INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
            phrase += Sql.InnerJoin + Constant.DBTables.Detections + Sql.On +
                Constant.DBTables.Detections + Sql.Dot + Constant.DetectionColumns.DetectionID + Sql.Equal +
                Constant.DBTables.Classifications + "." + Constant.DetectionColumns.DetectionID;
            return phrase;
        }


        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="datalabel"></param>
        /// <returns> ( label IS NULL OR  label = '' ) ;</returns>
        public static string LabelIsNullOrDataLabelEqualsEmpty(string datalabel)
        {
            return Sql.OpenParenthesis + datalabel + Sql.IsNull + Sql.Or + datalabel + Sql.Equal + Sql.QuotedEmptyString + Sql.CloseParenthesis;
        }

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="dataLabel"></param>
        /// <param name="mathOperator"></param>
        /// <param name="value"></param>
        /// <returns>DataLabel operator "value", e.g., DataLabel > "5"</returns>
        public static string DataLabelOperatorValue(string dataLabel, string mathOperator, string value)
        {
            value = value == null ? String.Empty : value.Trim();
            return dataLabel + mathOperator + Sql.Quote(value);
        }

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="detectionCategory"></param>
        /// <returns>Detections.Category = <DetectionCategory></returns>
        public static string DetectionCategoryEqualsDetectionCategory(string detectionCategory)
        {
            return Constant.DBTables.Detections + "." + Constant.DetectionColumns.Category + Sql.Equal + detectionCategory;
        }

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="classificationCategory"></param>
        /// <returns>Classifications.Category = <ClassificationCategory></returns>
        public static string ClassificationsCategoryEqualsClassificationCategory(string classificationCategory)
        {
            return Constant.DBTables.Classifications + "." + Constant.DetectionColumns.Category + Sql.Equal + classificationCategory;
        }

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="lowerBound"></param>
        /// <param name="upperBound"></param>
        /// <returns>Group By Detections.Id Having Max ( Detections.conf ) BETWEEN <lowerBound> AND <upperBound></returns>
        public static string GroupByDetectionsIdHavingMaxDetectionsConf(double lowerBound, double upperBound)
        {
            return Sql.GroupBy + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID +
                Sql.Having + Sql.Max +
                Sql.OpenParenthesis + Constant.DBTables.Detections + "." + Constant.DetectionColumns.Conf + Sql.CloseParenthesis +
                Sql.Between + lowerBound.ToString() + Sql.And + upperBound.ToString();
        }

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="lowerBound"></param>
        /// <param name="upperBound"></param>
        /// <returns>GROUP BY Classifications.classificationID HAVING MAX  ( Classifications.conf ) BETWEEN <lowerBound> AND <upperBound></returns>
        public static string GroupByClassificationsIdHavingMaxClassificationsConf(double lowerBound, double upperBound)
        {
            return Sql.GroupBy + Constant.DBTables.Classifications + "." + Constant.ClassificationColumns.ClassificationID +
                Sql.Having + Sql.Max +
                Sql.OpenParenthesis + Constant.DBTables.Classifications + "." + Constant.DetectionColumns.Conf + Sql.CloseParenthesis +
                Sql.Between + lowerBound.ToString() + Sql.And + upperBound.ToString();
        }
    }
}
