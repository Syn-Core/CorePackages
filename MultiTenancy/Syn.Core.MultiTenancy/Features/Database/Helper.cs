using Microsoft.Data.SqlClient;

namespace Syn.Core.MultiTenancy.Features.Database
{
    internal static class Helper
    {
        /// <summary>
        /// Ensures that the database in the given connection string exists.
        /// If it does not exist, it will be created.
        /// </summary>
        /// <param name="connectionString">The connection string to the target database.</param>
        internal static void EnsureDatabaseExists(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                var databaseName = builder.InitialCatalog;

                // Temporarily connect to master DB
                var masterConnectionString = new SqlConnectionStringBuilder(connectionString)
                {
                    InitialCatalog = "master"
                }.ConnectionString;

                using var connection = new SqlConnection(masterConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = $@"
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = @dbname)
BEGIN
    EXEC('CREATE DATABASE [{databaseName}]')
END";
                command.Parameters.AddWithValue("@dbname", databaseName);
                command.ExecuteNonQuery();
                Console.WriteLine($"Database {databaseName} createded, {DateTime.Now}");
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
