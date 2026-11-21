#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Japlayer.Logging
{
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private const long MaxLogFileBytes = 5 * 1024 * 1024;
        private readonly string _filePath;
        private readonly BlockingCollection<string> _messageQueue = new(boundedCapacity: 2048);
        private readonly Thread _writerThread;

        public FileLoggerProvider(string filePath)
        {
            _filePath = filePath;
            _writerThread = new Thread(ProcessQueue)
            {
                IsBackground = true,
                Name = "JaplayerFileLogger"
            };
            _writerThread.Start();
        }

        public ILogger CreateLogger(string categoryName) => new FileLogger(_messageQueue, categoryName);

        private void ProcessQueue()
        {
            try
            {
                foreach (var logLine in _messageQueue.GetConsumingEnumerable())
                {
                    WriteLine(logLine);
                }
            }
            catch
            {
                // Ignore writer shutdown errors
            }
        }

        private void WriteLine(string logLine)
        {
            try
            {
                var directoryPath = Path.GetDirectoryName(_filePath);
                if (directoryPath != null && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                if (File.Exists(_filePath) && new FileInfo(_filePath).Length >= MaxLogFileBytes)
                {
                    var archivePath = _filePath + ".1";
                    File.Move(_filePath, archivePath, overwrite: true);
                }

                File.AppendAllText(_filePath, logLine + Environment.NewLine);
            }
            catch
            {
                // Swallowing exception to prevent crash due to logging failure
            }
        }

        public void Dispose()
        {
            _messageQueue.CompleteAdding();
            _writerThread.Join(TimeSpan.FromSeconds(1));
            _messageQueue.Dispose();
        }
    }

    public sealed class FileLogger(BlockingCollection<string> messageQueue, string categoryName) : ILogger
    {
        private readonly BlockingCollection<string> _messageQueue = messageQueue;
        private readonly string _categoryName = categoryName;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter == null || !IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";
            if (exception != null)
            {
                logLine += Environment.NewLine + exception.ToString();
            }

            _messageQueue.TryAdd(logLine);
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
