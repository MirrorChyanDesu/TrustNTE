using System;
using System.Collections.ObjectModel;
using System.Windows;
using TrustNTE.ViewModels;

namespace TrustNTE.Services.Interfaces
{
    public interface IWindowService
    {
        void ShowLogWindow(ObservableCollection<string> logs, Action onClosed);
        void CloseLogWindow();
        void SendLogToWindows(string message);
        bool? ShowPromptDialog(PromptDialogViewModel viewModel);
        bool? ShowListConfigDialog(ListConfigDialogViewModel viewModel);
        bool? ShowUpdateWindow(CheckUpdateWindowViewModel viewModel);
        void ShowPreviewWindow(object dataContext, Action onClosed);
        void ClosePreviewWindow();
        void ShowOverlayWindow(object dataContext);
        void HideOverlayWindow();
        void UpdateOverlayPosition(int x, int y, int width, int height);
        void SetOverlayOpacity(double opacity);
        void SetOverlayBackgroundOpacity(double opacity);
        void SetOverlayTextOpacity(double opacity);
        void ShowAutoMusicDebugWindow(object dataContext);

        void ShowStartErrorWindow();
        void ShowCombatDebugWindow(CombatDebugWindowViewModel viewModel);
        void CloseCombatDebugWindow();
        void ShowMapMatchDebugWindow(MapMatchDebugWindowViewModel viewModel);
        void CloseMapMatchDebugWindow();

        Window? ShowAutoFishingPreviewWindow(AutoFishingPreviewViewModel viewModel);
        void CloseAutoFishingPreviewWindow();
    }
}