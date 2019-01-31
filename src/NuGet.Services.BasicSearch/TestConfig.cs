using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace NuGet.Services.BasicSearch
{
    public class TestConfig
    {
        public static volatile TestConfig _instance = null;
        private static readonly object _lockInstance = new object();
        private ILogger _logger;
        private int readConfigDelaySeconds = 60;

        public static void Init(ILoggerFactory loggerFactory)
        {
            if (_instance == null)
            {
                lock (_lockInstance)
                {
                    if (_instance == null)
                    {
                        _instance = new TestConfig(loggerFactory);
                        _instance.ReadAndUpdateConfig();
                    }
                }
            }
        }

        public static TestConfig GetInstance()
        {
            return _instance;
        }

        public async Task WaitIfConfigured()
        {
            int secondsDelay = _instance.DelaySeconds;
            if (secondsDelay > 0)
            {
                await Task.Delay(secondsDelay * 1000);
            }
        }

        public string TestHttpStatusCode { get; set; }

        public int PercentThrow { get; set; }

        public int DelaySeconds { get; set; }

        public TestConfig(ILoggerFactory loggerFactory)
        {
            TestHttpStatusCode = string.Empty;
            DelaySeconds = -1;
            PercentThrow = 0;
            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<TestConfig>();
            }
        }

        public Task ReadAndUpdateConfig()
        {
            string configUrl = @"https://cmanutest.blob.core.windows.net/content2/config.json";
            var task = Task.Factory.StartNew(
                () =>
                {
                    while (true)
                    {
                        try
                        {
                            using (HttpClient client = new HttpClient())
                            {
                                var response = client.GetStringAsync(configUrl).Result;
                                if (response != null)
                                {
                                    var currentConfig = JsonConvert.DeserializeObject<TestConfig>(response);
                                    _instance.TestHttpStatusCode = currentConfig.TestHttpStatusCode;
                                    _instance.DelaySeconds = currentConfig.DelaySeconds;
                                    _instance.PercentThrow = currentConfig.PercentThrow;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogCritical(new EventId(), ex, ex.Message);
                        }
                        Thread.Sleep(readConfigDelaySeconds * 1000);
                    }
                }, TaskCreationOptions.LongRunning);

            return task;
        }

        public HttpResponseMessage Map()
        {
            int chance = RandomGen.Next(1, 100);
            if (chance < PercentThrow)
            {
                _logger.LogInformation($"Chance {chance} met. The call is highjacked. Will return {TestHttpStatusCode}");
                string ex = TestHttpStatusCode;

                switch (ex)
                {
                    case "500":
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    case "502":
                        return new HttpResponseMessage(HttpStatusCode.BadGateway);
                    case "503":
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                    case "504":
                        return new HttpResponseMessage(HttpStatusCode.GatewayTimeout);
                    case "408":
                        return new HttpResponseMessage(HttpStatusCode.GatewayTimeout);
                    default:
                        return null;
                }
            }
            else
            {
                _logger.LogInformation($"Chance {chance} not met.");
                return null;
            }
        }


    }

    //https://blogs.msdn.microsoft.com/pfxteam/2009/02/19/getting-random-numbers-in-a-thread-safe-way/
    public static class RandomGen
    {
        private static Random _global = new Random();
        [ThreadStatic]
        private static Random _local;

        public static int Next(int minValue, int maxValue)
        {
            Random inst = _local;
            if (inst == null)
            {
                int seed;
                lock (_global) seed = _global.Next();
                _local = inst = new Random(seed);
            }
            return inst.Next(minValue, maxValue);
        }
    }
}