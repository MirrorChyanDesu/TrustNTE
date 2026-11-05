using Size = OpenCvSharp.Size;

namespace TrustNTE.Helpers
{
    public static class GameConstants
    {
        // 游戏进程名
        public const string GameExe = "HTGame.exe";

        // 启动器进程名列表
        public static readonly string[] LauncherExes = { "NTEGame.exe", "NTEGlobalGame.exe", "NTETWGame.exe" };

        // 游戏窗口类名 (虚幻引擎)
        public const string GameWindowClass = "UnrealWindow";

        // 启动器窗口类名 (Qt)
        public const string LauncherWindowClass = "Qt51517QWindowOwnDC";

        /// <summary>
        /// 战斗 YOLO 模型路径（OpenVINO IR XML 格式）。
        /// 自训模型 Nte_RecognizeEnemy：640×640 输入 / 标准检测头（输出 [1,5,8400]）/ 类别 LV。
        /// </summary>
        public static readonly string CombatYoloModelPath = System.IO.Path.Combine(
            System.AppContext.BaseDirectory, "Assets", "NTE-CombatYOLO", "Nte_RecognizeEnemy.xml");
    }
}
