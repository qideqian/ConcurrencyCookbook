using Nito.AsyncEx.Synchronous;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Cookbook
{
    static class Chapter7
    {
        #region 7.1 用async代码封装Async方法与Completed事件(EAP)
        public static Task<string> DownloadStringTaskAsync(this WebClient client, Uri address)
        {
            var tcs = new TaskCompletionSource<string>();
            //这个事件处理程序会完成Task对象，并自行注销。
            DownloadStringCompletedEventHandler handler = null;
            handler = (_, e) =>
            {
                client.DownloadStringCompleted -= handler;
                if (e.Cancelled)
                    tcs.TrySetCanceled();
                else if (e.Error != null)
                    tcs.TrySetException(e.Error);
                else
                    tcs.TrySetResult(e.Result);
            };
            //登记事件，然后开始操作。
            client.DownloadStringCompleted += handler;
            client.DownloadStringAsync(address);
            return tcs.Task;
        }

        // 利用Nuget库Noto.AsyncEx
        public static Task<string> DownloadStringTaskAsync2(this WebClient client, Uri address)
        {
            var tcs = new TaskCompletionSource<string>();
            //这个事件处理程序会完成Task对象，并自行注销。
            DownloadStringCompletedEventHandler handler = null;
            handler = (_, e) =>
            {
                client.DownloadStringCompleted -= handler;
                //tcs.TryCompleteFromEventArgs(e, () => e.Result);
            };
            //登记事件，然后开始操作。
            client.DownloadStringCompleted += handler;
            client.DownloadStringAsync(address);
            return tcs.Task;
        }
        #endregion

        #region 7.2 用async代码封装Begin/End方法(APM)
        public static Task<WebResponse> GetResponseAsync(this WebRequest client)
        {
            return Task<WebResponse>.Factory.FromAsync(client.BeginGetResponse, client.EndGetResponse, null);
        }
        #endregion

        #region 7.3 用async代码封装所有异步操作
        public interface IMyAsyncHttpService
        {
            void DownloadString(Uri address, Action<string, Exception> action);
        }

        public static Task<string> DownloadStringAsync(this IMyAsyncHttpService httpService, Uri address)
        {
            var tcs = new TaskCompletionSource<string>();
            httpService.DownloadString(address, (result, exception) =>
            {
                if (exception != null)
                    tcs.TrySetException(exception);
                else
                    tcs.TrySetResult(result);
            });
            return tcs.Task;
        }
        #endregion

        #region 7.4 用async代码封装并行代码
        public static async void Run()
        {
            var list = new List<string>();
            await Task.Run(() => Parallel.ForEach(list, (s) => { }));
        }
        #endregion

        #region 7.5 用async代码封装Rx Observable对象
        //捕获事件流最后一个事件
        public static async void GetLastAsync()
        {
            IObservable<int> observable = Observable.Return(1);
            int lastElement = await observable;
        }

        //捕获事件流中的下一个事件
        public static async void GetFirstAsync()
        {
            IObservable<int> observable = Observable.Return(1);
            int nextElement = await observable.FirstAsync();
        }

        //捕获事件流中所有的事件
        public static async void GetAll()
        {
            IObservable<int> observable = Observable.Return(1);
            IList<int> allElement = await observable.ToList();
        }
        #endregion

        #region 7.6 用Rx Observable对象结合(ToObservable和StartAsync都返回一个observable对象，标识一个已经启动的异步操作。FromAsync在每次被订阅时都会启动一个全新独立的异步操作)
        //使用ToObservable前必须调用async方法并转换成Task对象。会立即启动异步操作
        public static void TaskToObservable()
        {
            var client = new HttpClient();
            IObservable<HttpResponseMessage> response = client.GetAsync("http://www.example.com/").ToObservable();
        }
        //支持取消功能，如果订阅已被处理，这个async方法会被取消。
        public static void TaskToObservable2()
        {
            var client = new HttpClient();
            IObservable<HttpResponseMessage> response = Observable.StartAsync(token => client.GetAsync("http://www.example.com/", token));
        }
        //订阅后才启动操作
        public static void TaskToObservable3()
        {
            var client = new HttpClient();
            IObservable<HttpResponseMessage> response = Observable.FromAsync(token => client.GetAsync("http://www.example.com/", token));
        }
        //可在源事件流中每到达一个事件就启动一个异步操作，可使用SelectMany的特殊重载
        //例子使用一个已后的URL事件流，在每个URL到达时发送一个请求。
        public static void TaskToObservable4()
        {
            IObservable<string> urls = Observable.Return("");
            var client = new HttpClient();
            IObservable<HttpResponseMessage> response = urls.SelectMany((url, token) => client.GetAsync(url, token));
        }
        #endregion

        #region 7.7 Rx Observable对象和数据流网格
        //把数据流块用作可观察流的输入
        //调用AsObservable来创建一个缓冲块到Observable对象的接口
        public static void BlockToObservable()
        {
            var buffer = new BufferBlock<int>();
            IObservable<int> integers = buffer.AsObservable();
            integers.Subscribe(data => Trace.WriteLine(data), ex => Trace.WriteLine(ex), () => Trace.WriteLine("Done"));
            buffer.Post(13);
        }
        //调用AsObserver让一个块订阅一个可观察流。
        public static void ObservableToBlock()
        {
            IObservable<DateTimeOffset> ticks = Observable.Interval(TimeSpan.FromSeconds(1)).Timestamp().Select(x => x.Timestamp).Take(5);
            var display = new ActionBlock<DateTimeOffset>(x => Trace.WriteLine(x));
            ticks.Subscribe(display.AsObserver());
            try
            {
                display.Completion.Wait();
                Trace.WriteLine("Dowe.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }
        #endregion
    }
}
