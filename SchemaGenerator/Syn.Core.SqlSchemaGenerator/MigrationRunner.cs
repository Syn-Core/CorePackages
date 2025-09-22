using Microsoft.Data.SqlClient;

using Syn.Core.Logger;
using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Execution;
using Syn.Core.SqlSchemaGenerator.Extensions;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator;
/// <summary>
/// Orchestrates schema comparison and migration execution for a batch of CLR entities.
/// Supports full reporting, safety analysis, and execution modes.
/// </summary>
public class MigrationRunner
{
    private readonly EntityDefinitionBuilder _entityDefinitionBuilder;
    private readonly AutoMigrate _autoMigrate;
    private readonly MigrationService _migrationService;
    private readonly DatabaseSchemaReader _dbReader;
    private readonly string _connectionString;

    /// <summary>
    /// Default constructor: builds all components from connection string.
    /// </summary>
    public MigrationRunner(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _entityDefinitionBuilder = new EntityDefinitionBuilder();
        _autoMigrate = new AutoMigrate(connectionString);
        var connection = new SqlConnection(connectionString);
        _dbReader = new DatabaseSchemaReader(connection);
        _migrationService = new MigrationService(_entityDefinitionBuilder, _autoMigrate, _dbReader);
    }

    /// <summary>
    /// Custom constructor: allows injecting components manually.
    /// Useful for testing or advanced configuration.
    /// </summary>
    public MigrationRunner(EntityDefinitionBuilder builder, AutoMigrate autoMigrate, DatabaseSchemaReader dbReader)
    {
        _connectionString = autoMigrate?._connectionString ?? throw new ArgumentNullException(nameof(autoMigrate._connectionString));
        _entityDefinitionBuilder = builder ?? throw new ArgumentNullException(nameof(builder));
        _autoMigrate = autoMigrate ?? throw new ArgumentNullException(nameof(autoMigrate));
        _dbReader = dbReader ?? throw new ArgumentNullException(nameof(dbReader));
        _migrationService = new MigrationService(builder, autoMigrate, dbReader);
    }



    /// <summary>
    /// Runs a migration session for all entity types found in the provided assemblies,
    /// filtered by one or more generic type parameters (interfaces or base classes).
    /// </summary>
    /// <typeparam name="T">First filter type (interface or base class).</typeparam>
    /// <param name="assemblies">Assemblies to scan for entity types.</param>
    /// <param name="execute">Whether to execute the migration scripts after generation.</param>
    /// <param name="dryRun">If true, scripts are generated but not executed.</param>
    /// <param name="interactive">If true, runs in interactive mode (step-by-step execution).</param>
    /// <param name="previewOnly">If true, shows the generated scripts without executing.</param>
    /// <param name="autoMerge">If true, attempts to auto-merge changes.</param>
    /// <param name="showReport">If true, displays a pre-migration report.</param>
    /// <param name="impactAnalysis">If true, performs an impact analysis before migration.</param>
    /// <param name="rollbackOnFailure">If true, attempts rollback on failure.</param>
    /// <param name="autoExecuteRollback">If true, automatically executes rollback scripts.</param>
    /// <param name="interactiveMode">Interactive mode type ("step" or "batch").</param>
    /// <param name="rollbackPreviewOnly">If true, shows rollback scripts without executing.</param>
    /// <param name="logToFile">If true, logs migration details to a file.</param>
    public void Initiate<T>(
        IEnumerable<Assembly> assemblies,
        bool execute = true,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool logToFile = false)
    {
        var entityTypes = assemblies.FilterTypesFromAssemblies(typeof(T));

        Initiate(
            entityTypes,
            execute,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis,
            rollbackOnFailure,
            autoExecuteRollback,
            interactiveMode,
            rollbackPreviewOnly,
            logToFile
        );
    }

    /// <summary>
    /// Runs a migration session for all entity types found in the provided assemblies,
    /// filtered by one or more generic type parameters (interfaces or base classes).
    /// </summary>
    /// <typeparam name="T1">First filter type (interface or base class).</typeparam>
    /// <typeparam name="T2">Second filter type (interface or base class).</typeparam>
    /// <param name="assemblies">Assemblies to scan for entity types.</param>
    /// <param name="execute">Whether to execute the migration scripts after generation.</param>
    /// <param name="dryRun">If true, scripts are generated but not executed.</param>
    /// <param name="interactive">If true, runs in interactive mode (step-by-step execution).</param>
    /// <param name="previewOnly">If true, shows the generated scripts without executing.</param>
    /// <param name="autoMerge">If true, attempts to auto-merge changes.</param>
    /// <param name="showReport">If true, displays a pre-migration report.</param>
    /// <param name="impactAnalysis">If true, performs an impact analysis before migration.</param>
    /// <param name="rollbackOnFailure">If true, attempts rollback on failure.</param>
    /// <param name="autoExecuteRollback">If true, automatically executes rollback scripts.</param>
    /// <param name="interactiveMode">Interactive mode type ("step" or "batch").</param>
    /// <param name="rollbackPreviewOnly">If true, shows rollback scripts without executing.</param>
    /// <param name="logToFile">If true, logs migration details to a file.</param>
    public void Initiate<T1, T2>(
        IEnumerable<Assembly> assemblies,
        bool execute = true,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool logToFile = false)
    {
        var entityTypes = assemblies.FilterTypesFromAssemblies(
            typeof(T1),
            typeof(T2)
        );

        Initiate(
            entityTypes,
            execute,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis,
            rollbackOnFailure,
            autoExecuteRollback,
            interactiveMode,
            rollbackPreviewOnly,
            logToFile
        );
    }


