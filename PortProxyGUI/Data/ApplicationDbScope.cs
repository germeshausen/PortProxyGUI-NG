using System.Text.Json;

namespace PortProxyGUI.Data;

public sealed class ApplicationDbScope : IDisposable
{
    public static readonly string AppDbDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PortProxyGUI");
    public static readonly string AppDbFile = Path.Combine(AppDbDirectory, "config.json");
    public static readonly string LockFile = Path.Combine(AppDbDirectory, "portproxy.lock");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _file;
    private readonly bool _readOnly;
    private readonly FileStream? _lockStream;
    private ConfigurationDocument _document;

    public IEnumerable<Rule> Rules => _document.Rules;

    private ApplicationDbScope(string file, bool readOnly, FileStream? lockStream)
    {
        _file = file;
        _readOnly = readOnly;
        _lockStream = lockStream;
        _document = Load(file);
    }

    public static ApplicationDbScope OpenShared()
    {
        Directory.CreateDirectory(AppDbDirectory);
        FileStream lockStream;
        try
        {
            lockStream = new FileStream(LockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            lockStream.Lock(0, 1);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"{AppIdentity.Name} wird bereits in einer anderen Windows-Sitzung ausgeführt.", ex);
        }

        return new ApplicationDbScope(AppDbFile, false, lockStream);
    }

    public static ApplicationDbScope FromFile(string file) => new(file, true, null);

    public Rule? GetRule(string type, string listenOn, int listenPort) =>
        _document.Rules.FirstOrDefault(x => x.Type == type && x.ListenOn == listenOn && x.ListenPort == listenPort);

    public void Add<T>(T obj) where T : class
    {
        if (obj is not Rule rule) throw new NotSupportedException($"Adding {obj.GetType().FullName} is not supported.");
        if (GetRule(rule.Type, rule.ListenOn, rule.ListenPort) is not null)
            throw new InvalidOperationException("Eine Regel mit diesem Typ, dieser Adresse und diesem Port existiert bereits.");
        rule.Id = Guid.NewGuid().ToString();
        _document.Rules.Add(rule);
        Save();
    }

    public void AddRange<T>(IEnumerable<T> objs) where T : class
    {
        foreach (var obj in objs)
        {
            if (obj is not Rule rule) throw new NotSupportedException($"Adding {obj.GetType().FullName} is not supported.");
            if (GetRule(rule.Type, rule.ListenOn, rule.ListenPort) is not null) continue;
            rule.Id = Guid.NewGuid().ToString();
            _document.Rules.Add(rule);
        }
        Save();
    }

    public void Update<T>(T obj) where T : class
    {
        if (obj is not Rule rule) throw new NotSupportedException($"Updating {obj.GetType().FullName} is not supported.");
        var existing = _document.Rules.FirstOrDefault(x => x.Id == rule.Id);
        if (existing is null) return;
        existing.Type = rule.Type;
        existing.ListenOn = rule.ListenOn;
        existing.ListenPort = rule.ListenPort;
        existing.ConnectTo = rule.ConnectTo;
        existing.ConnectPort = rule.ConnectPort;
        existing.IsInactive = rule.IsInactive;
        Save();
    }

    public void UpdateRange<T>(IEnumerable<T> objs) where T : class
    {
        foreach (var obj in objs)
        {
            if (obj is not Rule rule) continue;
            var existing = _document.Rules.FirstOrDefault(x => x.Id == rule.Id);
            if (existing is null) continue;
            existing.Type = rule.Type;
            existing.ListenOn = rule.ListenOn;
            existing.ListenPort = rule.ListenPort;
            existing.ConnectTo = rule.ConnectTo;
            existing.ConnectPort = rule.ConnectPort;
            existing.IsInactive = rule.IsInactive;
        }
        Save();
    }

    public void Remove<T>(T obj) where T : class
    {
        if (obj is not Rule rule) throw new NotSupportedException($"Removing {obj.GetType().FullName} is not supported.");
        _document.Rules.RemoveAll(x => x.Id == rule.Id);
        Save();
    }

    public void SetInactive(IEnumerable<string?> ids, bool inactive)
    {
        var selectedIds = ids.Where(id => id is not null).ToHashSet();
        foreach (var rule in _document.Rules.Where(rule => selectedIds.Contains(rule.Id)))
            rule.IsInactive = inactive;
        Save();
    }

    public void RemoveRange<T>(IEnumerable<T> objs) where T : class
    {
        var ids = objs.OfType<Rule>().Select(x => x.Id).ToHashSet();
        _document.Rules.RemoveAll(x => ids.Contains(x.Id));
        Save();
    }

    public AppConfig GetAppConfig() => new()
    {
        MainWindowSize = new System.Drawing.Size(_document.Window.Width, _document.Window.Height),
        PortProxyColumnWidths = _document.Window.ColumnWidths
    };

    public void SaveAppConfig(AppConfig config)
    {
        _document.Window.Width = config.MainWindowSize.Width;
        _document.Window.Height = config.MainWindowSize.Height;
        _document.Window.ColumnWidths = config.PortProxyColumnWidths;
        Save();
    }

    private static ConfigurationDocument Load(string file)
    {
        if (!File.Exists(file)) return new ConfigurationDocument();
        try
        {
            return JsonSerializer.Deserialize<ConfigurationDocument>(File.ReadAllText(file), JsonOptions) ?? new ConfigurationDocument();
        }
        catch (JsonException) when (File.Exists(file + ".bak"))
        {
            return JsonSerializer.Deserialize<ConfigurationDocument>(File.ReadAllText(file + ".bak"), JsonOptions) ?? new ConfigurationDocument();
        }
    }

    private void Save()
    {
        if (_readOnly) return;
        var temporaryFile = _file + ".tmp";
        var json = JsonSerializer.Serialize(_document, JsonOptions);
        using (var stream = new FileStream(temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }

        if (File.Exists(_file)) File.Replace(temporaryFile, _file, _file + ".bak", true);
        else File.Move(temporaryFile, _file);
    }

    public void Dispose()
    {
        if (_lockStream is not null)
        {
            try { _lockStream.Unlock(0, 1); } catch (IOException) { }
            _lockStream.Dispose();
        }
    }

    private sealed class ConfigurationDocument
    {
        public int Version { get; set; } = 1;
        public List<Rule> Rules { get; set; } = [];
        public WindowConfiguration Window { get; set; } = new();
    }

    private sealed class WindowConfiguration
    {
        public int Width { get; set; } = 720;
        public int Height { get; set; } = 500;
        public int[] ColumnWidths { get; set; } = [24, 64, 140, 100, 140, 100, 100];
    }
}
