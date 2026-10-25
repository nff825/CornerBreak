using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace CornerBreak
{
    public sealed class BreakSettings
    {
        public int Version { get; set; }
        public bool Eyes { get; set; }
        public bool Water { get; set; }
        public bool Move { get; set; }
        public bool AutoExpand { get; set; }
        public string Mascot { get; set; }
        public bool Paused { get; set; }
        public string PauseUntil { get; set; }
        public string SkippedHour { get; set; }

        public BreakSettings()
        {
            Version = 1;
            Eyes = Water = Move = AutoExpand = true;
            Mascot = "cat";
            PauseUntil = SkippedHour = String.Empty;
        }

        public BreakSettings Clone()
        {
            return (BreakSettings)MemberwiseClone();
        }
    }

    public static class SettingsStore
    {
        public static BreakSettings Load(string path)
        {
            if (!File.Exists(path))
            {
                if (Directory.Exists(path))
                    throw new IOException("CornerBreak 设置路径是文件夹：" + path);
                return new BreakSettings();
            }
            try
            {
                return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception error)
            {
                throw new InvalidDataException("无法读取 CornerBreak 设置 '" + path + "'：" + error.Message, error);
            }
        }

        public static BreakSettings Parse(string json)
        {
            if (String.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal))
                throw new InvalidDataException("CornerBreak 设置必须是一个 JSON 对象。");
            var raw = new JavaScriptSerializer().DeserializeObject(json) as IDictionary<string, object>;
            if (raw == null)
                throw new InvalidDataException("CornerBreak 设置必须是一个 JSON 对象。");

            // PowerShell 的设置属性查找不区分大小写；未知字段不进入保存白名单。
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in raw) values[pair.Key] = pair.Value;
            var result = new BreakSettings();
            object value;
            if (values.TryGetValue("Version", out value))
            {
                if (!(value is int || value is long) || Convert.ToInt64(value, CultureInfo.InvariantCulture) != 1)
                    throw InvalidField("Version", "整数 1");
                result.Version = 1;
            }
            result.Eyes = ReadBoolean(values, "Eyes", result.Eyes);
            result.Water = ReadBoolean(values, "Water", result.Water);
            result.Move = ReadBoolean(values, "Move", result.Move);
            result.AutoExpand = ReadBoolean(values, "AutoExpand", result.AutoExpand);
            result.Paused = ReadBoolean(values, "Paused", result.Paused);
            result.Mascot = ReadString(values, "Mascot", result.Mascot);
            result.PauseUntil = ReadString(values, "PauseUntil", result.PauseUntil);
            result.SkippedHour = ReadString(values, "SkippedHour", result.SkippedHour);
            Validate(result);
            return result;
        }

        public static void Save(string path, BreakSettings settings)
        {
            // 先完整验证，再创建目录或临时文件，保证无效数据不破坏现有设置。
            Validate(settings);
            BreakSettings snapshot = settings.Clone();
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, ".cornerbreak-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                string json = new JavaScriptSerializer().Serialize(snapshot);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(true));
                if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, fullPath + ".bak");
                else File.Move(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public static void Validate(BreakSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings", "CornerBreak 设置必须是一个 JSON 对象。");
            if (settings.Version != 1) throw InvalidField("Version", "整数 1");
            if (settings.Mascot != "cat" && settings.Mascot != "fish" && settings.Mascot != "companion" && settings.Mascot != "none")
                throw InvalidField("Mascot", "cat、fish、companion 或 none 字符串");
            if (settings.PauseUntil == null) throw InvalidField("PauseUntil", "时间字符串或空字符串");
            DateTime parsed;
            if (settings.PauseUntil.Length != 0 && !DateTime.TryParse(settings.PauseUntil,
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                throw InvalidField("PauseUntil", "有效的时间字符串或空字符串");
            if (settings.SkippedHour == null || (settings.SkippedHour.Length != 0 &&
                    !DateTime.TryParseExact(settings.SkippedHour, "yyyy-MM-dd-HH", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out parsed)))
                throw InvalidField("SkippedHour", "有效的 yyyy-MM-dd-HH 时间或空字符串");
        }

        private static bool ReadBoolean(IDictionary<string, object> values, string name, bool defaultValue)
        {
            object value;
            if (!values.TryGetValue(name, out value)) return defaultValue;
            if (!(value is bool)) throw InvalidField(name, "布尔值 true 或 false");
            return (bool)value;
        }

        private static string ReadString(IDictionary<string, object> values, string name, string defaultValue)
        {
            object value;
            if (!values.TryGetValue(name, out value)) return defaultValue;
            if (!(value is string)) throw InvalidField(name, "字符串");
            return (string)value;
        }

        private static InvalidDataException InvalidField(string name, string expected)
        {
            return new InvalidDataException("CornerBreak 设置字段 " + name + " 必须是" + expected + "。");
        }
    }
}
