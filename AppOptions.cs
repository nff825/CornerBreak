using System;
using System.Globalization;

namespace CornerBreak
{
    public sealed class AppOptions
    {
        public bool Preview { get; set; }
        public bool Show { get; set; }
        public bool ExitRequested { get; set; }
        public DateTime? PreviewAt { get; set; }
        public string PreviewMascot { get; set; }
        public string PreviewScenario { get; set; }
        public string RenderPreview { get; set; }
        public string RenderSettings { get; set; }
        public string RenderWidget { get; set; }

        public AppOptions()
        {
            PreviewMascot = "cat";
            PreviewScenario = "Active";
        }

        public static AppOptions Parse(string[] args)
        {
            var options = new AppOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i].TrimStart('-').Replace("-", "").ToLowerInvariant();
                switch (key)
                {
                    case "preview": options.Preview = true; break;
                    case "show": options.Show = true; break;
                    case "exit": options.ExitRequested = true; break;
                    case "previewat":
                        options.PreviewAt = DateTime.Parse(Value(args, ref i), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                        break;
                    case "previewmascot": options.PreviewMascot = Value(args, ref i); break;
                    case "previewscenario": options.PreviewScenario = Value(args, ref i); break;
                    case "renderpreview": options.RenderPreview = Value(args, ref i); break;
                    case "rendersettings": options.RenderSettings = Value(args, ref i); break;
                    case "renderwidget": options.RenderWidget = Value(args, ref i); break;
                    default: throw new ArgumentException("未知参数：" + args[i]);
                }
            }
            if (Array.IndexOf(new[] { "cat", "fish", "companion", "none" }, options.PreviewMascot) < 0)
                throw new ArgumentException("预览角色必须是 cat、fish、companion 或 none。");
            if (Array.IndexOf(new[] { "Active", "Waiting", "Skipped", "Paused", "Disabled" }, options.PreviewScenario) < 0)
                throw new ArgumentException("预览状态无效。");
            if ((!String.IsNullOrEmpty(options.RenderPreview) || !String.IsNullOrEmpty(options.RenderSettings) || !String.IsNullOrEmpty(options.RenderWidget)) && !options.Preview)
                throw new ArgumentException("导出预览图片需要 --preview，以免影响日常设置。");
            int renderCount = (String.IsNullOrEmpty(options.RenderPreview) ? 0 : 1) +
                (String.IsNullOrEmpty(options.RenderSettings) ? 0 : 1) + (String.IsNullOrEmpty(options.RenderWidget) ? 0 : 1);
            if (renderCount > 1)
                throw new ArgumentException("每次只导出一种预览图片。");
            if (options.ExitRequested && (options.Preview || options.Show))
                throw new ArgumentException("--exit 不可与 --preview 或 --show 同时使用。");
            return options;
        }

        private static string Value(string[] args, ref int index)
        {
            if (++index >= args.Length || String.IsNullOrWhiteSpace(args[index]))
                throw new ArgumentException("参数缺少值。");
            return args[index];
        }
    }
}
