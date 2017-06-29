using Microsoft.Extensions.Logging;
using Rabbit.Rpc.Routing;
using Rabbit.Rpc.Routing.Implementation;
using Rabbit.Rpc.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rabbit.Rpc
{
    public class SpecialServiceRouteManager : ServiceRouteManagerBase
    {
#region Field
        
        private readonly ISerializer<string> _serializer;
        private readonly IServiceRouteFactory _serviceRouteFactory;
        private readonly ILogger<SpecialServiceRouteManager> _logger;
        private ServiceRoute[] _routes = new ServiceRoute[0];

#endregion Field

#region Constructor

        public SpecialServiceRouteManager(ISerializer<string> serializer,
            IServiceRouteFactory serviceRouteFactory, ILogger<SpecialServiceRouteManager> logger) : base(serializer)
        {
            _serializer = serializer;
            _serviceRouteFactory = serviceRouteFactory;
            _logger = logger;
        }

#endregion Constructor

#region Overrides of ServiceRouteManagerBase

        /// <summary>
        ///     获取所有可用的服务路由信息。
        /// </summary>
        /// <returns>服务路由集合。</returns>
        public override Task<IEnumerable<ServiceRoute>> GetRoutesAsync()
        {
            return Task.FromResult<IEnumerable<ServiceRoute>>(_routes);
        }

        /// <summary>
        ///     清空所有的服务路由。
        /// </summary>
        /// <returns>一个任务。</returns>
        public override Task ClearAsync()
        {
            if (_routes.Length > 0)
                _routes = new ServiceRoute[0];
            return Task.FromResult(0);
        }

        /// <summary>
        ///     设置服务路由。
        /// </summary>
        /// <param name="routes">服务路由集合。</param>
        /// <returns>一个任务。</returns>
        protected async override Task SetRoutesAsync(IEnumerable<ServiceRouteDescriptor> routes)
        {
            var oldRoutes = _routes;
            var newRoutes = (await _serviceRouteFactory.CreateServiceRoutesAsync(routes)).ToArray();
            if (oldRoutes.Length == 0 && newRoutes.Length > 0)
            {
                //触发服务路由创建事件。
                OnCreated(newRoutes.Select(route => new ServiceRouteEventArgs(route)).ToArray());
            }
            else
            {
                //旧的服务Id集合。
                var oldServiceIds = oldRoutes.Select(i => i.ServiceDescriptor.Id).ToArray();
                //新的服务Id集合。
                var newServiceIds = newRoutes.Select(i => i.ServiceDescriptor.Id).ToArray();

                //被删除的服务Id集合
                var removeServiceIds = oldServiceIds.Except(newServiceIds).ToArray();
                //新增的服务Id集合。
                var addServiceIds = newServiceIds.Except(oldServiceIds).ToArray();
                //可能被修改的服务Id集合。
                var mayModifyServiceIds = newServiceIds.Except(removeServiceIds).ToArray();

                //触发服务路由创建事件。
                OnCreated(
                    newRoutes.Where(i => addServiceIds.Contains(i.ServiceDescriptor.Id))
                        .Select(route => new ServiceRouteEventArgs(route))
                        .ToArray());

                //触发服务路由删除事件。
                OnRemoved(
                    oldRoutes.Where(i => removeServiceIds.Contains(i.ServiceDescriptor.Id))
                        .Select(route => new ServiceRouteEventArgs(route))
                        .ToArray());

                //触发服务路由变更事件。
                var currentMayModifyRoutes =
                    newRoutes.Where(i => mayModifyServiceIds.Contains(i.ServiceDescriptor.Id)).ToArray();
                var oldMayModifyRoutes =
                    oldRoutes.Where(i => mayModifyServiceIds.Contains(i.ServiceDescriptor.Id)).ToArray();

                foreach (var oldMayModifyRoute in oldMayModifyRoutes)
                {
                    if (!currentMayModifyRoutes.Contains(oldMayModifyRoute))
                        OnChanged(
                            new ServiceRouteChangedEventArgs(
                                currentMayModifyRoutes.First(
                                    i => i.ServiceDescriptor.Id == oldMayModifyRoute.ServiceDescriptor.Id),
                                oldMayModifyRoute));
                }

            }
            _routes = newRoutes;
        }

        #endregion Overrides of ServiceRouteManagerBase
    }
}
