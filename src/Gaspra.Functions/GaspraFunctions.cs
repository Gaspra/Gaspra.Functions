using Gaspra.Functions.Correlation.Interfaces;
using Gaspra.Functions.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gaspra.Functions
{
    public class GaspraFunctions : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IEnumerable<IFunction> _functions;
        private readonly ICorrelationContext _cxt;
        private readonly IEnumerable<string> _helpParameters = new[] { "h", "help" };

        public GaspraFunctions(
            ILogger<GaspraFunctions> logger,
            IHostApplicationLifetime hostApplicationLifetime,
            IEnumerable<IFunction> functions,
            ICorrelationContext cxt)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
            _functions = functions;
            _cxt = cxt;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var openingHoursDataConverter = new OpeningHoursDataConverter();

            try
            {
                await openingHoursDataConverter.ConvertCsv();
            }
            catch (Exception ex)
            {
                var exception = ex;
            }

            if (Debug.DebugMode)
            {
                _cxt.FunctionName = Debug.FunctionName;

                _cxt.FunctionParameters = Debug.FunctionParameters;
            }

            await ProcessFunction(cancellationToken);

            _hostApplicationLifetime.StopApplication();
        }

        private async Task ProcessFunction(CancellationToken cancellationToken)
        {
            var function = _functions.FirstOrDefault(
                f => f.Aliases.Any(
                    a => a.Equals(_cxt.FunctionName, StringComparison.InvariantCultureIgnoreCase)));

            if (function == null)
            {
                _logger.LogError("Function [{requestedFunction}] was not found, please choose from: {availableFunctions}",
                    _cxt.FunctionName,
                    _functions.Select(f => $"[{string.Join(", ", f.Aliases)}]")
                    );
            }
            else
            {
                if (_cxt
                    .FunctionParameters
                    .Any(p => _helpParameters
                        .Any(h => h.Equals(p.Key, StringComparison.InvariantCultureIgnoreCase))))
                {
                    _logger.LogInformation(function.About);
                }
                else
                {
                    if (function.ValidateParameters(_cxt.FunctionParameters))
                    {
                        _logger.LogInformation("Executing [{requestedFunction}] (ctrl+c to exit)",
                            _cxt.FunctionName);

                        Console.CancelKeyPress += (sender, e) =>
                        {
                            _cxt.FunctionCancellationSource
                                .Cancel();
                        };

                        await function.Run(_cxt.FunctionCancellationSource.Token, _cxt.FunctionParameters);
                    }
                    else
                    {
                        _logger.LogWarning("Function [{requestedFunction}] parameters are invalid so it will not run.",
                            _cxt.FunctionName);
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Finished [{requestedFunction}] in [{executionTime}]",
                _cxt.FunctionName,
                DateTimeOffset.UtcNow - _cxt.FunctionTimestamp);

            return Task.CompletedTask;
        }
    }
}
