using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using TrustNTE.Helpers;

namespace TrustNTE.Views.Controls
{
    public partial class LogConsoleControl : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(LogConsoleControl),
            new PropertyMetadata("运行日志", OnTitleChanged));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogConsoleControl c)
            {
                c.TitleText.Text = e.NewValue as string;
            }
        }

        private readonly List<string> _pending = new List<string>();
        private readonly object _gate = new object();
        private DispatcherTimer? _flushTimer;
        public int MaxVisibleLines { get; set; } = 500;

        public LogConsoleControl()
        {
            InitializeComponent();

            _flushTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _flushTimer.Tick += (_, _) => FlushLogs();
            _flushTimer.Start();

            this.Unloaded += (_, _) =>
            {
                _flushTimer?.Stop();
            };
        }

        public void EnqueueLine(string line)
        {
            lock (_gate)
            {
                _pending.Add(line);
                if (_pending.Count > MaxVisibleLines)
                {
                    _pending.RemoveAt(0);
                }
            }
        }

        private void FlushLogs()
        {
            string[] batch;

            lock (_gate)
            {
                if (_pending.Count == 0) return;
                batch = _pending.ToArray();
                _pending.Clear();
            }

            foreach (var line in batch)
            {
                LogText.Inlines.Add(new Run(line) { Foreground = LogPalette.Pick(line) });
                LogText.Inlines.Add(new LineBreak());
            }

            while (LogText.Inlines.Count > MaxVisibleLines * 2)
            {
                if (LogText.Inlines.FirstInline != null)
                {
                    LogText.Inlines.Remove(LogText.Inlines.FirstInline);
                }
            }

            LogScrollViewer.ScrollToEnd();
        }

        public void Clear()
        {
            lock (_gate)
            {
                _pending.Clear();
            }
            LogText.Inlines.Clear();
        }
    }
}