using System.Text.Encodings.Web;
using System.Text.Json;

namespace Vocabula.Spike;

public static class SpikeOutputJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static string Serialize(SpikeOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return JsonSerializer.Serialize(output, Options);
    }
}
