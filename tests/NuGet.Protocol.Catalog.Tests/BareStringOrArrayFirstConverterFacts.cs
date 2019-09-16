// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Xunit;

namespace NuGet.Protocol.Catalog
{
    public class BareStringOrArrayFirstConverterFacts
    {
        private class SerializationTestObject
        {
            [JsonProperty]
            [JsonConverter(typeof(BareStringOrArrayFirstConverter))]
            public string StringProperty { get; set; }
        }

        [Fact]
        public void ReadsBareStrings()
        {
            const string input = "{\"stringProperty\": \"foo\"}";
            var obj = JsonConvert.DeserializeObject<SerializationTestObject>(input);
            Assert.Equal("foo", obj.StringProperty);
        }

        [Fact]
        public void ReadsStringArrays()
        {
            const string input = "{\"stringProperty\": [\"foo\", \"bar\"]}";
            var obj = JsonConvert.DeserializeObject<SerializationTestObject>(input);
            Assert.Equal("foo", obj.StringProperty);
        }

        [Fact]
        public void ReturnsNullOnEmptyArrays()
        {
            const string input = "{\"stringProperty\": []}";
            var obj = JsonConvert.DeserializeObject<SerializationTestObject>(input);
            Assert.Null(obj.StringProperty);
        }

        [Fact]
        public void WritesStrings()
        {
            var obj = new SerializationTestObject { StringProperty = "foo" };
            var str = JsonConvert.SerializeObject(obj, Formatting.None);
            Assert.Equal("{\"StringProperty\":\"foo\"}", str);
        }
    }
}
