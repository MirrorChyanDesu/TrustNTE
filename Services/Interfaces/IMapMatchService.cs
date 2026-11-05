using OpenCvSharp;
using System;
using System.Threading.Tasks;

namespace TrustNTE.Services.Interfaces
{
    /// <summary>
    /// 小地图匹配大地图的服务接口。
    /// </summary>
    public interface IMapMatchService
    {
        /// <summary>
        /// 尝试在小地图中提取特征，并匹配到大地图上。
        /// </summary>
        /// <param name="fullScreenFrame">当前游戏全屏截图。</param>
        /// <param name="centerX">小地图中心 X 坐标（全屏基准模版）。</param>
        /// <param name="centerY">小地图中心 Y 坐标（全屏基准模版）。</param>
        /// <param name="radius">小地图半径。</param>
        /// <returns>匹配成功返回大地图上的坐标，失败返回 null。</returns>
        Point? MatchMiniMapToBigMap(Mat fullScreenFrame, int centerX, int centerY, int radius);
    }
}