using Microsoft.Reactive.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Cookbook
{
    public class Chapter6
    {
        #region 6.1async方法的单元测试
        [TestMethod]
        public async Task MyMethodAsync_ReturnFalse()
        {
            bool result = await Task.FromResult<bool>(false);
            Assert.IsFalse(result);

            //AsyncContext.Run会等待所有异步方法完成
            AsyncContext.Run(async () =>
            {
                bool result2 = await Task.FromResult<bool>(false);
                Assert.IsFalse(result2);
            });
        }
        #endregion

        #region 6.2预计失败的async方法的单元测试
        //常规ExpectedExceptionAttribute进行错误测，不推荐用这种方式
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public async Task Divide_WhenDenominatorIsZero_ThrowsDivideByZero()
        {
            await MyClass.DivideAsync(4, 0);
        }

        [TestMethod]
        public async Task Divide_WhenDenominatorIsZero_ThrowsDivideByZero2()
        {
            await Assert.ThrowsExceptionAsync<DivideByZeroException>(async () =>
            {
                await MyClass.DivideAsync(4, 0);
            });
        }

        /// <summary>
        /// 确保一个异步委托抛出异常
        /// △△△△△△△△△△△△
        /// </summary>
        /// <typeparam name="TException">所预计异常的类型</typeparam>
        /// <param name="action">被测试的异步委托</param>
        /// <param name="allowDerivedTypes">是否接受派生的类</param>
        /// <returns></returns>
        public static async Task ThrowsExceptionAsync<TException>(Func<Task> action, bool allowDerivedTypes = true)
        {
            try
            {
                await action();
                Assert.Fail("Delegate did not throw expected exception " + typeof(TException).Name + ".");
                //Assert.Fail("未引发预期的异常" + typeof(TException).Name + "。");
            }
            catch (Exception ex)
            {
                if (allowDerivedTypes && !(ex is TException))
                    Assert.Fail("Delegate throw exception of type " + ex.GetType().Name + ",but " + typeof(TException).Name + " or a derived type was expected.");
                //Assert.Fail("引发异常异常" + ex.GetType().Name + "，但不是预期的异常" + typeof(TException).Name + "也不是" + typeof(TException).Name + "的衍生异常。");
                if (!allowDerivedTypes && ex.GetType() != typeof(TException))
                    Assert.Fail("Delegate threw exception of type " + ex.GetType().Name + ",but " + typeof(TException).Name + " was expected.");
                //Assert.Fail("引发异常异常" + ex.GetType().Name + ",但不是预期的异常" + typeof(TException).Name + "。");
            }
        }
        #endregion

        #region 6.3 async void方法的单元测试
        //利用AsyncContext.Run，这个类会等待所有异步操作完成（包括async void 方法），再将异常传递出去。
        #endregion

        #region 6.4 数据流网络的单元测试
        [TestMethod]
        public async Task MyCustomBlock_AddsOneToDataItems()
        {
            var myCustomBlock = Chapter4.CreateMyCustomBlock();
            myCustomBlock.Post(3);
            myCustomBlock.Post(13);
            myCustomBlock.Complete();

            Assert.AreEqual(4, myCustomBlock.Receive());
            Assert.AreEqual(14, myCustomBlock.Receive());
            await myCustomBlock.Completion;
        }

        [TestMethod]
        public async Task MyCustomBlock_Fault_DiscardsDataAndFaults()
        {
            var myCustomBlock = Chapter4.CreateMyCustomBlock();
            myCustomBlock.Post(3);
            myCustomBlock.Post(13);
            myCustomBlock.Fault(new InvalidOperationException());
            try
            {
                await myCustomBlock.Completion;
            }
            catch (AggregateException ex)
            {
                AssertExceptionIs<InvalidOperationException>(ex.Flatten().InnerException, false);
            }
        }

        public static void AssertExceptionIs<TException>(Exception ex, bool allowDerivedTypes = true)
        {
            if (allowDerivedTypes && !(ex is TException))
                Assert.Fail("Delegate throw exception of type " + ex.GetType().Name + ",but " + typeof(TException).Name + " or a derived type was expected.");
            //Assert.Fail("引发异常异常" + ex.GetType().Name + "，但不是预期的异常" + typeof(TException).Name + "也不是" + typeof(TException).Name + "的衍生异常。");
            if (!allowDerivedTypes && ex.GetType() != typeof(TException))
                Assert.Fail("Delegate threw exception of type " + ex.GetType().Name + ",but " + typeof(TException).Name + " was expected.");
            //Assert.Fail("引发异常异常" + ex.GetType().Name + ",但不是预期的异常" + typeof(TException).Name + "。");
        }
        #endregion

        #region 6.5 Rx Observable对象的单元测试
        public interface IHttpService
        {
            IObservable<string> GetString(string url);
        }
        public class MyTimeOutClass
        {
            private readonly IHttpService _httpService;

            public MyTimeOutClass(IHttpService httpService)
            {
                _httpService = httpService;
            }
            public IObservable<string> GetStringWithTimeout(string url)
            {
                return _httpService.GetString(url).Timeout(TimeSpan.FromSeconds(1));
            }
            public IObservable<string> GetStringWithTimeout(string url, IScheduler scheduler = null)
            {
                return _httpService.GetString(url).Timeout(TimeSpan.FromSeconds(1), scheduler ?? Scheduler.Default);
            }
        }
        class SuccessHttpServiceStub : IHttpService
        {
            public IObservable<string> GetString(string url)
            {
                return Observable.Return("stub");
            }
        }

        [TestMethod]
        public async Task MyTimeoutClass_SuccessfulGet_ReturnsResult()
        {
            var stub = new SuccessHttpServiceStub();
            var my = new MyTimeOutClass(stub);
            var result = await my.GetStringWithTimeout("http://www.example.com/").SingleAsync();
            var result2 = await Observable.Return("stub").Timeout(TimeSpan.FromSeconds(1)).SingleAsync();//同上封装后的代码
            Assert.AreEqual("stub", result);
        }

        private class FailureHttpServiceStub : IHttpService
        {
            public IObservable<string> GetString(string url)
            {
                return Observable.Throw<string>(new HttpRequestException());
            }
        }
        [TestMethod]
        public async Task MyTimeoutClass_FailedGet_PropagatesFailure()
        {
            var stub = new FailureHttpServiceStub();
            var my = new MyTimeOutClass(stub);
            await ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                await my.GetStringWithTimeout("http://www.example.com/").SingleAsync();
            });
        }
        #endregion

        #region 6.6用虚拟事假测试Rx Observable对象
        class SuccessHttpServiceStub2 : IHttpService
        {
            public IScheduler Scheduler { get; set; }
            public TimeSpan Delay { get; set; }
            public IObservable<string> GetString(string url)
            {
                return Observable.Return("stub").Delay(Delay, Scheduler);
            }
        }
        [TestMethod]
        public void MyTimeoutClass_SuccessfulGetShortDelay_ReturnsResult()
        {
            var scheduler = new TestScheduler();
            var stub = new SuccessHttpServiceStub2() { Scheduler = scheduler, Delay = TimeSpan.FromSeconds(0.5) };
            var my = new MyTimeOutClass(stub);
            string result = null;
            my.GetStringWithTimeout("http://www.example.com/", scheduler).Subscribe(r => { result = r; });
            scheduler.Start();
            Assert.AreEqual("stub", result);
        }

        [TestMethod]
        public void MyTimeoutClass_SuccessfulGetLongDelay_ThrowTimeoutException()
        {
            var scheduler = new TestScheduler();
            var stub = new SuccessHttpServiceStub2() { Scheduler = scheduler, Delay = TimeSpan.FromSeconds(1.5) };
            var my = new MyTimeOutClass(stub);
            Exception result = null;
            my.GetStringWithTimeout("http://www.example.com/", scheduler).Subscribe(_ => Assert.Fail("Received value"), ex => { result = ex; });
            scheduler.Start();
            Assert.IsInstanceOfType(result, typeof(TimeoutException));
        }
        #endregion
    }

    class MyClass
    {
        public static Task<int> DivideAsync(int a, int b)
        {
            //return Task.FromResult(a / b);
            throw new DivideByZeroException();
        }
    }
}
