using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AduosSyncServices.ServicesManager.Services
{
    public class ConfigService
    {
        public IConfigurationRoot LoadAppSettings(string path)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(path) ?? ".")
                .AddJsonFile(Path.GetFileName(path), optional: false, reloadOnChange: true);

            return builder.Build();
        }

        public void SaveAppSettings(string path, Dictionary<string, string> values)
        {
            var json = File.ReadAllText(path);
            var jsonObject = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();

            foreach (var kvp in values)
            {
                var parts = kvp.Key.Split(':');
                JsonObject current = jsonObject;

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (current[parts[i]] == null || current[parts[i]].GetType() != typeof(JsonObject))
                        current[parts[i]] = new JsonObject();
                    current = current[parts[i]].AsObject();
                }

                current.Remove(parts[^1]);

                if (kvp.Value.StartsWith("{") || kvp.Value.StartsWith("["))
                {
                    current[parts[^1]] = JsonNode.Parse(kvp.Value);
                }
                else
                {
                    current[parts[^1]] = JsonValue.Create(kvp.Value);
                }
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(path, jsonObject.ToJsonString(options));
        }
    }
}
