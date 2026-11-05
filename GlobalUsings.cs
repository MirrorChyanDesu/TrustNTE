// 终极别名，强制解决 WPF 与 WinForms 的所有命名冲突
global using Application = System.Windows.Application;
global using UserControl = System.Windows.Controls.UserControl;
global using Binding = System.Windows.Data.Binding;
global using MessageBox = System.Windows.MessageBox;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
global using Point = OpenCvSharp.Point; 
global using PropertyMetadata = System.Windows.PropertyMetadata; 
                                                               