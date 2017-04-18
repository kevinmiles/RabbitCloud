using Rabbit.Rpc.Runtime.Server.Implementation.ServiceDiscovery.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Generic.Tests
{
    public interface I1 { }
    public interface I2 : I1 { }

    public class X1 : I2
    {
        public int Value { get; set; }
    }
    public class X2<T1, T3, T4, T5>
    {
        public class X6<T2>
        {
            public int Value { get; set; }
        }
        public int Value { get; set; }
    }
    public struct X3 : I2
    {
        public int Value { get; set; }
    }
    public class X4
    {
        public int Value { get; set; }
        public class X5
        {
            public int Value { get; set; }
        }
    }

    [RpcServiceBundle]
    public interface IUserService<in T1, out T2>
        where T2 : class
    {
        //unsafe Task<X2<X1, X3, X4, X4.X5>> Test<T>(X1 x1, Common.X2<X1, X3, X4, X4.X5> x2, X3 x3, X4 x4, X4.X5 x5, T x6, T2 x7, T2[] x8, ref T2 x9, out T2 x10, int* x11)
        //    where T : List<int>, ICollection<int>, IEnumerable, new();
        Task<T> TestX11<T>(int p0)
            where T : class, I2, new();
        Task<T> TestX12<T>()
            where T : X1, I2, new();
        //ValueTask<T> TestX3<T>(string p0)
        //    where T : struct, I2;
        //T2 zz(T1 t);
    }

    public class UserService : IUserService<I2, X1>
    {
        #region Implementation of IUserService

        /*public unsafe Task<X2<X1, X3, X4, X4.X5>> Test<T>(X1 x1, X2<X1, X3, X4, X4.X5> x2, X3 x3, X4 x4, X4.X5 x5, T x6, int x7, int[] x8, ref int x9, out int x10, int* x11)
            where T : List<int>, ICollection<int>, IEnumerable, new()
        {
            throw new NotImplementedException();
        }*/

        public async ValueTask<T> TestX3<T>(string p0)
            where T : struct, I2
        {
            return default(T);
        }

        public async Task<T> TestX11<T>(int p0)
            where T : class, I2, new()
        {
            return new T();
        }

        public async Task<T> TestX12<T>()
            where T : X1, I2, new()
        {
            return new T();
        }

        public X1 zz(I2 t)
        {
            return new X1();
        }

        #endregion Implementation of IUserService

    }
    public class ResultModel<T>
    {
        public class Nested
        {
            public T Data1 { get; set; }
        }
        public class NestedBadT<T>
        {
            public T Data1 { get; set; }
            public T Data2 { get; set; }
        }
        public class NestedGoodT<T2>
        {
            public T Data1 { get; set; }
            public T2 Data2 { get; set; }
        }
        public int State { get; set; }
        public T Data { get; set; }
    }
    [RpcServiceBundle]
    public interface IService
    {
        //多级泛型测试
        Task<ResultModel<int>> GetData(int i);
        //ValueTask<T>测试
        ValueTask<ResultModel<int>> GetData2(int i);
        //同步测试
        int GetData3(int i);
        //坏的Nested泛型
        ResultModel<string>.NestedBadT<int> GetData4(int i);
        //好的Nested泛型
        ResultModel<string>.NestedGoodT<int> GetData5(int i);
        //Nested非泛型
        ResultModel<string>.Nested GetData6(int i);
        //没有命名空间的类
        NoNSClass GetData7(int i);
        NoNSClassT<NoNSClass> GetData8(int i);
    }
    [RpcServiceBundle]
    public interface IServiceT<T1, T2>
    {

    }

    public class DefService : IService
    {
        public async Task<ResultModel<int>> GetData(int i)
        {
            return new ResultModel<int> { State = 1, Data = 2 };
        }
        public async ValueTask<ResultModel<int>> GetData2(int i)
        {
            return new ResultModel<int> { State = 1, Data = 2 };
        }

        public int GetData3(int i) => 0;

        public ResultModel<string>.NestedBadT<int> GetData4(int i) => new ResultModel<string>.NestedBadT<int>();
        public ResultModel<string>.NestedGoodT<int> GetData5(int i) => new ResultModel<string>.NestedGoodT<int>();
        public ResultModel<string>.Nested GetData6(int i) => new ResultModel<string>.Nested();
        public NoNSClass GetData7(int i) => new NoNSClass();
        public NoNSClassT<NoNSClass> GetData8(int i) => new NoNSClassT<NoNSClass>();
    }
}

public class NoNSClass
{
    public int Value { get; set; }
}

public class NoNSClassT<T>
{
    public int Value { get; set; }
    public T Data { get; set; }
}