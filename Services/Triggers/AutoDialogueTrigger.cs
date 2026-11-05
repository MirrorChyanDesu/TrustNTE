using OpenCvSharp;
using System;
using TrustNTE.Helpers;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Triggers
{
    public class AutoDialogueTrigger : ITaskTrigger
    {
        private enum StoryActionType
        {
            Click,
            KeyPress,
            DialogueChoice
        }

        private sealed class StoryRule
        {
            public string DisplayName { get; init; } = string.Empty;
            public string TemplateGroup { get; init; } = string.Empty;
            public Rect SearchRoi { get; init; }
            public string? SecondTemplateGroup { get; init; }
            public Rect? SecondSearchRoi { get; init; }
            public StoryActionType Action { get; init; }
            public int VirtualKey { get; init; }
            public int? ChoiceClickX { get; init; }
            public int? ChoiceClickY { get; init; }

            /// <summary>是否需要开启了“自动传送”才执行</summary>
            public bool RequireTeleport { get; init; } = false;
        }

        private static readonly StoryRule[] StoryRules =
        {
            // ── 传送模块（受界面开关控制） ──
            new StoryRule { DisplayName = "传送", TemplateGroup = "Nte_Teleport", SearchRoi = new Rect(1488, 940, 68, 44), Action = StoryActionType.Click, RequireTeleport = true },
            new StoryRule { DisplayName = "传送", TemplateGroup = "Nte_Teleport", SearchRoi = new Rect(1615, 937, 66, 45), Action = StoryActionType.Click, RequireTeleport = true },

            // ── 新增：运通全城完成派送 ──
            new StoryRule { DisplayName = "运通全城完成派送", TemplateGroup = "Nte_YunTongCitywideDeliveryCompleted", SearchRoi = new Rect(1153, 873, 63, 43), Action = StoryActionType.Click },

            // ── 新增：退出消息列表（End + Message 双模板 AND） ──
            new StoryRule
            {
                DisplayName = "退出消息列表",
                TemplateGroup = "Nte_DialogueEnd",
                SearchRoi = new Rect(1222, 871, 71, 40),
                SecondTemplateGroup = "Nte_DialogueMessage",
                SecondSearchRoi = new Rect(108, 32, 76, 53),
                Action = StoryActionType.KeyPress,
                VirtualKey = KeyCode.Esc
            },

            // ── 原有的其他规则 ──
            new StoryRule { DisplayName = "确认跳过", TemplateGroup = "Nte_ConfirmSkip", SearchRoi = new Rect(1151, 614, 189, 110), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "概述确认", TemplateGroup = "Nte_OverviewConfirm", SearchRoi = new Rect(1115, 622, 227, 126), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "剧情单独跳过", TemplateGroup = "Nte_StorySkip", SearchRoi = new Rect(1814, 26, 84, 79), Action = StoryActionType.KeyPress, VirtualKey = KeyCode.Esc },
            new StoryRule { DisplayName = "剧情加感叹号加跳过", TemplateGroup = "Nte_StorySkipExclamation", SearchRoi = new Rect(1676, 25, 223, 80), Action = StoryActionType.KeyPress, VirtualKey = KeyCode.Esc },
            new StoryRule { DisplayName = "按下V调整时间", TemplateGroup = "Nte_PressVAdjustTime", SearchRoi = new Rect(76, 285, 214, 85), Action = StoryActionType.KeyPress, VirtualKey = KeyCode.V },
            new StoryRule { DisplayName = "调整时间确认", TemplateGroup = "Nte_AdjustTimeConfirm", SearchRoi = new Rect(1061, 648, 202, 125), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "消息交互", TemplateGroup = "Nte_MessageInteract", SearchRoi = new Rect(899, 631, 334, 256), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "前往任务", TemplateGroup = "Nte_GoTask", SearchRoi = new Rect(1387, 732, 198, 131), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "追踪任务", TemplateGroup = "Nte_TrackTask", SearchRoi = new Rect(1521, 963, 217, 84), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "按下V追踪任务", TemplateGroup = "Nte_PressVTrackTask", SearchRoi = new Rect(77, 288, 165, 95), Action = StoryActionType.KeyPress, VirtualKey = KeyCode.V },
            new StoryRule { DisplayName = "取消追踪", TemplateGroup = "Nte_CancelTrackTask", SearchRoi = new Rect(1549, 967, 168, 66), Action = StoryActionType.KeyPress, VirtualKey = KeyCode.Esc },
            new StoryRule { DisplayName = "领取奖励", TemplateGroup = "Nte_GetReward", SearchRoi = new Rect(1566, 260, 232, 609), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "点击空白处", TemplateGroup = "Nte_ClickBlank", SearchRoi = new Rect(837, 935, 279, 54), Action = StoryActionType.KeyPress, VirtualKey = KeyCode.Esc },
            new StoryRule { DisplayName = "副本领取", TemplateGroup = "Nte_DungeonClaim", SearchRoi = new Rect(1156, 554, 166, 76), Action = StoryActionType.KeyPress, VirtualKey = KeyCode.F },
            new StoryRule { DisplayName = "棋盘跳过", TemplateGroup = "Nte_SkipChessboard", SearchRoi = new Rect(1822, 21, 74, 71), Action = StoryActionType.KeyPress, VirtualKey = KeyCode.Esc },
            new StoryRule { DisplayName = "抽卡点击", TemplateGroup = "Nte_CloseChessboard", SearchRoi = new Rect(859, 1011, 213, 39), Action = StoryActionType.KeyPress, VirtualKey = KeyCode.Esc },
            new StoryRule { DisplayName = "下一页", TemplateGroup = "Nte_NextPage", SearchRoi = new Rect(1521, 759, 153, 79), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "领取礼物奖励", TemplateGroup = "Nte_GiftClaim", SearchRoi = new Rect(1171, 811, 311, 83), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "角色礼物领取", TemplateGroup = "Nte_CharGiftClaim", SearchRoi = new Rect(1636, 172, 121, 818), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "角色礼物领取新增", TemplateGroup = "Nte_CloseGiftClaim", SearchRoi = new Rect(822, 1009, 303, 53), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "派送完成哈嗦", TemplateGroup = "Nte_HathorDelivery", SearchRoi = new Rect(957, 675, 366, 251), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "派送完成", TemplateGroup = "Nte_DeliveryComplete", SearchRoi = new Rect(957, 675, 366, 251), Action = StoryActionType.Click },
            new StoryRule { DisplayName = "幽灵号点击空白关闭", TemplateGroup = "Nte_GhostClickBlankClose", SearchRoi = new Rect(837, 662, 239, 387), Action = StoryActionType.Click },

            new StoryRule
            {
                DisplayName = "对话选项",
                TemplateGroup = "Nte_DialogEllipsis",
                SearchRoi = new Rect(1747, 33, 51, 59),
                SecondTemplateGroup = "Nte_Forbid",
                SecondSearchRoi = new Rect(1826, 45, 39, 41),
                Action = StoryActionType.DialogueChoice,
                VirtualKey = KeyCode.F,
                ChoiceClickX = 1346,
                ChoiceClickY = 807
            },
        };

        private const int ActionCooldownMs = 500;
        public string Name => "自动剧情";
        public bool IsEnabled { get; set; } = false;

        private readonly IConfigService _configService;
        private readonly IInputRouter _router;
        private readonly ILogService _logService;
        private readonly AutoStoryTemplateService _storyTemplates;
        private readonly IntPtr _hwnd;

        private DateTime _lastActionTime = DateTime.MinValue;
        private Rect? _lastHitRect;
        private string _lastHitGroup = string.Empty;
        private DateTime _lastHitTime = DateTime.MinValue;

        public AutoDialogueTrigger(
            IConfigService configService,
            IInputRouter router,
            ILogService logService,
            AutoStoryTemplateService storyTemplates,
            IntPtr hwnd)
        {
            _configService = configService;
            _router = router;
            _logService = logService;
            _storyTemplates = storyTemplates;
            _hwnd = hwnd;
        }

        public void DrawDebug(Mat drawMat)
        {
            if (!IsEnabled) return;
            foreach (var rule in StoryRules)
            {
                Cv2.Rectangle(drawMat, rule.SearchRoi, new Scalar(0, 255, 255), 1);
                if (rule.SecondSearchRoi.HasValue)
                    Cv2.Rectangle(drawMat, rule.SecondSearchRoi.Value, new Scalar(255, 0, 255), 1);
            }
            if (_lastHitRect.HasValue && (DateTime.Now - _lastHitTime).TotalSeconds <= 1)
            {
                Cv2.Rectangle(drawMat, _lastHitRect.Value, new Scalar(0, 255, 0), 2);
                Cv2.PutText(drawMat, _lastHitGroup,
                    new Point(_lastHitRect.Value.X, Math.Max(12, _lastHitRect.Value.Y - 5)),
                    HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);
            }
        }

        public void OnCapture(Mat resizedMat, double scale)
        {
            var config = _configService.Load();
            if (!config.AutoDialogueEnabled) return;
            if ((DateTime.Now - _lastActionTime).TotalMilliseconds <= ActionCooldownMs) return;

            foreach (var rule in StoryRules)
            {
                // 【核心修改点】：如果这条规则受传送开关控制，且开关关闭，直接跳过
                if (rule.RequireTeleport && !config.AutoTeleportEnabled) continue;

                Rect? hitRect = _storyTemplates.MatchAny(
                    resizedMat, rule.SearchRoi, rule.TemplateGroup, AutoStoryTemplateService.MatchThreshold);
                if (hitRect == null) continue;

                // 复合规则双模板 AND
                if (rule.SecondTemplateGroup != null && rule.SecondSearchRoi.HasValue)
                {
                    Rect? secondHit = _storyTemplates.MatchAny(
                        resizedMat, rule.SecondSearchRoi.Value, rule.SecondTemplateGroup, AutoStoryTemplateService.MatchThreshold);
                    if (secondHit == null) continue;
                }

                ExecuteRule(rule, hitRect.Value, config);

                _lastActionTime = DateTime.Now;
                _lastHitRect = hitRect.Value;
                _lastHitGroup = rule.TemplateGroup;
                _lastHitTime = DateTime.Now;
                return;
            }
        }

        private void ExecuteRule(StoryRule rule, Rect hitRect, Models.AppConfig config)
        {
            string head = BuildRoiHead(rule);
            string action;

            switch (rule.Action)
            {
                case StoryActionType.Click:
                    {
                        int centerX = hitRect.X + hitRect.Width / 2;
                        int centerY = hitRect.Y + hitRect.Height / 2;
                        var (px, py) = _router.ClickLogical(_hwnd, centerX, centerY);
                        action = _router.ClickTag(px, py, "左键点击");
                        break;
                    }
                case StoryActionType.KeyPress:
                    {
                        _router.KeyPress(_hwnd, rule.VirtualKey, 50);
                        action = _router.KeyTag(rule.VirtualKey);
                        break;
                    }
                case StoryActionType.DialogueChoice:
                default:
                    {
                        string strategy = config.AutoDialogueOptionStrategy;
                        if (strategy == "第二个" && rule.ChoiceClickX.HasValue && rule.ChoiceClickY.HasValue)
                        {
                            var (px, py) = _router.ClickLogical(_hwnd, rule.ChoiceClickX.Value, rule.ChoiceClickY.Value);
                            action = _router.ClickTag(px, py, "左键点击") + "（策略:第二个）";
                        }
                        else
                        {
                            _router.KeyPress(_hwnd, rule.VirtualKey, 50);
                            action = _router.KeyTag(rule.VirtualKey) + $"（策略:{strategy}）";
                        }
                        break;
                    }
            }

            EmitStoryLog(head, action, config.AutoStoryLogMode);
        }

        private static string BuildRoiHead(StoryRule rule)
        {
            if (rule.SecondTemplateGroup == null || !rule.SecondSearchRoi.HasValue)
            {
                return $"[自动剧情] {rule.DisplayName} {rule.TemplateGroup} 判断区域 " +
                       $"{rule.SearchRoi.X},{rule.SearchRoi.Y},{rule.SearchRoi.Right},{rule.SearchRoi.Bottom}," +
                       $"宽高({rule.SearchRoi.Width},{rule.SearchRoi.Height})";
            }

            var b = rule.SecondSearchRoi.Value;
            return $"[自动剧情] {rule.DisplayName} {rule.TemplateGroup}+{rule.SecondTemplateGroup} 判断区域A " +
                   $"{rule.SearchRoi.X},{rule.SearchRoi.Y},{rule.SearchRoi.Right},{rule.SearchRoi.Bottom}," +
                   $"宽高({rule.SearchRoi.Width},{rule.SearchRoi.Height}) 判断区域B " +
                   $"{b.X},{b.Y},{b.Right},{b.Bottom},宽高({b.Width},{b.Height})";
        }

        private void EmitStoryLog(string head, string action, string logMode)
        {
            if (logMode == "双线输出")
            {
                _logService.LogInfo(head);
                _logService.LogInfo(action);
            }
            else
            {
                _logService.LogInfo($"{head} {action}");
            }
        }
    }
}