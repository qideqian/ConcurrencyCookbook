using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Forms;

namespace Cookbook
{
    //要使用Rx,需要在应用中安装一个NuGet包Rx-Main。
    //Install-Package Rx-Main -Version 2.3.0-beta2
    //更名为：System.Reactive
    class Chapter5 : Form
    {
        #region 5.1转换.NET事件
        static void Example1()
        {
            var progress = new Progress<int>();
            var progressReports = Observable.FromEventPattern<int>(handler => progress.ProgressChanged += handler, handler => progress.ProgressChanged -= handler);
            progressReports.Subscribe(DataMisalignedException => Trace.WriteLine("OnNext:" + DataMisalignedException.EventArgs));
        }

        static void Example2()
        {
            var timer = new System.Timers.Timer(interval: 1000) { Enabled = true };
            var ticks = Observable.FromEventPattern<ElapsedEventHandler, ElapsedEventArgs>(
                handler => (s, a) => handler(s, a),
                handler => timer.Elapsed += handler,
                handler => timer.Elapsed -= handler);
            ticks.Subscribe(DataMisalignedException => Trace.WriteLine("OnNext:" + DataMisalignedException.EventArgs.SignalTime));
        }

        static void Example3()
        {
            var timer = new System.Timers.Timer(interval: 1000) { Enabled = true };
            var ticks = Observable.FromEventPattern(timer, "Elapsed");
            ticks.Subscribe(data => Trace.WriteLine("OnNext:" + ((ElapsedEventArgs)data.EventArgs).SignalTime));
        }

        static void Example4()
        {
            var client = new WebClient();
            var downloadedStrings = Observable.FromEventPattern(client, "DownloadStringCompledted");
            downloadedStrings.Subscribe(data =>
            {
                var eventArgs = (DownloadStringCompletedEventArgs)data.EventArgs;
                if (eventArgs.Error != null) Trace.WriteLine("OnNext:" + eventArgs.Error);
                else Trace.WriteLine("OnNext:" + eventArgs.Result);
            },
            ex => Trace.WriteLine("OnError:" + ex.ToString()),
            () => Trace.WriteLine("OnCompleted"));
            client.DownloadStringAsync(new Uri("http://invalid.example.com/"));
        }
        #endregion

        #region 5.2发通知给上下文
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("UI thread is " + Environment.CurrentManagedThreadId);
            Observable.Interval(TimeSpan.FromSeconds(1)).Subscribe(x => Trace.WriteLine("Interval " + x + " On thread " + Environment.CurrentManagedThreadId));
        }
        private void Button_Click2(object sender, RoutedEventArgs e)
        {
            //通知到UI线程
            var uiContext = SynchronizationContext.Current;
            Trace.WriteLine("UI thread is " + Environment.CurrentManagedThreadId);
            Observable.Interval(TimeSpan.FromSeconds(1)).SubscribeOn(uiContext).Subscribe(x => Trace.WriteLine("Interval " + x + " On thread " + Environment.CurrentManagedThreadId));
        }
        private void Button_Click3(object sender, RoutedEventArgs e)
        {
            //通知到UI线程
            var uiContext = SynchronizationContext.Current;
            Trace.WriteLine("UI thread is " + Environment.CurrentManagedThreadId);
            Observable.FromEventPattern<MouseEventHandler, MouseEventArgs>(handler => (s, a) => handler(s, a), handler => MouseMove += handler, handler => MouseMove -= handler)
                .Select(evt => evt.EventArgs)
                .ObserveOn(Scheduler.Default)
                .Select(position =>
                {
                    //复杂的计算过程
                    Thread.Sleep(100);
                    var result = position.X + position.Y;
                    Trace.WriteLine("Calculated result " + result + " on thread " + Environment.CurrentManagedThreadId);
                    return result;
                })
                .ObserveOn(uiContext)
                .Subscribe(x => Trace.WriteLine("Result " + x + " on thread " + Environment.CurrentManagedThreadId));
        }
        #endregion

