using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCSManager.Core.Models;

namespace DCSManager.Core.Interfaces;

public interface IDcsVersionChecker
{
    Task<IReadOnlyList<DcsVersionStatus>> CheckAllAsync(
        IEnumerable<DcsInstall> installs,
        CancellationToken ct = default);
}
