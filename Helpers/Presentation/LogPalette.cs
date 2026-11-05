using System.Windows.Media;
using TrustNTE.Models;

using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace TrustNTE.Helpers
{
    public static class LogPalette
    {
        // 基础默认颜色（会被配置文件里的自定义颜色覆盖）
        // 注：变量名历史遗留（Cyan其实是洋红，Purple其实是亮绿，White其实是青色），为了不破坏原有逻辑暂不改名
        private static Brush Red = Frozen("#FF5252");
        private static Brush Orange = Frozen("#FFA940");
        private static Brush Cyan = Frozen("#FF00FF");     // 鼠标：洋红
        private static Brush Purple = Frozen("#00FF00");   // 键盘：亮绿
        private static Brush Gray = Frozen("#7A828E");     // 高频：灰蓝
        private static Brush White = Frozen("#00FFFF");    // 默认文本：青色

        private static Brush Frozen(string hex)
        {
            try
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                b.Freeze();
                return b;
            }
            catch
            {
                // catch 兜底时必须 Freeze，防止跨线程报错
                var b = new SolidColorBrush(Colors.White);
                b.Freeze();
                return b;
            }
        }

        public static void UpdateFromConfig(AppConfig config)
        {
            Red = Frozen(config.LogColorError);
            Orange = Frozen(config.LogColorGuard);
            Cyan = Frozen(config.LogColorMouse);
            Purple = Frozen(config.LogColorKeyboard);
            Gray = Frozen(config.LogColorHighFreq);
            White = Frozen(config.LogColorDefault);
        }

        /// <summary>
        /// 恢复默认颜色
        /// </summary>
        public static void ResetToDefault(AppConfig config)
        {
            config.LogColorDefault = "#00FFFF";
            config.LogColorError = "#FF5252";
            config.LogColorGuard = "#FFA940";
            config.LogColorHighFreq = "#7A828E";
            config.LogColorMouse = "#FF00FF";
            config.LogColorKeyboard = "#00FF00";
            UpdateFromConfig(config);
        }

        public static Brush Pick(string line)
        {
            if (string.IsNullOrEmpty(line)) return White;

            if (line.Contains("[ERROR]")) return Red;
            if (line.Contains("[WARN]")) return Orange;

            // 1. 错误与异常：红色
            if (line.Contains("错误") || line.Contains("失败") || line.Contains("异常") || line.Contains("找不到") || line.Contains("ERROR"))
                return Red;

            // 2. 守护与监管：橙色
            if (line.Contains("[守护]") || line.Contains("[监管器]"))
                return Orange;

            // 3. 钓鱼日志：直接返回默认颜色，不再吃灰！
            // 注意：必须放在“高频日志”判断的前面，才能截胡 [自动钓鱼] 状态切换等日志
            if (line.Contains("[钓鱼]") || line.Contains("[自动钓鱼]"))
                return White;

            // 4. 剩下的高频日志（如音游、按键）保留灰色防瞎眼
            if (line.Contains("心跳") || line.Contains("控鱼") || line.Contains("击发按键") || line.Contains("[按键发送]") || line.Contains("平A") || line.Contains("状态切换"))
                return Gray;

            // 5. 鼠标点击：自定义颜色（洋红）
            if (line.Contains("左键") || line.Contains("右键") || line.Contains("点击") || line.Contains("Click"))
                return Cyan;

            // 6. 键盘按键：自定义颜色（亮绿）
            if (line.Contains("键 [") || line.Contains("按下") || line.Contains("Key"))
                return Purple;

            return White;
        }
    }
}