using System.Text.Json;

namespace HrServiceDesk.Domain.Common;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
