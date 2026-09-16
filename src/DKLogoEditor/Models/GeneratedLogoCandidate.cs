namespace DKLogoEditor.Models;

public sealed record GeneratedLogoCandidate(
    int Index,
    byte[] ImageBytes,
    string MediaType,
    string ModelId,
    double? CostUsd,
    string RawFilePath);
