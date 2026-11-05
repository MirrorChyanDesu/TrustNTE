using System;

namespace TrustNTE.Services.Interfaces
{
    /// <summary>
    /// 归一化 1920×1080 逻辑坐标 → 目标窗口物理客户区坐标。
    /// [方案B] 从 AutomationService.MapLogical 拆出的独立坐标服务，
    /// 使 AutomationService 与 InputRouter 共享同一换算而不构成依赖环。
    /// </summary>
    public interface ICoordinateMapper
    {
        /// <summary>逻辑坐标换算为 hwnd 客户区物理坐标。</summary>
        (int Px, int Py) LogicalToClient(IntPtr hwnd, int logicalX, int logicalY);
    }
}
