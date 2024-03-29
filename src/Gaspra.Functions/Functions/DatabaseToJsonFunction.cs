﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gaspra.Functions.Correlation;
using Gaspra.Functions.Extensions;
using Gaspra.Functions.Interfaces;
using Gaspra.SqlGenerator.Interfaces;
using Microsoft.Extensions.Logging;

namespace Gaspra.Functions.Functions
{
    public class DatabaseToJsonFunction : IFunction
    {
        private readonly ILogger _logger;
        private readonly IDatabaseToJsonGenerator _databaseToJsonGenerator;

        private string connectionString = "";
        private IList<string> _schemas = new List<string>();

        public DatabaseToJsonFunction(
            ILogger<DatabaseToJsonFunction> logger,
            IDatabaseToJsonGenerator databaseToJsonGenerator)
        {
            _logger = logger;
            _databaseToJsonGenerator = databaseToJsonGenerator;
        }

        public IReadOnlyCollection<string> Aliases => new[] { "databasetojson", "dtj" };

        public IReadOnlyCollection<IFunctionParameter> Parameters => new List<IFunctionParameter>
        {
            new FunctionParameter("c", null, false, "Connection string"),
            new FunctionParameter("s", null, true, "Schemas to generate merge stored procedures for, comma delimited")
        };

        public string About => "Database to JSON";

        public bool ValidateParameters(IReadOnlyCollection<IFunctionParameter> parameters)
        {
            if(!parameters.Any())
            {
                return false;
            }

            var connectionStringParameter = parameters
                .FirstOrDefault(p => p.Key.Equals("c"));

            if(connectionStringParameter == null || !connectionStringParameter.Values.Any())
            {
                return false;
            }

            connectionString = connectionStringParameter.Values.First().ToString();

            var schemas = parameters
                .FirstOrDefault(p => p.Key.Equals("s"));

            if (schemas != null && schemas.Values.Any())
            {
                var schemaList = schemas.Values.First().ToString();

                _schemas = schemaList?.Split(",").Select(s => s.Trim()).ToList();
            }

            return true;
        }

        public async Task Run(CancellationToken cancellationToken, IReadOnlyCollection<IFunctionParameter> parameters)
        {
            var jsonDatabase = await _databaseToJsonGenerator.Generate(
                connectionString,
                _schemas.ToList()
                );

            if (jsonDatabase.TryWriteFile("database.json"))
            {
                _logger.LogInformation($"File written: database");
            }
            else
            {
                _logger.LogError($"File failed to write: database");
            }
        }
    }
}
