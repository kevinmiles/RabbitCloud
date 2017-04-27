using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Rabbit.Rpc.Runtime.Server.Implementation.ServiceDiscovery.Attributes
{
    /// <summary>
    /// Service标记类型的服务条目提供程序。
    /// </summary>
    public class AttributeServiceEntryProvider : IServiceEntryProvider
    {
        #region Field

        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<Type> _types;
        private readonly IClrServiceEntryFactory _clrServiceEntryFactory;
        private readonly ILogger<AttributeServiceEntryProvider> _logger;

        #endregion Field

        #region Constructor

        public AttributeServiceEntryProvider(IServiceProvider serviceProvider, IEnumerable<Type> types, IClrServiceEntryFactory clrServiceEntryFactory, ILogger<AttributeServiceEntryProvider> logger)
        {
            _serviceProvider = serviceProvider;
            _types = types;
            _clrServiceEntryFactory = clrServiceEntryFactory;
            _logger = logger;
        }

        #endregion Constructor

        #region Implementation of IServiceEntryProvider

        /// <summary>
        /// 获取服务条目集合。
        /// </summary>
        /// <returns>服务条目集合。</returns>
        public IEnumerable<ServiceEntry> GetEntries()
        {
            var services = _types.Where(i =>
            {
                var typeInfo = i.GetTypeInfo();
                return typeInfo.IsInterface && typeInfo.GetCustomAttribute<RpcServiceBundleAttribute>() != null;
            }).ToArray();
            var serviceImplementations = _types.Where(i =>
            {
                var typeInfo = i.GetTypeInfo();
                return typeInfo.IsClass && !typeInfo.IsAbstract && i.Namespace != null && !i.Namespace.StartsWith("System") &&
                !i.Namespace.StartsWith("Microsoft");
            }).ToArray();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation($"发现了以下服务：{string.Join(",", services.Select(i => i.ToString()))}。");
            }

            var entries = new List<ServiceEntry>();
            foreach (var service in services)
            {
                var knownImpl = _serviceProvider.GetService(service);
                if (knownImpl != null)
                {
                    entries.AddRange(_clrServiceEntryFactory.CreateServiceEntry(service, knownImpl.GetType()));
                }
                else
                {
                    var serviceImplementation = serviceImplementations.Where(i => service.GetTypeInfo().IsAssignableFrom(i)).ToList();
                    if (serviceImplementation.Count == 0)
                    {
                        _logger.LogWarning($"对服务：{service}, 未手动注册且未发现任何实现, 跳过自动注册。");
                    }
                    else if (serviceImplementation.Count > 1)
                    {
                        _logger.LogWarning($"对服务：{service}, 未手动注册且发现多于一个实现, 跳过自动注册。");
                    }
                    else
                    {
                        entries.AddRange(_clrServiceEntryFactory.CreateServiceEntry(service, serviceImplementation[0]));
                    }
                }
            }
            return entries;
        }

        #endregion Implementation of IServiceEntryProvider
    }
}