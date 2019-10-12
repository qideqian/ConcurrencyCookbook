using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
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

        #region 10.4 异步属性
        class AsyncData
        {
            //属性可以直接返回一个Task<int>，不建议这么做。这种属性更适合改为方法。
            public Task<int> Data
            {
                get { return GetDataAsync(); }
            }

            private async Task<int> GetDataAsync()
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                return 13;
            }

            //异步属性，只启动一次
            public AsyncLazy<int> Data2
            {
                get { return _data; }
            }
            public AsyncLazy<int> _data = new AsyncLazy<int>(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                return 13;
            });
        }
        //调用方式
        async Task Example3()
        {
            var instance = new AsyncData();
            var value = await instance.Data;
        }
        #endregion

        #region 10.5 异步事件
        public class MyEventArgs : EventArgs
        {
            //延期处理器
            private readonly DeferralManager _deferrals = new DeferralManager();//编写异步事件处理器时，事件参数类最好是线程安全的，最简单办法就是让它不可变（设为自读）

            //自身构造函数和属性

            public IDisposable GetDeferral()
            {
                return _deferrals.DeferralSource.GetDeferral();
            }
            internal Task WaitForDeferralsAsync()
            {
                return _deferrals.WaitForDeferralsAsync();
            }
        }

        public event EventHandler<MyEventArgs> MyEvent;//要执行的事件
        private Task RaiseMyEventAsync()
        {
            var handler = MyEvent;
            if (handler == null)
                return Task.FromResult(0);
            var args = new MyEventArgs();
            handler(this, args);
            return args.WaitForDeferralsAsync();
        }

        //异步事件处理器
        async void AsyncHandler(object sender, MyEventArgs args)
        {
            using (args.GetDeferral())
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
        #endregion

        #region 10.6 异步销毁
        #region Disposable方式
        class MyClass1 : IDisposable
        {
            private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();
            public async Task<int> CalculateValueAsync()
            {
                await Task.Delay(TimeSpan.FromSeconds(2), _disposeCts.Token);
                return 13;
            }
            public void Dispose()
            {
                _disposeCts.Cancel();
            }

            //Dispose及CancellationToken均可销毁
            public async Task<int> CalculateValueAsync(CancellationToken cancellationToken)
            {
                using (var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token))
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), combinedCts.Token);
                    return 13;
                }
            }
        }
        async Task Test()
        {
            Task<int> task;
            using (var resource = new MyClass1())
            {
                task = resource.CalculateValueAsync();
            }
            var result = await task;//抛出异常OperationCanceledException.
        }
        #endregion

        #region “异步完成”方式
        //一些类需要知道操作完成的时间，需要采用实现“异步完成”的方式。
        //异步完成与异步初始化很相似：它们都很少有官方的指引资料。
        //以下是一种可行的模式，它基于TPL数据流块的运行方式。异步完成的重要部分可以封装在一个接口中。

        /// <summary>
        /// 表明一个类需要异步完成，并提供完成的结果。
        /// </summary>
        interface IAsyncCompletion
        {
            /// <summary>
            /// 开始本实例的完成过程。概念上类似于“IDisposable.Dispose”。
            /// 在调用本方法后，就不能调用除了“Completion”以外的任何成员。
            /// </summary>
            void Complete();

            /// <summary>
            /// 取得本实例完成的结果。
            /// </summary>
            Task Completion { get; }
        }
        class MyClass2 : IAsyncCompletion
        {
            private readonly TaskCompletionSource<object> _completion = new TaskCompletionSource<object>();
            private Task _completing;

            public Task Completion
            {
                get { return _completion.Task; }
            }

            public void Complete()
            {
                if (_completing != null) return;
                _completing = CompleteAsync();
            }

            private async Task CompleteAsync()
            {
                try
                {
                    //异步地等待任何运行中的操作
                    //异步做类的初始化代码？
                    await Task.FromResult(1);//如：←
                }
                catch (Exception ex)
                {
                    _completion.TrySetException(ex);
                }
                finally
                {
                    _completion.TrySetResult(null);
                }
            }

        }

        //调用的辅助方法
        static class AsyncHelpers
        {
            public static async Task Using<TResource>(Func<TResource> construct, Func<TResource, Task> process)
                where TResource : IAsyncCompletion
            {
                var resource = construct();//创建需要使用的资源
                Exception exception = null;//使用资源，并捕获所有异常
                try
                {
                    await process(resource);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                resource.Complete();//完成（逻辑上销毁）资源。
                await resource.Completion;
                //如果需要，就重新抛出“process”产生的异常
                if (exception != null)
                    ExceptionDispatchInfo.Capture(exception).Throw();
            }
            public static async Task<TResult> Using<TResource, TResult>(Func<TResource> construct, Func<TResource, Task<TResult>> process)
                where TResource : IAsyncCompletion
            {
                var resource = construct();//创建需要使用的资源
                Exception exception = null;//使用资源，并捕获所有异常
                TResult result = default(TResult);
                try
                {
                    result = await process(resource);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                resource.Complete();//完成（逻辑上销毁）资源。
                try
                {
                    await resource.Completion;
                }
                catch
                {
                    //只有当“process”没有抛出异常时，才允许抛出“Completion”的异常。
                    if (exception == null)
                        throw;
                }
                //如果需要，就重新抛出“process”产生的异常
                if (exception != null)
                    ExceptionDispatchInfo.Capture(exception).Throw();
                return result;
            }
        }
        //调用代码
        async Task Test2()
        {
            await AsyncHelpers.Using(() => new MyClass2(), async resource =>
            {
                // 使用资源。
                //跟新显示进度？
                await Task.FromResult(1);//如：←
            });
        }
        #endregion
        #endregion
    }
}
