namespace RimBank.SharedKernel.Tests;

// The sample ledger's read model: an immutable record with no behaviour (tech-stack §8.2).
public sealed record SampleLedgerView(Money Balance, int Postings, bool IsClosed);
