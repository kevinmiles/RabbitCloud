using Echo.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rabbit.Rpc;
using Rabbit.Rpc.Exceptions;
using Rabbit.Rpc.ProxyGenerator;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Rabbit.Transport.DotNetty;
using Rabbit.Rpc.Convertibles;

namespace Echo.Client
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var serviceCollection = new ServiceCollection();

            serviceCollection
                .AddLogging()
                .AddClient()
                .UseSharedFileRouteManager(@"d:\routes.txt")
                .UseDotNettyTransport();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            serviceProvider.GetRequiredService<ILoggerFactory>()
                .AddConsole((c, l) => (int)l >= 3);

            var serviceProxyGenerater = serviceProvider.GetRequiredService<IServiceProxyGenerater>();
            var serviceProxyFactory = serviceProvider.GetRequiredService<IServiceProxyFactory>();
            var services = serviceProxyGenerater.GenerateProxys(new[] { typeof(IUserService) }).ToArray();

            //创建IUserService的代理。
            var userService = serviceProxyFactory.CreateProxy<IUserService>(services.Single(typeof(IUserService).GetTypeInfo().IsAssignableFrom));

            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            while (true)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        Console.WriteLine($"userService.GetUserName:{await userService.GetUserName(1)}");
                        Console.WriteLine($"userService.GetUserId:{await userService.GetUserId("rabbit")}");
                        Console.WriteLine(
                            $"userService.GetUserLastSignInTime:{await userService.GetUserLastSignInTime(1)}");
                        Console.WriteLine($"userService.Exists:{await userService.Exists(1)}");
                        var user = await userService.GetUser(1);
                        Console.WriteLine($"userService.GetUser:name={user.Name},age={user.Age}");
                        Console.WriteLine($"userService.Update:{await userService.Update(1, user)}");
                        Console.WriteLine($"userService.GetDictionary:{(await userService.GetDictionary())["key"]}");
                        await userService.Try();
                        await userService.TryThrowException();
                    }
                    catch (RpcRemoteException remoteException)
                    {
                        logger.LogError(remoteException.Message);
                    }
                    catch
                    {
                    }
                }).Wait();
                Console.ReadLine();
            }
        }
    }

    /// <summary>
    /// 服务代理工厂扩展。
    /// </summary>
    public static class ServiceProxyFactoryExExtensions
    {
        private static readonly Type[] ctorTypes = new Type[] { typeof(Rabbit.Rpc.Runtime.Client.IRemoteInvokeService), typeof(ITypeConvertibleService) };
        /// <summary>
        /// 创建服务代理。
        /// </summary>
        /// <typeparam name="T">服务接口类型。</typeparam>
        /// <param name="serviceProxyFactory">服务代理工厂。</param>
        /// <param name="proxyType">代理类型。</param>
        /// <returns>服务代理实例。</returns>
        public static T CreateProxySpecial<T>(this IServiceProvider serviceProvider, Type proxyType, params System.Net.EndPoint[] endPoints)
        {
            return (T)proxyType.GetConstructor(ctorTypes).Invoke(new object[] { new object(), serviceProvider.GetService<ITypeConvertibleService>() });
        }
    }
}