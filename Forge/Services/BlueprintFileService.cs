using System.IO;
using System.Text.Json;

namespace Forge.Services
{
    public class BlueprintLoadException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public BlueprintLoadException(string message, IReadOnlyList<string>? errors = null) : base(message)
        {
            Errors = errors ?? Array.Empty<string>();
        }
    }

    /// <summary>Open/Save of .json blueprint documents with schema validation.</summary>
    public static class BlueprintFileService
    {
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>Reads a file and validates its structure. Returns the raw JSON text.</summary>
        public static string Open(string path)
        {
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new BlueprintLoadException($"Could not read '{path}': {ex.Message}");
            }
            Validate(json);
            return json;
        }

        /// <summary>Validates then writes the JSON text. Writes via a temp file so a failure never corrupts the original.</summary>
        public static void Save(string path, string json)
        {
            Validate(json);

            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }

        public static string Serialize(PriceBlueprintDto blueprint) => JsonSerializer.Serialize(blueprint, WriteOptions);

        public static void Validate(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var errors = BlueprintValidator.Validate(doc.RootElement);
                if (errors.Count > 0)
                {
                    throw new BlueprintLoadException("Blueprint failed validation.", errors);
                }
            }
            catch (JsonException ex)
            {
                throw new BlueprintLoadException($"Invalid JSON: {ex.Message}");
            }
        }
    }
}
