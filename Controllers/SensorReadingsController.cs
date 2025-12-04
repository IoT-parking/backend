using backend.DTOs;
using backend.Repositories;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Controller for managing sensor readings
/// </summary>
[ApiController]
[Route("api/v1/sensors")]
public class SensorReadingsController : ControllerBase
{
    private readonly ISensorReadingRepository _repository;
    private readonly IExportService _exportService;
    private readonly BlockchainService _blockchainService;
    private readonly ILogger<SensorReadingsController> _logger;

    public SensorReadingsController(
        ISensorReadingRepository repository,
        IExportService exportService,
        BlockchainService blockchainService,
        ILogger<SensorReadingsController> logger)
    {
        _repository = repository;
        _exportService = exportService;
        _blockchainService = blockchainService;
        _logger = logger;
    }

    /// <summary>
    /// Gets filtered and sorted sensor readings
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<SensorReadingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SensorReadingDto>>> GetReadings([FromQuery] SensorFilterParameters parameters)
    {
        try
        {
            var result = await _repository.GetFilteredAsync(parameters);

            var dtoData = result.Data.Select(x => new SensorReadingDto
            {
                Id = x.Id ?? string.Empty,
                SensorType = x.SensorType,
                SensorInstanceId = x.SensorInstanceId,
                Value = x.Value,
                Unit = x.Unit,
                Timestamp = x.Timestamp
            });

            return Ok(new PagedResponse<SensorReadingDto>
            {
                Data = dtoData,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalRecords = result.TotalRecords
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sensor readings");
            return StatusCode(500, "An error occurred while retrieving sensor readings");
        }
    }

    /// <summary>
    /// Gets reading by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SensorReadingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SensorReadingDto>> GetReadingById(string id)
    {
        try
        {
            var reading = await _repository.GetByIdAsync(id);

            if (reading == null)
                return NotFound();

            var dto = new SensorReadingDto
            {
                Id = reading.Id ?? string.Empty,
                SensorType = reading.SensorType,
                SensorInstanceId = reading.SensorInstanceId,
                Value = reading.Value,
                Unit = reading.Unit,
                Timestamp = reading.Timestamp
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sensor reading with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the sensor reading");
        }
    }

    /// <summary>
    /// Exports data to CSV format
    /// </summary>
    [HttpGet("export/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportToCsv([FromQuery] SensorFilterParameters parameters)
    {
        try
        {
            // Get all data without pagination for export
            parameters.PageSize = int.MaxValue;
            parameters.PageNumber = 1;

            var result = await _repository.GetFilteredAsync(parameters);
            var csvBytes = _exportService.ExportToCsv(result.Data);

            var fileName = $"sensor_readings_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return File(csvBytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting data to CSV");
            return StatusCode(500, "An error occurred while exporting data");
        }
    }

    /// <summary>
    /// Exports data to JSON format
    /// </summary>
    [HttpGet("export/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportToJson([FromQuery] SensorFilterParameters parameters)
    {
        try
        {
            parameters.PageSize = int.MaxValue;
            parameters.PageNumber = 1;

            var result = await _repository.GetFilteredAsync(parameters);
            var jsonBytes = _exportService.ExportToJson(result.Data);

            var fileName = $"sensor_readings_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            return File(jsonBytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting data to JSON");
            return StatusCode(500, "An error occurred while exporting data");
        }
    }

    /// <summary>
    /// Gets unique sensor types
    /// </summary>
    [HttpGet("sensor-types")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> GetSensorTypes()
    {
        try
        {
            var types = await _repository.GetUniqueSensorTypesAsync();
            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sensor types");
            return StatusCode(500, "An error occurred while retrieving sensor types");
        }
    }

    /// <summary>
    /// Gets unique sensor instances
    /// </summary>
    [HttpGet("sensor-instances")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> GetSensorInstances([FromQuery] string? sensorType = null)
    {
        try
        {
            var instances = await _repository.GetUniqueSensorInstancesAsync(sensorType);
            return Ok(instances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sensor instances");
            return StatusCode(500, "An error occurred while retrieving sensor instances");
        }
    }

    /// <summary>
    /// Gets last N readings for sensor instance
    /// </summary>
    [HttpGet("last/{sensorInstanceId}")]
    [ProducesResponseType(typeof(IEnumerable<SensorReadingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SensorReadingDto>>> GetLastReadings(
        string sensorInstanceId,
        [FromQuery] int count = 100)
    {
        try
        {
            var readings = await _repository.GetLastNReadingsAsync(sensorInstanceId, count);

            var dtos = readings.Select(x => new SensorReadingDto
            {
                Id = x.Id ?? string.Empty,
                SensorType = x.SensorType,
                SensorInstanceId = x.SensorInstanceId,
                Value = x.Value,
                Unit = x.Unit,
                Timestamp = x.Timestamp
            });

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving last readings for sensor {SensorInstanceId}", sensorInstanceId);
            return StatusCode(500, "An error occurred while retrieving readings");
        }
    }

    /// <summary>
    /// Gets average value from last N readings for sensor instance
    /// </summary>
    [HttpGet("average/{sensorInstanceId}")]
    [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetAverageReading(
        string sensorInstanceId,
        [FromQuery] int count = 100)
    {
        try
        {
            var average = await _repository.GetAverageOfLastNReadingsAsync(sensorInstanceId, count);

            if (!average.HasValue)
                return NotFound(new { message = "No readings found for this sensor" });

            return Ok(new { sensorInstanceId, average = average.Value, count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating average for sensor {SensorInstanceId}", sensorInstanceId);
            return StatusCode(500, "An error occurred while calculating average");
        }
    }

    /// <summary>
    /// Deletes all readings (only for testing/development)
    /// </summary>
    [HttpDelete("all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAllReadings()
    {
        try
        {
            await _repository.DeleteAllAsync();
            _logger.LogWarning("All sensor readings have been deleted");
            return Ok(new { message = "All sensor readings have been deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all readings");
            return StatusCode(500, "An error occurred while deleting readings");
        }
    }

    /// <summary>
    /// Gets current status, wallet address and token balance for all sensors
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(IEnumerable<SensorStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SensorStatusDto>>> GetSensorsStatus()
    {
        try
        {
            // Unique sensor IDs from the database
            var sensorIds = await _repository.GetUniqueSensorInstancesAsync();

            // For each sensor, query the Blockchain for the balance
            var tasks = sensorIds.Select(async sensorId =>
            {
                var wallet = _blockchainService.GetWalletForSensor(sensorId);
                var balance = await _blockchainService.GetBalanceAsync(sensorId);

                return new SensorStatusDto
                {
                    SensorId = sensorId,
                    Wallet = wallet ?? "Unknown",
                    Tokens = balance
                };
            });

            var result = await Task.WhenAll(tasks);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sensor statuses");
            return StatusCode(500, "Error communicating with blockchain or database");
        }
    }
}