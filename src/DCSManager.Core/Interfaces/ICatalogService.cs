using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCSManager.Core.Models;

namespace DCSManager.Core.Interfaces;

public interface ICatalogService
{
    Task<IReadOnlyList<PluginDefinition>> GetCatalogAsync(CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
}
