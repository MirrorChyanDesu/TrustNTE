using System;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Triggers
{
    public sealed class TriggerContext
    {
        public required AutomationService Automation { get; init; }
        public required ILogService Log { get; init; }
        public required IConfigService Config { get; init; }
        public required IInputService Input { get; init; }
        public required INteInteractionService NteInteraction { get; init; }
        public required IInputRouter Router { get; init; }
        public required IntPtr Hwnd { get; init; }
        public required AutoStoryTemplateService StoryTemplates { get; init; }

        public required Func<int, int, (int Px, int Py)> LegacyCoordMapper { get; init; }

        public YoloModelProvider? YoloProvider { get; init; }
    }
}
