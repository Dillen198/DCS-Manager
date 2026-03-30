using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DCSManager.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DCSManager.Services;

public class GitHubReleaseService : IGitHubReleaseService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubReleaseService> _logger;
    private readonly ConcurrentDictionary<string, (string ETag, GitHubRelease Release)> _cache = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GitHubReleaseService(IHttpClientFactory httpClientFactory, ILogger<GitHubReleaseService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo, CancellationToken ct = default)
    {
        var key = $"{owner}/{repo}";
        var url = $"repos/{owner}/{repo}/releases/latest";

        try
        {
            var client = _httpClientFactory.CreateClient("github");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (_cache.TryGetValue(key, out var cached))
                request.Headers.TryAddWithoutValidation("If-None-Match", cached.ETag);

            using var response = await client.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified && _cache.TryGetValue(key, out var hit))
            {
                _logger.LogDebug("GitHub cache hit for {Key}", key);
                return hit.Release;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json, _jsonOptions);

            if (release != null)
            {
                var etag = response.Headers.ETag?.Tag ?? "";
                _cache[key] = (etag, release);
            }

            return release;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("No releases found for {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch GitHub release for {Key}", key);
            return null;
        }
    }
}
