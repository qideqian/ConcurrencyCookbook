using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace Cookbook
{
    class Chapter9 : Form
    {
        //取消是一种信号，包含两个不同的方面：触发取消的源头和相应取消的接收器。
        public void CancelableMethodWithOverload(CancellationToken cancellationToken)
        {
            //这里放代码
        }
        public void CancelableMethodWithOverload()
        {
            CancelableMethodWithOverload(CancellationToken.None);
        }
        public void CancelableMethodWithDefault(CancellationToken cancellationToken = default(CancellationToken))
        {
            //这里放代码
        }

        #region 9.1 发出取消请求
        private async Task CancalableMethodAsync(CancellationToken token)
        {
            await Task.FromResult(1);
        }
        void IssueCancelRequest()
        {
            var cts = new CancellationTokenSource();
            var task = CancalableMethodAsync(cts.Token);
            //到这里操作已经启动。
            cts.Cancel();//发出取消请求。
        }

        async Task IssueCancelRequestAsync()
        {
            var cts = new CancellationTokenSource();
            var task = CancalableMethodAsync(cts.Token);
            //这里操作正在允许
            cts.Cancel();//发出取消请求

            //（异步地）等待操作结束
            try
            {
                await task;//如果运行到这里，说明在取消请求生效前，操作正常完成。
            }
            catch (OperationCanceledException)
            {
                //如果允许到这里，说明操作在完成前被取消。
            }
            catch (Exception)
            {
                throw;//如果运行到这里，说明在取消请求生效前，操作出错并结束。
            }
        }

        private CancellationTokenSource _cts;
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            //StartButton.IsEnabled = false;
            //CancelButton.IsEnabled = true;
            try
            {
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                MessageBox.Show("Delay completed successfully.");
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Delay was canceled.");
            }
            catch (Exception)
            {
                MessageBox.Show("Delay completed with error.");
                throw;
            }
            finally
            {
                //StartButton.IsEnabled = true;
                //CancelButton.IsEnabled = false;
            }
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
        }
        #endregion

        #region 9.2 通过轮询响应取消请求
        public int CancelableMethod(CancellationToken cancellationToken)
        {
            for (int i = 0; i != 100; ++i)
            {
                Thread.Sleep(1000);//这里做一些计算工作。
                cancellationToken.ThrowIfCancellationRequested();
            }
            return 42;
        }
        public int CancelableMethod2(CancellationToken cancellationToken)
        {
            for (int i = 0; i != 100000; ++i)
            {
                Thread.Sleep(1);//这里做一些计算工作。
                if (i % 100 == 0)
                    cancellationToken.ThrowIfCancellationRequested();
            }
            return 42;
        }
        #endregion

        #region 9.3 超时后取消
        async Task IssueTimeoutAsync()
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var token = cts.Token;
            await Task.Delay(TimeSpan.FromSeconds(10), token);
        }

        async Task IssueTimeoutAsync2()
        {
            var cts = new CancellationTokenSource();
            var token = cts.Token;
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            await Task.Delay(TimeSpan.FromSeconds(10), token);
        }
        #endregion

        #region 9.4 取消async代码
        public async Task<int> CancelableMethodAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            return 42;
        }
        #endregion

        #region 9.5 取消并行代码
        static void RotateMatrices(IEnumerable<Matrix> matrices, float degrees, CancellationToken token)
        {
            Parallel.ForEach(matrices, new ParallelOptions { CancellationToken = token }, matrix => matrix.Rotate(degrees));
        }
        //不推荐使用。会把错误封装近AggregateException中
        static void RotateMatrices2(IEnumerable<Matrix> matrices, float degrees, CancellationToken token)
        {
            Parallel.ForEach(matrices, matrix =>
            {
                matrix.Rotate(degrees);
                token.ThrowIfCancellationRequested();
            });
        }

        //并行LINQ(PLINQ)
        static IEnumerable<int> MultiplyBy2(IEnumerable<int> values, CancellationToken cancellationToken)
        {
            return values.AsParallel().WithCancellation(cancellationToken).Select(item => item * 2);
        }
        #endregion

        #region 9.6 取消响应式代码
        //注：Rx有它自己关于取消的理念，那就是：停止订阅。.Net4.0引入了通用的取消框架。
        //本节介绍了几种让Rx与这种通用框架有机融合的方法。
        //如果某段代码中只用到了Rx，那就使用Rx的"订阅/停止订阅"体系。
        //只有在边界上才引入CancellationToken，以保持代码清晰。

        private IDisposable _mouseMovesSubscription;
        private void StartButton_Click2(object sender, RoutedEventArgs e)
        {
            var mouseMove = Observable
                .FromEventPattern<MouseEventHandler, MouseEventArgs>(
                    handler => (s, a) => handler(s, a),
                    handler => MouseMove += handler,
                    handler => MouseMove -= handler)
                .Select(x => x.EventArgs);
            _mouseMovesSubscription = mouseMove.Subscribe(val =>
            {
                Trace.WriteLine("(" + val.X + "," + val.Y + ")");
            });
        }
        private void CancelButton_Click2(object sender, RoutedEventArgs e)
        {
            if (_mouseMovesSubscription != null)
                _mouseMovesSubscription.Dispose();
        }

        //捕获事件流最后一个事件
        public static async void GetLastAsync(CancellationToken cancellationToken)
        {
            IObservable<int> observable = Observable.Return(1);
            int lastElement = await observable.TakeLast(1).ToTask(cancellationToken);
            int lastElement2 = await observable.ToTask(cancellationToken);
        }
        //捕获事件流中的下一个事件
        public static async void GetFirstAsync(CancellationToken cancellationToken)
        {
            IObservable<int> observable = Observable.Return(1);
            int nextElement = await observable.Take(1).ToTask(cancellationToken);
        }
        //捕获事件流中所有的事件
        public static async void GetAll(CancellationToken cancellationToken)
        {
            IObservable<int> observable = Observable.Return(1);
            IList<int> allElement = await observable.ToList().ToTask(cancellationToken);
        }

        //把发出的取消请求作为对释放订阅接口的相应。
        public void Example()
        {
            using(var cancellation = new CancellationDisposable())
            {
                CancellationToken token = cancellation.Token;//把这个标记传递给对它作出相应的方法
            }
            //到这里，这个标记已经是取消的。
        }
        #endregion
    }
}
