using NetCord.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using NetCordLogLevel = NetCord.Logging.LogLevel;

namespace AlenAlex.Api.Infrastructure.Discord;

internal sealed class NetCordLogger(ILogger logger) : IGatewayLogger, IRestLogger
{
    public bool IsEnabled(NetCordLogLevel logLevel) => logger.IsEnabled(Map(logLevel));

    public void Log<TState>(NetCordLogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        logger.Log(Map(logLevel), default, state, exception, formatter);

    private static MsLogLevel Map(NetCordLogLevel level) => level switch
    {
        NetCordLogLevel.Trace => MsLogLevel.Trace,
        NetCordLogLevel.Debug => MsLogLevel.Debug,
        NetCordLogLevel.Information => MsLogLevel.Information,
        NetCordLogLevel.Warning => MsLogLevel.Warning,
        NetCordLogLevel.Error => MsLogLevel.Error,
        NetCordLogLevel.Critical => MsLogLevel.Critical,
        _ => MsLogLevel.None,
    };
}
