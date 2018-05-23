using System;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch
{
    public class CompatibleLogger : ILogger
    {
        private readonly ILogger<CompatibleLogger> _logger;

        public CompatibleLogger(ILogger<CompatibleLogger> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
