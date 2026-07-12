namespace NaturalStoneImpex.Api.Services.Segmentation;

public class VisualizerOptions
{
    public bool Enabled { get; set; } = true;
    public string EncoderPath { get; set; } = "MLModels/mobilesam-encoder.onnx";
    public string DecoderPath { get; set; } = "MLModels/mobilesam-decoder.onnx";
    public long MaxUploadBytes { get; set; } = 10_485_760;
    public int MaxImageDimension { get; set; } = 2048;
    public int MaxConcurrentEncodes { get; set; } = 2;
    public int EmbeddingCacheMinutes { get; set; } = 15;
    public int PerIpDailyLimit { get; set; } = 20;
    public int GlobalDailyLimit { get; set; } = 500;
}
