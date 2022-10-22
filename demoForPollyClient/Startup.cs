using GrpcServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Bulkhead;
using Polly.CircuitBreaker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static GrpcServices.OrderGrpc;

namespace demoForPollyClient
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddGrpcClient<OrderGrpc.OrderGrpcClient>(grpcClientFactoryOptions =>
            {
                grpcClientFactoryOptions.Address = new Uri("https://localhost:5001");
            }).ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var handler = new SocketsHttpHandler();
                // 允许无效或自签名证书
                handler.SslOptions.RemoteCertificateValidationCallback = (a, b, c, d) => true;
                return handler;
            })
            .AddTransientHttpErrorPolicy(policyBuilder =>
            {
                // 当服务报错，重试多少次
                // 当应用程序抛HttpRequestException或者响应500、408才会去执行这个重试策略
                //return policyBuilder.RetryAsync(3);

                // 当遇到HttpRequestException或者响应500、408时，等待10*N秒，然后重试，总共重试3次
                //return policyBuilder.WaitAndRetryAsync(3, retryIndex =>
                //{
                //    return TimeSpan.FromSeconds(retryIndex * 10);
                //});

                // 当遇到HttpRequestException或者响应500、408时，等待10*N秒，然后重试，直到响应成功
                return policyBuilder.WaitAndRetryForeverAsync(retryIndex =>
                {
                    return TimeSpan.FromSeconds(retryIndex * 10);
                });
            });

            var reg = services.AddPolicyRegistry();
            // 添加定义永远重试的策略
            reg.Add("RetryForever", Policy.HandleResult<HttpResponseMessage>(message =>
            {
                // 当响应代码为201的时候满足策略触发条件
                return message.StatusCode == System.Net.HttpStatusCode.Created;
            }).RetryForeverAsync());

            // 给OrderClient添加这个自定义的策略
            services.AddHttpClient("OrderClient").AddPolicyHandlerFromRegistry("RetryForever");

            // 给OrderClient V2添加这个自定义的策略
            services.AddHttpClient("OrderClient-V2").AddPolicyHandlerFromRegistry((registry, message) =>
            {
                // 当请求的方式是Get的时候，应用RetryForever策略，否则不执行任何策略
                return message.Method == HttpMethod.Get ?
                    registry.Get<IAsyncPolicy<HttpResponseMessage>>("RetryForever") :
                    Policy.NoOpAsync<HttpResponseMessage>();
            });

            // 当处理Exception异常时应用重试策略
            Policy.Handle<Exception>().WaitAndRetryAsync(3, retryIndex =>
            {
                return TimeSpan.FromSeconds(retryIndex * 10);
            });

            // Policy.Handle<Exception>().Fallback

            services.AddHttpClient("OrderClient-V3")
                // 添加一个策略
                .AddPolicyHandler(Policy<HttpResponseMessage>.Handle<HttpRequestException>()
                // 定义熔断策略
                .CircuitBreakerAsync
                (
                    // 报错多次以后进行熔断，这里设置10次
                    handledEventsAllowedBeforeBreaking: 10,
                    // 熔断的时间，这里设置10秒
                    durationOfBreak: TimeSpan.FromSeconds(10),
                    // 当发生熔断时触发的一个事件
                    onBreak: (r, t) => { },
                    // 当熔断恢复时触发的一个事件
                    onReset: () => { },
                    // 在恢复之前进行验证服务是否可用的请求时，打一部分流量去验证我们的服务是否可用的事件
                    onHalfOpen: () => { }
                ));

            services.AddHttpClient("OrderClient-V4")
                // 添加一个策略
                .AddPolicyHandler(Policy<HttpResponseMessage>.Handle<HttpRequestException>()
                // 定义高级熔断策略，支持按采样和失败比例来触发
                .AdvancedCircuitBreakerAsync
                (
                    // 失败的比例，有多少比例的请求失败时进行熔断，这里设置80%
                    failureThreshold: 0.8,
                    // 采样的时间，多少时间范围内请求的80%的失败，这里设置10秒
                    samplingDuration: TimeSpan.FromSeconds(10),
                    // 最小的吞吐量，当请求量比较小的时候，10秒之内采样如果失败两三个请求就会造成80%的失败，所以我们设置请求数最少有100个的时候，才会去触发熔断策略
                    minimumThroughput: 100,
                    // 熔断的时间，这里设置10秒
                    durationOfBreak: TimeSpan.FromSeconds(10),
                    // 当发生熔断时触发的一个事件
                    onBreak: (r, t) => { },
                    // 当熔断恢复时触发的一个事件
                    onReset: () => { },
                    // 在恢复之前进行验证服务是否可用的请求时，打一部分流量去验证我们的服务是否可用的事件
                    onHalfOpen: () => { }
                ));

            var breakPolicy = Policy<HttpResponseMessage>.Handle<HttpRequestException>()
                // 定义高级熔断策略，支持按采样和失败比例来触发
                .AdvancedCircuitBreakerAsync
                (
                    // 失败的比例，有多少比例的请求失败时进行熔断，这里设置80%
                    failureThreshold: 0.8,
                    // 采样的时间，多少时间范围内请求的80%的失败，这里设置10秒
                    samplingDuration: TimeSpan.FromSeconds(10),
                    // 最小的吞吐量，当请求量比较小的时候，10秒之内采样如果失败两三个请求就会造成80%的失败，所以我们设置请求数最少有100个的时候，才会去触发熔断策略
                    minimumThroughput: 100,
                    // 熔断的时间，这里设置10秒
                    durationOfBreak: TimeSpan.FromSeconds(10),
                    // 当发生熔断时触发的一个事件
                    onBreak: (r, t) => { },
                    // 当熔断恢复时触发的一个事件
                    onReset: () => { },
                    // 在恢复之前进行验证服务是否可用的请求时，打一部分流量去验证我们的服务是否可用的事件
                    onHalfOpen: () => { }
                );

            // 定义一个友好的响应
            var message = new HttpResponseMessage 
            { 
                Content = new StringContent("{}"),
            };
            // 针对熔断异常BrokenCircuitException做出一个友好的响应
            var fallbackPolicy = Policy<HttpResponseMessage>.Handle<BrokenCircuitException>().FallbackAsync(message);

            var retryPolicy = Policy<HttpResponseMessage>.Handle<Exception>()
                // 当遇到HttpRequestException或者响应500、408时，等待10*N秒，然后重试，总共重试3次
                .WaitAndRetryAsync(3, retryIndex => { return TimeSpan.FromSeconds(retryIndex * 10); });

            // 组合多个策略
            var wrapPolicy = Policy.WrapAsync(fallbackPolicy, retryPolicy, breakPolicy);

            // 给HttpClient应用组合策略
            services.AddHttpClient("OrderClient-V5").AddPolicyHandler(wrapPolicy);

            // 限制最大并发数为30的时候进行限流
            var bulkPolicy = Policy.BulkheadAsync<HttpResponseMessage>(30);
            // 给HttpClient应用限流策略
            services.AddHttpClient("OrderClient-V6").AddPolicyHandler(bulkPolicy);

            var bulkAdvancedPolicy = Policy.BulkheadAsync<HttpResponseMessage>
            (
                // 最大并发数量，这里设置30
                maxParallelization: 30,
                // 最大队列数量，当我们请求超过30的并发数量时，定义了队列数就有这么多请求排队，超出队列的抛异常，否则没有定义队列数那么就直接抛异常
                maxQueuingActions: 20,
                // 当请求被拒绝时(超过并发数被限流)做什么操作
                onBulkheadRejectedAsync: context => Task.CompletedTask
            );

            // 定义一个友好的响应
            var bulkFriendlymessage = new HttpResponseMessage
            {
                Content = new StringContent("{}"),
            };
            // 针对限流异常BulkheadRejectedException做出一个友好的响应
            var fallbackBulkPolicy = Policy<HttpResponseMessage>.Handle<BulkheadRejectedException>().FallbackAsync(bulkFriendlymessage);
            // 组合多个策略
            var wrapBulkPolicy = Policy.WrapAsync(fallbackBulkPolicy, bulkAdvancedPolicy);
            // 给HttpClient应用限流策略
            services.AddHttpClient("OrderClient-V7").AddPolicyHandler(wrapBulkPolicy);

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    OrderGrpcClient service = context.RequestServices.GetService<OrderGrpcClient>();
                    try
                    {
                        CreateOrderResult result = service.CreateOrder(new CreateOrderCommand { BuyerId = "abc" });
                        await context.Response.WriteAsync(result.OrderId.ToString());
                    }
                    catch (Exception ex)
                    {

                    }
                });
                endpoints.MapControllers();
            });
        }
    }
}
