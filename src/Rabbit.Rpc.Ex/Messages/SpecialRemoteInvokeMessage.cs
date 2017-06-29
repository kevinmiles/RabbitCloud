using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rabbit.Rpc.Messages
{
    public class SpecialRemoteInvokeMessage : RemoteInvokeMessage
    {
        /// <summary>
        /// 本地服务Id。
        /// </summary>
        public string LocalServiceId { get; set; }

        /// <summary>
        /// 泛型参数。
        /// </summary>
        public List<object> GenericParameters { get; set; }

        public RemoteInvokeMessage ToRemoteInvokeMessage() => new RemoteInvokeMessage { ServiceId = this.ServiceId, Parameters = this.Parameters };
    }
}