    /// <summary>
    /// Runs a migration session for all entity types found in the provided assemblies,
    /// optionally filtered by one or more interfaces or base classes.
    /// </summary>
    /// <param name="assembly">Assembly to scan for entity types.</param>
    /// <param name="execute">Whether to execute the migration scripts after generation.</param>
    /// <param name="dryRun">If true, scripts are generated but not executed.</param>
    /// <param name="interactive">If true, runs in interactive mode (step-by-step execution).</param>
    /// <param name="previewOnly">If true, shows the generated scripts without executing.</param>
    /// <param name="autoMerge">If true, attempts to auto-merge changes.</param>
    /// <param name="showReport">If true, displays a pre-migration report.</param>
    /// <param name="impactAnalysis">If true, performs an impact analysis before migration.</param>
    /// <param name="rollbackOnFailure">If true, attempts rollback on failure.</param>
    /// <param name="autoExecuteRollback">If true, automatically executes rollback scripts.</param>
    /// <param name="interactiveMode">Interactive mode type ("step" or "batch").</param>
    /// <param name="rollbackPreviewOnly">If true, shows rollback scripts without executing.</param>
    /// <param name="logToFile">If true, logs migration details to a file.</param>
    /// <param name="filterTypes">
    /// Optional filter types (interfaces or base classes). Only types assignable to at least one of these will be included.
    /// </param>
    public void Initiate(
    Assembly assembly,
    bool execute = true,
    bool dryRun = false,
    bool interactive = false,
    bool previewOnly = false,
    bool autoMerge = false,
    bool showReport = false,
    bool impactAnalysis = false,
    bool rollbackOnFailure = true,
    bool autoExecuteRollback = false,
    string interactiveMode = "step",
    bool rollbackPreviewOnly = false,
    bool logToFile = false,
    params Type[] filterTypes)
    {
        var entityTypes = assembly.FilterTypesFromAssembly(filterTypes);

        Initiate(
            entityTypes,
            execute,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis,
            rollbackOnFailure,
            autoExecuteRollback,
            interactiveMode,
            rollbackPreviewOnly,
            logToFile
        );
    }

    /// <summary>
    /// Runs a migration session for all entity types found in the provided assemblies,
    /// optionally filtered by one or more interfaces or base classes.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for entity types.</param>
    /// <param name="execute">Whether to execute the migration scripts after generation.</param>
    /// <param name="dryRun">If true, scripts are generated but not executed.</param>
    /// <param name="interactive">If true, runs in interactive mode (step-by-step execution).</param>
    /// <param name="previewOnly">If true, shows the generated scripts without executing.</param>
    /// <param name="autoMerge">If true, attempts to auto-merge changes.</param>
    /// <param name="showReport">If true, displays a pre-migration report.</param>
    /// <param name="impactAnalysis">If true, performs an impact analysis before migration.</param>
    /// <param name="rollbackOnFailure">If true, attempts rollback on failure.</param>
    /// <param name="autoExecuteRollback">If true, automatically executes rollback scripts.</param>
    /// <param name="interactiveMode">Interactive mode type ("step" or "batch").</param>
    /// <param name="rollbackPreviewOnly">If true, shows rollback scripts without executing.</param>
    /// <param name="logToFile">If true, logs migration details to a file.</param>
    /// <param name="filterTypes">
    /// Optional filter types (interfaces or base classes). Only types assignable to at least one of these will be included.
    /// </param>
    public void Initiate(
        IEnumerable<Assembly> assemblies,
        bool execute = true,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool logToFile = false,
        params Type[] filterTypes)
    {
        var entityTypes = assemblies.FilterTypesFromAssemblies(filterTypes);

        Initiate(
            entityTypes,
            execute,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis,
            rollbackOnFailure,
            autoExecuteRollback,
            interactiveMode,
            rollbackPreviewOnly,
            logToFile
        );
    }


