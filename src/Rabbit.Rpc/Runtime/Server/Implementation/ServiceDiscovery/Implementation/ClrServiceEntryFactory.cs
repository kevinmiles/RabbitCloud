using Microsoft.Extensions.DependencyInjection;
using Rabbit.Rpc.Convertibles;
using Rabbit.Rpc.Ids;
using Rabbit.Rpc.Runtime.Server.Implementation.ServiceDiscovery.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Rabbit.Rpc.Runtime.Server.Implementation.ServiceDiscovery.Implementation
{
    /// <summary>
    /// Clr������Ŀ������
    /// </summary>
    public class ClrServiceEntryFactory : IClrServiceEntryFactory
    {
        #region Field

        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceIdGenerator _serviceIdGenerator;
        private readonly ITypeConvertibleService _typeConvertibleService;

        #endregion Field

        #region Constructor

        public ClrServiceEntryFactory(IServiceProvider serviceProvider, IServiceIdGenerator serviceIdGenerator, ITypeConvertibleService typeConvertibleService)
        {
            _serviceProvider = serviceProvider;
            _serviceIdGenerator = serviceIdGenerator;
            _typeConvertibleService = typeConvertibleService;
        }

        #endregion Constructor

        #region Implementation of IClrServiceEntryFactory

        /// <summary>
        /// ����������Ŀ��
        /// </summary>
        /// <param name="service">�������͡�</param>
        /// <param name="serviceImplementation">����ʵ�����͡�</param>
        /// <returns>������Ŀ���ϡ�</returns>
        public IEnumerable<ServiceEntry> CreateServiceEntry(Type service, Type serviceImplementation)
        {
            foreach (var methodInfo in service.GetTypeInfo().GetMethods())
            {
                //var implementationMethodInfo = serviceImplementation.GetTypeInfo().GetMethod(methodInfo.Name, methodInfo.GetParameters().Select(p => p.ParameterType).ToArray());
                yield return Create(methodInfo);
            }
        }

        #endregion Implementation of IClrServiceEntryFactory

        #region Private Method

        private ServiceEntry Create(MethodInfo method)
        {
            var serviceId = _serviceIdGenerator.GenerateServiceId(method);

            var serviceDescriptor = new ServiceDescriptor
            {
                Id = serviceId
            };

            var descriptorAttributes = method.GetCustomAttributes<RpcServiceDescriptorAttribute>();
            foreach (var descriptorAttribute in descriptorAttributes)
            {
                descriptorAttribute.Apply(serviceDescriptor);
            }

            return new ServiceEntry
            {
                Descriptor = serviceDescriptor,
                Func = (parameters, ct) =>
               {
                   var serviceScopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
                   using (var scope = serviceScopeFactory.CreateScope())
                   {
                       var instance = scope.ServiceProvider.GetRequiredService(method.DeclaringType);

                       var list = new List<object>();
                       foreach (var parameterInfo in method.GetParameters())
                       {
                           var parameterType = parameterInfo.ParameterType;
                           if (parameterType == typeof(CancellationToken))
                           {
                               list.Add(ct);
                               continue;
                           }
                           var value = parameters[parameterInfo.Name];

                           var parameter = _typeConvertibleService.Convert(value, parameterType);
                           list.Add(parameter);
                       }

                       var result = method.Invoke(instance, list.ToArray());

                       return Task.FromResult(result);
                   }
               }
            };
        }

        #endregion Private Method
    }
}