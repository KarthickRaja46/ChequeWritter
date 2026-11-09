using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChequeWriter.Core;

/// <summary>
/// Saves everything (banks, payees, register, settings) in one JSON file.
/// Writes go to a temp file first and the previous version is kept as .bak,
/// so a crash or power cut cannot leave a half-written file.
/// </summary>
public class DataStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath { get; }
    public AppData Data { get; private set; }

    public DataStore(string filePath)
    {
        FilePath = filePath;
        Data = Load(filePath);
    }

    /// <summary>
    /// Data lives next to the program when a file named "portable.txt" is there (handy on a USB stick),
    /// otherwise in %APPDATA%\ChequeWriter.
    /// </summary>
    public static string DefaultPath(string exeFolder)
    {
        var folder = File.Exists(Path.Combine(exeFolder, "portable.txt"))
            ? Path.Combine(exeFolder, "Data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChequeWriter");
        return Path.Combine(folder, "chequewriter-data.json");
    }

    public static AppData Load(string path)
    {
        if (!File.Exists(path)) return DefaultLayouts.NewAppData();
        var json = File.ReadAllText(path, Encoding.UTF8);
        var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions)
                   ?? throw new InvalidDataException("The data file is empty or damaged.");
        if (data.Banks.Count == 0) data.Banks.Add(DefaultLayouts.RakBusiness());
        return data;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Data, JsonOptions), Encoding.UTF8);
        if (File.Exists(FilePath))
            File.Replace(tmp, FilePath, FilePath + ".bak");
        else
            File.Move(tmp, FilePath);
    }

    /// <summary>Replaces all data with the contents of a backup file (the current data is saved as .bak first).</summary>
    public void RestoreFrom(string backupFile)
    {
        var restored = Load(backupFile);
        Data = restored;
        Save();
    }

    public void BackupTo(string backupFile)
    {
        Save();
        File.Copy(FilePath, backupFile, overwrite: true);
    }

    /// <summary>Adds the payee to the saved list if it is new (case-insensitive).</summary>
    public void RememberPayee(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return;
        if (!Data.Payees.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            Data.Payees.Add(new Payee { Name = name });
    }

    /// <summary>"000123" -> "000124", keeping leading zeros. Non-numeric numbers are returned unchanged.</summary>
    public static string NextNumber(string current)
    {
        current = (current ?? "").Trim();
        if (current.Length == 0 || !current.All(char.IsDigit)) return current;
        var next = (decimal.Parse(current) + 1).ToString(new string('0', current.Length));
        return next;
    }
}
