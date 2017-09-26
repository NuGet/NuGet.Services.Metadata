using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Protocol;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public static class JsonSerializerUtility
    {
        /// <summary>
        /// The <see cref="JsonSerializerSettings"/> to use.
        /// </summary>
        public static JsonSerializerSettings SerializerSettings => _serializerSettings.Value;
        private static Lazy<JsonSerializerSettings> _serializerSettings = new Lazy<JsonSerializerSettings>(() =>
        {
            var settings = new JsonSerializerSettings();

            settings.Converters.Add(new NuGetVersionConverter());
            settings.Converters.Add(new StringEnumConverter());

            return settings;
        });
    }
}