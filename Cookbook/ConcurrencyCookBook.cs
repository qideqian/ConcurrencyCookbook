using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Cookbook
{
    class ConcurrencyCookbook
    {
        #region 2.1暂停一段时间
        /// <summary>
        /// 用于单元测试，定义一个异步完成的任务
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        static async Task<T> DelayResult<T>(T result, TimeSpan delay)
        {
            await Task.Delay(delay);
            return result;
        }

        /// <summary>
        /// 指数退避
        /// 一种重试策略
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        static async Task<string> DownloadStringWithRetries(string uri)
        {
            using (var client = new HttpClient())
            {
                //第1次重试等1秒，第2次等2秒，第3次等4秒。
                var nextDelay = TimeSpan.FromSeconds(1);
                for (int i = 0; i != 3; ++i)
                {
                    try
                    {
                        return await client.GetStringAsync(uri);
                    }
                    catch
                    {
                    }
                    await Task.Delay(nextDelay);
                    nextDelay = nextDelay + nextDelay;
                }
                //最后重试一次，以便让调用者知道出错信息。
                return await client.GetStringAsync(uri);
            }
        }

        /// <summary>
        /// Task.Delay实现一个简单的超时功能
        /// 本例代码目的是：如果服务在3秒内没有响应，就返回null
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        static async Task<string> DownloadStringWithTimeout(string uri)
        {
            using (var client = new HttpClient())
            {
                var downloadTask = client.GetStringAsync(uri);
                var timeoutTask = Task.Delay(3000);

                var completedTask = await Task.WhenAny(downloadTask, timeoutTask);
                if (completedTask == timeoutTask)
                    return null;
                return await downloadTask;
            }
        }
        #endregion

        #region 2.2返回完成的任务
        /// <summary>
        /// Task.FromResult只能提供结果正确的同步Task对象。其它类型的结果需要使用TaskCompletionSource
        /// Task.FromResult只不过是TaskCompletionSource的一个简化版本
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        static Task<T> NotImplementedAsync<T>()
        {
            var tcs = new TaskCompletionSource<T>();
            tcs.SetException(new NotImplementedException());
            return tcs.Task;
        }
        #endregion

        #region 2.3报告进度
        /// <summary>
        /// 使用IProgress<T>和Progress<T>类型。编写的async方法需要有IProgress<T>参数，其中T是需要报告的进度类型
        /// T最好为不可变类型，或者至少是一个值类型。当T是一个可变的引用类型时，必须每次调用IProgress<T>.Report时，创建一个单独的副本。
        /// </summary>
        /// <param name="progress"></param>
        /// <returns></returns>
        static async Task MyMethodAsync(IProgress<double> progress = null)
        {
            double percentComplete = 0;
            var num = 100;//模拟任务总数
            while (percentComplete < num)
            {
                await Task.Delay(1);//模拟实际执行的任务
                percentComplete++;//任务进度+1

                if (progress != null)
                    progress.Report(percentComplete);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        static async Task CallMyMethodAsync()
        {
            var progress = new Progress<double>();
            progress.ProgressChanged += (sender, args) =>
            {
                //输出：已完成arge个任务
            };
            await MyMethodAsync(progress);
        }
        #endregion

        #region 2.4等待一组任务完成
        static async Task<string> DwonloadAllAsync(IEnumerable<string> urls)
        {
            var httpClient = new HttpClient();

            //定义每一个rul的使用方法
            var downloads = urls.Select(url => httpClient.GetStringAsync(url));
            //注意，到这里，序列还没有求值，所以所有任务都还没真正启动

            //下面，所有的URL下载同步开始
            Task<string>[] downloadTasks = downloads.ToArray();
            //到这里，所有的任务已经开始执行了。

            //用异步方式等待所有下载完成。
            string[] htmlPages = await Task.WhenAll(downloadTasks);

            return string.Concat(htmlPages);
        }


        static async Task ThrowNotImplementedExceptionAsync()
        {
            throw new NotImplementedException();
        }
        static async Task ThrowInvalidOperationExceptionAsync()
        {
            throw new InvalidOperationException();
        }
        static async Task ObserveOneExceptionAsync()
        {
            var task1 = ThrowNotImplementedExceptionAsync();
            var task2 = ThrowInvalidOperationExceptionAsync();
            try
            {
                await Task.WhenAll(task1, task2);
            }
            catch (Exception ex)
            {
                //ex 要么是NotImplementedException，要么是InvalidOperationException
            }
        }
        static async Task ObserveAllExceptionsAsync()
        {
            var task1 = ThrowNotImplementedExceptionAsync();
            var task2 = ThrowInvalidOperationExceptionAsync();
            Task allTasks = Task.WhenAll(task1, task2);
            try
            {
                await allTasks;
            }
            catch
            {
                AggregateException allException = allTasks.Exception;
            }
        }

        #endregion

        #region 2.5等待任意一个任务完成
        /// <summary>
        /// 返回第一个响应的URL的数据长度。
        /// </summary>
        /// <param name="urlA"></param>
        /// <param name="urlB"></param>
        /// <returns></returns>
        private static async Task<int> FirstRespondingUrlAsync(string urlA,string urlB)
        {
            var httpClient = new HttpClient();
            //并发地开始两个下载任务。
            Task<byte[]> downloadTaskA = httpClient.GetByteArrayAsync(urlA);
            Task<byte[]> downloadTaskB = httpClient.GetByteArrayAsync(urlB);
            //等待任意一个任务完成。
            Task<byte[]> completedTask = await Task.WhenAny(downloadTaskA, downloadTaskB);
            //返回从URL得到的数据的长度
            byte[] data = await completedTask;
            return data.Length;
        }
        #endregion

        #region 2.6任务完成时的处理

        #endregion
    }

    #region 2.2返回完成的任务
    interface IMyAsyncInterface
    {
        Task<int> GetValueAsync();
    }
    class MySynchronousImplementation : IMyAsyncInterface
    {
        public Task<int> GetValueAsync()
        {
            return Task.FromResult(13);//可以使用Task.FromResult方法创建并返回一个新的Task<T>对象，这个Task对象是已经完成的，并有指定的值。
        }
    }
    #endregion
}
