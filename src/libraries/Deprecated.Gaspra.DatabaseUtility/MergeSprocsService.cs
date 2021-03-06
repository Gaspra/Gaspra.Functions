﻿using Gaspra.Database.Extensions;
using Gaspra.Database.Interfaces;
using Deprecated.Gaspra.DatabaseUtility.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deprecated.Gaspra.DatabaseUtility.Interfaces;
using Deprecated.Gaspra.DatabaseUtility.Models;
using Deprecated.Gaspra.DatabaseUtility.Models.Database;
using Deprecated.Gaspra.DatabaseUtility.Models.Merge;
using Deprecated.Gaspra.DatabaseUtility.Models.Tree;
using IDataAccess = Gaspra.Database.Interfaces.IDataAccess;

namespace Deprecated.Gaspra.DatabaseUtility
{
    public class MergeSprocsService : IMergeSprocsService
    {
        private readonly ILogger logger;

        private readonly IDatabaseStructure _databaseStructure;
        private readonly IDataAccess _dataAccess;
        private readonly IScriptFactory _scriptFactory;

        public MergeSprocsService(
            ILogger<MergeSprocsService> logger,
            IDataAccess dataAccess,
            IScriptFactory scriptFactory,
            IDatabaseStructure databaseStructure)
        {
            this.logger = logger;

            _databaseStructure = databaseStructure;
            _dataAccess = dataAccess;
            _scriptFactory = scriptFactory;
        }

        public async Task<IEnumerable<MergeStatement>> GenerateMergeSprocs(string connectionString, IEnumerable<string> schemaNames)
        {
            Schema databaseSchema = null;
            var schemaName = "Analytics";

            /*
             * build up database objects
             */
            try
            {
                var databaseResult = await _dataAccess.GetDatabase(connectionString);

                var database = await _databaseStructure.CalculateStructure(connectionString.DatabaseName(), databaseResult);








    //            var databaseInformation = await _dataAccess.GetDatabaseInformation(connectionString);
    //
    //            var columnInfo = databaseInformation.Columns;
    //
    //            var fkInfo = databaseInformation.ForeignKeys;
    //
    //            var extendedProps = databaseInformation.ExtendedProperties;
    //
    //            databaseSchema = Schema
    //                .From(columnInfo, extendedProps, fkInfo)
    //                .Where(s => s.Name.Equals(schemaName))
    //                .FirstOrDefault();
    //
    //            databaseSchema
    //                .CalculateDependencies();
    //
    //            logger.LogInformation("Read schema information for [{schemaName}] with [{tableCount}] tables",
    //                schemaName,
    //                databaseSchema.Tables.Count());
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Unable to calculate schema: [{schemaName}] due to: {ex}",
                    schemaName,
                    ex.Message);
            }

            //
            //calculate dependency tree and build data structure
            //
            var dependencyTree = DependencyTree.Calculate(databaseSchema);

            var dataStructure = new DataStructure(databaseSchema, dependencyTree);

            logger.LogInformation("Calculated dependency tree with [{branchCount}] branches",
                dependencyTree.Branches.Count());

            //
            //build up merge variables
            //
            var (mergeVariables, errornousTables) = MergeVariables.From(dataStructure);

            if (errornousTables.Any())
            {
                logger.LogError("Tables that won't generate merge sprocs: [{tables}], due to exceptions: [{exceptions}]",
                    dataStructure.Schema.Tables.Select(t => t.Name).Except(mergeVariables.Select(m => m.Table.Name)),
                    errornousTables);
            }

            logger.LogInformation("Calculated [{mergeVariableCount}] merge variables",
                mergeVariables.Count());

            //
            //Create merge statements
            //
            var mergeStatements = new List<MergeStatement>();

            foreach (var mergeVariable in mergeVariables)
            {
                var script = await _scriptFactory.ScriptFrom(mergeVariable);

                mergeStatements.Add(new MergeStatement(script, mergeVariable));
            }

            logger.LogInformation("Built [{mergeStatementCount}] merge statements",
                mergeStatements.Count());

            return mergeStatements;
        }
    }
}
