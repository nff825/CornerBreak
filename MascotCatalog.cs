using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace CornerBreak
{
    public sealed class MascotOption
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public string Caption { get; private set; }
        public MascotOption(string id, string label, string caption)
        { Id = id; Label = label; Caption = caption; }
    }

    public sealed class MascotCatalog : IDisposable
    {
        public static readonly MascotOption[] Options = {
            new MascotOption("cat", "猫猫探头", "猫猫陪你歇一会儿"),
            new MascotOption("fish", "蓝色大肥鱼", "和蓝胖鱼一起放个空"),
            new MascotOption("companion", "二次元伙伴", "小伙伴喊你休息啦"),
            new MascotOption("none", "不显示角色", "")
        };
        public Dictionary<string, Bitmap> Images { get; private set; }

        public MascotCatalog()
        {
            Images = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
            var assembly = typeof(MascotCatalog).Assembly;
            foreach (var option in Options)
            {
                if (option.Id == "none") continue;
                // Images live inside the executable. Detach each bitmap from
                // its resource stream, then keep one shared copy per process.
                using (Stream stream = assembly.GetManifestResourceStream("CornerBreak.Mascots." + option.Id + ".png"))
                {
                    if (stream == null) continue;
                    try
                    {
                        using (Image source = Image.FromStream(stream)) Images.Add(option.Id, new Bitmap(source));
                    }
                    catch (ArgumentException) { }
                }
            }
        }

        public void Dispose()
        {
            foreach (var image in Images.Values) image.Dispose();
            Images.Clear();
        }
    }
}
