using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cookbook
{
    class Chapter3
    {
        #region 3.1数据的并行处理
        //对每一个矩阵都进行旋转
        void RetateMatrices(IEnumerable<Matrix> matrices, float degrees)
        {
            Parallel.ForEach(matrices, matrix => matrix.Rotate(degrees));
        }

        //如果发现有无效的矩阵，则中断循环
        void InvertMatrices(IEnumerable<Matrix> matrices)
        {
            Parallel.ForEach(matrices, (matrix, state) =>
            {
                if (!matrix.IsInvertible)
                    state.Stop();
                else
                    matrix.Invert();
            });
        }

        //外部取消循环
        void RetateMatrices(IEnumerable<Matrix> matrices, float degrees, CancellationToken token)
        {
            Parallel.ForEach(matrices, new ParallelOptions { CancellationToken = token }, matrix => matrix.Rotate(degrees));
        }

        //锁的方式保护共享状态，不是最高效的方式
        int InvertMatrices2(IEnumerable<Matrix> matrices)
        {
            object mutex = new object();
            int nonInvertibleCount = 0;
            Parallel.ForEach(matrices, matrix =>
            {
                if (matrix.IsInvertible)
                {
                    matrix.Invert();
                }
                else
                {
                    lock (mutex)
                    {
                        ++nonInvertibleCount;
                    }
                }
            });
            return nonInvertibleCount;
        }
        #endregion

        #region 3.2并行聚合
        //锁的方式保护共享状态，不是最高效的方式
        static int ParallelSum(IEnumerable<int> values)
        {
            object mutex = new object();
            int result = 0;
            Parallel.ForEach(source: values,
                localInit: () => 0,
                body: (item, state, localValue) => localValue + item,
                localFinally: localValue =>
                {
                    lock (mutex)
                        result += localValue;
                });
            return result;
        }

        //PLINQ方式
        static int ParallelSum2(IEnumerable<int> vlues)
        {
            return vlues.AsParallel().Sum();
        }

        //通过Aggregate实现通用的聚合功能
        static double ParallelSum3(IEnumerable<double> vlues)
        {
            return vlues.AsParallel().Aggregate(
                seed: 0D,
                func: (sum, item) => sum + item
            );
        }
        #endregion

        #region 并行调用
        //分为两个数组处理
        static void ProcessArray(double[] array)
        {
            Parallel.Invoke(
                () => ProcessPartialArray(array, 0, array.Length / 2),
                () => ProcessPartialArray(array, array.Length / 2, array.Length)
            );
        }

        static void ProcessPartialArray(double[] array, int begin, int end)
        {
            //计算密集型的处理过程...
            throw new NotImplementedException();
        }

        //运行之前无法确定调用数量
        static void DoAction20Times(Action action)
        {
            Action[] actions = Enumerable.Repeat(action, 20).ToArray();
            Parallel.Invoke(actions);
        }

        //支持取消操作
        static void DoAction20Times(Action action, CancellationToken token)
        {
            Action[] actions = Enumerable.Repeat(action, 20).ToArray();
            Parallel.Invoke(new ParallelOptions { CancellationToken = token }, actions);
        }
        #endregion
    }
}
