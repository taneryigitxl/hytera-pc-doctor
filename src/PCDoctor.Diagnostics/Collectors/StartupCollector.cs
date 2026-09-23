using Microsoft.Win32;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Results;
using PCDoctor.Core.Models.Startup;

namespace PCDoctor.Diagnostics.Collectors;

public sealed class StartupCollector : IStartupService
{
    private const string DisabledSuffix = ".tondy-disabled";
    private const string DisabledRoot = @"Software\Tondy\PcDoctor\DisabledStartup";
    private readonly IAppLogger _logger;
    private readonly WmiClient _wmi;

    public StartupCollector(IAppLogger logger, WmiClient wmi)
    {
        _logger = logger;
        _wmi = wmi;
    }

    public async Task<OperationResult<IReadOnlyList<StartupEntry>>> GetStartupEntriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = new List<StartupEntry>();
            entries.AddRange(ReadRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU", "Run", "HKCU Run", Environment.UserName, true));
            entries.AddRange(ReadRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU", "RunOnce", "HKCU RunOnce", Environment.UserName, true));
            entries.AddRange(ReadRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM", "Run", "HKLM Run", "All users", true));
            entries.AddRange(ReadRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM", "RunOnce", "HKLM RunOnce", "All users", true));
            entries.AddRange(ReadDisabledRunKey(Registry.CurrentUser, "HKCU", "Run", "HKCU Run", Environment.UserName));
            entries.AddRange(ReadDisabledRunKey(Registry.CurrentUser, "HKCU", "RunOnce", "HKCU RunOnce", Environment.UserName));
            entries.AddRange(ReadDisabledRunKey(Registry.LocalMachine, "HKLM", "Run", "HKLM Run", "All users"));
            entries.AddRange(ReadDisabledRunKey(Registry.LocalMachine, "HKLM", "RunOnce", "HKLM RunOnce", "All users"));

            var startupFolders = new (string Path, string Label, string User)[]
            {
                (Environment.GetFolderPath(Environment.SpecialFolder.Startup), "User Startup folder", Environment.UserName),
                (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Common Startup folder", "All users")
            };

            foreach (var folder in startupFolders)
            {
                entries.AddRange(ReadStartupFolder(folder.Path, folder.Label, folder.User));
            }

            try
            {
                var wmiRows = await _wmi.QueryAsync(
                    "SELECT Name, Command, Location, User FROM Win32_StartupCommand",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in wmiRows)
                {
                    var candidate = new StartupEntry
                    {
                        Name = row.Text("Name"),
                        Command = row.Text("Command", string.Empty),
                        Location = row.Text("Location"),
                        Source = "Win32_StartupCommand",
                        User = row.Text("User"),
                        IsEnabled = true,
                        CanToggle = false,
                        Kind = StartupEntryKind.Other
                    };

                    if (!entries.Any(existing =>
                            string.Equals(existing.Name, candidate.Name, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(existing.Command, candidate.Command, StringComparison.OrdinalIgnoreCase)))
                    {
                        entries.Add(candidate);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("Win32_StartupCommand query failed; registry and startup folders were still collected.", ex);
            }

            return OperationResult<IReadOnlyList<StartupEntry>>.Ok(
                entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToList());
        }
        catch (Exception ex)
        {
            _logger.Error("Startup inventory failed.", ex);
            return OperationResult<IReadOnlyList<StartupEntry>>.Fail(
                "Startup programs could not be enumerated.",
                ex.ToString());
        }
    }

    public Task<OperationResult<bool>> SetEnabledAsync(StartupEntry entry, bool enabled, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => SetEnabled(entry, enabled), cancellationToken);
    }

    private OperationResult<bool> SetEnabled(StartupEntry entry, bool enabled)
    {
        try
        {
            if (!entry.CanToggle)
            {
                return OperationResult<bool>.Fail("This startup entry cannot be toggled from Tondy Pc Doctor.");
            }

            if (entry.Kind == StartupEntryKind.Registry)
            {
                ToggleRegistry(entry, enabled);
            }
            else if (entry.Kind == StartupEntryKind.Folder)
            {
                ToggleFolder(entry, enabled);
            }
            else
            {
                return OperationResult<bool>.Fail("This startup entry cannot be toggled from Tondy Pc Doctor.");
            }

            return OperationResult<bool>.Ok(true);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error("Startup toggle was denied.", ex);
            return OperationResult<bool>.Fail(
                "This startup entry requires administrator permission.",
                ex.ToString());
        }
        catch (Exception ex)
        {
            _logger.Error("Startup toggle failed.", ex);
            return OperationResult<bool>.Fail(ex.Message, ex.ToString());
        }
    }

    private static void ToggleRegistry(StartupEntry entry, bool enabled)
    {
        var hive = entry.Hive == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
        var activePath = entry.RegistryPath ?? throw new InvalidOperationException("Registry path is missing.");
        var valueName = entry.ValueName ?? entry.Name;
        var disabledPath = $@"{DisabledRoot}\{entry.Hive}\{Path.GetFileName(activePath)}";

        if (enabled)
        {
            MoveValue(hive, disabledPath, hive, activePath, valueName);
        }
        else
        {
            MoveValue(hive, activePath, hive, disabledPath, valueName);
        }
    }

    private static void MoveValue(RegistryKey sourceHive, string sourcePath, RegistryKey targetHive, string targetPath, string valueName)
    {
        using var source = sourceHive.OpenSubKey(sourcePath, writable: true)
                           ?? throw new InvalidOperationException($"Registry key not found: {sourcePath}");
        var value = source.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                    ?? throw new InvalidOperationException($"Registry value not found: {valueName}");
        var kind = source.GetValueKind(valueName);

        using var target = targetHive.CreateSubKey(targetPath, true)
                           ?? throw new InvalidOperationException($"Registry key could not be created: {targetPath}");
        target.SetValue(valueName, value, kind);
        source.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static void ToggleFolder(StartupEntry entry, bool enabled)
    {
        var path = entry.FilePath ?? throw new InvalidOperationException("Startup file path is missing.");
        if (enabled)
        {
            if (!path.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var restored = path[..^DisabledSuffix.Length];
            File.Move(path, restored, overwrite: false);
        }
        else
        {
            if (path.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            File.Move(path, path + DisabledSuffix, overwrite: false);
        }
    }

    private IEnumerable<StartupEntry> ReadRunKey(RegistryKey root, string path, string hive, string _, string source, string user, bool enabled)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            if (key is null)
            {
                return [];
            }

            return key.GetValueNames()
                .Select(name => CreateRegistryEntry(root, path, hive, name, Convert.ToString(key.GetValue(name)) ?? string.Empty, source, user, enabled))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not read startup registry key {path}.", ex);
            return [];
        }
    }

    private IEnumerable<StartupEntry> ReadDisabledRunKey(RegistryKey root, string hive, string keyName, string source, string user)
    {
        var path = $@"{DisabledRoot}\{hive}\{keyName}";
        var activePath = hive == "HKLM"
            ? $@"Software\Microsoft\Windows\CurrentVersion\{keyName}"
            : $@"Software\Microsoft\Windows\CurrentVersion\{keyName}";
        return ReadRunKey(root, path, hive, keyName, source, user, false)
            .Select(entry => new StartupEntry
            {
                Name = entry.Name,
                Command = entry.Command,
                Location = $@"{root.Name}\{activePath}",
                Source = source,
                User = user,
                IsEnabled = false,
                CanToggle = true,
                Kind = StartupEntryKind.Registry,
                Hive = hive,
                RegistryPath = activePath,
                ValueName = entry.ValueName
            });
    }

    private static StartupEntry CreateRegistryEntry(
        RegistryKey root,
        string path,
        string hive,
        string name,
        string command,
        string source,
        string user,
        bool enabled)
    {
        var valueName = string.IsNullOrWhiteSpace(name) ? "(default)" : name;
        return new StartupEntry
        {
            Name = valueName,
            Command = command,
            Location = $@"{root.Name}\{path}",
            Source = source,
            User = user,
            IsEnabled = enabled,
            CanToggle = true,
            Kind = StartupEntryKind.Registry,
            Hive = hive,
            RegistryPath = path,
            ValueName = valueName
        };
    }

    private IEnumerable<StartupEntry> ReadStartupFolder(string path, string source, string user)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return [];
            }

            return Directory.GetFiles(path)
                .Where(file => !string.Equals(Path.GetFileName(file), "desktop.ini", StringComparison.OrdinalIgnoreCase))
                .Select(file =>
                {
                    var disabled = file.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
                    var display = disabled
                        ? Path.GetFileNameWithoutExtension(file[..^DisabledSuffix.Length])
                        : Path.GetFileNameWithoutExtension(file);
                    return new StartupEntry
                    {
                        Name = display,
                        Command = disabled ? file[..^DisabledSuffix.Length] : file,
                        Location = path,
                        Source = source,
                        User = user,
                        IsEnabled = !disabled,
                        CanToggle = true,
                        Kind = StartupEntryKind.Folder,
                        FilePath = file
                    };
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not read startup folder {path}.", ex);
            return [];
        }
    }
}
