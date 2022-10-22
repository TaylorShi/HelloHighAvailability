## 什么是Polly

> https://github.com/App-vNext/Polly

> http://www.thepollyproject.org

![](/Assets/2022-10-23-00-51-54.png)

> Polly是一个.NET弹性和瞬时故障处理库，它允许开发者以流畅和线程安全的方式表达诸如重试、断路、超时、隔板隔离、速率限制和回退等策略。

![](/Assets/2022-10-23-00-51-53.png)

Polly是.Net生态非常著名的一个组件包。

Polly针对.NET标准1.1(覆盖范围：.NET Core 1.0、Mono、Xamarin、UWP、WP8.1+)和.NET标准2.0+(覆盖范围：.NET Core 2.0+、.NET Core 3.0，以及后来的Mono、Xamarin和UWP目标)。NuGet软件包还包括.NET框架4.6.1和4.7.2的直接目标。

### Polly组件包

- Polly，这是Polly的核心包
- Polly.Extensions.Http，Polly基于Http的一些扩展
- Microsoft.Extensions.Http.Polly，HttpClientFactory组件包的Polly扩展包

### Polly的能力

- **失败重试**，当调用失败时能够自动重试
- **服务熔断**，当部分服务不可用时，应用可以快速响应一个熔断的结果，避免持续的请求这些不可用的服务而导致整个应用程序跪掉
- **超时处理**，指为服务的请求设置一个超时时间，当超过超时时间时可以按照预定的操作进行处理，比如说返回一个缓存结果
- **舱壁隔离**，实际上是一个限流功能，可以为服务定义最大的流量和队列，这样子避免我们的服务因为请求量过大而被压崩
- **缓存策略**，让我们与类似于AOP的方式为应用嵌入缓存的机制，可以当缓存命中时可以快速地响应缓存，而不是持续地请求服务
- **失败降级**，指当服务不可用时，可以响应一个更友好的结果而不是报错
- **组合策略**，可以让我们将上面的策略组合在一起，按照一定的顺序，可以对不同场景组合不同的策略类，实现应用程序

## 相关文章

* [乘风破浪，遇见最佳跨平台跨终端框架.Net Core/.Net生态 - 浅析ASP.NET Core可用性设计，使用Polly定义重试、熔断、限流、降级策略](https://www.cnblogs.com/taylorshi/p/16817461.html)