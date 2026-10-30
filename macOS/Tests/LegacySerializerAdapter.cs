// Only the shared legacy test's fixture serialization uses this old API.
// Production persistence is the real Core SettingsStore/System.Text.Json.
namespace System.Web.Script.Serialization
{
    internal sealed class JavaScriptSerializer
    {
        public string Serialize(object value) => System.Text.Json.JsonSerializer.Serialize(value);
    }
}