        #region 5.3用窗口和缓冲对事件分组
        private void Button_Click4(object sender, RoutedEventArgs e)
        {
            //Buffer驻留指定数量的到达事件，然后以一个集合的形式一次性地传送过去。
            Observable.Interval(TimeSpan.FromSeconds(1)).Buffer(2).Subscribe(x => Trace.WriteLine(DateTime.Now.Second + ":Got " + x[0] + " and " + x[1]));

            Observable.Interval(TimeSpan.FromSeconds(1)).Window(2).Subscribe(group =>
            {
                Trace.WriteLine(DateTime.Now.Second + ":Starting new group");
                group.Subscribe(
                    x => Trace.WriteLine(DateTime.Now.Second + ":Saw " + x),
                    () => Trace.WriteLine(DateTime.Now.Second + ":Ending group"));
            });
        }
        private void Button_Click5(object sender, RoutedEventArgs e)
        {
            //抑制输入信息，并把输入塑造成我们想要的样子。
            Observable.FromEventPattern<MouseEventHandler, MouseEventArgs>(handler => (s, a) => handler(s, a), handler => MouseMove += handler, handler => MouseMove -= handler)
                .Buffer(TimeSpan.FromSeconds(1))
                .Subscribe(x => Trace.WriteLine(DateTime.Now.Second + ":Saw " + x.Count + " items."));
        }
        #endregion

        #region 5.4用限流和抽样抑制事件流
        private void Button_Click6(object sender, RoutedEventArgs e)
        {
            Observable.FromEventPattern<MouseEventHandler, MouseEventArgs>(handler => (s, a) => handler(s, a), handler => MouseMove += handler, handler => MouseMove -= handler)
                .Select(x => x.EventArgs)
                .Throttle(TimeSpan.FromSeconds(1))//事件在指定时间段内不再发生时才执行。如果指定的时间段内一直有新事件发生，将一直不会执行。
                .Subscribe(x => Trace.WriteLine(DateTime.Now.Second + ":Saw " + x.X + x.Y + " items."));

            Observable.FromEventPattern<MouseEventHandler, MouseEventArgs>(handler => (s, a) => handler(s, a), handler => MouseMove += handler, handler => MouseMove -= handler)
                .Select(x => x.EventArgs)
                .Sample(TimeSpan.FromSeconds(1))//执行在指定时间段内发生的最后一次事件，如果时间段内没有发送任何事件，将不会执行。
                .Subscribe(x => Trace.WriteLine(DateTime.Now.Second + ":Saw " + x.X + x.Y + " items."));
        }
        #endregion

        #region 5.5超时（超时后内部的操作并没有真正取消，操作将继续执行，直到成功或失败）
        private void Button_Click7(object sender, RoutedEventArgs e)
        {
            //向域名发送一个web请求，并使用1秒超时
            var client = new HttpClient();
            client.GetStringAsync("http://www.example.com/").ToObservable()
                .Timeout(TimeSpan.FromSeconds(1))
                .Subscribe(x => Trace.WriteLine(DateTime.Now.Second + ":Saw " + x.Length), ex => Trace.WriteLine(ex));

            //鼠标1秒不移动后抛出异常并结束流
            Observable.FromEventPattern<MouseEventHandler, MouseEventArgs>(handler => (s, a) => handler(s, a), handler => MouseMove += handler, handler => MouseMove -= handler)
                .Select(x => x.EventArgs)
                .Timeout(TimeSpan.FromSeconds(1))
                .Subscribe(x => Trace.WriteLine(DateTime.Now.Second + ":Saw " + (x.X + x.Y)), ex => Trace.WriteLine(ex));

            //超时后由clicks事件序列代替
            var clicks = Observable.FromEventPattern<MouseEventHandler, MouseEventArgs>(handler => (s, a) => handler(s, a), handler => MouseMove += handler, handler => MouseMove -= handler)
                .Select(x => x.EventArgs);
            Observable.FromEventPattern<MouseEventHandler, MouseEventArgs>(handler => (s, a) => handler(s, a), handler => MouseMove += handler, handler => MouseMove -= handler)
                .Select(x => x.EventArgs)
                .Timeout(TimeSpan.FromSeconds(1), clicks)
                .Subscribe(x => Trace.WriteLine(DateTime.Now.Second + ":Saw " + (x.X + x.Y)), ex => Trace.WriteLine(ex));
        }
        #endregion
    }
}
