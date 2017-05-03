using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal static class DataTableBackedListExtensions
    {
        public static DataTable ExtractDataTable<TRow>(this DataTableBackedList<TRow> list) where TRow : DataRowBackedObject
        {
            PrivateObject listAccessor = new PrivateObject(list);
            return (DataTable)listAccessor.GetProperty("DataTable");
        }
    }
}
