using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Cookbook
{
    class Chapter10
    {
        #region 10.1 异步接口和继承（可以用await等待的是返回的类（而不是方法）；对一个异步方法的定义，可以用异步方式实现，也可以用同步方式实现。）
        interface IMyAsyncInterface
        {
            Task<int> CountBytesAsync(string url);
        }
        class MyAsyncClass2 : IMyAsyncInterface
        {
            public async Task<int> CountBytesAsync(string url)
            {
                var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(url);
                return bytes.Length;
            }

            //10.2使用
            public async Task<MyAsyncClass2> InitializaAsync()
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                return this;
            }
        }
        static async Task UseMyInterfaceAsync(IMyAsyncInterface service)
        {
            var result = await service.CountBytesAsync("http://www.example.com");
            Trace.WriteLine(result);
        }
        #endregion

        #region 10.2 异步构造：工厂（需要在一个类的构造函数里进行异步操作。）
        //将构造函数与一个异步的初始化方法配对使用
        async Task Example1()
        {
            var instance = new MyAsyncClass2();
            await instance.InitializaAsync();//很容易忘记调用InitializaAsync方法，并且类的实例在构造完后不能马上使用
        }

        //构造函数和InitializeAsync是private，因此其他代码不可能误用。创建实例的唯一方法是使用静态的CreateAsyncfactory工厂方法，并且在初始化完成前，调用者是不能访问这个实例的。
        //其他代码可以这样创建实例：var instance = await MyAsyncClass.CreateAsync();
        class MyAsyncClass
        {
            private MyAsyncClass() { }
            //完成耗时的初始化操作
            private async Task<MyAsyncClass> InitializeAsync()
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                return this;//返回当前初始化完后的类
            }
            //把可等待的InitializeAsync抛给外包，外部决定是否需要await等待
            public static Task<MyAsyncClass> CreateAsync()
            {
                var result = new MyAsyncClass();
                return result.InitializeAsync();
            }
        }
        #endregion

        #region 10.3 异步构造：异步初始化模式（建议不使用此方案，而使用上例中的异步工程或异步Lazy对象初始化（见13.1节），除非依赖注入/控制反转、数据绑定方式等创建实例）
        //（实现这种模式，就要在构造函数内启动初始化（并赋值给这个Initialization属性）。异步初始化的结果（包括所有的异常）是通过Initialization属性对外公开的）

        /// <summary>
        /// 把一个类标记为“需要异步初始化”
        /// 并提供初始化的结果
        /// </summary>
        public interface IAsyncInitialization
        {
            /// <summary>
            /// 本实例的异步初始化的结果。
            /// </summary>
            Task Initialization { get; }
        }

        interface IMyFundamentalType { }

        //实现一个使用异步初始化的类
        class MyFundamentalType : IMyFundamentalType, IAsyncInitialization
        {
            public MyFundamentalType()
            {
                Initialization = InitializeAsync();
            }
            public Task Initialization { get; private set; }

            private async Task InitializeAsync()
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        //依赖注入/控制反转可以使用下面的方式创建和初始化一个类的实例。
        void Example2()
        {
            //IMyFundamentalType instance = UltimateDIFactory.Create<IMyFundamentalType>();
            //var instanceAsyncInit = instance as IAsyncInitialization;
            //if (instanceAsyncInit != null)
            //    await instanceAsyncInit.Initialization;
        }

        interface IMyComposedType { }
        /// <summary>
        /// 这个混合类在进行它自己的初始化前，先等待它的所有部件都初始化完毕。
        /// 需要遵循一个规则，即在InitializeAsync结束前，每个部件都必须初始化完毕。
        /// 只要混合类的初始化完成了，就能保证它所依赖的每个类型也是经过初始化的。
        /// 部件的初始化过程中产生的任何异常，会传递给混合类的初始化过程。
        /// </summary>
        class MyComposedType : IMyComposedType, IAsyncInitialization
        {
            private readonly IMyFundamentalType _fundamental;
            public MyComposedType(IMyFundamentalType fundamental)
            {
                _fundamental = fundamental;
                Initialization = InitializeAsync();
            }

            public Task Initialization { get; private set; }
            private async Task InitializeAsync()
            {
                //如有必要，异步地等待基础实例的初始化。
                var fundamentalAsyncInit = _fundamental as IAsyncInitialization;
                if (fundamentalAsyncInit != null)
                    await fundamentalAsyncInit.Initialization;

                //以下做自己的初始化工作（同步或异步）

            }

            //调用下面的辅助方法的方式
            private async Task InitializeAsync2()
            {
                //异步地等待三个实例全部初始化完毕（有些可能不需要初始化）。
                await AsyncInitialization.WhenAllInitializedAsync(_fundamental, _fundamental);//传入多个需要初始化的实例

                //以下做自己的初始化工作（同步或异步）

            }
        }

        /// <summary>
        /// 赋值方法，检查某个实例是否实现了IAsyncInitialization，并对它做初始化
        /// </summary>
        public static class AsyncInitialization
        {
            public static Task WhenAllInitializedAsync(params object[] instances)
            {
                return Task.WhenAll(instances.OfType<IAsyncInitialization>().Select(x => x.Initialization));
            }
        }
        #endregion
    }
}
