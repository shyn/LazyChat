using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;

namespace LazyChat
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"严重错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "Microsoft YaHei, Noto Sans CJK SC, PingFang SC, WenQuanYi Micro Hei, Arial"
                })
                .LogToTrace();
        }
    }
}
