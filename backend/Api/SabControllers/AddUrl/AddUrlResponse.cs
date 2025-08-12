using System.Text.Json.Serialization;

namespace NzbWebDAV.Api.SabControllers.AddUrl;

public class AddUrlResponse : SabBaseResponse
{
    [JsonPropertyName("nzo_ids")]
    public List<string> NzoIds { get; set; } = [];
}