    /// <summary>
    /// Runs a migration session for a list of CLR entity types.
    /// Compares each entity with its database version, generates migration script,
    /// analyzes impact and safety, shows detailed reports, and optionally executes interactively.
    /// </summary>
    public void Initiate(
        IEnumerable<Type> entityTypes,
        bool execute = true,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool logToFile = false)
    {
        // إعداد اللوج
        ConsoleLog.GlobalPrefix = "Runner";
        if (logToFile)
            ConsoleLog.LogFilePath = "migration-runner.log";

        ConsoleLog.Info("=== Migration Runner Started ===");

        // تأكد من وجود قاعدة البيانات
        EnsureDatabaseExists(_connectionString);

        int newTables = 0;
        int alteredTables = 0;
        int unchangedTables = 0;

        var newEntities = _entityDefinitionBuilder.BuildAllWithRelationships(entityTypes).ToList();

        // ترتيب الكيانات حسب العلاقات
        newEntities = OrderEntitiesByDependencies(newEntities);


        foreach (var newEntity in newEntities)
        {
            ConsoleLog.Info($"[RUNNER] Processing entity: {newEntity.ClrType?.Name ?? newEntity.Name}");

            try
            {
                var oldEntity = _migrationService.LoadEntityFromDatabase(newEntity);

                var script = _migrationService.BuildMigrationScript(
                    oldEntity,
                    newEntity,
                    execute: false,
                    dryRun,
                    interactive,
                    previewOnly,
                    autoMerge,
                    showReport,
                    impactAnalysis
                );

                var commands = _autoMigrate.SplitSqlCommands(script);
                var impact = impactAnalysis ? _autoMigrate.AnalyzeImpact(oldEntity, newEntity) : new();
                if (impactAnalysis) _autoMigrate.AssignSeverityAndReason(impact);

                // 🧠 Safety Analysis
                ConsoleLog.Info("\n🔍 Migration Safety Analysis:");
                var safety = _migrationService.AnalyzeMigrationSafety(script, oldEntity, newEntity);
                if (safety.IsSafe)
                {
                    ConsoleLog.Success("✅ All commands are safe.");
                }
                else
                {
                    ConsoleLog.Warning("⚠️ Unsafe commands detected:");
                    foreach (var reason in safety.Reasons)
                        ConsoleLog.Error($"   - {reason}");
                }

                // 📋 Show Report
                if (showReport)
                {
                    _autoMigrate.ShowPreMigrationReport(oldEntity, newEntity, commands, impact, impactAnalysis);
                    ConsoleLog.Info("");
                }

                // 🧮 Classification
                if (string.IsNullOrWhiteSpace(script) || script.Contains("-- No changes detected."))
                {
                    unchangedTables++;
                }
                else if (oldEntity.Columns.Count == 0 && oldEntity.Constraints.Count == 0)
                {
                    newTables++;
                }
                else
                {
                    alteredTables++;
                }

                // 🚀 Execute if approved
                if (execute)
                {
                    if (interactive)
                    {
                        _autoMigrate.ExecuteInteractiveAdvanced(
                            script,
                            oldEntity,
                            newEntity,
                            rollbackOnFailure,
                            autoExecuteRollback,
                            interactiveMode,
                            rollbackPreviewOnly,
                            logToFile
                        );
                    }
                    else
                    {
                       _ =  _migrationService.BuildMigrationScript(
                            oldEntity,
                            newEntity,
                            execute: true,
                            dryRun,
                            interactive,
                            previewOnly,
                            autoMerge,
                            showReport,
                            impactAnalysis
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleLog.Error($"❌ [RUNNER] Migration failed for {newEntity.Name}: {ex.Message}");
            }
        }

        ConsoleLog.Info("\n=== Migration Runner Completed ===\n");
        ConsoleLog.Info("📊 Summary:");
        ConsoleLog.Success($"🆕 New tables created: {newTables}");
        ConsoleLog.Warning($"🔧 Tables altered: {alteredTables}");
        ConsoleLog.Success($"✅ Unchanged tables: {unchangedTables}");
        ConsoleLog.Info("\n======================================\n");
    }



    /// <summary>
    /// Ensures that the database in the given connection string exists.
    /// If it does not exist, it will be created.
    /// </summary>
    /// <param name="connectionString">The connection string to the target database.</param>
    private void EnsureDatabaseExists(string connectionString)
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



    private List<EntityDefinition> OrderEntitiesByDependencies(List<EntityDefinition> entities)
    {
        // خريطة التبعيات: اسم الجدول -> مجموعة الجداول اللي بيعتمد عليها
        var dependencyGraph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var allEntityNames = new HashSet<string>(entities.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            if (!dependencyGraph.ContainsKey(entity.Name))
                dependencyGraph[entity.Name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // هنا بنستخدم ForeignKeyDefinition.ReferencedTable
            foreach (var fk in entity.ForeignKeys)
            {
                if (allEntityNames.Contains(fk.ReferencedTable))
                {
                    dependencyGraph[entity.Name].Add(fk.ReferencedTable);
                }
            }
        }

        var sorted = new List<EntityDefinition>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tempMark = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(string entityName)
        {
            if (tempMark.Contains(entityName))
                throw new InvalidOperationException($"Circular dependency detected involving {entityName}");

            if (!visited.Contains(entityName))
            {
                tempMark.Add(entityName);

                foreach (var dep in dependencyGraph[entityName])
                    Visit(dep);

                tempMark.Remove(entityName);
                visited.Add(entityName);

                var entityDef = entities.First(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));
                sorted.Add(entityDef);
            }
        }

        foreach (var entity in entities)
            Visit(entity.Name);

        return sorted;
    }

}


