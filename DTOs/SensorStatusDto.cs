namespace backend.DTOs;

public class SensorStatusDto
{
    public string SensorId { get; set; } = string.Empty;
    public string Wallet { get; set; } = string.Empty;
    public decimal Tokens { get; set; }
}