using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CornerBreak
{
    // The schema remains compatible with the Windows v0.2 settings. Each
    // platform stores its own file; a Mac app never writes inside its bundle.
    public sealed class BreakSettings
    {
        public int Version { get; set; } = 1;
        public bool Eyes { get; set; } = true;
        public bool Water { get; set; } = true;
        public bool Move { get; set; } = true;
        public bool AutoExpand { get; set; } = true;
        public bool FloatingMode { get; set; } = true;
        public int? FloatingX { get; set; }
        public int? FloatingY { get; set; }
        public string Mascot { get; set; } = "cat";
        public bool Paused { get; set; }
        public string PauseUntil { get; set; } = String.Empty;
        public string SkippedHour { get; set; } = String.Empty;

        public BreakSettings Clone() { return (BreakSettings)MemberwiseClone(); }
    }

    public static class SettingsStore
    {
        public static BreakSettings Load(string path)
        {
            if (!File.Exists(path))
            {
                if (Directory.Exists(path)) throw new IOException("CornerBreak 设置路径是文件夹：" + path);
                return new BreakSettings();
            }
            try { return Parse(File.ReadAllText(path, Encoding.UTF8)); }
            catch (Exception error) when (error is JsonException || error is IOException || error is UnauthorizedAccessException || error is InvalidDataException)
            {
                throw new InvalidDataException("无法读取 CornerBreak 设置 '" + path + "'：" + error.Message, error);
            }
        }

        // Recovery is read-only. A damaged original is kept for diagnosis;
        // only an explicit later user action writes a validated replacement.
        public static BreakSettings LoadWithRecovery(string path, out string warning)
        {
            warning = String.Empty;
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path) && File.Exists(path + ".bak"))
                {
                    var recovered = Load(path + ".bak");
                    warning = "设置文件缺失，已使用上次设置备份。";
                    return recovered;
                }
                return Load(path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException || error is InvalidDataException)
            {
                try
                {
                    if (File.Exists(path + ".bak"))
                    {
                        var recovered = Load(path + ".bak");
                        warning = "已使用上次设置备份；原文件已保留。";
                        return recovered;
                    }
                }
                catch (Exception backupError) when (backupError is IOException || backupError is UnauthorizedAccessException || backupError is JsonException || backupError is InvalidDataException) { }
                warning = "设置读取失败，暂用默认设置；原文件已保留。";
                return new BreakSettings();
            }
        }

        public static BreakSettings Parse(string json)
        {
            if (String.IsNullOrWhiteSpace(json)) throw InvalidField("设置", "JSON 对象");
            using (var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 }))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object) throw InvalidField("设置", "JSON 对象");
                var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in document.RootElement.EnumerateObject()) values[property.Name] = property.Value;
                var settings = new BreakSettings();
                JsonElement value;
                if (values.TryGetValue("Version", out value))
                {
                    int version;
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out version) || version != 1)
                        throw InvalidField("Version", "整数 1");
                }
                settings.Eyes = ReadBoolean(values, "Eyes", settings.Eyes);
                settings.Water = ReadBoolean(values, "Water", settings.Water);
                settings.Move = ReadBoolean(values, "Move", settings.Move);
                settings.AutoExpand = ReadBoolean(values, "AutoExpand", settings.AutoExpand);
                settings.FloatingMode = ReadBoolean(values, "FloatingMode", settings.FloatingMode);
                settings.FloatingX = ReadCoordinate(values, "FloatingX");
                settings.FloatingY = ReadCoordinate(values, "FloatingY");
                settings.Mascot = ReadString(values, "Mascot", settings.Mascot);
                settings.Paused = ReadBoolean(values, "Paused", settings.Paused);
                settings.PauseUntil = ReadString(values, "PauseUntil", settings.PauseUntil);
                settings.SkippedHour = ReadString(values, "SkippedHour", settings.SkippedHour);
                Validate(settings);
                return settings;
            }
        }

        public static void Save(string path, BreakSettings settings)
        {
            Validate(settings);
            BreakSettings snapshot = settings.Clone();
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, ".cornerbreak-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    byte[] preamble = new UTF8Encoding(true).GetPreamble();
                    stream.Write(preamble, 0, preamble.Length);
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }
                if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, fullPath + ".bak", true);
                else File.Move(temporaryPath, fullPath);
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }

        public static void Validate(BreakSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (settings.Version != 1) throw InvalidField("Version", "整数 1");
            if (settings.FloatingX.HasValue != settings.FloatingY.HasValue)
                throw InvalidField("FloatingX/FloatingY", "同时为空或同时为整数坐标");
            if (settings.FloatingX.HasValue && (Math.Abs((long)settings.FloatingX.Value) > 1000000 || Math.Abs((long)settings.FloatingY.Value) > 1000000))
                throw InvalidField("FloatingX/FloatingY", "-1000000 至 1000000 范围内的整数坐标");
            if (settings.Mascot != "cat" && settings.Mascot != "fish" && settings.Mascot != "companion" && settings.Mascot != "none")
                throw InvalidField("Mascot", "cat、fish、companion 或 none 字符串");
            if (settings.PauseUntil == null) throw InvalidField("PauseUntil", "时间字符串或空字符串");
            DateTime parsed;
            if (settings.PauseUntil.Length != 0 && !DateTime.TryParse(settings.PauseUntil, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                throw InvalidField("PauseUntil", "有效的时间字符串或空字符串");
            if (settings.SkippedHour == null || (settings.SkippedHour.Length != 0 && !DateTime.TryParseExact(settings.SkippedHour, "yyyy-MM-dd-HH", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)))
                throw InvalidField("SkippedHour", "有效的 yyyy-MM-dd-HH 时间或空字符串");
        }

        private static bool ReadBoolean(Dictionary<string, JsonElement> values, string name, bool fallback)
        {
            JsonElement value;
            if (!values.TryGetValue(name, out value)) return fallback;
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                throw InvalidField(name, "布尔值 true 或 false");
            return value.GetBoolean();
        }

        private static string ReadString(Dictionary<string, JsonElement> values, string name, string fallback)
        {
            JsonElement value;
            if (!values.TryGetValue(name, out value)) return fallback;
            if (value.ValueKind != JsonValueKind.String) throw InvalidField(name, "字符串");
            return value.GetString();
        }

        private static int? ReadCoordinate(Dictionary<string, JsonElement> values, string name)
        {
            JsonElement value;
            if (!values.TryGetValue(name, out value) || value.ValueKind == JsonValueKind.Null) return null;
            int coordinate;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out coordinate) || coordinate < -1000000 || coordinate > 1000000)
                throw InvalidField(name, "-1000000 至 1000000 范围内的整数坐标或 null");
            return coordinate;
        }

        private static InvalidDataException InvalidField(string name, string expected)
        { return new InvalidDataException("CornerBreak 设置字段 " + name + " 必须是" + expected + "。"); }
    }
}
