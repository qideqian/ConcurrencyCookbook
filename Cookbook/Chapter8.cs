using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Cookbook
{
    class Chapter8
    {
        #region 8.1 不可变栈和队列（不会经常修改，可以被多个线程安全访问的栈或队列。）
        //后进先出
        //如果需要存储很多快照并希望它们能尽可能多地共享内存，那不可变集合特别有用
        //值得注意的是，程序对局部变量stack进行了覆盖。不可变集合采用的模式是返回一个修改过的集合，原始的集合 引用是不变化的。
        //这意味着，如果引用了特定的不可变集合的实例，它是不会变化的。
        static void Example1()
        {
            var stack = ImmutableStack<int>.Empty;
            stack = stack.Push(13);
            stack = stack.Push(7);
            //先显示“7”，接着显示“13”
            foreach (var item in stack) Trace.WriteLine(item);

            int lastItem;
            stack = stack.Pop(out lastItem);
            //lastItem == 7
        }

        void Example2()
        {
            var stack = ImmutableStack<int>.Empty;
            stack = stack.Push(13);
            var biggerStack = stack.Push(7);
            //先显示“7”，接着显示“13”
            foreach (var item in biggerStack) Trace.WriteLine(item);
            //只显示“13”
            foreach (var item in stack) Trace.WriteLine(item);
        }

        void Example3()
        {
            var queue = ImmutableQueue<int>.Empty;
            queue = queue.Enqueue(13);
            queue = queue.Enqueue(7);
            //先显示“13”，接着显示“7”
            foreach (var item in queue) Trace.WriteLine(item);
            int nextItem;
            queue = queue.Dequeue(out nextItem);
            //显示“13”
            Trace.WriteLine(nextItem);
        }
        #endregion

        #region 8.2 不可变列表（支持索引，不经常修改，可以被多个线程安全访问。）
        /*
         不可变列表的性能差异
         操作             List<T>     ImmutableList<T>
         Add              平摊O(1)    O(logN)
         Insert            O(N)       O(logN)
         RemoveAt          O(N)       O(logN)
         Item[index]       O(N)       O(logN)
             */

        void Example4()
        {
            var list = ImmutableList<int>.Empty;
            list = list.Insert(0, 13);
            list = list.Insert(0, 7);
            //先显示“7”，接着显示“13”
            foreach (var item in list) Trace.WriteLine(item);
            list = list.RemoveAt(1);

            //遍历ImmutableList<T>的最好方法
            foreach (var item in list) Trace.WriteLine(item);
            //这个方法运行正常，但速度会慢很多。
            for (int i = 0; i != list.Count; ++i) Trace.WriteLine(list[i]);
        }
        #endregion

        #region 8.3 不可变Set集合（不需要存放重复内容，不经常修改，可以被多个线程安全访问。）
        /*
         不可变Set集合的性能差异
         操作             ImmutableHashSet<T>     ImmutableSortedSet<T>
         Add              O(logN)                  O(logN)
         Remove           O(logN)                  O(logN)
         Item[index]      不可用                   O(logN)
             */
        //不可变Set集合是非常实用的数据结构，但是填充较大不可变Set集合的速度会很慢。
        //大多数不可变集合有特殊的构建方法，可以先快速地以可变方式构建，然后转换成不可变集合。
        //这种构建方法可用于很多不可变集合。
        void Example5()
        {
            var hashSet = ImmutableHashSet<int>.Empty;
            hashSet = hashSet.Add(13);
            hashSet = hashSet.Add(7);
            //显示“7”和“13”，次序不确定
            foreach (var item in hashSet) Trace.WriteLine(item);

            var sortedSet = ImmutableSortedSet<int>.Empty;
            sortedSet = sortedSet.Add(13);
            sortedSet = sortedSet.Add(7);
            //先显示“7”，接着显示“13”
            foreach (var item in sortedSet) Trace.WriteLine(item);
            var smallestItem = sortedSet[0];
            // smallestItem == 7
            sortedSet = sortedSet.Remove(7);
        }
        #endregion

        #region 8.4 不可变字典（需要一个不经常修改且可被多个线程安全访问的键/值集合。）
        /*
         不可变字典的性能差异
         操作             ImmutableDictionary<T>     ImmutableSortedDictionary<T>
         Add              O(logN)                    O(logN)
         SetItem          O(logN)                    O(logN)
         Item[key]        O(logN)                    O(logN)
         Remove           O(logN)                    O(logN)
             */
        //实用场景：需要存储查询集合中的参考数据。这些参考数据很少修改但需要被不同的线程访问。
        void Example6()
        {
            var dictionary = ImmutableDictionary<int, string>.Empty;
            dictionary = dictionary.Add(10, "Ten");
            dictionary = dictionary.Add(21, "Twenty-One");
            dictionary = dictionary.SetItem(10, "Diez");
            //显示"10Diez"和"21Twenty-One"，次序不确定
            foreach (var item in dictionary) Trace.WriteLine(item.Key + item.Value);
            var ten = dictionary[10];//ten == "Diez"
            dictionary = dictionary.Remove(21);

            var sortedDictionary = ImmutableSortedDictionary<int, string>.Empty;
            sortedDictionary = sortedDictionary.Add(10, "Ten");
            sortedDictionary = sortedDictionary.Add(21, "Twenty-One");
            sortedDictionary = sortedDictionary.SetItem(10, "Diez");
            //先显示"10Diez"，接着显示"21Twenty-One"
            foreach (var item in sortedDictionary) Trace.WriteLine(item.Key + item.Value);
            var ten2 = sortedDictionary[10];//ten == "Diez"
            sortedDictionary = sortedDictionary.Remove(21);
        }
        #endregion

        #region 8.5 线程安全字典（需要有一个键/值集合，多个线程同时读写时仍能保持同步。）
        //适合用在需要共享数据的场合，即多个线程共享同一个集合。但如果一些线程只添加元素，另一些线程只移除元素，最好使用生存者/消费者集合。
        void Example7()
        {
            var dictionary = new ConcurrentDictionary<int, string>();
            var newValue = dictionary.AddOrUpdate(0, key => "Zero", (key, oldValue) => "Zero");//这些委托只能创建新的值，不能修改程序中的其它变量

            //添加（或修改）键0，对应值“Zero”。
            dictionary[0] = "Zero";

            //读取
            string currentValue;
            bool keyExists = dictionary.TryGetValue(0, out currentValue);

            //移除
            string removeValue;
            bool keyExisted = dictionary.TryRemove(0, out removeValue);
        }
        #endregion

        #region 8.6 阻塞队列（需要有一个管道，在线程之间传递消息或数据。例如，一个线程正在装载的同时吧数据压进管道。与此同时，另一个线程在管道的接收端接收并处理数据。）
        //如果有独立的线程(如线程池线程)作为生存者或消费者，阻塞队列就是一个十分不错的选择。
        private readonly BlockingCollection<int> _blockingQueue = new BlockingCollection<int>();
        void Example8()
        {
            _blockingQueue.Add(7);
            _blockingQueue.Add(13);
            _blockingQueue.CompleteAdding();

            Task.Run(() =>
            {
                foreach (var item in _blockingQueue.GetConsumingEnumerable()) Trace.WriteLine(item);
            });
        }
        void Example9()
        {
            //集合的项目限制为1
            BlockingCollection<int> _blockingQueue2 = new BlockingCollection<int>(boundedCapacity: 1);
            _blockingQueue2.Add(7);//这个添加过程立即完成。
            _blockingQueue2.Add(13);//7被移除后，添加13才会完成。
            _blockingQueue2.CompleteAdding();
        }
        #endregion

        #region 8.7 阻塞栈和包（需要有一个管道，在线程之间传递消息或数据，但不想（或不需要）这个管道使用"先进先出"的语义。）
        void Example10()
        {
            BlockingCollection<int> _blockingStack = new BlockingCollection<int>(new ConcurrentStack<int>());//后进先出（栈）
            BlockingCollection<int> _blockingBag = new BlockingCollection<int>(new ConcurrentBag<int>());//无序（包）

            //单线程中如果先运行生存者代码，后运行消费者代码，那项目的次序就和使用栈完全一样
            //生存者代码
            _blockingStack.Add(7);
            _blockingStack.Add(13);
            _blockingStack.CompleteAdding();
            //消费者代码
            //先显示“13”，后显示“7”
            foreach (var item in _blockingStack.GetConsumingEnumerable()) Trace.WriteLine(item);

            //注：如果生产者代码和消费者代码在不同的线程中（这是常见情况），消费者会一直取得最近加入的项目。
            //例如，生存者加入7，接着消费者取走7，生存者加入13，接着消费者取走13。
            //消费者在返回第一个项目前，不会等待生存者调用CompleteAdding。
        }
        #endregion

        #region 8.8 异步队列（需要一个管道，在代码的各个部分之间以先进先出的方式传递消息或数据）
        //如：一段代码在加载数据，并向管道推送数据，并向管道推送数据。同时UI线程在接收并显示数据。
        async void Example11()
        {
            BufferBlock<int> _asyncQueue = new BufferBlock<int>();
            //生存者代码
            await _asyncQueue.SendAsync(7);
            await _asyncQueue.SendAsync(13);
            _asyncQueue.Complete();

            //消费者代码(单线程时使用，如果多线程可能对每个OutputAvailableAsync都返回true，如果队列中项目都取完了，ReceiveAsync会抛出InvalidOperationException异常。)
            //先显示“7”，后显示“13”。
            while (await _asyncQueue.OutputAvailableAsync()) Trace.WriteLine(await _asyncQueue.ReceiveAsync());

            //消费者代码（多线程时）
            while (true)
            {
                int item;
                try
                {
                    item = await _asyncQueue.ReceiveAsync();
                }
                catch (InvalidOperationException)//抛出此异常时，为多线程获取时对多个OutputAvailableAsync都返回了true
                {
                    break;
                }
                Trace.WriteLine(item);
            }
        }
        //不支持TPL数据流时使用Nito.AsyncEx中的AsyncProduceConsumerQueue<T>类。
        async void Example12()
        {
            AsyncProducerConsumerQueue<int> _asyncQueue = new AsyncProducerConsumerQueue<int>();
            //生存者代码
            await _asyncQueue.EnqueueAsync(7);
            await _asyncQueue.EnqueueAsync(13);
            _asyncQueue.CompleteAdding();
            //消费者代码
            //先显示“7”，后显示“13”。
            while (await _asyncQueue.OutputAvailableAsync()) Trace.WriteLine(await _asyncQueue.DequeueAsync());

            //AsyncProducerConsumerQueue<T>具有限流功能，如果生存者的运行速度可能比消费者快，这个功能就是必需的。
            AsyncProducerConsumerQueue<int> _asyncQueue2 = new AsyncProducerConsumerQueue<int>(maxCount: 1);
            //单个消费者
            await _asyncQueue2.EnqueueAsync(7);//这个添加过程会立即执行
            await _asyncQueue2.EnqueueAsync(13);//这个添加过程会（异步地）等待，直到7被移除，然后才会加入13。
            _asyncQueue2.CompleteAdding();
            //多个消费者
            while (true)
            {
                var dequeueResult = await _asyncQueue.TryDequeueAsync();
                if (dequeueResult.Success) break;
                Trace.WriteLine(dequeueResult.Item);
            }
        }
        #endregion
    }
}
