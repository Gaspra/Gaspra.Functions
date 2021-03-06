﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gaspra.Database.Extensions;
using Gaspra.Database.Models;
using Gaspra.SqlGenerator.Interfaces;
using Gaspra.SqlGenerator.Models;
using Microsoft.Extensions.Logging;

namespace Gaspra.SqlGenerator.Factories
{
    public class ScriptVariableFactory : IScriptVariableFactory
    {
        private readonly ILogger _logger;

        public ScriptVariableFactory(ILogger<ScriptVariableFactory> logger)
        {
            _logger = logger;
        }

        public Task<IReadOnlyCollection<IMergeScriptVariableSet>> MergeVariablesFrom(DatabaseModel database)
        {
            var mergeScriptVariableSets = new List<MergeScriptVariableSet>();

            foreach (var schema in database.Schemas)
            {
                foreach (var table in schema.Tables)
                {
                    try
                    {
                        if (!table.ShouldSkipTable())
                        {
                            var scriptFileName = $"{schema.Name}.Merge{table.Name}.sql";

                            var scriptName = $"Merge{table.Name}";

                            var tableTypeVariableName = $"{table.Name}";

                            var tableTypeName = $"TT_{table.Name}";

                            var tableTypeColumns = table.TableTypeColumns(schema);

                            var mergeIdentifierColumns = table.MergeIdentifierColumns(schema);

                            var deleteIdentifierColumns = table.DeleteIdentifierColumns(schema);

                            var retentionPolicy = table.RetentionPolicy();

                            var tablesToJoin = table.TablesToJoin(schema);

                            var mergeScriptVariableSet = new MergeScriptVariableSet
                            {
                                Schema = schema,
                                Table = table,
                                DeleteIdentifierColumns = deleteIdentifierColumns,
                                MergeIdentifierColumns = mergeIdentifierColumns,
                                RetentionPolicy = retentionPolicy,
                                ScriptFileName = scriptFileName,
                                ScriptName = scriptName,
                                TablesToJoin = tablesToJoin,
                                TableTypeColumns = tableTypeColumns,
                                TableTypeName = tableTypeName,
                                TableTypeVariableName = tableTypeVariableName
                            };

                            mergeScriptVariableSets.Add(mergeScriptVariableSet);
                        }
                        else
                        {
                            _logger.LogInformation("[{schema}].[{table}] has the extended property to skip, it won't be calculated in the merge variables",
                                schema.Name,
                                table.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Unable to calculate merge variable set for [{schema}].[{table}]",
                            schema.Name,
                            table.Name
                            );
                    }
                }
            }

            return Task.FromResult((IReadOnlyCollection<IMergeScriptVariableSet>)mergeScriptVariableSets);
        }
    }
}
