using System;
using System.IO;

namespace CornerBreak
{
    public static class AppPaths
    {
        public static string GetDataDirectory()
        {
            if (OperatingSystem.IsMacOS())
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (String.IsNullOrWhiteSpace(home)) home = Environment.GetEnvironmentVariable("HOME");
                if (String.IsNullOrWhiteSpace(home)) throw new IOException("无法确定当前用户的主目录。");
                return Path.Combine(home, "Library", "Application Support", "CornerBreak");
            }
            // Windows is used only to verify this new desktop front end.
            // Its settings must not replace the stable WinForms application's.
            string data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (String.IsNullOrWhiteSpace(data)) throw new IOException("无法确定当前用户的应用数据目录。");
            return Path.Combine(data, "CornerBreak.TestDesktop");
        }

        public static string GetSettingsPath() { return Path.Combine(GetDataDirectory(), "settings.json"); }
        public static string GetLogPath() { return Path.Combine(GetDataDirectory(), "CornerBreak.log"); }
    }
}
