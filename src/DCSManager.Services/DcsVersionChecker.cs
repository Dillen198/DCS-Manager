using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using DCSManager.Core.Interfaces;
using DCSManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace DCSManager.Services;

public partial class DcsVersionChecker : IDcsVersionChecker
{
    private const string PatchNotesUrl = "https://www.digitalcombatsimulator.com/en/news/changelog/rss/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DcsVersionChecker> _logger;

    // Cache the RSS result for 30 minutes to avoid hammering the DCS site
    private (DateTimeOffset FetchedAt, string Content)? _rssCache;

    [GeneratedRegex("\"branch\"\\s*:\\s*\"(?<branch>[^\"]+)\"")]
    private static partial Regex BranchRegex();

    [GeneratedRegex("\"version\"\\s*:\\s*\"(?<version>[^\"]+)\"")]
    private static partial Regex VersionRegex();

    public DcsVersionChecker(IHttpClientFactory httpClientFactory, ILogger<DcsVersionChecker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DcsVersionStatus>> CheckAllAsync(
        IEnumerable<DcsInstall> installs,
        CancellationToken ct = default)
    {
        var results = new List<DcsVersionStatus>();

        // Fetch RSS once for all installs
        string? rss = null;
        try { rss = await GetRssAsync(ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch DCS changelog RSS");
        }

        foreach (var install in installs)
        {
            var (version, branch) = ReadAutoupdateCfg(install.InstallPath);
            string? latest = null;

            if (rss != null)
            {
                try { latest = ParseLatestVersion(rss, branch); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse RSS for branch {Branch}", branch);
                }
            }

            results.Add(new DcsVersionStatus
            {
                Install          = install,
                InstalledVersion = version ?? install.Version,
                LatestVersion    = latest,
                Branch           = branch ?? install.Branch,
                CheckFailed      = rss == null
            });
        }

        return results;
    }

    // ── autoupdate.cfg ────────────────────────────────────────────────────────

    private (string? version, string? branch) ReadAutoupdateCfg(string installPath)
    {
        try
        {
            var cfgPath = Path.Combine(installPath, "autoupdate.cfg");
            if (!File.Exists(cfgPath)) return (null, null);

            string? branch = null, version = null;

            foreach (var line in File.ReadLines(cfgPath))
            {
                if (branch == null && line.Contains("\"branch\""))
                {
                    var m = BranchRegex().Match(line);
                    if (m.Success) branch = m.Groups["branch"].Value;
                }
                if (version == null && line.Contains("\"version\""))
                {
                    var m = VersionRegex().Match(line);
                    if (m.Success) version = m.Groups["version"].Value;
                }
                if (branch != null && version != null) break;
            }

            return (version, branch);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read autoupdate.cfg at {Path}", installPath);
            return (null, null);
        }
    }

    // ── RSS feed ──────────────────────────────────────────────────────────────

    private async Task<string> GetRssAsync(CancellationToken ct)
    {
        // Return cached copy if < 30 minutes old
        if (_rssCache.HasValue &&
            DateTimeOffset.UtcNow - _rssCache.Value.FetchedAt < TimeSpan.FromMinutes(30))
        {
            return _rssCache.Value.Content;
        }

        var client = _httpClientFactory.CreateClient("dcs-rss");
        var content = await client.GetStringAsync(PatchNotesUrl, ct);
        _rssCache = (DateTimeOffset.UtcNow, content);
        return content;
    }

    /// <summary>
    /// Parses the DCS changelog RSS and returns the latest version for the given branch.
    /// Branch matching mirrors the Python reference implementation:
    ///   - "openbeta" / "testing" → link must contain the branch name
    ///   - null / "stable"        → link must NOT contain "openbeta" or "testing"
    /// The version is the second-to-last path segment of the item link, e.g.:
    ///   https://…/changelog/openbeta/2.9.10.63702/ → "2.9.10.63702"
    /// </summary>
    private static string? ParseLatestVersion(string rssXml, string? branch)
    {
        var doc = XDocument.Parse(rssXml);
        var ns  = XNamespace.None;

        var items = doc.Descendants("item");
        foreach (var item in items)
        {
            // <link> is not a real child element in RSS — it's a text node sibling.
            // Use NextNode trick or fall back to casting Element.
            var link = item.Element("link")?.Value
                ?? item.Nodes().OfType<XText>()
                         .Select(t => t.Value.Trim())
                         .FirstOrDefault(t => t.StartsWith("http"));

            if (string.IsNullOrWhiteSpace(link)) continue;

            if (IsBranchMatch(branch, link))
            {
                // Version is the last non-empty path segment
                var parts = link.TrimEnd('/').Split('/');
                return parts[^1];
            }
        }

        return null;
    }

    private static bool IsBranchMatch(string? branch, string link)
    {
        var normalised = (branch ?? "stable").ToLowerInvariant();

        return normalised switch
        {
            "stable"  => !link.Contains("openbeta", StringComparison.OrdinalIgnoreCase) &&
                         !link.Contains("testing",  StringComparison.OrdinalIgnoreCase),
            _         => link.Contains(normalised, StringComparison.OrdinalIgnoreCase)
        };
    }
}
