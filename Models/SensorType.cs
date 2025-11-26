namespace backend.Models;

/// <summary>
/// Enum representing sensor types in parking IoT system
/// </summary>
public static class SensorType
{
    public const string Occupancy = "occupancy";
    public const string CarbonMonoxide = "carbon_monoxide";
    public const string Temperature = "temperature";
    public const string EnergyConsumption = "energy_consumption";
    
    /// <summary>
    /// Returns unit of measurement for given sensor type
    /// </summary>
    public static string GetUnit(string sensorType)
    {
        return sensorType switch
        {
            Occupancy => "boolean",
            CarbonMonoxide => "ppm",
            Temperature => "°C",
            EnergyConsumption => "kWh",
            _ => string.Empty
        };
    }
}
