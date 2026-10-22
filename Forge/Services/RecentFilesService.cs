using System.IO;
using System.Text.Json;

namespace Forge.Services
{
    /// <summary>Persisted most-recently-used file list, stored in %APPDATA%\Forge\recent.json.</summary>
    public class RecentFilesService
    {
        private readonly string _storePath;
        private readonly int _max;

        public RecentFilesService(string? storePath = null, int max = 10)
        {
            _storePath = storePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Forge", "recent.json");
            _max = max;
        }

        public IReadOnlyList<string> Load()
        {
            try
            {
                if (!File.Exists(_storePath)) return Array.Empty<string>();
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_storePath));
                return list ?? new List<string>();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        public IReadOnlyList<string> Add(string path)
        {
            string full = Path.GetFullPath(path);
            var list = Load().Where(p => !string.Equals(p, full, StringComparison.OrdinalIgnoreCase)).ToList();
            list.Insert(0, full);
            if (list.Count > _max) list.RemoveRange(_max, list.Count - _max);
            Persist(list);
            return list;
        }

        public IReadOnlyList<string> Remove(string path)
        {
            var list = Load().Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)).ToList();
            Persist(list);
            return list;
        }

        private void Persist(List<string> list)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
                File.WriteAllText(_storePath, JsonSerializer.Serialize(list));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Recent files are a convenience; never fail the app over them.
            }
        }
    }
}
