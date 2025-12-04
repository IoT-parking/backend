using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/blockchain")]
public class BlockchainController : ControllerBase
{
    private readonly BlockchainService _blockchainService;
    private readonly ILogger<BlockchainController> _logger;

    public BlockchainController(BlockchainService blockchainService, ILogger<BlockchainController> logger)
    {
        _blockchainService = blockchainService;
        _logger = logger;
    }

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances()
    {
        try
        {
            var balances = await _blockchainService.GetAllBalancesAsync();
            return Ok(balances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching blockchain balances");
            return StatusCode(500, "Internal server error while fetching balances");
        }
    }
}
