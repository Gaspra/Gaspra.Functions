﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaspra.Database.Extensions;
using Gaspra.SqlGenerator.Interfaces;
using Gaspra.SqlGenerator.Models;

namespace Gaspra.SqlGenerator.Factories.Sections.Procedure.Delete
{
    public class InsertMergeResultSection : IScriptSection
    {
        private readonly IScriptLineFactory _scriptLineFactory;

        public ScriptOrder Order { get; } = new(new[] { 1, 1, 0, 5 });

        public InsertMergeResultSection(IScriptLineFactory scriptLineFactory)
        {
            _scriptLineFactory = scriptLineFactory;
        }

        public Task<bool> Valid(IMergeScriptVariableSet variableSet)
        {
            return Task.FromResult(variableSet.Table.SoftDeleteColumn() != null);
        }

        public async Task<string> Value(IMergeScriptVariableSet variableSet)
        {
            var identityColumn = variableSet
                .Table
                .Columns
                .FirstOrDefault(c => c.IdentityColumn);

            var script = new List<string>
            {
                "INSERT INTO",
                "    @MergeResult",
                "SELECT",
                "     'DELETE'",
                $"   ,sd.{identityColumn.Name}",
                "FROM",
                $"    [{variableSet.Schema.Name}].[{variableSet.Table.Name}] {variableSet.Table.Name}",
                $"    INNER JOIN @SoftDelete sd ON {variableSet.Table.Name}.{identityColumn.Name}=sd.{identityColumn.Name}",
                "WHERE",
                $"    {variableSet.Table.Name}.Deleted=0"
            };

            var scriptLines = await _scriptLineFactory.LinesFrom(
                1,
                script.ToArray()
                );

            return await _scriptLineFactory.StringFrom(scriptLines);
        }
    }
}
