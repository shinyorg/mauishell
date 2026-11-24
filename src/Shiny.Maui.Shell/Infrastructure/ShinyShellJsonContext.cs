using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shiny.Infrastructure;

/// <summary>
/// Source-generated JSON metadata for the types the generated AI tools exchange with a model: the
/// route list GetRoutes returns and the navigate tool's arguments. The generated tools put it first
/// in their serializer's resolver chain, so they work in trimmed and AOT apps, where reflection
/// cannot read <see cref="GeneratedRouteInfo"/>'s constructor parameter names.
/// </summary>
[JsonSerializable(typeof(GeneratedRouteInfo[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(string))]
public partial class ShinyShellJsonContext : JsonSerializerContext;
