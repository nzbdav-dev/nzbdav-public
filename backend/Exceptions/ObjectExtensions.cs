using System.Text.Json;

namespace NzbWebDAV.Exceptions;

public static class ObjectExtensions
{
    public static string ToJson(this object obj)
    {
        return JsonSerializer.Serialize(obj);
    }
}