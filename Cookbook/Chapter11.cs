using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        class MyClass
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
    }
}
