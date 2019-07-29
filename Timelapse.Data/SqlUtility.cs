namespace Timelapse.Data
{
    /// <summary>
    /// Common helper functions to support building raw SQL.
    /// </summary>
    public static class SqlUtility
    {
        /// <summary>
        /// Format the passed value for use as string value in a SQL statement or query.
        /// </summary>
        public static string QuoteForSql(string value)
        {
            // promote null values to empty strings
            if (value == null)
            {
                return "''";
            }

            // for an input of "foo's bar" the output is "'foo''s bar'"
            return "'" + value.Replace("'", "''") + "'";
        }
    }
}
