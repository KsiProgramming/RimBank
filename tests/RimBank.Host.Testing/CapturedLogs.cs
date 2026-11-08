using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RimBank.Host.Testing;

/// <summary>
/// A logger provider that keeps every log entry an in-memory host writes, so a test can check that no personal data
/// reaches the logs (mission quality bar "Security", roadmap 7.2). Each entry is kept as the texts a log sink could
/// write: the category, the formatted message, every structured value, every scope and the exception.
/// </summary>
public sealed class CapturedLogs : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    /// <summary>Every entry written so far.</summary>
    public IReadOnlyCollection<CapturedLogEntry> Entries => [.. _entries];

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>The entries whose texts contain <paramref name="value"/>, compared without regard to case.</summary>
    /// <param name="value">A value that must not be logged.</param>
    /// <returns>The entries that contain it.</returns>
    public IReadOnlyList<CapturedLogEntry> Containing(string value) =>
        [.. _entries.Where(entry => entry.Texts.Any(text => text.Contains(value, StringComparison.OrdinalIgnoreCase)))];

    private sealed class CapturingLogger(string category, CapturedLogs logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            logs._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            var texts = new List<string> { category, formatter(state, exception) };
            texts.AddRange(Values(state));
            logs._scopes.ForEachScope((scope, collected) => collected.AddRange(Values(scope)), texts);
            if (exception is not null)
            {
                texts.Add(exception.ToString());
            }

            logs._entries.Enqueue(new CapturedLogEntry(category, logLevel, texts));
        }

        private static IEnumerable<string> Values(object? state) =>
            state switch
            {
                null => [],
                IEnumerable<KeyValuePair<string, object?>> pairs => pairs.Select(pair => $"{pair.Key}={pair.Value}").Append(state.ToString() ?? string.Empty),
                _ => [state.ToString() ?? string.Empty],
            };
    }
}

/// <summary>One captured log entry.</summary>
/// <param name="Category">The logger's category.</param>
/// <param name="Level">The entry's level.</param>
/// <param name="Texts">Every text a sink could write for it: message, structured values, scopes and exception.</param>
public sealed record CapturedLogEntry(string Category, LogLevel Level, IReadOnlyList<string> Texts);
