using System.Diagnostics.CodeAnalysis;

namespace RimBank.SharedKernel;

/// <summary>
/// Marks a write port: the core's interface that loads an aggregate's event stream and appends the events its
/// decisions return, for example the Party Reference Data Directory's directory port (tech-stack §3.4 and §8.1,
/// ADR 0012).
/// </summary>
/// <remarks>
/// Implemented over Marten event streams in the context's <c>.Postgresql</c> adapter. Command handlers use it; the
/// architecture tests forbid it in query handlers and projections.
/// </remarks>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "A marker the architecture tests classify types by (ADR 0012).")]
public interface IWritePort;
