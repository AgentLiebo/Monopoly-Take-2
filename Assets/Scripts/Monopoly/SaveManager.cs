using System.IO;
using System.Text.Json;

namespace MonopolyTake2;

public sealed class SaveManager
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        IncludeFields = false
    };

    public void SaveGame(MonopolyGameState state, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(state, Options));
    }

    public MonopolyGameState LoadGame(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MonopolyGameState>(json, Options) ?? new MonopolyGameState();
    }

    public bool HasSave(string path) => File.Exists(path);
}
