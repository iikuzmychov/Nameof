namespace Nameof.Tests.Infrastructure.Model;

internal sealed record DiagnosticVerifyResult
{
    public required string Id { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public string? Location { get; init; }
}
