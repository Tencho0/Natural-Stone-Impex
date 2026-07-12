namespace NaturalStoneImpex.Api.Models.Entities;

public enum VisualizationStatus
{
    Succeeded = 0,
    Failed = 1
}

/// <summary>Quota/telemetry row per uploaded photo. Contains no personal data:
/// IpHash is SHA-256 of (ip + day), no photos or results are ever stored.</summary>
public class VisualizationRequest
{
    public int Id { get; set; }
    public string IpHash { get; set; } = string.Empty;
    public VisualizationStatus Status { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
