using Gaspra.DatabaseUtility.Models.Database;
using Gaspra.DatabaseUtility.Models.Merge;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gaspra.DatabaseUtility.Extensions
{
    public static class MergeSprocBuilderExtensions
    {
        public static string BuildMergeSproc(this MergeVariables variables)
        {
            var sproc = "";

            if (variables.MergeIdentifierColumns.Any())
            {
                sproc = $@"{General.Head()}
{MergeSproc.Drop(variables.SchemaName, variables.ProcedureName())}
{TableType.Drop(variables.TableTypeName(), variables.SchemaName)}
{TableType.Head(variables.TableTypeName(), variables.SchemaName)}
{TableType.Body(variables.TableTypeName(), variables.SchemaName, variables.TableTypeColumns)}
{TableType.Tail(variables.TableTypeName(), variables.SchemaName)}
{MergeSproc.Head(variables.SchemaName, variables.ProcedureName(), variables.TableTypeVariableName(), variables.TableTypeName())}";

                if (variables.TablesToJoin != null && variables.TablesToJoin.Any())
                {
                    sproc += $@"{MergeSproc.TableVariable(variables.ProcedureName(), variables.Table, variables.TableTypeVariableName(), variables.SchemaName, variables.TablesToJoin)}
{MergeSproc.Body($"{variables.ProcedureName()}Variable", variables.MergeIdentifierColumns.Select(c => c.Name), variables.DeleteIdentifierColumns.Select(c => c.Name), variables.RetentionPolicy, variables.Table, variables.SchemaName)}";
                }
                else
                {
                    sproc += $"{MergeSproc.Body(variables.TableTypeVariableName(), variables.MergeIdentifierColumns.Select(c => c.Name), variables.DeleteIdentifierColumns.Select(c => c.Name), variables.RetentionPolicy, variables.Table, variables.SchemaName)}";
                }

                sproc += $@"{MergeSproc.Tail(variables.SchemaName, variables.ProcedureName())}"; //got to figure out how to use the table type columns in the merge body (getting id's for columns that don't exist)
            }
            return sproc;
        }

        public static class General
        {
            public static string Head()
            {
                return
$@"SET NOCOUNT ON
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
";
            }

            public static string NullableColumn(Column column)
            {
                return column.Nullable ? "NULL" : "NOT NULL";
            }

            public static string DataType(Column column)
            {
                var dataType = $"[{column.DataType}]";

                if (column.DataType.Equals("decimal") && column.Precision.HasValue && column.Scale.HasValue)
                {
                    dataType += $"({column.Precision.Value},{column.Scale.Value})";
                }
                else if (column.MaxLength.HasValue)
                {
                    dataType += $"({column.MaxLength.Value})";
                }

                return dataType;
            }
        }

        public static class TableType
        {
            public static string Head(string tableTypeName, string schemaName)
            {
                return
$@"IF NOT EXISTS (SELECT 1 FROM [sys].[types] st JOIN [sys].[schemas] ss ON st.schema_id = ss.schema_id WHERE st.name = N'{tableTypeName}' AND ss.name = N'{schemaName}')
BEGIN";
            }

            public static string Body(string tableTypeName, string schemaName, IEnumerable<Column> columns)
            {
                var body =
$@"    CREATE TYPE [{schemaName}].[{tableTypeName}] AS TABLE(
{string.Join($",{Environment.NewLine}", columns.OrderBy(c => c.Name).Select(c => $"        [{c.Name}] {General.DataType(c)} {General.NullableColumn(c)}"))}
    )";
                return body;
            }

            public static string Tail(string tableTypeName, string schemaName)
            {
                return
$@"END
GO

ALTER AUTHORIZATION ON TYPE::[{schemaName}].[{tableTypeName}] TO SCHEMA OWNER
GO
";
            }

            public static string Drop(string tableTypeName, string schemaName)
            {
                return
$@"IF EXISTS (SELECT 1 FROM [sys].[types] st JOIN [sys].[schemas] ss ON st.schema_id = ss.schema_id WHERE st.name = N'{tableTypeName}' AND ss.name = N'{schemaName}')
BEGIN
    DROP TYPE [{schemaName}].[{tableTypeName}]
END
GO
";
            }
        }

        public static class MergeSproc
        {
            public static string Head(string schemaName, string sprocName, string tableTypeVariable, string tableType)
            {
                return
$@"IF NOT EXISTS (SELECT 1 FROM [sys].[objects] WHERE [object_id] = OBJECT_ID(N'[{schemaName}].[{sprocName}]') AND [type] IN (N'P'))
BEGIN
	EXEC [dbo].[sp_executesql] @statement = N'CREATE PROCEDURE [{schemaName}].[{sprocName}] AS'
END
GO

ALTER PROCEDURE [{schemaName}].[{sprocName}]
    @{tableTypeVariable} {tableType} READONLY
AS
BEGIN

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

";
            }

            public static string TableVariable(string sprocName, Table databaseTable, string tableTypeVariable, string schemaName, IEnumerable<(Table joinTable, IEnumerable<Column> joinColumns, IEnumerable<Column> selectColumns)> tablesToJoin)
            {
                var tableVariable =
$@"DECLARE @{sprocName}Variable TABLE
(
{string.Join($",{Environment.NewLine}", databaseTable.Columns.Where(c => !c.IdentityColumn).Select(c => $"    [{c.Name}] {General.DataType(c)} {General.NullableColumn(c)}"))}
)

INSERT INTO @{sprocName}Variable
SELECT
{string.Join($",{Environment.NewLine}", databaseTable.Columns.Where(c => !c.IdentityColumn).Select(c => $"    [{GetInsertInto(c)}]"))}
FROM
    @{tableTypeVariable} AS tt
    INNER JOIN {string.Join($"{Environment.NewLine}INNER JOIN ", tablesToJoin.Select(t => $"[{schemaName}].[{t.joinTable.Name}] AS alias_{t.joinTable.Name.ToLower()} ON {string.Join(" AND ", t.selectColumns.Select(c => $"tt.[{c.Name}] = alias_{t.joinTable.Name.ToLower()}.[{c.Name}]"))}"))}
";
                return tableVariable;
            }

            private static string GetInsertInto(Column column)
            {
                return column.Name;
            }

            public static string Body(string tableTypeVariable, IEnumerable<string> matchOn, IEnumerable<string> deleteOn, RetentionPolicy retentionPolicy, Table databaseTable, string schemaName)
            {
                var sproc = "";

                var deleteOnFactId = matchOn.Where(m => !deleteOn.Any(d => d.Equals(m))).FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(deleteOnFactId) && deleteOn.Any())
                {
                    sproc +=
$@"DECLARE @InsertedValues TABLE (
    [{databaseTable.Name}Id] [int],
{string.Join($",{Environment.NewLine}", databaseTable.Columns.Where(c => matchOn.Any(m => m.Equals(c.Name))).Select(c => $"    [{c.Name}] {General.DataType(c)}")) }
){Environment.NewLine}{Environment.NewLine}"
;
                }

                sproc +=
$@"MERGE [{schemaName}].[{databaseTable.Name}] AS t
USING @{tableTypeVariable} AS s
    ON ({Environment.NewLine}        {string.Join($"{Environment.NewLine}        AND ", matchOn.Select(m => $"t.[{m}] = s.[{m}]"))}
    )

WHEN NOT MATCHED BY TARGET
    THEN INSERT (
        {string.Join($",{Environment.NewLine}        ", databaseTable.Columns.Where(c => !c.IdentityColumn).Select(c => $"[{c.Name}]"))}
    )
    VALUES (
        {string.Join($",{Environment.NewLine}        ", databaseTable.Columns.Where(c => !c.IdentityColumn).Select(c => $"s.[{c.Name}]"))}
    )
";

                if(!matchOn.Count().Equals(databaseTable.Columns.Count) &&
                    !databaseTable.Columns.Where(c => !c.IdentityColumn).Select(c => c.Name).All(n => matchOn.Any(m => m.Equals(n, StringComparison.InvariantCultureIgnoreCase))))
                {
                    sproc += $@"
WHEN MATCHED
    THEN UPDATE SET
        {string.Join($",{Environment.NewLine}        ", databaseTable.Columns.Where(c => !c.IdentityColumn).Select(c => $"t.[{c.Name}] = s.[{c.Name}]"))}
";
                }

                if (retentionPolicy != null)
                {
                    sproc += $@"
WHEN NOT MATCHED BY SOURCE AND t.{retentionPolicy.ComparisonColumn} < DATEADD(month, -{retentionPolicy.RetentionMonths}, GETUTCDATE())
    THEN DELETE
";
                }

                if (!string.IsNullOrWhiteSpace(deleteOnFactId) && deleteOn.Any())
                {
                    sproc +=
$@"
OUTPUT
    inserted.{databaseTable.Name}Id,
    {string.Join($",{Environment.NewLine}    ", databaseTable.Columns.Where(c => matchOn.Any(m => m.Equals(c.Name))).Select(c => $"inserted.{c.Name}")) }
INTO @InsertedValues
";
}
                    sproc += $";{Environment.NewLine}";

                if (!string.IsNullOrWhiteSpace(deleteOnFactId) && deleteOn.Any())
                {
                    sproc += $@"
DELETE
    mrg_table
FROM
    [{schemaName}].[{databaseTable.Name}] mrg_table
    INNER JOIN @InsertedValues iv_inner ON mrg_table.{matchOn.Where(m => !deleteOn.Any(d => d.Equals(m))).FirstOrDefault()} = iv_inner.{matchOn.Where(m => !deleteOn.Any(d => d.Equals(m))).FirstOrDefault()}
    LEFT JOIN @InsertedValues iv_outer ON mrg_table.{databaseTable.Name}Id = iv_outer.{databaseTable.Name}Id
WHERE
    iv_outer.{databaseTable.Name}Id IS NULL
";
                }


                sproc += $"{Environment.NewLine}";

                return sproc;
            }

            public static string Tail(string schemaName, string sprocName)
            {
                return
$@"END
GO
ALTER AUTHORIZATION ON [{schemaName}].[{sprocName}] TO SCHEMA OWNER
GO";
            }

            public static string Drop(string schemaName, string sprocName)
            {
                return
$@"IF EXISTS (SELECT 1 FROM [sys].[objects] WHERE [object_id] = OBJECT_ID(N'[{schemaName}].[{sprocName}]') AND [type] IN (N'P'))
BEGIN
    DROP PROCEDURE [{schemaName}].[{sprocName}]
END
GO
";
            }

        }
    }
}
