using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace Forge.Services
{
    public sealed record AuditRecord(string RuleName, string Description, double InputPrice, double OutputPrice, double Adjustment);

    public sealed record PriceResult(double BasePrice, double FinalPrice, IReadOnlyList<AuditRecord> AuditTrail);

    public class PricingEngineException : Exception
    {
        public PricingEngineException(string message) : base(message) { }
    }

    /// <summary>
    /// In-process wrapper over price_blueprint.dll (PB_Calculate). The native call is
    /// thread-safe (no shared output buffer), so no managed lock is required.
    /// </summary>
    public class PricingEngine
    {
        private const string DllName = "price_blueprint.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PB_Calculate")]
        private static extern int PB_Calculate(byte[] blueprintJson, byte[] contextJson, byte[]? outBuffer, int outCapacity);

        private static byte[] ToNative(string s) => Encoding.UTF8.GetBytes(s + "\0");

        /// <summary>Raw call: returns the engine's JSON output (result or {"error":...}).</summary>
        public virtual string CalculateRaw(string blueprintJson, string contextJson)
        {
            byte[] bp = ToNative(blueprintJson);
            byte[] ctx = ToNative(contextJson);

            byte[] buffer = new byte[4096];
            int required = PB_Calculate(bp, ctx, buffer, buffer.Length);
            if (required > buffer.Length)
            {
                buffer = new byte[required];
                required = PB_Calculate(bp, ctx, buffer, buffer.Length);
            }
            return Encoding.UTF8.GetString(buffer, 0, required - 1);
        }

        public PriceResult Calculate(string blueprintJson, IReadOnlyDictionary<string, object> context)
        {
            string contextJson = JsonSerializer.Serialize(context);
            return Parse(CalculateRaw(blueprintJson, contextJson));
        }

        public static PriceResult Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
            {
                throw new PricingEngineException(err.GetString() ?? "Unknown engine error");
            }

            var trail = new List<AuditRecord>();
            if (root.TryGetProperty("auditTrail", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in arr.EnumerateArray())
                {
                    trail.Add(new AuditRecord(
                        r.GetProperty("ruleName").GetString() ?? "",
                        r.GetProperty("description").GetString() ?? "",
                        r.GetProperty("inputPrice").GetDouble(),
                        r.GetProperty("outputPrice").GetDouble(),
                        r.GetProperty("adjustment").GetDouble()));
                }
            }

            return new PriceResult(
                root.GetProperty("basePrice").GetDouble(),
                root.GetProperty("finalPrice").GetDouble(),
                trail);
        }
    }
}
