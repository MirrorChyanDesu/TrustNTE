using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services
{
    /// <summary>输入路由器实现（Singleton，线程安全）。</summary>
    public sealed class InputRouter : IInputRouter
    {
        private readonly IConfigService _configService;
        private readonly IInputService _inputService;
        private readonly INteInteractionService _nte;
        private readonly ICoordinateMapper _mapper;

        /// <summary>按住中的键 → 按下时所用驱动。[卡键防护] 核心状态。</summary>
        private readonly Dictionary<int, InputDriveType> _heldKeys = new();

        public InputRouter(
            IConfigService configService,
            IInputService inputService,
            INteInteractionService nte,
            ICoordinateMapper mapper)
        {
            _configService = configService;
            _inputService = inputService;
            _nte = nte;
            _mapper = mapper;
        }

        public InputDriveType CurrentDrive => InputDriveTypeExtensions.Parse(_configService.Load().MouseDriveType);

        // ==================== 鼠标 ====================

        public (int Px, int Py) ClickLogical(IntPtr hwnd, int logicalX, int logicalY, bool isLeft = true)
        {
            var (px, py) = _mapper.LogicalToClient(hwnd, logicalX, logicalY);
            Click(hwnd, px, py, isLeft);
            return (px, py);
        }

        public void Click(IntPtr hwnd, int clientX, int clientY, bool isLeft = true)
            => ClickWith(CurrentDrive, hwnd, clientX, clientY, isLeft);

        public void ClickWith(InputDriveType drive, IntPtr hwnd, int clientX, int clientY, bool isLeft = true)
        {
            if (hwnd == IntPtr.Zero) return;
#if DEBUG
            DebugCount(true);
#endif
            switch (drive)
            {
                case InputDriveType.Physical:
                    _inputService.SendPhysicalClickWithRestore(hwnd, clientX, clientY, isLeft);
                    break;
                case InputDriveType.Background:
                    _inputService.SendBackgroundMouseClick(hwnd, clientX, clientY, isLeft);
                    break;
                case InputDriveType.Trick:
                default:
                    _nte.NteClick(hwnd, clientX, clientY, isLeft);
                    break;
            }
        }

        // ==================== 中键（战斗锁定目标） ====================

        public (int Px, int Py) MiddleClickLogical(IntPtr hwnd, int logicalX, int logicalY)
        {
            var (px, py) = _mapper.LogicalToClient(hwnd, logicalX, logicalY);
            MiddleClick(hwnd, px, py);
            return (px, py);
        }

        public void MiddleClick(IntPtr hwnd, int clientX, int clientY)
            => MiddleClickWith(CurrentDrive, hwnd, clientX, clientY);

        public void MiddleClickWith(InputDriveType drive, IntPtr hwnd, int clientX, int clientY)
        {
            if (hwnd == IntPtr.Zero) return;
#if DEBUG
            DebugCount(true);
#endif
            switch (drive)
            {
                case InputDriveType.Physical:
                    _inputService.SendPhysicalMiddleClickWithRestore(hwnd, clientX, clientY);
                    break;
                case InputDriveType.Background:
                    _inputService.SendMiddleClick(hwnd, clientX, clientY);
                    break;
                case InputDriveType.Trick:
                default:
                    _nte.NteMiddleClick(hwnd, clientX, clientY);
                    break;
            }
        }

        // ==================== 键盘 ====================

        public void KeyPress(IntPtr hwnd, int virtualKey, int holdMs = 50)
        {
            if (hwnd == IntPtr.Zero) return;
            KeyDown(hwnd, virtualKey);
            Thread.Sleep(holdMs);
            KeyUp(hwnd, virtualKey);
        }

        public void KeyDown(IntPtr hwnd, int virtualKey) => KeyDownWith(CurrentDrive, hwnd, virtualKey);

        private void KeyDownWith(InputDriveType drive, IntPtr hwnd, int virtualKey)
        {
            if (hwnd == IntPtr.Zero) return;

            lock (_heldKeys)
            {
                // 防止多线程并发按下同一个键但驱动不同，避免双通道按下
                if (_heldKeys.TryGetValue(virtualKey, out var oldDrive) && oldDrive != drive)
                {
                    KeyUpWith(hwnd, virtualKey, oldDrive);
                }
                _heldKeys[virtualKey] = drive;
            }
#if DEBUG
            DebugCount(false);
#endif
            switch (drive)
            {
                case InputDriveType.Physical:
                    Win32Input.KeyDown(virtualKey);
                    break;
                case InputDriveType.Background:
                    _inputService.SendKey(hwnd, virtualKey, true);
                    break;
                case InputDriveType.Trick:
                default:
                    _nte.NteKeyDown(hwnd, virtualKey);
                    break;
            }
        }

        public void KeyUp(IntPtr hwnd, int virtualKey)
        {
            InputDriveType drive;
            lock (_heldKeys)
            {
                if (!_heldKeys.Remove(virtualKey, out drive)) drive = CurrentDrive;
            }
            KeyUpWith(hwnd, virtualKey, drive);
        }

        private void KeyUpWith(IntPtr hwnd, int virtualKey, InputDriveType drive)
        {
            switch (drive)
            {
                case InputDriveType.Physical:
                    // 物理键是全局 SendInput，与目标窗口无关。
                    // 即使 hwnd 已失效（引擎已停止）也必须补发抬起，否则系统级卡键。
                    Win32Input.KeyUp(virtualKey);
                    break;
                case InputDriveType.Background:
                    if (hwnd == IntPtr.Zero) return;
                    _inputService.SendKey(hwnd, virtualKey, false);
                    break;
                case InputDriveType.Trick:
                default:
                    if (hwnd == IntPtr.Zero) return;
                    _nte.NteKeyUp(hwnd, virtualKey);
                    break;
            }
        }

        public void KeyPressWith(InputDriveType drive, IntPtr hwnd, int virtualKey, int holdMs = 50)
        {
            if (hwnd == IntPtr.Zero) return;
            KeyDownWith(drive, hwnd, virtualKey);
            Thread.Sleep(holdMs);
            KeyUp(hwnd, virtualKey);
        }

        // ==================== 卡键防护：一次性释放所有按住的键 ====================

        /// <summary>
        /// 一次性抬起当前所有按住的键。引擎停止、程序退出前必须调用。
        /// 物理驱动键不依赖 hwnd，即使句柄已失效也能抬起。
        /// </summary>
        public void ReleaseAllHeldKeys(IntPtr hwnd)
        {
            lock (_heldKeys)
            {
                foreach (var kv in _heldKeys)
                {
                    try { KeyUpWith(hwnd, kv.Key, kv.Value); } catch { }
                }
                _heldKeys.Clear();
            }
        }

        // ==================== 文本注入 ====================

        public Task InjectTextAsync(IntPtr hwnd, string text, int delayMs, CancellationToken ct)
            => NteTextInjector.InjectTextAsync(hwnd, text, delayMs, ct);

        // ==================== [DEBUG] 内部吞吐量统计 ====================
#if DEBUG
        private int _dbgClicks, _dbgKeys;
        private long _lastDbgFlushMs = Environment.TickCount64;

        private void DebugCount(bool isClick)
        {
            if (isClick) Interlocked.Increment(ref _dbgClicks);
            else Interlocked.Increment(ref _dbgKeys);

            long now = Environment.TickCount64;
            long last = Interlocked.Read(ref _lastDbgFlushMs);
            if (now - last >= 5000 && Interlocked.CompareExchange(ref _lastDbgFlushMs, now, last) == last)
            {
                int c = Interlocked.Exchange(ref _dbgClicks, 0);
                int k = Interlocked.Exchange(ref _dbgKeys, 0);
                System.Diagnostics.Debug.WriteLine($"[InputRouter] 5s 窗口: 点击×{c} 按键×{k} 驱动={CurrentDrive.ToConfigString()}");
            }
        }
#endif
    }
}
