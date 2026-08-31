using System.Text.Encodings.Web;
using System.Text.Json;
using Vocabula.Core.Models;

namespace Vocabula.Core;

public static class NotebookProcessingResultJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static string Serialize(NotebookProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, Options);
    }
}
