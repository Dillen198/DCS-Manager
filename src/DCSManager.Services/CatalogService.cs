using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DCSManager.Core.Interfaces;
using DCSManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace DCSManager.Services;

public class CatalogService : ICatalogService
{
    private const string CatalogUrl = "https://raw.githubusercontent.com/Dillen198/DCS-Manager/main/catalog/plugins.json";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CatalogService> _logger;
    private IReadOnlyList<PluginDefinition>? _catalog;
    private string? _etag;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CatalogService(IHttpClientFactory httpClientFactory, ILogger<CatalogService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PluginDefinition>> GetCatalogAsync(CancellationToken ct = default)
    {
        if (_catalog != null)
            return _catalog;

        await RefreshAsync(ct);
        return _catalog ?? Array.Empty<PluginDefinition>();
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var fetched = await FetchRemoteCatalogAsync(ct);
            if (fetched != null)
            {
                _catalog = fetched;
                return;
            }

            // Fall back to embedded resource
            _catalog = LoadEmbeddedCatalog();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<PluginDefinition>?> FetchRemoteCatalogAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("github");
            using var request = new HttpRequestMessage(HttpMethod.Get, CatalogUrl);
            if (_etag != null)
                request.Headers.TryAddWithoutValidation("If-None-Match", _etag);

            using var response = await client.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                return _catalog; // already up to date

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            _etag = response.Headers.ETag?.Tag;

            var root = JsonSerializer.Deserialize<CatalogRoot>(json, _jsonOptions);
            return root?.Plugins ?? new List<PluginDefinition>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch remote catalog, using embedded fallback");
            return null;
        }
    }

    private IReadOnlyList<PluginDefinition> LoadEmbeddedCatalog()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("DCSManager.Services.catalog.plugins.json");
            if (stream == null)
            {
                _logger.LogError("Embedded catalog resource not found");
                return Array.Empty<PluginDefinition>();
            }
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var root = JsonSerializer.Deserialize<CatalogRoot>(json, _jsonOptions);
            return root?.Plugins ?? new List<PluginDefinition>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load embedded catalog");
            return Array.Empty<PluginDefinition>();
        }
    }

    private class CatalogRoot
    {
        public List<PluginDefinition> Plugins { get; set; } = new();
    }
}
