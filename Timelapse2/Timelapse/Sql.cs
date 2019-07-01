using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse
{
    public static class Sql
    {
        public const string AddColumn = " ADD COLUMN ";
        public const string AlterTable = " ALTER TABLE ";
        public const string And = " AND ";
        public const string As = " AS ";
        public const string AsInteger = " AS INTEGER ";
        public const string AsReal = " AS REAL ";
        public const string Ascending = " ASC ";
        public const string Descending = " DESC ";
        public const string Dot = ".";

        public const string DotStar = Sql.Dot + Sql.Star;
        
        public const string BeginTransaction = " BEGIN TRANSACTION ";
        public const string Between = " BETWEEN ";
        public const string Cast = " CAST ";
        public const string CreateIndex = " CREATE INDEX ";
        public const string CreateTable = " CREATE TABLE ";
        public const string CreationStringPrimaryKey = "INTEGER PRIMARY KEY AUTOINCREMENT";
        public const string CloseParenthesis = " ) ";
        public const string CollateNocase = " COLLATE NOCASE ";
        public const string Comma = ", ";
        public const string DataSource = "Data Source=";
        public const string Default = " DEFAULT ";
        public const string DeleteFrom = "DELETE FROM ";
        public const string DropIndex = " DROP INDEX ";
        public const string DropTable = " DROP TABLE ";
        public const string EndTransaction = " END TRANSACTION ";
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
        public const string Integer = " INTEGER ";
        public const string IsNull = " IS NULL ";
        public const string LeftJoin = " LEFT JOIN ";
        public const string LessThanEqual = " <= ";
        public const string LessThan = " < ";
        public const string Max = " MAX ";
        public const string Name = " NAME ";
        public const string NameFromSqliteMaster = " NAME FROM SQLITE_MASTER ";
        public const string NotNull = " NOT NULL ";
        public const string Null = " NULL ";
        public const string NullAs = Null + " " + As;
        public const string On = " ON ";
        public const string OpenParenthesis = " ( ";
        public const string Or = " OR ";
        public const string OrderBy = " ORDER BY ";
        public const string PragmaTableInfo = " PRAGMA TABLE_INFO ";
        public const string PragmaSetForeignKeys = " PRAGMA foreign_keys=1";
        public const string PrimaryKey = " PRIMARY KEY ";
        public const string RenameTo = " RENAME TO ";
        public const string Select = " SELECT ";
        public const string SelectDistinct = " SELECT DISTINCT ";
        public const string SelectStarFrom = Sql.Select + Sql.Star + Sql.From; //" SELECT * FROM "
        public const string SelectCount = " Select Count ";
        public const string SelectCountStarFrom = Sql.SelectCount + Sql.OpenParenthesis + Sql.Star + Sql.CloseParenthesis + Sql.From;
        public const string Semicolon = " ; ";
        public const string Set = " SET ";
        public const string Star = "*";
        public const string String = " STRING ";
        public const string MasterTableList = "sqlite_master";
        public const string Real = " REAL ";
        public const string Text = "TEXT";
        public const string Trim = " TRIM ";
        public const string TypeEqualsTable = " TYPE='table' ";
        public const string UnionAll = " UNION ALL";
        public const string Update = " UPDATE ";
        public const string Values = " VALUES ";
        public const string When = " WHEN ";
        public const string Where = " WHERE ";
        public const string WhereIDIn = Where + "Id IN ";
        public const string WhereIDNotIn = Where + " Id NOT IN ";
        public const string Then = " THEN ";
    }
}
