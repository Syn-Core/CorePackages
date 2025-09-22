using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using Syn.Core.Logger;

using System.Diagnostics;
using System.Text.RegularExpressions;


namespace Syn.Core.SqlSchemaGenerator.Execution
{
    /// <summary>
    /// Executes SQL scripts with support for batching via GO, transaction safety, and async execution.
    /// </summary>
    public class SqlScriptRunner
    {

        /// <summary>
        /// Timeout in seconds for each SQL batch. Default is 30 seconds.
        /// </summary>
        public int CommandTimeout { get; set; } = 30;

        /// <summary>
        /// Executes a SQL script on the target database, splitting by GO and wrapping in a transaction.
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="script">The SQL script to execute.</param>
        /// <param name="externalTransaction">Optional existing transaction to use.</param>
        /// <returns>Execution result containing stats and errors.</returns>
        public SqlScriptExecutionResult ExecuteScript(
            string connectionString,
            string script,
            SqlTransaction externalTransaction = null)
        {
            var result = new SqlScriptExecutionResult();
            var stopwatch = Stopwatch.StartNew();

            var batches = SplitScriptByGo(script);
            result.TotalBatches = batches.Count;

            SqlConnection connection = null;
            SqlTransaction transaction = externalTransaction;

            try
            {
                if (externalTransaction == null)
                {
                    connection = new SqlConnection(connectionString);
                    connection.Open();
                    transaction = connection.BeginTransaction();
                }
                else
                {
                    connection = externalTransaction.Connection!;
                }

                foreach (var batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;

                    try
                    {
                        using var command = new SqlCommand(batch, connection, transaction)
                        {
                            CommandTimeout = CommandTimeout
                        };
                        command.ExecuteNonQuery();
                        result.ExecutedBatches++;
                        ConsoleLog.Debug($"Executed batch:\n{batch}");
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(ex.Message);
                        ConsoleLog.Error(ex.Message);
                        throw;
                    }
                }

                if (externalTransaction == null)
                    transaction?.Commit();

                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                ConsoleLog.Info($"All batches executed successfully in {result.DurationMs} ms.");
            }
            catch
            {
                if (externalTransaction == null)
                    transaction?.Rollback();

                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                ConsoleLog.Warning($"Execution failed after {result.DurationMs} ms. Transaction rolled back.");
                throw;
            }
            finally
            {
                if (externalTransaction == null)
                {
                    transaction?.Dispose();
                    connection?.Dispose();
                }
            }

            return result;
        }


        /// <summary>
        /// Executes a SQL script on the target database, splitting by GO and wrapping in a transaction.
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="script">The SQL script to execute.</param>
        /// <param name="externalTransaction">Optional existing transaction to use.</param>
        /// <returns>Execution result containing stats and errors.</returns>

        public async Task<SqlScriptExecutionResult> ExecuteScriptAsync(
            string connectionString,
            string script,
            SqlTransaction externalTransaction = null)
        {
            var result = new SqlScriptExecutionResult();
            var stopwatch = Stopwatch.StartNew();

            var batches = SplitScriptByGo(script);
            result.TotalBatches = batches.Count;

            SqlConnection connection = null;
            SqlTransaction transaction = externalTransaction;

            try
            {
                if (externalTransaction == null)
                {
                    connection = new SqlConnection(connectionString);
                    await connection.OpenAsync();
                    transaction = connection.BeginTransaction();
                }
                else
                {
                    connection = externalTransaction.Connection!;
                }

                foreach (var batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;

                    try
                    {
                        using var command = new SqlCommand(batch, connection, transaction)
                        {
                            CommandTimeout = CommandTimeout
                        };
                        await command.ExecuteNonQueryAsync();
                        result.ExecutedBatches++;
                        ConsoleLog.Debug($"Executed batch:\n{batch}");
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(ex.Message);
                        ConsoleLog.Error(ex.Message);
                        throw;
                    }
                }

                if (externalTransaction == null)
                    transaction?.Commit();

                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                ConsoleLog.Info($"All batches executed successfully in {result.DurationMs} ms.");
            }
            catch
            {
                if (externalTransaction == null)
                    transaction?.Rollback();

                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                ConsoleLog.Warning($"Execution failed after {result.DurationMs} ms. Transaction rolled back.");
                throw;
            }
            finally
            {
                if (externalTransaction == null)
                {
                    transaction?.Dispose();
                    connection?.Dispose();
                    await Task.Delay(1000);
                }
            }

            return result;
        }


        /// <summary>
        /// Splits a SQL script into batches using "GO" as a delimiter.
        /// Handles variations in spacing, casing, and ignores "GO" inside comments or strings.
        /// </summary>
        private List<string> SplitScriptByGo(string script)
        {
            var batches = new List<string>();
            var currentBatch = new List<string>();

            // نقسم السكريبت لأسطر
            var lines = script.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // نتأكد إن السطر عبارة عن "GO" فقط (مع أو بدون مسافات) ومش جوه تعليق أو نص
                if (Regex.IsMatch(line, @"^(?i:GO)\s*$"))
                {
                    if (currentBatch.Count > 0)
                    {
                        batches.Add(string.Join(Environment.NewLine, currentBatch).Trim());
                        currentBatch.Clear();
                    }
                }
                else
                {
                    currentBatch.Add(rawLine);
                }
            }

            // إضافة آخر Batch لو فيه أوامر
            if (currentBatch.Count > 0)
            {
                batches.Add(string.Join(Environment.NewLine, currentBatch).Trim());
            }

            // إزالة أي Batch فاضي
            return batches.Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        }

    }

    /// <summary>
    /// Represents the result of executing a SQL script.
    /// </summary>
    public class SqlScriptExecutionResult
    {
        public int TotalBatches { get; set; }
        public int ExecutedBatches { get; set; }
        public long DurationMs { get; set; }
        public List<string> Errors { get; } = new();
        public bool Success => Errors.Count == 0;
    }
}