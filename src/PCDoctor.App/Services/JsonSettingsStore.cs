using System.IO;
using System.Text.Json;
using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;

namespace PCDoctor.App.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly IAppLogger _logger;
    private readonly string _path;
    private SettingsModel _model = new();

    public JsonSettingsStore(IAppLogger logger)
    {
        _logger = logger;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCDoctor");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "settings.json");
        Load();
    }

    public AppTheme Theme
    {
        get => _model.Theme;
        set
        {
            _model.Theme = value;
            Save();
        }
    }

    public AppLanguage Language
    {
        get => _model.Language;
        set
        {
            _model.Language = value;
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var json = File.ReadAllText(_path);
            _model = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
        }
        catch (Exception ex)
        {
            _logger.Warn("Could not load settings.json. Default theme and language will be used.", ex);
            _model = new SettingsModel();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_model, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.Error("Could not save settings.json.", ex);
        }
    }

    private sealed class SettingsModel
    {
        public AppTheme Theme { get; set; } = AppTheme.Dark;
        public AppLanguage Language { get; set; } = AppLanguage.Turkish;
    }
}
