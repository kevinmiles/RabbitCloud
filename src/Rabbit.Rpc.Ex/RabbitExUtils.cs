using Microsoft.Extensions.DependencyInjection;
using Rabbit.Rpc.Convertibles;
using Rabbit.Rpc.Routing;
using Rabbit.Rpc.Runtime.Client;
using Rabbit.Rpc.Runtime.Client.Implementation;
using Rabbit.Rpc.Runtime.Server.Implementation.ServiceDiscovery;
using Rabbit.Rpc.Runtime.Server.Implementation.ServiceDiscovery.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Rabbit.Rpc
{

    public static class RabbitExUtils
    {
        public static IRpcBuilder UseSpecialServiceRouteManager(this IRpcBuilder builder)
            => builder.UseRouteManager<SpecialServiceRouteManager>();

        public static IRpcBuilder RegistRabbitExClientRuntime(this IRpcBuilder builder)
        {
            builder.UseSpecialServiceRouteManager();
            //属于ServiceRuntime的一部分, 但在RegistRemoteServiceEx中会用到
            builder.Services.AddSingleton<IClrServiceEntryFactory, ClrServiceEntryFactory>();
            builder.Services.AddSingleton<IRemoteInvokeService, SpecialRemoteInvokeService>();
            return builder;
        }

        /// <summary>
        /// [服务端用]
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRpcBuilder RegistLocalService<TService, TImplementation>(this IRpcBuilder builder)
            where TService : class
            where TImplementation : class, TService
        {
            builder.Services.AddSingleton<TService, TImplementation>();
            return builder;
        }

        /// <summary>
        /// [服务端用]
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRpcBuilder RegistLocalService<TService, TImplementation>(this IRpcBuilder builder, Func<IServiceProvider, TImplementation> factory)
            where TService : class
            where TImplementation : class, TService
        {
            builder.Services.AddSingleton<TService, TImplementation>(factory);
            return builder;
        }

        /// <summary>
        /// [客户端用]
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <param name="serviceProvider"></param>
        /// <param name="key"></param>
        /// <param name="endPoints"></param>
        /// <returns></returns>
        public static IServiceProvider RegistRemoteServiceEx<TService>(this IServiceProvider serviceProvider, string channel = null, params IPEndPoint[] endPoints)
            where TService : class
        {
            if (endPoints == null || endPoints.Length == 0) throw new ArgumentException("No endPoints specified.", nameof(endPoints));

            var srm = serviceProvider.GetRequiredService<IServiceRouteManager>();

            var csef = serviceProvider.GetRequiredService<IClrServiceEntryFactory>();
            var addr = endPoints.Select(ep => new Address.IpAddressModel { Ip = ep.Address.ToString(), Port = ep.Port }).ToArray();
            var srvs = csef.CreateServiceEntry(typeof(TService), null).Select(se =>
            {
                se.Descriptor.Id = GenServiceIdWithChannel(se.Descriptor.Id, channel);
                return new ServiceRoute
                {
                    Address = addr,
                    ServiceDescriptor = se.Descriptor
                };
            });
            srm.SetNewRoutes(srvs);

            return serviceProvider;
        }

        public static string GenServiceIdWithChannel(string serviceId, string channel)
        {
            if (!string.IsNullOrEmpty(channel))
                serviceId = channel + "/" + serviceId;

            return serviceId;
        }

        internal static void SetNewRoutes(this IServiceRouteManager srm, IEnumerable<ServiceRoute> routes)
        {
            var oldR = srm.GetRoutesAsync().Result;
            var dic = new Dictionary<string, ServiceRoute>();
            foreach (var o in oldR)
            {
                dic[o.ServiceDescriptor.Id] = o;
            }
            foreach (var n in routes)
            {
                dic[n.ServiceDescriptor.Id] = n;
            }

            srm.SetRoutesAsync(dic.Values).Wait();
        }

        public static TService CreateRemoteServiceEx<TService>(this IServiceProvider serviceProvider, Type proxyType, string channel = null)
            where TService : class
        {
            var _remoteInvokeService = serviceProvider.GetRequiredService<IRemoteInvokeService>();
            var _typeConvertibleService = serviceProvider.GetRequiredService<ITypeConvertibleService>();
            if (string.IsNullOrEmpty(channel))
                return (TService)proxyType.GetTypeInfo().GetConstructor(new Type[] { typeof(IRemoteInvokeService), typeof(ITypeConvertibleService) })
                    .Invoke(new object[] { _remoteInvokeService, _typeConvertibleService });

            return (TService)proxyType.GetTypeInfo().GetConstructor(new Type[] { typeof(IRemoteInvokeService), typeof(ITypeConvertibleService), typeof(string) })
                .Invoke(new object[] { _remoteInvokeService, _typeConvertibleService, channel });
        }

        public static TService CreateRemoteServiceEx<TService>(this IServiceProvider serviceProvider, Type proxyType, params IPEndPoint[] endPoints)
            where TService : class
        {
            var guid = Guid.NewGuid().ToString();
            RegistRemoteServiceEx<TService>(serviceProvider, guid, endPoints);
            return CreateRemoteServiceEx<TService>(serviceProvider, proxyType, guid);
        }
    }
}
