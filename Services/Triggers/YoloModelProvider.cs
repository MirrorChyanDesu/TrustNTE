using OpenCvSharp;
using TrustNTE.Services.Interfaces;
namespace TrustNTE.Services.Triggers
{
    /// <summary>YOLO 检测结果最小公共形状（适配器负责把你的服务结果转成它）</summary>
    public sealed record CombatYoloBox(Rect Rect, float Confidence, string? ClassName = null);

    /// <summary>战斗用 YOLO 检测器抽象——用 3~5 行适配器包住你现有的 YOLO 服务</summary>
    public interface ICombatYoloDetector
    {
        IReadOnlyList<CombatYoloBox>? Detect(Mat frame, float confidence);
        void Dispose();
    }

    /// <summary>
    /// YOLO 模型管理器：
    /// - 进入游戏后（EnableCombatTriggers 内）才开始后台加载模型——进入游戏前不存在战斗
    /// - Detect()：仅 Ready 时真正检测；Loading/Failed/未启动 一律返回 null，
    ///   战斗触发器按"该路 miss"处理，图集/Lv指纹两路无缝顶上，加载期自动战斗照常工作
    /// - 掉线重连：Ready 不重复加载；Failed 则下次进入游戏自动重试
    /// </summary>
    public sealed class YoloModelProvider
    {
        public enum YoloLoadState { NotStarted, Loading, Ready, Failed }

        private readonly Func<ICombatYoloDetector> _factory;   // ⚠️ 适配点：内部创建/加载你的现有 YOLO 服务
        private readonly ILogService _log;
        private readonly object _lock = new();
        private YoloLoadState _state = YoloLoadState.NotStarted;
        private ICombatYoloDetector? _detector;
        private string? _lastError;

        public YoloModelProvider(Func<ICombatYoloDetector> detectorFactory, ILogService log)
        { _factory = detectorFactory; _log = log; }

        public YoloLoadState State { get { lock (_lock) return _state; } }
        public bool IsReady => State == YoloLoadState.Ready;

        public string StatusText
        {
            get
            {
                lock (_lock)
                {
                    return _state switch
                    {
                        YoloLoadState.NotStarted => "未启动(未进游戏)",
                        YoloLoadState.Loading => "加载中",
                        YoloLoadState.Ready => "就绪",
                        YoloLoadState.Failed => $"失败({_lastError})",
                        _ => "?"
                    };
                }
            }
        }

        /// <summary>游戏就绪时调用。幂等；Loading/Ready 直接返回；Failed 允许重试（给重连一次机会）</summary>
        public void EnsureStartLoading()
        {
            lock (_lock)
            {
                if (_state is YoloLoadState.Loading or YoloLoadState.Ready) return;
                _state = YoloLoadState.Loading;
                _lastError = null;
            }
            _log.LogInfo("[YOLO] 进入游戏，开始后台加载目标检测模型…（期间图集/Lv指纹路先行）");
            Task.Run(() =>
            {
                try
                {
                    var det = _factory();   // 🔧 模型加载发生在后台线程，不阻塞 UI / 引擎主循环
                    if (det == null) throw new InvalidOperationException("模型工厂返回 null（检查模型文件路径）");
                    lock (_lock)
                    {
                        _detector?.Dispose();
                        _detector = det;
                        _state = YoloLoadState.Ready;
                    }
                    _log.LogInfo("[YOLO] ✅ 目标检测模型加载完成，YOLO 路上线");
                }
                catch (Exception ex)
                {
                    lock (_lock) { _state = YoloLoadState.Failed; _lastError = ex.Message; }
                    _log.LogError($"[YOLO] ❌ 模型加载失败（YOLO 路本次禁用，图集/指纹路不受影响；重进游戏将重试）: {ex.Message}");
                }
            });
        }

        /// <summary>非 Ready 一律返回 null——调用方静默跳过该路</summary>
        public IReadOnlyList<CombatYoloBox>? Detect(Mat frame, float confidence)
        {
            ICombatYoloDetector? det;
            lock (_lock)
            {
                if (_state != YoloLoadState.Ready) return null;
                det = _detector;
            }
            try { return det!.Detect(frame, confidence); }
            catch (Exception ex)
            {
                _log.LogError($"[YOLO] 检测异常: {ex.Message}");
                return null;
            }
        }
    }
}
