using Microsoft.Data.SqlClient;

namespace Syn.Core.SqlSchemaGenerator.Builders;

internal class ExcuteQuery
{
    private readonly string _connectionString;
    public ExcuteQuery(string connectionString)
    {
        _connectionString = connectionString;
    }
    #region Checking Query
    /// <summary>
    /// Checks if a table has no rows.
    /// </summary>
    internal bool IsTableEmpty(string schema, string tableName)
    {
        var sql = $@"
SELECT COUNT(*) 
FROM [{schema}].[{tableName}]";

        // ✅ استخدام ExecuteScalar<int> بدل الكود اليدوي
        int rowCount = ExecuteScalar<int>(sql);

        // ✅ تتبع واضح
        if (rowCount == 0)
            Console.WriteLine($"[TRACE:TableCheck] {schema}.{tableName} → Table is empty");
        else
            Console.WriteLine($"[TRACE:TableCheck] {schema}.{tableName} → Table has {rowCount} rows");

        return rowCount == 0;
    }

    internal int ColumnNullCount(string schema, string tableName, string columnName)
    {
        var sql = $@"SELECT COUNT(*) FROM [{schema}].[{tableName}] WHERE [{columnName}] IS NULL";
        return ExecuteScalar<int>(sql);
    }

    /// <summary>
    /// Checks if a column contains any NULL values.
    /// </summary>
    internal bool ColumnHasNulls(string schema, string tableName, string columnName)
    {
        var sql = $@"
SELECT COUNT(*) 
FROM [{schema}].[{tableName}] 
WHERE [{columnName}] IS NULL";

        // ✅ استخدام الميثود العامة ExecuteScalar<int>
        int count = ExecuteScalar<int>(sql);

        // ✅ تتبع واضح
        if (count > 0)
            Console.WriteLine($"[TRACE:NullCheck] {schema}.{tableName}.{columnName} → Found {count} NULL values");
        else
            Console.WriteLine($"[TRACE:NullCheck] {schema}.{tableName}.{columnName} → No NULL values found");

        return count > 0;
    }


    /// <summary>
    /// Executes a scalar SQL query and returns the result as T.
    /// </summary>
    internal T ExecuteScalar<T>(string sql)
    {
        using (var conn = new SqlConnection(_connectionString))
        using (var cmd = new SqlCommand(sql, conn))
        {
            conn.Open();
            object result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return default(T);
            return (T)Convert.ChangeType(result, typeof(T));
        }
    }

    #endregion
}
