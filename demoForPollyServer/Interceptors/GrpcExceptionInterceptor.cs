using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Threading.Tasks;

namespace demoForPollyServer.Interceptors
{
    /// <summary>
    /// Grpc异常拦截器
    /// </summary>
    public class GrpcExceptionInterceptor : Interceptor
    {
        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await base.UnaryServerHandler(request, context, continuation);
            }
            catch (System.Exception ex)
            {
                var metaData = new Metadata();
                metaData.Add("message", ex.Message);
                throw new RpcException(new Status(StatusCode.Unknown, "Unknown"), metaData);
            }
        }
    }
}
