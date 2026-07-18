using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

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
        public string BlueprintName { get; set; }
        public List<PricingRuleDto> Rules { get; set; } = new();

        public PriceBlueprintDto(string name)
        {
            BlueprintName = name;
        }
    }

    // Fluent Builder for assembling the pricing blueprint
    public class BlueprintBuilder
    {
        private readonly PriceBlueprintDto _blueprint;

        public BlueprintBuilder(string name)
        {
            _blueprint = new PriceBlueprintDto(name);
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
        public static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("   Forge: Pricing Operating System Assembler     ");
            Console.WriteLine("=================================================");

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

                Console.WriteLine("\nGenerated Blueprint JSON Specification:\n");
                Console.WriteLine(jsonOutput);

                Console.WriteLine("\nWriting specification blueprint to disk...");
                string path = Path.Combine(solutionRoot, "enterprise_blueprint.json");
                File.WriteAllText(path, jsonOutput);
                Console.WriteLine($"Successfully saved to: {path}\n");
                Console.WriteLine("Forge successfully assembled the final pricing blueprint.");
            }
        }

        private static void StartWebServer(string wwwrootPath, string solutionRoot, int port)
        {
            string url = $"http://localhost:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            
            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Failed to start web server on {url}.");
                Console.WriteLine("Make sure the port is not in use, or run as administrator if needed.");
                Console.WriteLine($"Details: {ex.Message}");
                return;
            }

            Console.WriteLine($"\n--> Blueprint Studio server running on: {url}");
            Console.WriteLine($"    Serving web files from: {wwwrootPath}");
            Console.WriteLine($"    Reading/writing to: {Path.Combine(solutionRoot, "enterprise_blueprint.json")}\n");
            Console.WriteLine("    Press [Enter] or Ctrl+C in this terminal to shut down the server.");

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
                    Console.WriteLine($"Warning: Could not open browser automatically: {ex.Message}");
                }
            });

            var cts = new CancellationTokenSource();
            var serverTask = Task.Run(() => HandleRequests(listener, wwwrootPath, solutionRoot, cts.Token));

            Console.ReadLine();
            Console.WriteLine("Shutting down Blueprint Studio server...");
            cts.Cancel();
            listener.Stop();
            try { serverTask.Wait(); } catch { }
            Console.WriteLine("Server stopped.");
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
            
            response.Headers.Add("Access-Control-Allow-Origin", "*");
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
                        byte[] data = await File.ReadAllBytesAsync(filePath);
                        response.ContentType = "application/json";
                        response.ContentLength64 = data.Length;
                        await response.OutputStream.WriteAsync(data, 0, data.Length);
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

                string localFilePath = Path.Combine(wwwrootPath, rawPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(localFilePath))
                {
                    string ext = Path.GetExtension(localFilePath).ToLower();
                    switch (ext)
                    {
                        case ".html": response.ContentType = "text/html"; break;
                        case ".css": response.ContentType = "text/css"; break;
                        case ".js": response.ContentType = "application/javascript"; break;
                        case ".json": response.ContentType = "application/json"; break;
                        case ".png": response.ContentType = "image/png"; break;
                        case ".jpg": case ".jpeg": response.ContentType = "image/jpeg"; break;
                        default: response.ContentType = "application/octet-stream"; break;
                    }

                    byte[] fileData = await File.ReadAllBytesAsync(localFilePath);
                    response.ContentLength64 = fileData.Length;
                    await response.OutputStream.WriteAsync(fileData, 0, fileData.Length);
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
