using System;
using System.Collections.Concurrent;
using System.Buffers;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using Serilog;

namespace Forge
{
    // C# representation of a pricing rule
    [JsonDerivedType(typeof(BasePriceRuleDto))]
    [JsonDerivedType(typeof(PercentageAdjustmentRuleDto))]
    [JsonDerivedType(typeof(FlatAdjustmentRuleDto))]
    [JsonDerivedType(typeof(PriceCapRuleDto))]
    [JsonDerivedType(typeof(TieredPricingRuleDto))]
    [JsonDerivedType(typeof(RoundingRuleDto))]
    public abstract class PricingRuleDto
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; } = true;

        protected PricingRuleDto(string type, string name, bool enabled = true)
        {
            Type = type;
            Name = name;
            Enabled = enabled;
        }
    }

    public class BasePriceRuleDto : PricingRuleDto
    {
        public double DefaultPrice { get; set; }
        public string ContextKey { get; set; }

        public BasePriceRuleDto(string name, double defaultPrice, string contextKey = "base_price", bool enabled = true) 
            : base("BasePrice", name, enabled)
        {
            DefaultPrice = defaultPrice;
            ContextKey = contextKey;
        }
    }

    public class PercentageAdjustmentRuleDto : PricingRuleDto
    {
        public double Factor { get; set; }
        public string ConditionKey { get; set; }
        public string Description { get; set; }

        public PercentageAdjustmentRuleDto(string name, double factor, string conditionKey, string description, bool enabled = true) 
            : base("PercentageAdjustment", name, enabled)
        {
            Factor = factor;
            ConditionKey = conditionKey;
            Description = description;
        }
    }

    public class FlatAdjustmentRuleDto : PricingRuleDto
    {
        public double Amount { get; set; }
        public string ConditionKey { get; set; }
        public string Description { get; set; }

        public FlatAdjustmentRuleDto(string name, double amount, string conditionKey, string description, bool enabled = true) 
            : base("FlatAdjustment", name, enabled)
        {
            Amount = amount;
            ConditionKey = conditionKey;
            Description = description;
        }
    }

    public class PriceCapRuleDto : PricingRuleDto
    {
        public double? MinPrice { get; set; }
        public double? MaxPrice { get; set; }

        public PriceCapRuleDto(string name, double? minPrice = null, double? maxPrice = null, bool enabled = true) 
            : base("PriceCap", name, enabled)
        {
            MinPrice = minPrice;
            MaxPrice = maxPrice;
        }
    }

    public class TieredPricingRuleDto : PricingRuleDto
    {
        public string QuantityKey { get; set; }
        public bool Graduated { get; set; } = false;
        public List<TierDto> Tiers { get; set; } = new();

        public TieredPricingRuleDto(string name, string quantityKey, bool graduated = false, bool enabled = true) 
            : base("TieredPricing", name, enabled)
        {
            QuantityKey = quantityKey;
            Graduated = graduated;
        }
    }

    public class RoundingRuleDto : PricingRuleDto
    {
        public string RoundingMode { get; set; }

        public RoundingRuleDto(string name, string roundingMode, bool enabled = true) 
            : base("Rounding", name, enabled)
        {
            RoundingMode = roundingMode;
        }
    }

    public class TierDto
    {
        public double MinQuantity { get; set; }
        public double DiscountPercentage { get; set; }
    }

    // C# representation of the full blueprint
    public class PriceBlueprintDto
    {
        public string Version { get; set; } = "1.0.0";
        public string BlueprintName { get; set; }
        public List<PricingRuleDto> Rules { get; set; } = new();

        public PriceBlueprintDto(string name, string version = "1.0.0")
        {
            BlueprintName = name;
            Version = version;
        }
    }

    // Fluent Builder for assembling the pricing blueprint
    public class BlueprintBuilder
    {
        private readonly PriceBlueprintDto _blueprint;

        public BlueprintBuilder(string name, string version = "1.0.0")
        {
            _blueprint = new PriceBlueprintDto(name, version);
        }

        public BlueprintBuilder SetBasePrice(string ruleName, double defaultPrice, string contextKey = "base_price", bool enabled = true)
        {
            _blueprint.Rules.Add(new BasePriceRuleDto(ruleName, defaultPrice, contextKey, enabled));
            return this;
        }

        public BlueprintBuilder AddPercentageAdjustment(string ruleName, double factor, string conditionKey, string description, bool enabled = true)
        {
            _blueprint.Rules.Add(new PercentageAdjustmentRuleDto(ruleName, factor, conditionKey, description, enabled));
            return this;
        }

        public BlueprintBuilder AddFlatAdjustment(string ruleName, double amount, string conditionKey, string description, bool enabled = true)
        {
            _blueprint.Rules.Add(new FlatAdjustmentRuleDto(ruleName, amount, conditionKey, description, enabled));
            return this;
        }

        public BlueprintBuilder AddPriceCap(string ruleName, double? minPrice = null, double? maxPrice = null, bool enabled = true)
        {
            _blueprint.Rules.Add(new PriceCapRuleDto(ruleName, minPrice, maxPrice, enabled));
            return this;
        }

        public BlueprintBuilder AddVolumeTiers(string ruleName, string quantityKey, params (double minQty, double discountPct)[] tiers)
        {
            return AddVolumeTiers(ruleName, quantityKey, false, true, tiers);
        }

        public BlueprintBuilder AddVolumeTiers(string ruleName, string quantityKey, bool enabled, params (double minQty, double discountPct)[] tiers)
        {
            return AddVolumeTiers(ruleName, quantityKey, false, enabled, tiers);
        }

        public BlueprintBuilder AddVolumeTiers(string ruleName, string quantityKey, bool graduated, bool enabled, params (double minQty, double discountPct)[] tiers)
        {
            var rule = new TieredPricingRuleDto(ruleName, quantityKey, graduated, enabled);
            foreach (var (minQty, discountPct) in tiers)
            {
                rule.Tiers.Add(new TierDto { MinQuantity = minQty, DiscountPercentage = discountPct });
            }
            _blueprint.Rules.Add(rule);
            return this;
        }

        public BlueprintBuilder AddRounding(string ruleName, string roundingMode, bool enabled = true)
        {
            _blueprint.Rules.Add(new RoundingRuleDto(ruleName, roundingMode, enabled));
            return this;
        }

        public PriceBlueprintDto Build()
        {
            return _blueprint;
        }
    }

    public class Program
    {
        private static readonly ConcurrentDictionary<string, byte[]> _staticFileCache = new();
        private const long MaxCacheableFileSize = 32 * 1024; // 32 KB

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(GetSolutionRoot(), "logs", "forge-.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("=================================================");
                Log.Information("   Forge: Pricing Operating System Assembler     ");
                Log.Information("=================================================");

                bool runWeb = false;
                int port = 5000;

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--web" || args[i] == "-w")
                    {
                        runWeb = true;
                    }
                    else if ((args[i] == "--port" || args[i] == "-p") && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[i + 1], out int p))
                        {
                            port = p;
                            i++;
                        }
                    }
                }

                string solutionRoot = GetSolutionRoot();

                if (runWeb)
                {
                    string wwwrootPath = Path.Combine(solutionRoot, "Forge", "wwwroot");
                    if (!Directory.Exists(wwwrootPath))
                    {
                        wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
                    }
                    StartWebServer(wwwrootPath, solutionRoot, port);
                }
                else
                {
                    // Assemble a pricing blueprint using the Fluent Builder
                    var blueprint = new BlueprintBuilder("Enterprise Custom Contract")
                        .SetBasePrice("Base Configuration Rate", 250.0)
                        .AddVolumeTiers("Volume License Tiers", "quantity", 
                            (10, 0.05),   // 5% off at 10 units
                            (50, 0.10),   // 10% off at 50 units
                            (100, 0.15),  // 15% off at 100 units
                            (500, 0.25)   // 25% off at 500 units
                        )
                        .AddPercentageAdjustment("Partner Discount", 0.90, "is_partner", "Partner Channel 10% discount")
                        .AddFlatAdjustment("Shipping & Handling Surcharge", 15.0, "quantity < 10", "Flat rate standard shipping fee for small orders")
                        .AddPercentageAdjustment("International Tax Surcharge", 1.15, "region != US", "International regional sales tax of 15%")
                        .AddPriceCap("Contract Price Floor Cap", minPrice: 130.0)
                        .Build();

                    // Configure JSON options for serialization
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    string jsonOutput = JsonSerializer.Serialize(blueprint, options);

                    Log.Information("Generated Blueprint JSON Specification:\n{JsonOutput}", jsonOutput);

                    Log.Information("Writing specification blueprint to disk...");
                    string path = Path.Combine(solutionRoot, "enterprise_blueprint.json");
                    File.WriteAllText(path, jsonOutput);
                    Log.Information("Successfully saved to: {Path}", path);
                    Log.Information("Forge successfully assembled the final pricing blueprint.");
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void StartWebServer(string wwwrootPath, string solutionRoot, int port)
        {
            var listener = new HttpListener();
            string url = "";
            bool bound = false;
            int maxAttempts = 100;
            int attempt = 0;

            while (!bound && attempt < maxAttempts)
            {
                int currentPort = port + attempt;
                url = $"http://localhost:{currentPort}/";
                
                listener.Prefixes.Clear();
                listener.Prefixes.Add(url);

                try
                {
                    listener.Start();
                    bound = true;
                    if (attempt > 0)
                    {
                        Log.Warning("Requested port {RequestedPort} was occupied. Automatically selected available port {AvailablePort}.", port, currentPort);
                    }
                }
                catch (HttpListenerException ex)
                {
                    Log.Debug(ex, "Port {Port} occupied. Retrying next port...", currentPort);
                    attempt++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to start web server on {Url}. Aborting.", url);
                    return;
                }
            }

            if (!bound)
            {
                Log.Error("Could not find any available port starting from {RequestedPort} after {Attempts} attempts.", port, maxAttempts);
                return;
            }

            Log.Information("--> Blueprint Studio server running on: {Url}", url);
            Log.Information("    Serving web files from: {WwwrootPath}", wwwrootPath);
            Log.Information("    Reading/writing to: {BlueprintPath}", Path.Combine(solutionRoot, "enterprise_blueprint.json"));
            Log.Information("    Press [Enter] or Ctrl+C in this terminal to shut down the server.");

            // Launch browser asynchronously
            Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(500);
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not open browser automatically");
                }
            });

            var cts = new CancellationTokenSource();
            var serverTask = Task.Run(() => HandleRequests(listener, wwwrootPath, solutionRoot, cts.Token));

            Console.ReadLine();
            Log.Information("Shutting down Blueprint Studio server...");
            cts.Cancel();
            listener.Stop();
            try { serverTask.Wait(); } catch { }
            Log.Information("Server stopped.");
        }

        private static async Task HandleRequests(HttpListener listener, string wwwrootPath, string solutionRoot, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    if (token.IsCancellationRequested) break;
                    
                    _ = ProcessRequestAsync(context, wwwrootPath, solutionRoot);
                }
                catch (Exception)
                {
                    if (token.IsCancellationRequested) break;
                }
            }
        }

        private static async Task ProcessRequestAsync(HttpListenerContext context, string wwwrootPath, string solutionRoot)
        {
            var request = context.Request;
            var response = context.Response;
            
            // CORS & CSRF Protection: Validate Origin header if present to block malicious websites from calling our local API
            string? origin = request.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin))
            {
                bool isLocalOrigin = origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) || 
                                     origin.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase) || 
                                     origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase) ||
                                     origin.StartsWith("https://127.0.0.1:", StringComparison.OrdinalIgnoreCase);
                
                if (!isLocalOrigin)
                {
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                    byte[] accessDenied = Encoding.UTF8.GetBytes("{\"error\": \"Forbidden: Cross-Origin request blocked. Local-only server access restriction.\"}");
                    response.ContentType = "application/json";
                    response.ContentLength64 = accessDenied.Length;
                    await response.OutputStream.WriteAsync(accessDenied, 0, accessDenied.Length);
                    response.Close();
                    return;
                }
                
                response.Headers.Add("Access-Control-Allow-Origin", origin);
            }
            
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                return;
            }

            try
            {
                string rawPath = request.Url?.AbsolutePath ?? "/index.html";
                if (rawPath == "/" || string.IsNullOrEmpty(rawPath))
                {
                    rawPath = "/index.html";
                }

                if (rawPath.Equals("/api/blueprint", StringComparison.OrdinalIgnoreCase) && request.HttpMethod == "GET")
                {
                    string filePath = Path.Combine(solutionRoot, "enterprise_blueprint.json");
                    if (File.Exists(filePath))
                    {
                        response.ContentType = "application/json";

                        // Compress if client supports it
                        string acceptEncoding = request.Headers["Accept-Encoding"] ?? "";
                        Stream responseStream = response.OutputStream;
                        bool isCompressed = false;

                        if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                        {
                            response.Headers.Add("Content-Encoding", "gzip");
                            responseStream = new GZipStream(response.OutputStream, CompressionMode.Compress, leaveOpen: true);
                            isCompressed = true;
                        }
                        else if (acceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase))
                        {
                            response.Headers.Add("Content-Encoding", "deflate");
                            responseStream = new DeflateStream(response.OutputStream, CompressionMode.Compress, leaveOpen: true);
                            isCompressed = true;
                        }

                        try
                        {
                            response.SendChunked = true;
                            using (var fileStream = File.OpenRead(filePath))
                            {
                                await StreamWithBufferPoolAsync(fileStream, responseStream);
                            }
                        }
                        finally
                        {
                            if (isCompressed)
                            {
                                await responseStream.DisposeAsync();
                            }
                        }
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        byte[] error = Encoding.UTF8.GetBytes("{\"error\": \"Blueprint file not found on disk.\"}");
                        response.ContentType = "application/json";
                        response.ContentLength64 = error.Length;
                        await response.OutputStream.WriteAsync(error, 0, error.Length);
                    }
                    response.Close();
                    return;
                }

                if (rawPath.Equals("/api/blueprint", StringComparison.OrdinalIgnoreCase) && request.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                    {
                        string body = await reader.ReadToEndAsync();
                        try
                        {
                            using (var doc = JsonDocument.Parse(body))
                            {
                                string filePath = Path.Combine(solutionRoot, "enterprise_blueprint.json");
                                await File.WriteAllTextAsync(filePath, body);
                                
                                response.StatusCode = (int)HttpStatusCode.OK;
                                byte[] success = Encoding.UTF8.GetBytes("{\"status\": \"success\", \"message\": \"Blueprint saved successfully.\"}");
                                response.ContentType = "application/json";
                                response.ContentLength64 = success.Length;
                                await response.OutputStream.WriteAsync(success, 0, success.Length);
                            }
                        }
                        catch (JsonException ex)
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                            byte[] error = Encoding.UTF8.GetBytes($"{{\"error\": \"Invalid JSON blueprint: {ex.Message}\"}}");
                            response.ContentType = "application/json";
                            response.ContentLength64 = error.Length;
                            await response.OutputStream.WriteAsync(error, 0, error.Length);
                        }
                    }
                    response.Close();
                    return;
                }

                if (rawPath.Equals("/api/simulate", StringComparison.OrdinalIgnoreCase) && request.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                    {
                        string body = await reader.ReadToEndAsync();
                        string tempScenariosPath = Path.Combine(solutionRoot, $"scenarios_{Guid.NewGuid()}.json");
                        
                        try
                        {
                            // Validate input JSON is correct
                            using (var doc = JsonDocument.Parse(body))
                            {
                                // Write scenario list to temp file
                                await File.WriteAllTextAsync(tempScenariosPath, body);
                            }

                            string blueprintPath = Path.Combine(solutionRoot, "enterprise_blueprint.json");
                            string exeFilename = "price_blueprint.exe";
                            string exePath = Path.Combine(solutionRoot, exeFilename);
                            
                            // If running on a non-Windows environment or if the exe does not exist, fall back to check standard extensions
                            if (!File.Exists(exePath))
                            {
                                // Fall back to Linux/macOS executable name if exe not found
                                if (File.Exists(Path.Combine(solutionRoot, "price_blueprint")))
                                {
                                    exePath = Path.Combine(solutionRoot, "price_blueprint");
                                }
                            }

                            if (!File.Exists(exePath))
                            {
                                throw new FileNotFoundException($"Pricing engine executable not found. Please compile it first.");
                            }

                            var startInfo = new ProcessStartInfo
                            {
                                FileName = exePath,
                                Arguments = $"\"{blueprintPath}\" \"{tempScenariosPath}\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };

                            using (var process = new Process { StartInfo = startInfo })
                            {
                                process.Start();

                                // Read streams asynchronously
                                string output = await process.StandardOutput.ReadToEndAsync();
                                string errorOutput = await process.StandardError.ReadToEndAsync();
                                
                                await process.WaitForExitAsync();

                                if (process.ExitCode != 0)
                                {
                                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    byte[] error = Encoding.UTF8.GetBytes($"{{\"error\": \"Pricing engine error: {errorOutput.Trim()}\"}}");
                                    response.ContentType = "application/json";
                                    response.ContentLength64 = error.Length;
                                    await response.OutputStream.WriteAsync(error, 0, error.Length);
                                }
                                else
                                {
                                    response.StatusCode = (int)HttpStatusCode.OK;
                                    byte[] data = Encoding.UTF8.GetBytes(output);
                                    response.ContentType = "application/json";
                                    response.ContentLength64 = data.Length;
                                    await response.OutputStream.WriteAsync(data, 0, data.Length);
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                            byte[] error = Encoding.UTF8.GetBytes($"{{\"error\": \"Invalid JSON request body: {ex.Message}\"}}");
                            response.ContentType = "application/json";
                            response.ContentLength64 = error.Length;
                            await response.OutputStream.WriteAsync(error, 0, error.Length);
                        }
                        catch (Exception ex)
                        {
                            response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            byte[] error = Encoding.UTF8.GetBytes($"{{\"error\": \"Simulation failed: {ex.Message}\"}}");
                            response.ContentType = "application/json";
                            response.ContentLength64 = error.Length;
                            await response.OutputStream.WriteAsync(error, 0, error.Length);
                        }
                        finally
                        {
                            try
                            {
                                if (File.Exists(tempScenariosPath))
                                {
                                    File.Delete(tempScenariosPath);
                                }
                            }
                            catch {}
                        }
                    }
                    response.Close();
                    return;
                }

                if (rawPath.Equals("/api/validate", StringComparison.OrdinalIgnoreCase) && request.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                    {
                        string body = await reader.ReadToEndAsync();
                        string blueprintJson = body;
                        
                        // If request body is empty, read the active file from disk
                        if (string.IsNullOrWhiteSpace(blueprintJson))
                        {
                            string activePath = Path.Combine(solutionRoot, "enterprise_blueprint.json");
                            if (File.Exists(activePath))
                            {
                                blueprintJson = await File.ReadAllTextAsync(activePath);
                            }
                            else
                            {
                                response.StatusCode = (int)HttpStatusCode.BadRequest;
                                byte[] error = Encoding.UTF8.GetBytes("{\"error\": \"No blueprint JSON provided in request body and active enterprise_blueprint.json not found on disk.\"}");
                                response.ContentType = "application/json";
                                response.ContentLength64 = error.Length;
                                await response.OutputStream.WriteAsync(error, 0, error.Length);
                                response.Close();
                                return;
                            }
                        }

                        string tempScenariosPath = Path.Combine(solutionRoot, $"scenarios_val_{Guid.NewGuid()}.json");
                        
                        try
                        {
                            // 1. Parse blueprint to extract boundaries and variables
                            var quantityBoundaries = new List<double> { 1, 5, 10, 50, 100, 500 }; // default fallbacks
                            var conditionKeys = new HashSet<string> { "is_vip", "is_partner", "is_holiday" }; // default values
                            var ruleNames = new List<string>();

                            using (var doc = JsonDocument.Parse(blueprintJson))
                            {
                                var root = doc.RootElement;
                                if (root.TryGetProperty("Rules", out var rulesElement) && rulesElement.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var rule in rulesElement.EnumerateArray())
                                    {
                                        if (rule.TryGetProperty("Name", out var nameProp))
                                        {
                                            string? ruleName = nameProp.GetString();
                                            if (ruleName != null) ruleNames.Add(ruleName);
                                        }

                                        string type = (rule.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() : null) ?? "";
                                        if (type == "TieredPricing")
                                        {
                                            if (rule.TryGetProperty("Tiers", out var tiersProp) && tiersProp.ValueKind == JsonValueKind.Array)
                                            {
                                                foreach (var tier in tiersProp.EnumerateArray())
                                                {
                                                    if (tier.TryGetProperty("MinQuantity", out var minQtyProp))
                                                    {
                                                        double mq = minQtyProp.GetDouble();
                                                        quantityBoundaries.Add(mq);
                                                        if (mq > 1) quantityBoundaries.Add(mq - 1);
                                                        quantityBoundaries.Add(mq + 1);
                                                    }
                                                }
                                            }
                                        }
                                        else if (type == "PercentageAdjustment" || type == "FlatAdjustment")
                                        {
                                            if (rule.TryGetProperty("ConditionKey", out var condProp))
                                            {
                                                string cond = condProp.GetString() ?? "";
                                                foreach (var key in new[] { "is_partner", "is_vip", "is_holiday" })
                                                {
                                                    if (cond.Contains(key)) conditionKeys.Add(key);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // Clean and sort quantity boundaries
                            var quantitiesToTest = quantityBoundaries.Distinct().Where(q => q > 0).OrderBy(q => q).ToList();

                            // 2. Generate scenario grid
                            var basePrices = new[] { 100.0, 250.0 };
                            var regions = new[] { "US", "EU" };
                            
                            var scenarios = new List<object>();

                            foreach (double bp in basePrices)
                            {
                                foreach (double qty in quantitiesToTest)
                                {
                                    foreach (string reg in regions)
                                    {
                                        // Scenario 1: Standard (all false)
                                        var ctx1 = new Dictionary<string, object>
                                        {
                                            { "base_price", bp },
                                            { "quantity", qty },
                                            { "region", reg },
                                            { "is_vip", false },
                                            { "is_partner", false },
                                            { "is_holiday", false }
                                        };
                                        scenarios.Add(new { scenario_name = $"BP_{bp}_Qty_{qty}_Reg_{reg}_Std", context = ctx1 });
                                        
                                        // Scenario 2: VIP Partner
                                        var ctx2 = new Dictionary<string, object>
                                        {
                                            { "base_price", bp },
                                            { "quantity", qty },
                                            { "region", reg },
                                            { "is_vip", true },
                                            { "is_partner", true },
                                            { "is_holiday", false }
                                        };
                                        scenarios.Add(new { scenario_name = $"BP_{bp}_Qty_{qty}_Reg_{reg}_VipPartner", context = ctx2 });

                                        // Scenario 3: Holiday Sale
                                        var ctx3 = new Dictionary<string, object>
                                        {
                                            { "base_price", bp },
                                            { "quantity", qty },
                                            { "region", reg },
                                            { "is_vip", false },
                                            { "is_partner", false },
                                            { "is_holiday", true }
                                        };
                                        scenarios.Add(new { scenario_name = $"BP_{bp}_Qty_{qty}_Reg_{reg}_Holiday", context = ctx3 });
                                    }
                                }
                            }

                            // Write generated scenarios to unique file
                            var options = new JsonSerializerOptions { WriteIndented = true };
                            string scenariosJson = JsonSerializer.Serialize(scenarios, options);
                            await File.WriteAllTextAsync(tempScenariosPath, scenariosJson);

                            // 3. Temporarily save blueprint if we parsed a custom request body
                            string originalBlueprintPath = Path.Combine(solutionRoot, "enterprise_blueprint.json");
                            string validationBlueprintPath = originalBlueprintPath;
                            bool isCustomBlueprint = !string.IsNullOrWhiteSpace(body);

                            if (isCustomBlueprint)
                            {
                                validationBlueprintPath = Path.Combine(solutionRoot, $"blueprint_val_{Guid.NewGuid()}.json");
                                await File.WriteAllTextAsync(validationBlueprintPath, blueprintJson);
                            }

                            // 4. Run Process to get simulated outputs from C++ Pricing Engine
                            string exeFilename = "price_blueprint.exe";
                            string exePath = Path.Combine(solutionRoot, exeFilename);
                            if (!File.Exists(exePath))
                            {
                                if (File.Exists(Path.Combine(solutionRoot, "price_blueprint")))
                                {
                                    exePath = Path.Combine(solutionRoot, "price_blueprint");
                                }
                            }

                            if (!File.Exists(exePath))
                            {
                                throw new FileNotFoundException("Pricing engine executable not found. Please compile it first.");
                            }

                            string output = "";
                            string errorOutput = "";

                            try
                            {
                                var startInfo = new ProcessStartInfo
                                {
                                    FileName = exePath,
                                    Arguments = $"\"{validationBlueprintPath}\" \"{tempScenariosPath}\"",
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    CreateNoWindow = true
                                };

                                using (var process = new Process { StartInfo = startInfo })
                                {
                                    process.Start();
                                    output = await process.StandardOutput.ReadToEndAsync();
                                    errorOutput = await process.StandardError.ReadToEndAsync();
                                    await process.WaitForExitAsync();
                                    
                                    if (process.ExitCode != 0)
                                    {
                                        throw new Exception($"Pricing engine execution failed: {errorOutput.Trim()}");
                                    }
                                }
                            }
                            finally
                            {
                                if (isCustomBlueprint && File.Exists(validationBlueprintPath))
                                {
                                    try { File.Delete(validationBlueprintPath); } catch {}
                                }
                            }

                            // 5. Analyze pricing output for anomalies
                            var anomalies = new List<object>();
                            var executedRules = new HashSet<string>();
                            var nonZeroRules = new HashSet<string>();

                            using (var outputDoc = JsonDocument.Parse(output))
                            {
                                var resultsArray = outputDoc.RootElement;
                                var quantityRuns = new Dictionary<string, List<(double Qty, double FinalPrice, double BasePrice, string Name)>>();

                                foreach (var result in resultsArray.EnumerateArray())
                                {
                                    string scName = (result.TryGetProperty("scenarioName", out var scNameProp) ? scNameProp.GetString() : null) ?? "";
                                    double basePrice = result.TryGetProperty("basePrice", out var bpProp) ? bpProp.GetDouble() : 0.0;
                                    double finalPrice = result.TryGetProperty("finalPrice", out var fpProp) ? fpProp.GetDouble() : 0.0;

                                    // A. Negative/Zero price check
                                    if (finalPrice <= 0.0)
                                    {
                                        anomalies.Add(new { type = "Error", rule = "Negative/Zero Price", message = $"Zero or negative price calculated: ${finalPrice:F2} (Base: ${basePrice:F2}) in scenario '{scName}'" });
                                    }
                                    // B. Extreme discount check
                                    else if (finalPrice < 0.2 * basePrice)
                                    {
                                        double pct = (finalPrice / basePrice) * 100.0;
                                        anomalies.Add(new { type = "Warning", rule = "Extreme Discount", message = $"Extreme discount: Final price ${finalPrice:F2} is only {pct:F1}% of the base price (${basePrice:F2}) in scenario '{scName}'" });
                                    }
                                    // C. Extreme markup check
                                    else if (finalPrice > 3.0 * basePrice)
                                    {
                                        double pct = (finalPrice / basePrice) * 100.0;
                                        anomalies.Add(new { type = "Warning", rule = "Extreme Markup", message = $"Extreme markup: Final price ${finalPrice:F2} is {pct:F1}% of the base price (${basePrice:F2}) in scenario '{scName}'" });
                                    }

                                    // Parse audit trail for rule redundancy
                                    if (result.TryGetProperty("auditTrail", out var auditTrailProp) && auditTrailProp.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var record in auditTrailProp.EnumerateArray())
                                        {
                                            string ruleName = (record.TryGetProperty("ruleName", out var rnProp) ? rnProp.GetString() : null) ?? "";
                                            double adjustment = record.TryGetProperty("adjustment", out var adjProp) ? adjProp.GetDouble() : 0.0;

                                            if (!string.IsNullOrEmpty(ruleName))
                                            {
                                                executedRules.Add(ruleName);
                                                if (Math.Abs(adjustment) > 0.001)
                                                {
                                                    nonZeroRules.Add(ruleName);
                                                }
                                            }
                                        }
                                    }

                                    // For volume discount inversion check, parse run grouping
                                    int lastUnderscore = scName.LastIndexOf('_');
                                    if (lastUnderscore > 0)
                                    {
                                        string mode = scName.Substring(lastUnderscore + 1);
                                        int secondLastUnderscore = scName.LastIndexOf('_', lastUnderscore - 1);
                                        int thirdLastUnderscore = scName.LastIndexOf('_', secondLastUnderscore - 1);
                                        if (thirdLastUnderscore > 0)
                                        {
                                            string region = scName.Substring(thirdLastUnderscore + 1, secondLastUnderscore - thirdLastUnderscore - 1);
                                            string runKey = $"{basePrice}_{region}_{mode}";

                                            int qtyStart = scName.IndexOf("Qty_") + 4;
                                            int qtyEnd = scName.IndexOf("_Reg");
                                            if (qtyStart > 3 && qtyEnd > qtyStart)
                                            {
                                                double quantity = double.Parse(scName.Substring(qtyStart, qtyEnd - qtyStart));
                                                
                                                if (!quantityRuns.ContainsKey(runKey))
                                                {
                                                    quantityRuns[runKey] = new List<(double, double, double, string)>();
                                                }
                                                quantityRuns[runKey].Add((quantity, finalPrice, basePrice, scName));
                                            }
                                        }
                                    }
                                }

                                // D. Run Volume Discount Inversion detection
                                foreach (var run in quantityRuns)
                                {
                                    var sortedRun = run.Value.OrderBy(r => r.Qty).ToList();
                                    for (int i = 0; i < sortedRun.Count; i++)
                                    {
                                        for (int j = i + 1; j < sortedRun.Count; j++)
                                        {
                                            double qty1 = sortedRun[i].Qty;
                                            double price1 = sortedRun[i].FinalPrice;
                                            double total1 = price1 * qty1;

                                            double qty2 = sortedRun[j].Qty;
                                            double price2 = sortedRun[j].FinalPrice;
                                            double total2 = price2 * qty2;

                                            if (qty2 > qty1 && total2 < total1)
                                            {
                                                anomalies.Add(new {
                                                    type = "Error",
                                                    rule = "Volume Inversion",
                                                    message = $"Volume Inversion: Ordering {qty2} units costs a total of ${total2:F2} (${price2:F2} each), which is cheaper than ordering {qty1} units at a total of ${total1:F2} (${price1:F2} each). Run context: {run.Key}"
                                                });
                                            }
                                        }
                                    }
                                }
                            }

                            // E. Check for inactive or redundant rules
                            foreach (var ruleName in ruleNames)
                            {
                                if (!executedRules.Contains(ruleName))
                                {
                                    anomalies.Add(new { type = "Warning", rule = "Inactive Rule", message = $"Rule '{ruleName}' was never executed in any simulated scenario. Verify if its conditional logic is satisfiable." });
                                }
                                else if (!nonZeroRules.Contains(ruleName))
                                {
                                    anomalies.Add(new { type = "Warning", rule = "Redundant Rule", message = $"Rule '{ruleName}' was executed but had zero effect (adjustment was always $0.00) in all simulated scenarios." });
                                }
                            }

                            var report = new
                            {
                                valid = anomalies.Count == 0,
                                scenariosTested = scenarios.Count,
                                anomalies = anomalies
                            };

                            response.StatusCode = (int)HttpStatusCode.OK;
                            byte[] responseData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report, options));
                            response.ContentType = "application/json";
                            response.ContentLength64 = responseData.Length;
                            await response.OutputStream.WriteAsync(responseData, 0, responseData.Length);
                        }
                        catch (JsonException ex)
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                            byte[] error = Encoding.UTF8.GetBytes($"{{\"error\": \"Invalid JSON blueprint: {ex.Message}\"}}");
                            response.ContentType = "application/json";
                            response.ContentLength64 = error.Length;
                            await response.OutputStream.WriteAsync(error, 0, error.Length);
                        }
                        catch (Exception ex)
                        {
                            response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            byte[] error = Encoding.UTF8.GetBytes($"{{\"error\": \"Validation failure: {ex.Message}\"}}");
                            response.ContentType = "application/json";
                            response.ContentLength64 = error.Length;
                            await response.OutputStream.WriteAsync(error, 0, error.Length);
                        }
                        finally
                        {
                            try
                            {
                                if (File.Exists(tempScenariosPath))
                                {
                                    File.Delete(tempScenariosPath);
                                }
                            }
                            catch {}
                        }
                    }
                    response.Close();
                    return;
                }

                string localFilePath = Path.GetFullPath(Path.Combine(wwwrootPath, rawPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                string fullWwwroot = Path.GetFullPath(wwwrootPath);
                
                if (!localFilePath.StartsWith(fullWwwroot, StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                    byte[] accessDenied = Encoding.UTF8.GetBytes("<h1>403 Forbidden</h1><p>Access denied. Path traversal request blocked.</p>");
                    response.ContentType = "text/html";
                    response.ContentLength64 = accessDenied.Length;
                    await response.OutputStream.WriteAsync(accessDenied, 0, accessDenied.Length);
                    response.Close();
                    return;
                }

                if (File.Exists(localFilePath))
                {
                    string ext = Path.GetExtension(localFilePath).ToLower();
                    response.ContentType = ext switch
                    {
                        ".html" => "text/html",
                        ".css" => "text/css",
                        ".js" => "application/javascript",
                        ".json" => "application/json",
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        _ => "application/octet-stream"
                    };

                    // Compress if client supports it
                    string acceptEncoding = request.Headers["Accept-Encoding"] ?? "";
                    Stream responseStream = response.OutputStream;
                    bool isCompressed = false;

                    if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                    {
                        response.Headers.Add("Content-Encoding", "gzip");
                        responseStream = new GZipStream(response.OutputStream, CompressionMode.Compress, leaveOpen: true);
                        isCompressed = true;
                    }
                    else if (acceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase))
                    {
                        response.Headers.Add("Content-Encoding", "deflate");
                        responseStream = new DeflateStream(response.OutputStream, CompressionMode.Compress, leaveOpen: true);
                        isCompressed = true;
                    }

                    try
                    {
                        // Check Cache First
                        if (_staticFileCache.TryGetValue(localFilePath, out var cachedData))
                        {
                            response.SendChunked = true;
                            await responseStream.WriteAsync(cachedData, 0, cachedData.Length);
                        }
                        else
                        {
                            var fileInfo = new FileInfo(localFilePath);
                            if (fileInfo.Length <= MaxCacheableFileSize)
                            {
                                byte[] fileData = await File.ReadAllBytesAsync(localFilePath);
                                _staticFileCache.TryAdd(localFilePath, fileData);
                                response.SendChunked = true;
                                await responseStream.WriteAsync(fileData, 0, fileData.Length);
                            }
                            else
                            {
                                response.SendChunked = true;
                                using (var fileStream = File.OpenRead(localFilePath))
                                {
                                    await StreamWithBufferPoolAsync(fileStream, responseStream);
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (isCompressed)
                        {
                            await responseStream.DisposeAsync();
                        }
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    byte[] notFoundData = Encoding.UTF8.GetBytes("<h1>404 Not Found</h1><p>Requested file could not be found.</p>");
                    response.ContentType = "text/html";
                    response.ContentLength64 = notFoundData.Length;
                    await response.OutputStream.WriteAsync(notFoundData, 0, notFoundData.Length);
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                byte[] errorData = Encoding.UTF8.GetBytes($"<h1>500 Internal Server Error</h1><p>{ex.Message}</p>");
                response.ContentType = "text/html";
                response.ContentLength64 = errorData.Length;
                await response.OutputStream.WriteAsync(errorData, 0, errorData.Length);
            }
            finally
            {
                response.Close();
            }
        }

        private static async Task StreamWithBufferPoolAsync(Stream source, Stream destination, int bufferSize = 8192)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destination.WriteAsync(buffer, 0, bytesRead);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static string GetSolutionRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgePriceBlueprint.slnx")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
