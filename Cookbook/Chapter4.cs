using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Cookbook
{
    public class Chapter4
    {
        #region 4.1连接数据流块
        public static async void Example1()
        {
            var multiplyBlock = new TransformBlock<int, int>(item => item * 2);
            var subtrackBlock = new TransformBlock<int, int>(item => item - 2);
            //建立连接后，从multiplyBlock出来的数据将进入subtrackBlack。
            multiplyBlock.LinkTo(subtrackBlock);

            var options = new DataflowLinkOptions { PropagateCompletion = true };
            multiplyBlock.LinkTo(subtrackBlock, options);

            multiplyBlock.Complete();
            await subtrackBlock.Completion;
        }
        #endregion

        #region 4.2传递出错信息
        static void Example2()
        {
            var block = new TransformBlock<int, int>(item =>
            {
                if (item == 1) throw new InvalidOperationException("Blech.");
                return item * 2;
            });
            block.Post(1);
            block.Post(2);//第一个值引发错误，第二个值直接被删除。
        }

        //Completion属性返回一个任务，一单数据流块执行完成，这个任务也完成。如果数据流快出错，这个任务也出错。
        static async void Example3()
        {
            try
            {
                var block = new TransformBlock<int, int>(item =>
                    {
                        if (item == 1) throw new InvalidOperationException("Blech.");
                        return item * 2;
                    });
                block.Post(1);
                await block.Completion;//利用await捕获错误
            }
            catch (InvalidOperationException)
            {
                //这里捕获异常。
            }
        }

        static async void Example4()
        {
            try
            {
                var multiplyBlock = new TransformBlock<int, int>(item =>
                    {
                        if (item == 1) throw new InvalidOperationException("Blech.");
                        return item * 2;
                    });
                var subtracktBlock = new TransformBlock<int, int>(item => item - 2);
                multiplyBlock.LinkTo(subtracktBlock, new DataflowLinkOptions { PropagateCompletion = true });
                multiplyBlock.Post(1);
                await subtracktBlock.Completion;
            }
            catch (AggregateException ae)
            {
                //这里捕获异常。
                ae.Flatten();
            }
        }
        #endregion

        #region 4.3断开连接
        //要修改一个已有连接的过滤器，必须先端口旧链接，然后用心的过滤器建立新的连接。
        //如果要暂停数据流网络运行的话，可断开一个关键连接。
        static void Example5()
        {
            var multiplyBlock = new TransformBlock<int, int>(item => item * 2);
            var subtrackBlock = new TransformBlock<int, int>(item => item - 2);
            IDisposable link = multiplyBlock.LinkTo(subtrackBlock);
            multiplyBlock.Post(1);
            multiplyBlock.Post(2);
            //断开数据块的连接。
            //前面的代码中，数据可能已经通过连接传递过去，也可能还没有。
            //在实际应用中，考虑实用代码块，而不是调用Dispose。
            link.Dispose();
        }
        #endregion

        #region 4.4限制流量
        static void Example6()
        {
            var sourceBlock = new BufferBlock<int>();
            var options = new DataflowBlockOptions { BoundedCapacity = 1 };
            var targetBlockA = new BufferBlock<int>(options);
            var targetBlockB = new BufferBlock<int>(options);
            sourceBlock.LinkTo(targetBlockA);
            sourceBlock.LinkTo(targetBlockB);
        }
        #endregion

        #region 4.5数据流块的并行处理
        //MaxDegreeOfParallelism指定最大并行处理数，默认1(设置可由块并发处理的最大消息数。)
        static void Example7()
        {
            var multiplyBlock = new TransformBlock<int, int>(item => item * 2, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded });
            var subtractBlock = new TransformBlock<int, int>(item => item - 2);
            multiplyBlock.LinkTo(subtractBlock);
        }
        #endregion

        #region 创建自定义数据流块
        public static IPropagatorBlock<int, int> CreateMyCustomBlock()
        {
            var multiplyBlock = new TransformBlock<int, int>(item => item * 2);
            var addBlock = new TransformBlock<int, int>(item => item + 2);
            var divideBlock = new TransformBlock<int, int>(item => item / 2);

            var flowCompletion = new DataflowLinkOptions { PropagateCompletion = true };
            multiplyBlock.LinkTo(addBlock, flowCompletion);
            addBlock.LinkTo(divideBlock, flowCompletion);

            return DataflowBlock.Encapsulate(multiplyBlock, divideBlock);
        }
        #endregion
    }
}
