using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
//using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Cookbook
{
    class Chapter11
    {
        //需要同步的三个条件是：多段代码、共享数据、修改数据
        #region 11.1 阻塞锁（多个线程需要安全地读写共享数据。）
        //锁的四条重要规则
        //1.限制锁的作用范围。
        //2.文档中写清锁保护的内容。
        //3.锁范围内的代码尽量少。
        //4.在控制锁的时候绝不运行随意的代码。

        //最好的办法是使用lock语句。一个线程进入锁后，在锁被释放之前其它线程是无法进入的
        class MyClass1
        {
            //这个锁会保护_Value。
            private readonly object _mutex = new object();
            private int _value;
            public void Increment()
            {
                lock (_mutex)
                {
                    _value = _value + 1;
                }
            }
        }
        #endregion

        #region 11.2 异步锁（多个代码块需要安全地读写共享数据，并且这些代码块可能使用await语句。）
        //只有.NET4.5或更高本本才支持SemaphoreSlim
        class MyClass2
        {
            //这个锁保护_value
            private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1);
            private int _value;
            public async Task DelayAndIncrementAsync()
            {
                await _mutex.WaitAsync();
                try
                {
                    var oldValue = _value;
                    await Task.Delay(TimeSpan.FromSeconds(oldValue));
                    _value = oldValue + 1;
                }
                finally
                {
                    _mutex.Release();
                }
            }
        }
        //旧版本框架或是编写可移植类库。
        class MyClass3
        {
            private readonly AsyncLock _mutex = new AsyncLock();
            private int _value;
            public async Task DelayAndIncrementAsync()
            {
                using (await _mutex.LockAsync())
                {
                    var oldValue = _value;
                    await Task.Delay(TimeSpan.FromSeconds(oldValue));
                    _value = oldValue + 1;
                }
            }
        }
        #endregion

        #region 11.3 阻塞信号（需要从一个线程发送信号给另外一个线程）
        //ManualResetEventSlim是功能强大、通用的线程间信号，但必须合理地使用。
        //如果这个信号其实是一个线程间发送小块数据的信息，那可考虑使用生产者/消费者队列。
        //另一方面，如果信号只是用来协调对共享数据的访问，那可改用锁。
        class MyClass4
        {
            private readonly ManualResetEventSlim _initialized = new ManualResetEventSlim();
            private int _value;
            public int WaitForInitialization()
            {
                _initialized.Wait();
                return _value;
            }
            public void InitializeFromAnotherThread()
            {
                _value = 13;
                _initialized.Set();
            }
        }
        #endregion

        #region 11.4 异步信号（需要在代码的各个部分间发送通知，并且要求接收方必须进行异步等待。）
        //只需要发送一次通知
        class MyClass5
        {
            private readonly TaskCompletionSource<object> _initialized = new TaskCompletionSource<object>();
            private int _value1;
            private int _value2;
            public async Task<int> WaitForInitializationAsync()
            {
                await _initialized.Task;
                return _value1 + _value2;
            }
            public void Initialize()
            {
                _value1 = 13;
                _value2 = 17;
                _initialized.TrySetResult(null);
            }
        }

        //可打开和关闭信号（相当于异步的ManualResetEvent）
        class MyClass6
        {
            private readonly AsyncManualResetEvent _connected = new AsyncManualResetEvent();
            public async Task WaitForConnectedAsync()
            {
                await _connected.WaitAsync();
            }
            public void ConnectedChanged(bool connected)
            {
                if (connected)
                    _connected.Set();
                else
                    _connected.Reset();
            }
        }
        #endregion

        #region 11.5 限流（有一段高度并发的代码，由于它的并发程度实在太高了，需要有方法对并发性进行限流。）
        //代码并发程度太高，是指程序中的一部分无法跟上另一部分的速度，导致数据项累积并消耗内存。
        //这种情况下对部分代码进行限流，可以避免占用太多的内存。

        //代码并发的并发类型，解决方法各有不同。这些解决方案都是把并发性限制在某个范围之内。
        //数据流和并行代码都自带了对并发限流的方法
        IPropagatorBlock<int, int> DataflowMultiplyBy2()
        {
            var options = new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 10 };
            return new TransformBlock<int, int>(data => data * 2, options);
        }
        //使用PLINQ
        IEnumerable<int> ParallelMultiplyBy2(IEnumerable<int> values)
        {
            return values.AsParallel().WithDegreeOfParallelism(10).Select(item => item * 2);
        }
        //使用Parallel类
        void ParallelRotateMatrices(IEnumerable<Matrix> matrices, float degrees)
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
            Parallel.ForEach(matrices, options, matrix => matrix.Rotate(degrees));
        }
        //并发性异步代码可以用SemaphoreSlim来限流
        async Task<string[]> DownloadUrlsAsync(IEnumerable<string> urls)
        {
            var httpClient = new HttpClient();
            var semaphore = new SemaphoreSlim(10);
            var tasks = urls.Select(async url =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await httpClient.GetStringAsync(url);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();
            return await Task.WhenAll(tasks);
        }
        #endregion
    }
}
