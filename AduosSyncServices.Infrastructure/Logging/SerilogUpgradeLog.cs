using DbUp.Engine.Output;

namespace AduosSyncServices.Infrastructure.Logging
{
    public class SerilogUpgradeLog : IUpgradeLog
    {
        private readonly Serilog.ILogger _logger;

        public SerilogUpgradeLog(Serilog.ILogger logger)
        {
            _logger = logger;
        }

        public void LogTrace(string format, params object[] args)
            => _logger.Verbose(format, args);

        public void LogDebug(string format, params object[] args)
            => _logger.Debug(format, args);

        public void LogInformation(string format, params object[] args)
            => _logger.Information(format, args);

        public void WriteInformation(string format, params object[] args)
            => LogInformation(format, args);

        public void LogWarning(string format, params object[] args)
            => _logger.Warning(format, args);

        public void WriteWarning(string format, params object[] args)
            => LogWarning(format, args);

        public void LogError(string format, params object[] args)
            => _logger.Error(format, args);

        public void WriteError(string format, params object[] args)
            => LogError(format, args);

        public void LogError(Exception ex, string format, params object[] args)
            => _logger.Error(ex, format, args);
    }
}