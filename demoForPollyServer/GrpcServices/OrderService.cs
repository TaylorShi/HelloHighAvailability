using Grpc.Core;
using GrpcServices;
using System;
using System.Threading.Tasks;

namespace demoForPollyServer.GrpcServices
{
    /// <summary>
    /// 订单服务(基于Grpc)
    /// </summary>
    public class OrderService : OrderGrpc.OrderGrpcBase
    {
        /// <summary>
        /// 重写创建订单
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<CreateOrderResult> CreateOrder(CreateOrderCommand request, ServerCallContext context)
        {
            // 可替换成真实的创建订单服务的业务代码
            return Task.FromResult(new CreateOrderResult { OrderId = new Random().Next(80000,99999) });
        }
    }
}
