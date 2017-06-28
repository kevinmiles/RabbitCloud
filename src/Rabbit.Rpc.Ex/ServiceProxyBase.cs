using Rabbit.Rpc.Convertibles;
using Rabbit.Rpc.Messages;
using Rabbit.Rpc.Runtime.Client;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rabbit.Rpc
{
    /// <summary>
    /// 一个抽象的服务代理基类。
    /// </summary>
    public abstract class SpecialServiceProxyBase
    {
        #region Field

        private readonly IRemoteInvokeService _remoteInvokeService;
        private readonly ITypeConvertibleService _typeConvertibleService;
        private readonly string _channel;

        #endregion Field

        #region Constructor

        protected SpecialServiceProxyBase(IRemoteInvokeService remoteInvokeService, ITypeConvertibleService typeConvertibleService)
            : this(remoteInvokeService, typeConvertibleService, null)
        {
        }

        protected SpecialServiceProxyBase(IRemoteInvokeService remoteInvokeService, ITypeConvertibleService typeConvertibleService, string channel)
        {
            _remoteInvokeService = remoteInvokeService;
            _typeConvertibleService = typeConvertibleService;
            _channel = string.IsNullOrEmpty(channel) ? null : channel;
        }

        #endregion Constructor

        #region Protected Method

        /// <summary>
        /// 远程调用。
        /// </summary>
        /// <typeparam name="T">返回类型。</typeparam>
        /// <param name="parameters">参数字典。</param>
        /// <param name="serviceId">服务Id。</param>
        /// <returns>调用结果。</returns>
        protected async Task<T> Invoke<T>(IDictionary<string, object> parameters, string serviceId)
        {
            serviceId = RabbitExUtils.GenServiceIdWithChannel(serviceId, _channel);
            var message = await _remoteInvokeService.InvokeAsync(new RemoteInvokeContext
            {
                InvokeMessage = new RemoteInvokeMessage
                {
                    Parameters = parameters,
                    ServiceId = serviceId
                }
            });

            if (message == null)
                return default(T);

            var result = _typeConvertibleService.Convert(message.Result, typeof(T));

            return (T)result;
        }

        /// <summary>
        /// 远程调用。
        /// </summary>
        /// <param name="parameters">参数字典。</param>
        /// <param name="serviceId">服务Id。</param>
        /// <returns>调用任务。</returns>
        protected async Task Invoke(IDictionary<string, object> parameters, string serviceId)
        {
            serviceId = RabbitExUtils.GenServiceIdWithChannel(serviceId, _channel);
            await _remoteInvokeService.InvokeAsync(new RemoteInvokeContext
            {
                InvokeMessage = new RemoteInvokeMessage
                {
                    Parameters = parameters,
                    ServiceId = serviceId
                }
            });
        }

        #endregion Protected Method
    }
}
