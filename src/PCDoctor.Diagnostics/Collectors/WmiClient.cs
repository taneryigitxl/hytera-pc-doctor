using System.Management;
using PCDoctor.Core.Logging;

namespace PCDoctor.Diagnostics.Collectors;

public sealed class WmiClient
{
    private readonly IAppLogger _logger;

    public WmiClient(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<WmiRecord>> QueryAsync(string query, CancellationToken cancellationToken = default)
        => QueryAsync(@"root\cimv2", query, cancellationToken);

    public async Task<IReadOnlyList<WmiRecord>> QueryAsync(string wmiNamespace, string query, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Query(wmiNamespace, query), cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<WmiRecord> Query(string wmiNamespace, string query)
    {
        _logger.Debug($"WMI query [{wmiNamespace}]: {query}");

        var results = new List<WmiRecord>();
        var scope = new ManagementScope(wmiNamespace);
        scope.Connect();

        using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
        searcher.Options.Timeout = TimeSpan.FromSeconds(20);
        searcher.Options.Rewindable = false;

        using var collection = searcher.Get();
        foreach (var managementBaseObject in collection)
        {
            using (managementBaseObject)
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in managementBaseObject.Properties)
                {
                    values[property.Name] = property.Value;
                }

                results.Add(new WmiRecord(values));
            }
        }

        return results;
    }
}

public sealed class WmiRecord
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    public WmiRecord(IReadOnlyDictionary<string, object?> values)
    {
        _values = values;
    }

    public string Text(string name, string fallback = "Unknown")
    {
        if (!_values.TryGetValue(name, out var value) || value is null)
        {
            return fallback;
        }

        var text = Convert.ToString(value)?.Trim();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    public T Number<T>(string name, T fallback = default!) where T : struct
    {
        if (!_values.TryGetValue(name, out var value) || value is null)
        {
            return fallback;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public bool Flag(string name)
    {
        if (!_values.TryGetValue(name, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool flag => flag,
            ushort number => number != 0,
            uint number => number != 0,
            int number => number != 0,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => false
        };
    }

    public DateTimeOffset? Date(string name)
    {
        if (!_values.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        try
        {
            return value switch
            {
                DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Local)),
                string text when !string.IsNullOrWhiteSpace(text) => new DateTimeOffset(
                    DateTime.SpecifyKind(ManagementDateTimeConverter.ToDateTime(text), DateTimeKind.Local)),
                _ => null
            };
        }
        catch (Exception)
        {
            return null;
        }
    }
}
