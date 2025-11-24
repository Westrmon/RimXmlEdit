using Microsoft.Extensions.Logging;

namespace RimXmlEdit.Core.Utils;

public static class LoggerFactoryInstance
{
    private static readonly Lock _lock = new();
    private static ILoggerFactory? _factory;

    public static readonly EventId NotifyEventId = new(int.MaxValue - 1, "Notify");

    public enum LogLevelConfig : byte
    {
        Debug = 1,
        Information,
        Warning,
        Error,
    }

#if DEBUG
    public static LogLevelConfig ConsoleLevel { get; set; } = LogLevelConfig.Information;
#endif
    public static Action<LogLevel, string, string>? OnShowNotification { get; set; }

    public static LogLevelConfig FileLevel { get; set; } = LogLevelConfig.Warning;

    public static LogLevelConfig NotificationLevel { get; set; } = LogLevelConfig.Warning;

    public static ILoggerFactory Factory
    {
        get
        {
            if (_factory != null) return _factory;

            using (_lock.EnterScope())
            {
                if (_factory != null) return _factory;

                // 2. 文件提供者（受 FileLevel 控制）
                var logDir = Path.Combine(TempConfig.AppPath, "Logs");
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, $"log_{DateTime.Now:yyyyMMdd}.txt");

                _factory = LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Trace) // 内部最低级别，实际过滤在 Provider 里完成
                           .AddProvider(new FileLoggerProvider(logFile, (category, level) =>
                                            level >= (LogLevel)FileLevel))
                           .AddProvider(new NotificationLoggerProvider((category, level) =>
                                            level >= LogLevel.Information))
#if DEBUG
                           .AddProvider(new ConsoleLoggerProvider((category, level) =>
                                            level >= (LogLevel)ConsoleLevel))
#endif
                           ;
                });
            }

            return _factory!;
        }
    }

    public static void SetLevels(
        LogLevelConfig file,
        LogLevelConfig notification)
    {
        FileLevel = file;
        NotificationLevel = notification;
    }

    public static void SetConsoleLevel(LogLevelConfig console)
    {
#if DEBUG
        ConsoleLevel = console;
#endif
    }
}

/// <summary>
/// 控制台 Provider，支持独立级别过滤
/// </summary>
#if DEBUG

internal sealed class ConsoleLoggerProvider : ILoggerProvider
{
    private readonly Func<string, LogLevel, bool> _filter;

    public ConsoleLoggerProvider(Func<string, LogLevel, bool> filter) => _filter = filter;

    public ILogger CreateLogger(string categoryName) =>
        new FilteredConsoleLogger(categoryName, _filter);

    public void Dispose()
    { }
}

internal sealed class FilteredConsoleLogger : ILogger
{
    private readonly string _category;
    private readonly Func<string, LogLevel, bool> _filter;

    public FilteredConsoleLogger(string category, Func<string, LogLevel, bool> filter)
    {
        _category = category;
        _filter = filter;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => _filter(_category, logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var msg = formatter(state, exception);
        var color = logLevel switch
        {
            LogLevel.Information => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error or LogLevel.Critical => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };

        lock (Console.Out)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{logLevel}] {_category}: {msg}");
            if (exception != null) Console.WriteLine(exception);
            Console.ForegroundColor = old;
        }
    }
}

#endif

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly Func<string, LogLevel, bool> _filter;

    public FileLoggerProvider(string filePath, Func<string, LogLevel, bool> filter)
    {
        _filePath = filePath;
        _filter = filter;
    }

    public ILogger CreateLogger(string categoryName) =>
        new FilteredFileLogger(_filePath, categoryName, _filter);

    public void Dispose()
    { }
}

internal sealed class FilteredFileLogger : ILogger
{
    private readonly string _filePath;
    private readonly string _category;
    private readonly Func<string, LogLevel, bool> _filter;

    public FilteredFileLogger(string filePath, string category, Func<string, LogLevel, bool> filter)
    {
        _filePath = filePath;
        _category = category;
        _filter = filter;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => _filter(_category, logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var msg = formatter(state, exception);
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] {_category}: {msg}";
        if (exception != null) line += Environment.NewLine + exception;

        lock (_filePath)
        {
            File.AppendAllLinesAsync(_filePath, new[] { line });
        }
    }
}

internal sealed class NotificationLoggerProvider : ILoggerProvider
{
    private readonly Func<string, LogLevel, bool> _filter;

    public NotificationLoggerProvider(Func<string, LogLevel, bool> filter) => _filter = filter;

    public ILogger CreateLogger(string categoryName) =>
        new NotificationLogger(categoryName, _filter);

    public void Dispose()
    {
    }
}

internal sealed class NotificationLogger : ILogger
{
    private readonly string _category;
    private readonly Func<string, LogLevel, bool> _filter;

    public NotificationLogger(string category, Func<string, LogLevel, bool> filter)
    {
        _category = category;
        _filter = filter;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => _filter(_category, logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        if (LoggerFactoryInstance.OnShowNotification == null) return;
        bool isHighLevel = logLevel >= (LogLevel)LoggerFactoryInstance.NotificationLevel;
        bool isForceNotify = eventId.Id == LoggerFactoryInstance.NotifyEventId.Id;
        if (!isHighLevel && !isForceNotify) return;

        var msg = formatter(state, exception);
        LoggerFactoryInstance.OnShowNotification.Invoke(logLevel, _category, msg);
    }
}
