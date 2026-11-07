using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FitTrack.Tests.Infrastructure;

/// <summary>Uygulamanın log filtresinden geçen her satırı (istisna metni dahil) tutar.</summary>
public sealed class CapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<(string Category, string Message)> Lines { get; } = new();

    public void Clear() => Lines.Clear();

    public ILogger CreateLogger(string categoryName) => new CategoryLogger(categoryName, this);

    public void Dispose() { }

    private sealed class CategoryLogger(string category, CapturedLogs owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Lines.Enqueue((category, formatter(state, exception) + (exception is null ? "" : " " + exception)));
    }
}
