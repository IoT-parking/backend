using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Util;
using System.Numerics;

namespace backend.Services;

public class BlockchainService
{
    private readonly Web3 _web3;

    // Deterministic address for Anvil (Account #0, Nonce #0)
    private readonly string _contractAddress = "0x5FbDB2315678afecb367f032d93F642f64180aa3";
    private readonly string _adminPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    
    private readonly ILogger<BlockchainService> _logger;
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    
    // Flag blocking transactions until the contract is deployed
    private volatile bool _isContractReady = false;

    public BlockchainService(IConfiguration configuration, ILogger<BlockchainService> logger)
    {
        _logger = logger;
        var rpcUrl = configuration["Blockchain:RpcUrl"] ?? "http://blockchain:8545";
        var account = new Account(_adminPrivateKey);
        _web3 = new Web3(account, rpcUrl);

        // Start monitoring contract availability in the background
        Task.Run(WaitForContractDeployment);
    }

    private async Task WaitForContractDeployment()
    {
        _logger.LogInformation("Waiting for Smart Contract deployment...");
        
        // Try for 60 seconds
        for (int i = 0; i < 30; i++)
        {
            try 
            {
                var code = await _web3.Eth.GetCode.SendRequestAsync(_contractAddress);
                if (code != "0x")
                {
                    _isContractReady = true;
                    _logger.LogInformation($"[BLOCKCHAIN READY] Contract detected at address {_contractAddress}. Unlocking transactions.");
                    return;
                }
            }
            catch
            {
                // Ignore connection errors during container startup
            }
            
            await Task.Delay(2000); // Check every 2 seconds
        }
        
        _logger.LogError("[BLOCKCHAIN TIMEOUT] Contract was not detected after 60 seconds. Check the blockchain container.");
    }

    public string GetWalletForSensor(string sensorId)
    {
        var sha3 = new Sha3Keccack();
        var hash = sha3.CalculateHash(sensorId);
        return "0x" + hash.Substring(hash.Length - 40);
    }

    public async Task<decimal> GetBalanceAsync(string sensorId)
    {
        // If the contract is not ready, return 0 to avoid errors on the frontend
        if (!_isContractReady) return 0;

        try 
        {
            var address = GetWalletForSensor(sensorId);
            var balanceOfFunction = new BalanceOfFunction() { Owner = address };
            var queryHandler = _web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
            
            var balanceWei = await queryHandler.QueryAsync<BigInteger>(_contractAddress, balanceOfFunction);
            return Web3.Convert.FromWei(balanceWei);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Blockchain] Error reading balance for {sensorId}: {ex.Message}");
            return 0;
        }
    }

    public async Task RewardSensorAsync(string sensorId)
    {
        // STEP 1: Block if the contract does not yet exist
        if (!_isContractReady)
        {
            _logger.LogWarning($"[Blockchain Skip] Skipped reward for {sensorId} - contract is not ready yet.");
            return;
        }

        string sensorWalletAddress = GetWalletForSensor(sensorId);

        try 
        {
            await _semaphore.WaitAsync();

            // Get the current nonce from the network to avoid conflicts
            // (Nethereum does this automatically, but it's good to keep in mind under heavy load)
            var transferHandler = _web3.Eth.GetContractTransactionHandler<RewardSensorFunction>();
            var rewardFunction = new RewardSensorFunction()
            {
                To = sensorWalletAddress,
                Amount = Web3.Convert.ToWei(1) 
            };
            
            var txHash = await transferHandler.SendRequestAsync(_contractAddress, rewardFunction);
            _logger.LogInformation($"[Blockchain Reward] Sent token to {sensorId}. Tx: {txHash}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Blockchain Error] Failed to reward sensor: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    [Function("rewardSensor")]
    public class RewardSensorFunction : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public string To { get; set; } = string.Empty;

        [Parameter("uint256", "amount", 2)]
        public BigInteger Amount { get; set; }
    }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunction : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public string Owner { get; set; } = string.Empty;
    }
}