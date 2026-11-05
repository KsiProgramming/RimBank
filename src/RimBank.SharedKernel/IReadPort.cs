namespace RimBank.SharedKernel;

/// <summary>
/// Marks a read port: the core's interface that reads views (read models), for example the Party Reference Data
/// Directory's enquiry port (tech-stack §3.4 and §8.2, ADR 0012).
/// </summary>
/// <remarks>
/// Implemented over projected Marten documents in the context's <c>.Postgresql</c> adapter. Query handlers use it;
/// the architecture tests forbid it in command handlers and projections, so a business decision never depends on an
/// eventually consistent view (mission principle 5).
/// </remarks>
public interface IReadPort;
