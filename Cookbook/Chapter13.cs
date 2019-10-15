using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace Cookbook
{
    public class Chapter13
    {
        #region 13.1 初始化共享资源（程序的多个部分共享了一个资源，现在要在第一次访问该资源时对它初始化）
        class MyClass1
        {
            static int _simpleValue;
            static readonly Lazy<int> MySharedInteger = new Lazy<int>(() => _simpleValue++);
            void UseSharedInteger()
            {
                int sharedValue = MySharedInteger.Value;//值永远为0
            }
        }
        //可等待
        public class MyClass2
        {
            static int _simpleValue;
            public static readonly Lazy<Task<int>> MySharedAsyncInteger = new Lazy<Task<int>>(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                return _simpleValue++;
            });

            public async Task GetSharedIntegerAsync()
            {
                int sharedValue = await MySharedAsyncInteger.Value;
            }
        }
        //让委托在线程池中运行
        class MyClass3
        {
            static int _simpleValue;
            static readonly Lazy<Task<int>> MySharedAsyncInterget = new Lazy<Task<int>>(() => Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                return _simpleValue++;
            }));
        }
        //AsynEx库
        class MyClass4
        {
            static int _simpleValue;
            private static readonly AsyncLazy<int> MySharedAsyncInteger = new AsyncLazy<int>(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                return _simpleValue++;
            });
            public async Task UseSharedIntegerAsync()
            {
                int sharedValue = await MySharedAsyncInteger;
            }
        }
        #endregion

        #region 13.2 Rx延迟求职（想要在每次被订阅时就创建一个新的源observable对象。例如让每个订阅代表一个不同的Web服务请求。）
        //Rx库有一个操作符Observable.Defer，每次observable对象被订阅时，它就会执行一个委托。
        //该委托相当于是一个创建ovservable对象的工厂。
        void Example1()
        {
            var invokeServerObservable = Observable.Defer(() => GetValueAsync().ToObservable());
            invokeServerObservable.Subscribe(_ => { });
            invokeServerObservable.Subscribe(_ => { });
            Console.ReadKey();
        }
        static async Task<int> GetValueAsync()
        {
            Console.WriteLine("Calling server...");
            await Task.Delay(TimeSpan.FromSeconds(2));
            Console.WriteLine("Returning result...");
            return 13;
        }
        #endregion

        #region 13.3 异步数据绑定（异步地检索数据时，需要对结果进行数据绑定）
        class MyViewModel
        {
            //public MyViewModel()
            //{
            //    MyValue = NotifyTaskCompletion.Create(CalculateMyValueAsync())
            //}
            //public INotifyTaskCompletion<int> MyValue { get; private set; }
            //private async Task<int> CalculateMyValueAsync()
            //{
            //    await Task.Delay(TimeSpan.FromSeconds(10));
            //    return 13;
            //}
        }

        //编写自己的数据绑定的封装类代替AsyncEx库中的类
        class BindableTask<T> : INotifyPropertyChanged
        {
            private readonly Task<T> _task;
            public BindableTask(Task<T> task)
            {
                _task = task;
                var _ = WatchTaskAsync();
            }

            private async Task WatchTaskAsync()
            {
                try
                {
                    await _task;
                }
                catch { }
                OnPropertyChanged("IsNotCompleted");
                OnPropertyChanged("IsSuccessfullyCompleted");
                OnPropertyChanged("IsFaulted");
                OnPropertyChanged("Result");
            }
            public bool IsNotCompleted { get { return !_task.IsCompleted; } }
            public bool IsSuccessfullyCompleted { get { return _task.Status == TaskStatus.RanToCompletion; } }
            public bool IsFaulted { get { return _task.IsFaulted; } }
            public T Result { get { return IsSuccessfullyCompleted ? _task.Result : default(T); } }

            public event PropertyChangedEventHandler PropertyChanged;//因此事件会在UI线程中引发，不能使用ConfigureAwait(false)
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler == null) handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion

        #region 13.4 隐式状态（程序中有一些状态，要求调用栈的不同位置都可以访问。）.NET4.5+才支持
        //例如，在记录日志时要使用一个当前操作的标识符，但是又不希望把它作为参数添加到一个方法中。（当然，建议使用方法参数或把数据存储在类的成员中，那样效率更高）
        //在编写ASP.NET程序时可考虑使用HttpContext.Current.Items，它的功能和CallContext一样，但效率更高。
        void DoLongOperation()
        {
            var operationId = Guid.NewGuid();
            CallContext.LogicalSetData("OperationId", operationId);
            DoSomeStepOfOperation();
        }

        void DoSomeStepOfOperation()
        {
            //在这里记录日志
            Trace.WriteLine("In operation:" + CallContext.LogicalGetData("OperationId"));
        }
        #endregion
    }
}
