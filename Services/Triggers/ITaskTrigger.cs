using OpenCvSharp;

namespace TrustNTE.Services.Triggers
{
    public enum TriggerClass
    {
        /// <summary>保命类：不抢输入，掉线检测/UID识别等必须在任何时候生效</summary>
        Guardian,
        /// <summary>响应类：抢输入（右键、按键等），序列任务运行时必须静默</summary>
        Reactive
    }

    public interface ITaskTrigger
    {
        string Name { get; }
        bool IsEnabled { get; set; }

        TriggerClass Class => TriggerClass.Reactive;

        void OnCapture(Mat resizedMat, double scale);
        void DrawDebug(Mat frame);
    }
}