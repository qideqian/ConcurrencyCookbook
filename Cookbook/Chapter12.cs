using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Cookbook
{
    class Chapter12
    {
        #region 12.1 调度到线程池（指定一段代码在线程池线程中执行）
        public void Example1()
        {
            Task task = Task.Run(() =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            });
            Task<int> task2 = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                return 13;
            });
        }
        #endregion

        #region 12.2 任务调度器
        //需要让多个代码段按照指定的方式运行。
        //例如让所有代码段在UI线程中运行，或者只允许特定数量的代码段同时运行。
        public void Examlple2()
        {
            //.NET中有几个不同的上下文
            //大多数UI框架有一个表示UI线程的SynchronizationContext。ASP.NET有一个表示HTTP请求上下文的SynchronizationContext

            TaskScheduler scheduler = TaskScheduler.FromCurrentSynchronizationContext();//获取当前调度上下文
        }

        //.NET4.5引入了另一个功能强大的类，即ConcurrentExclusiveSchedulerPair，它实际上是相关相互关联的调度器。
        //只要ExclusiveScheduler上没有运行任务，ConcurrentScheduler就可以让多个任务同时执行。
        //只有当ConcurrentScheduler没有执行任务时，ExclusiveScheduler才可以执行任务，并且每次只允许一个任务。

        //ExclusiveScheduler执行的代码会在线程池中允许，但是使用了同一个ExclusiveScheduler对象的其他代码不能同时运行。
        public void Example3()
        {
            var schedulerPair = new ConcurrentExclusiveSchedulerPair();
            TaskScheduler concurrent = schedulerPair.ConcurrentScheduler;
            TaskScheduler exclusive = schedulerPair.ExclusiveScheduler;//确保每次只运行一个任务。
        }
        //ConcurrentExclusiveSchedulerPair另一个用法是作为限流调度器。创建的ConcurrentExclusiveSchedulerPair对象可以限制自身的并发数量。这时通常不使用ExclusiveScheduler
        //注意：这种限流方式只是对运行中的代码限流。
        public void Example4()
        {
            var schedulerPair = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, maxConcurrencyLevel: 8);
            TaskScheduler scheduler = schedulerPair.ConcurrentScheduler;
        }
        #endregion

        #region 12.3 调度并行代码（需要控制个别代码段在并行代码中的执行方式）
        void RotateMatrices(IEnumerable<IEnumerable<Matrix>> collections, float degrees)
        {
            var schedulerPair = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, maxConcurrencyLevel: 8);
            TaskScheduler scheduler = schedulerPair.ConcurrentScheduler;
            ParallelOptions options = new ParallelOptions { TaskScheduler = scheduler };
            Parallel.ForEach(collections, options, matrices => Parallel.ForEach(matrices, options, matrix => matrix.Rotate(degrees)));
        }
        #endregion

        #region 12.4 用调度器实现数据流的同步（需要控制个别代码段在数据流代码中的执行方式）
        void Example5()
        {
            var showList = new List<int>();
            var options = new ExecutionDataflowBlockOptions { TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext() };//获取执行代码的调度器，保证单线程更改数据
            var multiplyBlock = new TransformBlock<int, int>(item => item * 2);
            var displayBlock = new ActionBlock<int>(result => showList.Add(result), options);
            multiplyBlock.LinkTo(displayBlock);
        }
        #endregion
    }
}
