using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZooKeepr_Lock;

namespace ZookeeperDemo1
{
    class Program
    {
        static void Main(string[] args)
        {
            int count = 1;//库存 商品编号1079233
            if (count == 1)
            {
                ZooKeeprDistributedLock zklock = new ZooKeeprDistributedLock("Getorder_Pid1079233");
                //if (zklock.tryLock(TimeSpan.FromMilliseconds(50000)))
                if (zklock.tryLock())
                {
                    Console.WriteLine("Demo1创建订单成功！");
                }
                else
                {
                    Console.WriteLine("Demo1创建订单失败了！");
                }
                Console.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss"));
                zklock.unlock();
                Console.ReadKey();
            }
        }
    }
}
