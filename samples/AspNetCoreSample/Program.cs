﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using WeihanLi.Common.Aspect;
using WeihanLi.Common.Event;

namespace AspNetCoreSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(builder =>
                {
                    builder.UseStartup<Startup>();
                })
                //.UseServiceProviderFactory()
                .UseFluentAspectServiceProviderFactory(options =>
                {
                    options
                        .InterceptType<IEventPublisher>()
                        .With<EventPublishLogInterceptor>();
                }, t => t.Namespace?.StartsWith("WeihanLi") == false)
                .Build()
                .Run();
        }
    }
}
