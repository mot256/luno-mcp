using System.Net.Http.Headers;
using System.Text;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.Http;
using LunoMcpServer.Audit;

namespace LunoMcpServer;

[McpServerToolType]
public class LunoTools
{
    private readonly IAuditLogger _auditLogger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LunoTools(IHttpClientFactory httpClientFactory, IAuditLogger auditLogger, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _auditLogger = auditLogger;
        _httpContextAccessor = httpContextAccessor;
    }

    private Task LogAuditAsync(string action, string details)
    {
        var (apiKeyId, _) = GetCredentials();
        return _auditLogger.LogAsync(action, apiKeyId, details);
    }

    private void ValidateNotEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Parameter '{paramName}' must not be empty.");
    }

    private (string apiKeyId, string apiKeySecret) GetCredentials()
    {
        var apiKeyId = Environment.GetEnvironmentVariable("LUNO_API_KEY_ID");
        var apiKeySecret = Environment.GetEnvironmentVariable("LUNO_API_KEY_SECRET");
        if (string.IsNullOrWhiteSpace(apiKeyId) || string.IsNullOrWhiteSpace(apiKeySecret))
            throw new InvalidOperationException("Luno API credentials are not set in environment variables.");
        return (apiKeyId, apiKeySecret);
    }

    private HttpClient CreateLunoClient()
    {
        var (apiKeyId, apiKeySecret) = GetCredentials();
        var client = _httpClientFactory.CreateClient();
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKeyId}:{apiKeySecret}"));
        client.BaseAddress = new Uri("https://api.luno.com/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        return client;
    }

    [McpServerTool, Description("Get account balances. Equivalent to Luno GET /api/1/balance. No parameters. Returns all wallet balances for the authenticated user, including available, reserved, and total per currency. Typical errors: authentication failure.")]
    public async Task<string> GetBalances(System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync("api/1/balance", cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return json;
    }

    [McpServerTool, Description("Get ticker for a currency pair. Equivalent to Luno GET /api/1/ticker?pair={pair}. Parameter: 'pair' (string, required, e.g. 'XBTZAR'). Returns latest price, bid, ask, and volume for the specified currency pair. Typical errors: invalid pair code.")]
    public async Task<string> GetTicker(string pair, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync($"api/1/ticker?pair={pair}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return json;
    }

    [McpServerTool, Description("Place a market order (requires confirmation). Equivalent to Luno POST /api/1/marketorder. Parameters: 'pair' (string, required, market code), 'type' (string, required, BUY or SELL), 'volume' (string, required, amount to buy/sell), 'confirm' (bool, required). Returns order result or error. Typical errors: insufficient funds, invalid parameters.")]
    public async Task<string> PlaceMarketOrder(string pair, string type, string volume, bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(pair, nameof(pair));
        ValidateNotEmpty(type, nameof(type));
        ValidateNotEmpty(volume, nameof(volume));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will place a {type} market order for {volume} on {pair}. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("PlaceMarketOrder", $"pair={pair}, type={type}, volume={volume}");
        var client = CreateLunoClient();
        var content = new StringContent($"pair={pair}&type={type}&{(type == "BUY" ? "counter_volume" : "base_volume")}={volume}", Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync("api/1/marketorder", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return json;
    }

    // --- Accounts ---
    [McpServerTool, Description("Create account (requires confirmation). Equivalent to Luno POST /api/1/accounts. Parameters: 'currency' (string, required, e.g. 'ZAR'), 'name' (string, required, account label), 'confirm' (bool, required). Returns new account details. Typical errors: unsupported currency, missing parameters.")]
    public async Task<string> CreateAccount(string currency, string name, bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(currency, nameof(currency));
        ValidateNotEmpty(name, nameof(name));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will create an account for {currency} named '{name}'. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("CreateAccount", $"currency={currency}, name={name}");
        var client = CreateLunoClient();
        var content = new StringContent($"currency={currency}&name={name}", Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync("api/1/accounts", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Update account name (requires confirmation). Equivalent to Luno PUT /api/1/accounts/{accountId}/name. Parameters: 'accountId' (string, required), 'name' (string, required, new label), 'confirm' (bool, required). Typical errors: account not found, invalid name.")]
    public async Task<string> UpdateAccountName(string accountId, string name, bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(accountId, nameof(accountId));
        ValidateNotEmpty(name, nameof(name));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will update account {accountId} name to '{name}'. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("UpdateAccountName", $"accountId={accountId}, name={name}");
        var client = CreateLunoClient();
        var content = new StringContent($"name={name}", Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PutAsync($"api/1/accounts/{accountId}/name", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("List pending transactions. Equivalent to Luno GET /api/1/accounts/{accountId}/pending. Parameter: 'accountId' (string, required). Returns all unconfirmed transactions for the specified account. Typical errors: account not found.")]
    public async Task<string> ListPendingTransactions(string accountId, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync($"api/1/accounts/{accountId}/pending", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("List account transactions. Equivalent to Luno GET /api/1/accounts/{accountId}/transactions. Parameters: 'accountId' (string, required), 'minRow' (int, required), 'maxRow' (int, required). Returns transaction history for the account. Typical errors: account not found, invalid row range.")]
    public async Task<string> ListTransactions(string accountId, int minRow, int maxRow, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync($"api/1/accounts/{accountId}/transactions?min_row={minRow}&max_row={maxRow}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    // --- Market ---
    [McpServerTool, Description("Get full order book. Equivalent to Luno GET /api/1/orderbook?pair={pair}. Parameter: 'pair' (string, required). Returns all bids and asks for the specified market. Typical errors: invalid pair code.")]
    public async Task<string> GetOrderBook(string pair, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync($"api/1/orderbook?pair={pair}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Get top order book. Equivalent to Luno GET /api/1/orderbook_top?pair={pair}. Parameter: 'pair' (string, required). Returns best bid and ask for the specified market. Typical errors: invalid pair code.")]
    public async Task<string> GetOrderBookTop(string pair, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync($"api/1/orderbook_top?pair={pair}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Get tickers for all pairs. Equivalent to Luno GET /api/1/tickers. No parameters. Returns latest price info for all supported markets. Typical errors: authentication failure.")]
    public async Task<string> GetTickers(System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync("api/1/tickers", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("List recent trades. Equivalent to Luno GET /api/1/trades?pair={pair}. Parameters: 'pair' (string, required), 'since' (long, optional, timestamp). Returns recent trades for the market. Typical errors: invalid pair code.")]
    public async Task<string> ListTrades(string pair, long? since = null, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var url = $"api/1/trades?pair={pair}" + (since.HasValue ? $"&since={since.Value}" : "");
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Get candles. Equivalent to Luno GET /api/exchange/1/candles. Parameters: 'pair' (string, required), 'since' (long, required, timestamp), 'duration' (long, required, ms). Returns OHLCV candlestick data. Typical errors: invalid parameters.")]
    public async Task<string> GetCandles(string pair, long since, long duration, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync($"api/exchange/1/candles?pair={pair}&since={since}&duration={duration}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Get markets info. Equivalent to Luno GET /api/exchange/1/markets. No parameters. Returns metadata for all supported trading pairs. Typical errors: authentication failure.")]
    public async Task<string> GetMarkets(System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync("api/exchange/1/markets", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    // --- Orders ---
    [McpServerTool, Description("List orders. Equivalent to Luno GET /api/1/listorders. Parameters: 'pair' (string, optional), 'state' (string, optional), 'limit' (int, optional). Returns open and recent orders for the user. Typical errors: authentication failure.")]
    public async Task<string> ListOrders(string? pair = null, string? state = null, int? limit = null, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var url = "api/1/listorders";
        var query = "";
        if (!string.IsNullOrEmpty(pair)) query += $"pair={pair}&";
        if (!string.IsNullOrEmpty(state)) query += $"state={state}&";
        if (limit.HasValue) query += $"limit={limit.Value}&";
        if (!string.IsNullOrEmpty(query)) url += $"?{query.TrimEnd('&')}";

        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Get order by ID. Equivalent to Luno GET /api/1/orders/{orderId}. Parameter: 'orderId' (string, required). Returns details for the specified order. Typical errors: order not found.")]
    public async Task<string> GetOrder(string orderId, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync($"api/1/orders/{orderId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Place limit order (requires confirmation). Equivalent to Luno POST /api/1/postorder. Parameters: 'pair' (string, required), 'type' (string, required, BUY or SELL), 'volume' (string, required), 'price' (string, required), 'timeInForce' (string, optional, default 'GTC'), 'confirm' (bool, required). Returns order result. Typical errors: insufficient funds, invalid parameters.")]
    public async Task<string> PlaceLimitOrder(string pair, string type, string volume, string price, string timeInForce = "GTC", bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(pair, nameof(pair));
        ValidateNotEmpty(type, nameof(type));
        ValidateNotEmpty(volume, nameof(volume));
        ValidateNotEmpty(price, nameof(price));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will place a {type} limit order for {volume} {pair} at price {price}. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("PlaceLimitOrder", $"pair={pair}, type={type}, volume={volume}, price={price}, timeInForce={timeInForce}");
        var client = CreateLunoClient();
        var content = new StringContent($"pair={pair}&type={type}&volume={volume}&price={price}&time_in_force={timeInForce}", Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync("api/1/postorder", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Cancel order (requires confirmation). Equivalent to Luno POST /api/1/stoporder. Parameter: 'orderId' (string, required), 'confirm' (bool, required). Cancels the specified order. Typical errors: order not found, already cancelled.")]
    public async Task<string> CancelOrder(string orderId, bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(orderId, nameof(orderId));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will cancel order {orderId}. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("CancelOrder", $"orderId={orderId}");
        var client = CreateLunoClient();
        var content = new StringContent($"order_id={orderId}", Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync("api/1/stoporder", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    // --- Send/Receive ---
    [McpServerTool, Description("Send assets (requires confirmation). Equivalent to Luno POST /api/1/send. Parameters: 'currency' (string, required), 'address' (string, required), 'amount' (string, required), 'description' (string, optional), 'confirm' (bool, required). Sends assets to the specified address. Typical errors: insufficient funds, invalid address.")]
    public async Task<string> Send(string currency, string address, string amount, string? description = null, bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(currency, nameof(currency));
        ValidateNotEmpty(address, nameof(address));
        ValidateNotEmpty(amount, nameof(amount));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will send {amount} {currency} to {address}. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("Send", $"currency={currency}, address={address}, amount={amount}, description={description}");
        var client = CreateLunoClient();
        var body = $"currency={currency}&address={address}&amount={amount}" + (description != null ? $"&description={description}" : "");
        var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync("api/1/send", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Get receive address. Equivalent to Luno GET /api/1/funding_address?asset={asset}. Parameter: 'asset' (string, required). Returns deposit address for the specified asset. Typical errors: unsupported asset.")]
    public async Task<string> GetFundingAddress(string asset, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync($"api/1/funding_address?asset={asset}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Create receive address (requires confirmation). Equivalent to Luno POST /api/1/funding_address. Parameters: 'asset' (string, required), 'name' (string, optional), 'confirm' (bool, required). Creates a new deposit address. Typical errors: unsupported asset, missing parameters.")]
    public async Task<string> CreateFundingAddress(string asset, string? name = null, bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(asset, nameof(asset));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will create a receive address for {asset}{(name != null ? $" named '{name}'" : "")}. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("CreateFundingAddress", $"asset={asset}, name={name}");
        var client = CreateLunoClient();
        var body = $"asset={asset}" + (name != null ? $"&name={name}" : "");
        var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync("api/1/funding_address", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Validate address (requires confirmation). Equivalent to Luno POST /api/1/address/validate. Parameters: 'address' (string, required), 'currency' (string, required), 'confirm' (bool, required). Validates the address for the currency. Typical errors: invalid address, unsupported currency.")]
    public async Task<string> ValidateAddress(string address, string currency, bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(address, nameof(address));
        ValidateNotEmpty(currency, nameof(currency));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will validate address {address} for currency {currency}. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("ValidateAddress", $"address={address}, currency={currency}");
        var client = CreateLunoClient();
        var body = $"address={address}&currency={currency}";
        var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync("api/1/address/validate", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    // --- Transfers/Withdrawals ---
    [McpServerTool, Description("List withdrawals. Equivalent to Luno GET /api/1/withdrawals. Parameter: 'limit' (int, optional). Returns withdrawal history for the user. Typical errors: authentication failure.")]
    public async Task<string> ListWithdrawals(int? limit = null, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var url = "api/1/withdrawals" + (limit.HasValue ? $"?limit={limit.Value}" : "");
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Create withdrawal (requires confirmation). Equivalent to Luno POST /api/1/withdrawals. Parameters: 'type' (string, required), 'amount' (string, required), 'beneficiaryId' (string, optional), 'confirm' (bool, required). Initiates a withdrawal. Typical errors: insufficient funds, invalid beneficiary.")]
    public async Task<string> CreateWithdrawal(string type, string amount, string? beneficiaryId = null, bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(type, nameof(type));
        ValidateNotEmpty(amount, nameof(amount));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will withdraw {amount} via {type}. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("CreateWithdrawal", $"type={type}, amount={amount}, beneficiaryId={beneficiaryId}");
        var client = CreateLunoClient();
        var body = $"type={type}&amount={amount}" + (beneficiaryId != null ? $"&beneficiary_id={beneficiaryId}" : "");
        var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync("api/1/withdrawals", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Get withdrawal by ID. Equivalent to Luno GET /api/1/withdrawals/{withdrawalId}. Parameter: 'withdrawalId' (string, required). Returns details for the specified withdrawal. Typical errors: withdrawal not found.")]
    public async Task<string> GetWithdrawal(string withdrawalId, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync($"api/1/withdrawals/{withdrawalId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Cancel withdrawal. Equivalent to Luno DELETE /api/1/withdrawals/{withdrawalId}. Parameter: 'withdrawalId' (string, required). Cancels the specified withdrawal request. Typical errors: withdrawal not found, already cancelled.")]
    public async Task<string> CancelWithdrawal(string withdrawalId, System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.DeleteAsync($"api/1/withdrawals/{withdrawalId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    // --- Beneficiaries ---
    [McpServerTool, Description("List beneficiaries. Equivalent to Luno GET /api/1/beneficiaries. No parameters. Returns all beneficiaries linked to the user. Typical errors: authentication failure.")]
    public async Task<string> ListBeneficiaries(System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync("api/1/beneficiaries", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Create beneficiary (requires confirmation). Equivalent to Luno POST /api/1/beneficiaries. Parameters: 'bankName' (string, required, e.g. 'FNB'), 'bankAccountNumber' (string, required), 'accountType' (string, required, e.g. 'CURRENT'), 'bankRecipient' (string, required, recipient name), 'confirm' (bool, required). Adds a new beneficiary. Returns beneficiary details on success. Typical errors: invalid account number, unsupported bank, missing parameters.")]
    public async Task<string> CreateBeneficiary(string bankName, string bankAccountNumber, string accountType, string bankRecipient, bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(bankName, nameof(bankName));
        ValidateNotEmpty(bankAccountNumber, nameof(bankAccountNumber));
        ValidateNotEmpty(accountType, nameof(accountType));
        ValidateNotEmpty(bankRecipient, nameof(bankRecipient));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will create a beneficiary for {bankRecipient} at {bankName}. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("CreateBeneficiary", $"bankName={bankName}, bankAccountNumber={bankAccountNumber}, accountType={accountType}, bankRecipient={bankRecipient}");
        var client = CreateLunoClient();
        var body = $"bank_name={bankName}&bank_account_number={bankAccountNumber}&account_type={accountType}&bank_recipient={bankRecipient}";
        var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync("api/1/beneficiaries", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool, Description("Delete beneficiary (requires confirmation). Equivalent to Luno DELETE /api/1/beneficiaries/{beneficiaryId}. Parameter: 'beneficiaryId' (string, required, Luno beneficiary ID), 'confirm' (bool, required). Removes the specified beneficiary. Returns success or error if beneficiary not found or already deleted.")]
    public async Task<string> DeleteBeneficiary(string beneficiaryId, bool confirm = false, System.Threading.CancellationToken cancellationToken = default)
    {
        ValidateNotEmpty(beneficiaryId, nameof(beneficiaryId));
        if (!confirm)
        {
            return $"CONFIRMATION REQUIRED: This will delete beneficiary {beneficiaryId}. Please call again with confirm=true to proceed.";
        }
        await LogAuditAsync("DeleteBeneficiary", $"beneficiaryId={beneficiaryId}");
        var client = CreateLunoClient();
        var response = await client.DeleteAsync($"api/1/beneficiaries/{beneficiaryId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    // --- Users ---
    [McpServerTool, Description("List linked users. Equivalent to Luno GET /api/1/users/linked. No parameters. Returns all users linked to the authenticated account, including user IDs and relationship info. Typical errors: authentication failure.")]
    public async Task<string> ListLinkedUsers(System.Threading.CancellationToken cancellationToken = default)
    {
        var client = CreateLunoClient();
        var response = await client.GetAsync("api/1/users/linked", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    // --- Streaming API (stubs) ---
    [McpServerTool, Description("Connect to market stream (WebSocket, stub)")]
    public Task<string> ConnectMarketStream(string apiKeyId, string apiKeySecret, string pair)
    {
        // Stub: Implement WebSocket client if needed
        return Task.FromResult("Not implemented: Use wss://ws.luno.com/api/1/stream/" + pair);
    }

    [McpServerTool, Description("Connect to user stream (WebSocket, stub)")]
    public Task<string> ConnectUserStream(string apiKeyId, string apiKeySecret)
    {
        // Stub: Implement WebSocket client if needed
        return Task.FromResult("Not implemented: Use wss://ws.luno.com/api/1/userstream");
    }
}
