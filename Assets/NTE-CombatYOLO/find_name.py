import win32gui

def get_windows():
    def callback(hwnd, extra):
        if win32gui.IsWindowVisible(hwnd):
            title = win32gui.GetWindowText(hwnd)
            cls = win32gui.GetClassName(hwnd)
            # 只要包含“异环”或者“Unreal”或者"NTE"的都打印出来
            if "异环" in title or "Unreal" in cls or "NTE" in title:
                print(f"找到窗口 -> 句柄: {hwnd} | 标题: 【{title}】 | 类名: 【{cls}】")
    win32gui.EnumWindows(callback, None)

get_windows()