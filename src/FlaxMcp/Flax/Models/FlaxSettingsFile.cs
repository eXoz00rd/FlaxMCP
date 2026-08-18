using System.Text.Json.Nodes;

namespace FlaxMcp.Flax.Models;

public sealed record FlaxSettingsFile(
    string Name,
    string TypeName,
    JsonNode? Data
);
