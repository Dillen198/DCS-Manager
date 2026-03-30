using System.Collections.Generic;
using DCSManager.Core.Models;

namespace DCSManager.Core.Interfaces;

public interface IDcsInstallDetector
{
    IReadOnlyList<DcsInstall> DetectInstalls();
    DcsInstall? GetInstall(DcsInstallType type);
}
