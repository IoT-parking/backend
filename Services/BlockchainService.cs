using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Microsoft.Extensions.Options;
using System.Numerics;
using IoTParking.Blockchain;
using IoTParking.Blockchain.SensorToken;
using IoTParking.Blockchain.SensorToken.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;

namespace backend.Services;

public class BlockchainService
{
    private readonly ILogger<BlockchainService> _logger;
    private readonly IConfiguration _config;
    private Web3? _web3;
    private SensorTokenService? _tokenService;
    private Account? _account;
    
    private readonly Dictionary<string, string> _sensorWallets = new()
    {
        { "co_sensor_1", "0x70997970C51812dc3A010C7d01b50e0d17dc79C8" },
        { "co_sensor_2", "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC" },
        { "co_sensor_3", "0x90F79bf6EB2c4f870365E785982E1f101E93b906" },
        { "co_sensor_4", "0x15d34AAf54267DB7D7c367839AAf71A00a2C6A65" },
        { "energy_sensor_1", "0x9965507D1a55bcC2695C58ba16FB37d819B0A4dc" },
        { "energy_sensor_2", "0x976EA74026E726554dB657fA54763abd0C5a0aa9" },
        { "energy_sensor_3", "0x14dC79964da2C08b23698B3D3cc7Ca32193d9955" },
        { "energy_sensor_4", "0x23618e81E3f5cdF7f54C3d65f7FBc0aBf5B21E8f" },
        { "occ_sensor_1", "0xa0Ee7A142d267C1f36714E4a8F75612F20a79720" },
        { "occ_sensor_2", "0xBcd4042DE499D14e559a7136633C60945BC62401" },
        { "occ_sensor_3", "0x71C7656EC7ab88b098defB751B7401B5f6d8976F" },
        { "occ_sensor_4", "0xf4191cC91A07E474261765B620108A447d928236" },
        { "temp_sensor_1", "0x6335123A13941A58908332c86E128036270E40F5" },
        { "temp_sensor_2", "0x89205A3A3b2A69De6Dbf7f01ED13B2108B2c43e7" },
        { "temp_sensor_3", "0xA43917F40502a50742C240122718E275a5d7c356" },
        { "temp_sensor_4", "0x2509121772647754323214732165184128521192" }
    };

    public BlockchainService(ILogger<BlockchainService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var rpcUrl = _config["Blockchain:RpcUrl"] ?? "http://blockchain:8545";
            // Default key for Anvil/Foundry account 0
            var privateKey = _config["Blockchain:AdminPrivateKey"] ?? "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"; 
            var contractAddress = _config["Blockchain:ContractAddress"];
            var chainId = int.Parse(_config["Blockchain:ChainId"] ?? "31337");

            _account = new Account(privateKey, chainId);
            _web3 = new Web3(_account, rpcUrl);

            // Check if we need to deploy
            if (string.IsNullOrEmpty(contractAddress))
            {
                _logger.LogInformation("[BLOCKCHAIN] No contract address configured. Deploying new contract...");
                var deployment = new SensorTokenDeployment
                {
                    InitialOwner = _account.Address
                };
                
                var receipt = await SensorTokenService.DeployContractAndWaitForReceiptAsync(_web3, deployment);
                contractAddress = receipt.ContractAddress;
                _logger.LogInformation($"[BLOCKCHAIN] Contract deployed at: {contractAddress}");
            }

            _tokenService = new SensorTokenService(_web3, contractAddress);
            _logger.LogInformation($"[BLOCKCHAIN] Initialized for contract: {contractAddress}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BLOCKCHAIN] Error initializing the service");
        }
    }

    public string? GetWalletForSensor(string sensorInstanceId)
    {
        return _sensorWallets.ContainsKey(sensorInstanceId) 
            ? _sensorWallets[sensorInstanceId] 
            : "0x000000000000000000000000000000000000dEaD"; 
    }

    public async Task RewardSensorAsync(string sensorInstanceId)
    {
        if (_tokenService == null) 
        {
            _logger.LogWarning("[BLOCKCHAIN] Service not initialized. Cannot reward sensor.");
            return;
        }

        var walletAddress = GetWalletForSensor(sensorInstanceId);
        
        if (walletAddress.EndsWith("dEaD"))
        {
            _logger.LogWarning($"[BLOCKCHAIN] NO WALLET for sensor: '{sensorInstanceId}'. Check the _sensorWallets dictionary!");
            return;
        }

        try
        {
            var rewardAmount = Web3.Convert.ToWei(1); 
            _logger.LogInformation($"[BLOCKCHAIN] Attempting to send 1 token to {sensorInstanceId} ({walletAddress})...");

            var receipt = await _tokenService.RewardSensorRequestAndWaitForReceiptAsync(walletAddress, rewardAmount);

            if (receipt.Status.Value == 1)
            {
                _logger.LogInformation($"[BLOCKCHAIN] SUCCESS! Tokens delivered. Block: {receipt.BlockNumber}");
            }
            else
            {
                _logger.LogError($"[BLOCKCHAIN] FAILURE! Transaction reverted. Tx: {receipt.TransactionHash}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[BLOCKCHAIN] Critical error while sending to {sensorInstanceId}");
        }
    }

    public async Task<decimal> GetBalanceAsync(string sensorInstanceId)
    {
        if (_tokenService == null) return 0;

        var walletAddress = GetWalletForSensor(sensorInstanceId);

        try
        {
            var balanceWei = await _tokenService.BalanceOfQueryAsync(walletAddress);
            return Web3.Convert.FromWei(balanceWei);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[BLOCKCHAIN] Error while fetching balance for {sensorInstanceId}");
            return 0;
        }
    }

    public async Task<Dictionary<string, decimal>> GetAllBalancesAsync()
    {
        var balances = new Dictionary<string, decimal>();
        foreach (var sensorId in _sensorWallets.Keys)
        {
            balances[sensorId] = await GetBalanceAsync(sensorId);
        }
        return balances;
    }
}
