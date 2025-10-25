using System;

namespace JobContracts;

public record RunJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public string JobName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}