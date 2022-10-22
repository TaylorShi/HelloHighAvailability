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
                // ������Ч����ǩ��֤��
                handler.SslOptions.RemoteCertificateValidationCallback = (a, b, c, d) => true;
                return handler;
            })
            .AddTransientHttpErrorPolicy(policyBuilder =>
            {
                // �����񱨴����Զ��ٴ�
                // ��Ӧ�ó�����HttpRequestException������Ӧ500��408�Ż�ȥִ��������Բ���
                //return policyBuilder.RetryAsync(3);

                // ������HttpRequestException������Ӧ500��408ʱ���ȴ�10*N�룬Ȼ�����ԣ��ܹ�����3��
                //return policyBuilder.WaitAndRetryAsync(3, retryIndex =>
                //{
                //    return TimeSpan.FromSeconds(retryIndex * 10);
                //});

                // ������HttpRequestException������Ӧ500��408ʱ���ȴ�10*N�룬Ȼ�����ԣ�ֱ����Ӧ�ɹ�
                return policyBuilder.WaitAndRetryForeverAsync(retryIndex =>
                {
                    return TimeSpan.FromSeconds(retryIndex * 10);
                });
            });

            var reg = services.AddPolicyRegistry();
            // ��Ӷ�����Զ���ԵĲ���
            reg.Add("RetryForever", Policy.HandleResult<HttpResponseMessage>(message =>
            {
                // ����Ӧ����Ϊ201��ʱ��������Դ�������
                return message.StatusCode == System.Net.HttpStatusCode.Created;
            }).RetryForeverAsync());

            // ��OrderClient�������Զ���Ĳ���
            services.AddHttpClient("OrderClient").AddPolicyHandlerFromRegistry("RetryForever");

            // ��OrderClient V2�������Զ���Ĳ���
            services.AddHttpClient("OrderClient-V2").AddPolicyHandlerFromRegistry((registry, message) =>
            {
                // ������ķ�ʽ��Get��ʱ��Ӧ��RetryForever���ԣ�����ִ���κβ���
                return message.Method == HttpMethod.Get ?
                    registry.Get<IAsyncPolicy<HttpResponseMessage>>("RetryForever") :
                    Policy.NoOpAsync<HttpResponseMessage>();
            });

            // ������Exception�쳣ʱӦ�����Բ���
            Policy.Handle<Exception>().WaitAndRetryAsync(3, retryIndex =>
            {
                return TimeSpan.FromSeconds(retryIndex * 10);
            });

            // Policy.Handle<Exception>().Fallback

            services.AddHttpClient("OrderClient-V3")
                // ���һ������
                .AddPolicyHandler(Policy<HttpResponseMessage>.Handle<HttpRequestException>()
                // �����۶ϲ���
                .CircuitBreakerAsync
                (
                    // �������Ժ�����۶ϣ���������10��
                    handledEventsAllowedBeforeBreaking: 10,
                    // �۶ϵ�ʱ�䣬��������10��
                    durationOfBreak: TimeSpan.FromSeconds(10),
                    // �������۶�ʱ������һ���¼�
                    onBreak: (r, t) => { },
                    // ���۶ϻָ�ʱ������һ���¼�
                    onReset: () => { },
                    // �ڻָ�֮ǰ������֤�����Ƿ���õ�����ʱ����һ��������ȥ��֤���ǵķ����Ƿ���õ��¼�
                    onHalfOpen: () => { }
                ));

            services.AddHttpClient("OrderClient-V4")
                // ���һ������
                .AddPolicyHandler(Policy<HttpResponseMessage>.Handle<HttpRequestException>()
                // ����߼��۶ϲ��ԣ�֧�ְ�������ʧ�ܱ���������
                .AdvancedCircuitBreakerAsync
                (
                    // ʧ�ܵı������ж��ٱ���������ʧ��ʱ�����۶ϣ���������80%
                    failureThreshold: 0.8,
                    // ������ʱ�䣬����ʱ�䷶Χ�������80%��ʧ�ܣ���������10��
                    samplingDuration: TimeSpan.FromSeconds(10),
                    // ��С�������������������Ƚ�С��ʱ��10��֮�ڲ������ʧ������������ͻ����80%��ʧ�ܣ�������������������������100����ʱ�򣬲Ż�ȥ�����۶ϲ���
                    minimumThroughput: 100,
                    // �۶ϵ�ʱ�䣬��������10��
                    durationOfBreak: TimeSpan.FromSeconds(10),
                    // �������۶�ʱ������һ���¼�
                    onBreak: (r, t) => { },
                    // ���۶ϻָ�ʱ������һ���¼�
                    onReset: () => { },
                    // �ڻָ�֮ǰ������֤�����Ƿ���õ�����ʱ����һ��������ȥ��֤���ǵķ����Ƿ���õ��¼�
                    onHalfOpen: () => { }
                ));

            var breakPolicy = Policy<HttpResponseMessage>.Handle<HttpRequestException>()
                // ����߼��۶ϲ��ԣ�֧�ְ�������ʧ�ܱ���������
                .AdvancedCircuitBreakerAsync
                (
                    // ʧ�ܵı������ж��ٱ���������ʧ��ʱ�����۶ϣ���������80%
                    failureThreshold: 0.8,
                    // ������ʱ�䣬����ʱ�䷶Χ�������80%��ʧ�ܣ���������10��
                    samplingDuration: TimeSpan.FromSeconds(10),
                    // ��С�������������������Ƚ�С��ʱ��10��֮�ڲ������ʧ������������ͻ����80%��ʧ�ܣ�������������������������100����ʱ�򣬲Ż�ȥ�����۶ϲ���
                    minimumThroughput: 100,
                    // �۶ϵ�ʱ�䣬��������10��
                    durationOfBreak: TimeSpan.FromSeconds(10),
                    // �������۶�ʱ������һ���¼�
                    onBreak: (r, t) => { },
                    // ���۶ϻָ�ʱ������һ���¼�
                    onReset: () => { },
                    // �ڻָ�֮ǰ������֤�����Ƿ���õ�����ʱ����һ��������ȥ��֤���ǵķ����Ƿ���õ��¼�
                    onHalfOpen: () => { }
                );

            // ����һ���Ѻõ���Ӧ
            var message = new HttpResponseMessage 
            { 
                Content = new StringContent("{}"),
            };
            // ����۶��쳣BrokenCircuitException����һ���Ѻõ���Ӧ
            var fallbackPolicy = Policy<HttpResponseMessage>.Handle<BrokenCircuitException>().FallbackAsync(message);

            var retryPolicy = Policy<HttpResponseMessage>.Handle<Exception>()
                // ������HttpRequestException������Ӧ500��408ʱ���ȴ�10*N�룬Ȼ�����ԣ��ܹ�����3��
                .WaitAndRetryAsync(3, retryIndex => { return TimeSpan.FromSeconds(retryIndex * 10); });

            // ��϶������
            var wrapPolicy = Policy.WrapAsync(fallbackPolicy, retryPolicy, breakPolicy);

            // ��HttpClientӦ����ϲ���
            services.AddHttpClient("OrderClient-V5").AddPolicyHandler(wrapPolicy);

            // ������󲢷���Ϊ30��ʱ���������
            var bulkPolicy = Policy.BulkheadAsync<HttpResponseMessage>(30);
            // ��HttpClientӦ����������
            services.AddHttpClient("OrderClient-V6").AddPolicyHandler(bulkPolicy);

            var bulkAdvancedPolicy = Policy.BulkheadAsync<HttpResponseMessage>
            (
                // ��󲢷���������������30
                maxParallelization: 30,
                // ���������������������󳬹�30�Ĳ�������ʱ�������˶�����������ô�������Ŷӣ��������е����쳣������û�ж����������ô��ֱ�����쳣
                maxQueuingActions: 20,
                // �����󱻾ܾ�ʱ(����������������)��ʲô����
                onBulkheadRejectedAsync: context => Task.CompletedTask
            );

            // ����һ���Ѻõ���Ӧ
            var bulkFriendlymessage = new HttpResponseMessage
            {
                Content = new StringContent("{}"),
            };
            // ��������쳣BulkheadRejectedException����һ���Ѻõ���Ӧ
            var fallbackBulkPolicy = Policy<HttpResponseMessage>.Handle<BulkheadRejectedException>().FallbackAsync(bulkFriendlymessage);
            // ��϶������
            var wrapBulkPolicy = Policy.WrapAsync(fallbackBulkPolicy, bulkAdvancedPolicy);
            // ��HttpClientӦ����������
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
