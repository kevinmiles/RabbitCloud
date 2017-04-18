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
using Rabbit.Rpc.Address;
using Rabbit.Rpc.Runtime.Server;
using Rabbit.Rpc.Routing;
using System.Net;
using Newtonsoft.Json;
using Rabbit.Rpc.Ids;
using Rabbit.Rpc.Ids.Implementation;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Generic.Tests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            bool dirty = true;
            while (true)
            {
                if (dirty)
                {
                    Console.WriteLine("C. Start Client");
                    Console.WriteLine("S. Start Server");
                }
                dirty = true;
                try
                {
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.C:
                            StartClient().Wait();
                            break;
                        case ConsoleKey.S:
                            StartServer().Wait();
                            break;
                        default:
                            dirty = false;
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        public static async Task StartClient()
        {

            var serviceCollection = new ServiceCollection();

            serviceCollection
                .AddLogging()
                .AddClient()
                .UseSharedFileRouteManager(@"d:\routes.txt")
                .UseDotNettyTransport();
            serviceCollection
                .AddSingleton<IServiceIdGenerator, GenericServiceIdGenerator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            serviceProvider.GetRequiredService<ILoggerFactory>()
                .AddConsole(LogLevel.Trace);

            var serviceProxyGenerater = serviceProvider.GetRequiredService<IServiceProxyGenerater>();
            var serviceProxyFactory = serviceProvider.GetRequiredService<IServiceProxyFactory>();
            var services = serviceProxyGenerater.GenerateProxys(new[] { typeof(IService) }).ToArray();

            //创建IUserService的代理。
            var userService = serviceProxyFactory.CreateProxy<IService>(services.Single(typeof(IService).GetTypeInfo().IsAssignableFrom));

            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            while (true)
            {
                Console.WriteLine("START>>>");
                Console.WriteLine("GetData" + JsonConvert.SerializeObject(await userService.GetData(0)));
                Console.WriteLine("GetData2" + JsonConvert.SerializeObject(await userService.GetData2(0)));
                Console.WriteLine("GetData3" + JsonConvert.SerializeObject(userService.GetData3(0)));
                Console.WriteLine("GetData4" + JsonConvert.SerializeObject(userService.GetData4(0)));
                Console.WriteLine("GetData5" + JsonConvert.SerializeObject(userService.GetData5(0)));
                Console.WriteLine("GetData6" + JsonConvert.SerializeObject(userService.GetData6(0)));
                Console.WriteLine("GetData7" + JsonConvert.SerializeObject(userService.GetData7(0)));
                Console.WriteLine("GetData8" + JsonConvert.SerializeObject(userService.GetData8(0)));
                Console.WriteLine("<<<END");
                Console.ReadLine();
            }
        }

        public static async Task StartServer()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection
                .AddLogging()
                .AddRpcCore()
                .AddServiceRuntime()
                .UseSharedFileRouteManager("d:\\routes.txt")
                .UseDotNettyTransport();
            serviceCollection
                .AddSingleton<IServiceIdGenerator, GenericServiceIdGenerator>();

            serviceCollection.AddTransient<IService, DefService>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            serviceProvider.GetRequiredService<ILoggerFactory>()
                .AddConsole(LogLevel.Trace);

            //自动生成服务路由（这边的文件与Echo.Client为强制约束）
            {
                var serviceEntryManager = serviceProvider.GetRequiredService<IServiceEntryManager>();
                var addressDescriptors = serviceEntryManager.GetEntries().Select(i => new ServiceRoute
                {
                    Address = new[]
                    {
                        new IpAddressModel { Ip = "127.0.0.1", Port = 9981 }
                    },
                    ServiceDescriptor = i.Descriptor
                });

                var serviceRouteManager = serviceProvider.GetRequiredService<IServiceRouteManager>();
                serviceRouteManager.SetRoutesAsync(addressDescriptors).Wait();
            }

            var serviceHost = serviceProvider.GetRequiredService<IServiceHost>();

            //启动主机
            await serviceHost.StartAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9981));
            Console.WriteLine($"服务端启动成功，{DateTime.Now}。");
        }
    }
}