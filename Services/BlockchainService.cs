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
    // Adres deterministyczny dla Anvil (Account #0, Nonce #0)
    private readonly string _contractAddress = "0x5FbDB2315678afecb367f032d93F642f64180aa3";
    private readonly string _adminPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    
    private readonly ILogger<BlockchainService> _logger;
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    
    // Flaga blokująca transakcje do momentu wdrożenia kontraktu
    private volatile bool _isContractReady = false;

    public BlockchainService(IConfiguration configuration, ILogger<BlockchainService> logger)
    {
        _logger = logger;
        var rpcUrl = configuration["Blockchain:RpcUrl"] ?? "http://blockchain:8545";
        var account = new Account(_adminPrivateKey);
        _web3 = new Web3(account, rpcUrl);

        // Uruchom monitorowanie dostępności kontraktu w tle
        Task.Run(WaitForContractDeployment);
    }

    private async Task WaitForContractDeployment()
    {
        _logger.LogInformation("Oczekiwanie na wdrożenie Smart Contractu...");
        
        // Próbuj przez 60 sekund
        for (int i = 0; i < 30; i++)
        {
            try 
            {
                var code = await _web3.Eth.GetCode.SendRequestAsync(_contractAddress);
                if (code != "0x")
                {
                    _isContractReady = true;
                    _logger.LogInformation($"[BLOCKCHAIN READY] Kontrakt wykryty pod adresem {_contractAddress}. Odblokowywanie transakcji.");
                    return;
                }
            }
            catch
            {
                // Ignoruj błędy połączenia podczas startu kontenera
            }
            
            await Task.Delay(2000); // Sprawdzaj co 2 sekundy
        }
        
        _logger.LogError("[BLOCKCHAIN TIMEOUT] Kontrakt nie został wykryty po 60 sekundach. Sprawdź kontener blockchain.");
    }

    public string GetWalletForSensor(string sensorId)
    {
        var sha3 = new Sha3Keccack();
        var hash = sha3.CalculateHash(sensorId);
        return "0x" + hash.Substring(hash.Length - 40);
    }

    public async Task<decimal> GetBalanceAsync(string sensorId)
    {
        // Jeśli kontraktu nie ma, zwracamy 0, żeby nie sypać błędami na froncie
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
            _logger.LogWarning($"[Blockchain] Błąd odczytu salda dla {sensorId}: {ex.Message}");
            return 0;
        }
    }

    public async Task RewardSensorAsync(string sensorId)
    {
        // KROK 1: Blokada, jeśli kontrakt jeszcze nie istnieje
        if (!_isContractReady)
        {
            _logger.LogWarning($"[Blockchain Skip] Pominięto nagrodę dla {sensorId} - kontrakt jeszcze nie jest gotowy.");
            return;
        }

        string sensorWalletAddress = GetWalletForSensor(sensorId);

        try 
        {
            await _semaphore.WaitAsync();

            // Pobierz aktualny nonce z sieci, żeby uniknąć konfliktów
            // (Nethereum robi to automatycznie, ale przy dużym obciążeniu warto mieć to na uwadze)
            var transferHandler = _web3.Eth.GetContractTransactionHandler<RewardSensorFunction>();
            var rewardFunction = new RewardSensorFunction()
            {
                To = sensorWalletAddress,
                Amount = Web3.Convert.ToWei(1) 
            };
            
            var txHash = await transferHandler.SendRequestAsync(_contractAddress, rewardFunction);
            _logger.LogInformation($"[Blockchain Reward] Wysłano 1 token do {sensorId}. Tx: {txHash}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Blockchain Error] Nie udało się nagrodzić sensora: {ex.Message}");
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