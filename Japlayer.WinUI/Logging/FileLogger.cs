#nullable enable
using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Japlayer.Logging
{
    public class FileLoggerProvider(string filePath) : ILoggerProvider
    {
        private readonly string _filePath = filePath;

        public ILogger CreateLogger(string categoryName) => new FileLogger(_filePath, categoryName);

        public void Dispose()
        {
        }
    }

    public class FileLogger(string filePath, string categoryName) : ILogger
    {
        private readonly string _filePath = filePath;
        private readonly string _categoryName = categoryName;
        private static readonly object _lockObject = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter == null)
            {
                return;
            }

            var message = formatter(state, exception);
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";
            if (exception != null)
            {
                logLine += Environment.NewLine + exception.ToString();
            }

            lock (_lockObject)
            {
                try
                {
                    var directoryPath = Path.GetDirectoryName(_filePath);
                    if (directoryPath != null && !Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    File.AppendAllText(_filePath, logLine + Environment.NewLine);
                }
                catch
                {
                    // Swallowing exception to prevent crash due to logging failure
                }
            }
        }
    }

    public static class FileLoggerExtensions
    {
        public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath)
        {
            builder.AddProvider(new FileLoggerProvider(filePath));
            return builder;
        }
    }
}
