// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NgTests.Infrastructure
{
    public static class JObjectAssert
    {
        public static void AreEqual(JObject sourceJObject, JObject targetJObject, string[] ignoreProperties, string message)
        {
            var differences = GenerateDifferences(sourceJObject, targetJObject, ignoreProperties).ToList();
            if (differences.Count > 0)
            {
                throw new Exception(string.Format("{0} - Objects are not equal. Differences:\r\n{1}", 
                    message,
                    string.Join("\r\n", differences)));
            }
        }

        private static IEnumerable<Difference> GenerateDifferences(JObject sourceJObject, JObject targetJObject, string[] ignoreProperties)
        {
            if (!JToken.DeepEquals(sourceJObject, targetJObject))
            {
                foreach (KeyValuePair<string, JToken> sourceProperty in sourceJObject)
                {
                    if (!ignoreProperties.Contains(sourceProperty.Key))
                    {
                        JProperty targetProp = targetJObject.Property(sourceProperty.Key);

                        if (!JToken.DeepEquals(sourceProperty.Value, targetProp.Value))
                        {
                            // Compare based on type
                            if (sourceProperty.Value.Type == JTokenType.Array && targetProp.Value.Type == JTokenType.Array)
                            {
                                // Array
                                var sourceJArray = sourceProperty.Value as JArray;
                                var targetJArray = targetProp.Value as JArray;

                                if (sourceJArray.Count != targetJArray.Count)
                                {
                                    yield return new Difference
                                    {
                                        Path = "(array length) " + sourceProperty.Value.Path,
                                        Left = "Count: " + sourceJArray.Count,
                                        Right = "Count: " + targetJArray.Count
                                    };
                                }

                                for (int i = 0; i < Math.Min(sourceJArray.Count, targetJArray.Count); i++)
                                {
                                    var a = sourceJArray[i] as JObject;
                                    var b = targetJArray[i] as JObject;

                                    foreach (var difference in GenerateDifferences(a, b, ignoreProperties))
                                    {
                                        yield return difference;
                                    }
                                }
                            }
                            else if (sourceProperty.Value.Type == JTokenType.Object && targetProp.Value.Type == JTokenType.Object)
                            {
                                // Object
                                var a = sourceProperty.Value as JObject;
                                var b = targetProp.Value as JObject;
                                if (a != null && b != null)
                                {
                                    foreach (var difference in GenerateDifferences(a, b, ignoreProperties))
                                    {
                                        yield return difference;
                                    }
                                }
                            }
                            else
                            {
                                // Value comparison
                                yield return new Difference
                                {
                                    Path = sourceProperty.Value.Path,
                                    Left = sourceProperty.Value.ToString(),
                                    Right = targetProp.Value.ToString()
                                };
                            }
                        }
                    }
                }
            }
        }

        public class Difference
        {
            public string Path { get; set; }
            public string Left { get; set; }
            public string Right { get; set; }

            public override string ToString()
            {
                return Path + ": { left: " + Left + ", right: " + Right + "}";
            }
        }
    }
}