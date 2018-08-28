using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZooKeeperNet;
using static ZooKeeperNet.ZooKeeper;

namespace ZooKeepr_Lock
{
    public class ZooKeeprDistributedLock : IWatcher
    {
        /// <summary>
        /// zk链接字符串
        /// </summary>
        private String connectString = "127.0.0.1:2181";
        private ZooKeeper zk;
        private string root = "/locks"; //根        
        private string lockName; //竞争资源的标志        
        private string waitNode; //等待前一个锁        
        private string myZnode; //当前锁               
        private AutoResetEvent autoevent;
        private TimeSpan sessionTimeout = TimeSpan.FromMilliseconds(50000);
        private IList<Exception> exception = new List<Exception>();

        /// <summary>
        /// 创建分布式锁
        /// </summary>
        /// <param name="lockName">竞争资源标志,lockName中不能包含单词lock</param>
        public ZooKeeprDistributedLock(string lockName)
        {
            this.lockName = lockName;
            // 创建一个与服务器的连接            
            try
            {
                zk = new ZooKeeper(connectString, sessionTimeout, this);
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (true)
                {
                    if (zk.State == States.CONNECTING) { break; }
                    if (zk.State == States.CONNECTED) { break; }
                }
                sw.Stop();
                TimeSpan ts2 = sw.Elapsed;
                Console.WriteLine("zoo连接总共花费{0}ms.", ts2.TotalMilliseconds);

                var stat = zk.Exists(root, false);
                if (stat == null)
                {
                    // 创建根节点                    
                    zk.Create(root, new byte[0], Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
                }
            }
            catch (KeeperException e)
            {
                throw e;
            }
        }

        /// <summary>        
        /// zookeeper节点的监视器        
        /// </summary>        
        public virtual void Process(WatchedEvent @event)

        {
            if (this.autoevent != null)
            {
                //将事件状态设置为终止状态，允许一个或多个等待线程继续；如果该操作成功，则返回true；否则，返回false
                this.autoevent.Set();
            }
        }

        public virtual bool tryLock()
        {
            try
            {
                string splitStr = "_lock_";
                if (lockName.Contains(splitStr))
                {
                    //throw new LockException("lockName can not contains \\u000B");                
                }
                //创建临时子节点                
                myZnode = zk.Create(root + "/" + lockName + splitStr, new byte[0], Ids.OPEN_ACL_UNSAFE, CreateMode.EphemeralSequential);
                Console.WriteLine(myZnode + "    创建完成！ ");
                //取出所有子节点                
                IList<string> subNodes = zk.GetChildren(root, false).ToList<string>();
                //取出所有lockName的锁                
                IList<string> lockObjNodes = new List<string>();
                foreach (string node in subNodes)
                {
                    if (node.StartsWith(lockName))
                    {
                        lockObjNodes.Add(node);
                    }
                }
                Array alockObjNodes = lockObjNodes.ToArray();
                Array.Sort(alockObjNodes);
                Console.WriteLine(myZnode + "==" + lockObjNodes[0]);
                if (myZnode.Equals(root + "/" + lockObjNodes[0]))
                {
                    //如果是最小的节点,则表示取得锁   
                    Console.WriteLine(myZnode + "    获取锁成功！ ");
                    return true;
                }
                //如果不是最小的节点，找到比自己小1的节点               
                string subMyZnode = myZnode.Substring(myZnode.LastIndexOf("/", StringComparison.Ordinal) + 1);
                waitNode = lockObjNodes[Array.BinarySearch(alockObjNodes, subMyZnode) - 1];
            }
            catch (KeeperException e)
            {
                throw e;
            }
            return false;
        }


        public virtual bool tryLock(TimeSpan time)
        {
            try
            {
                if (this.tryLock())
                {
                    return true;
                }
                return waitForLock(waitNode, time);
            }
            catch (KeeperException e)
            {
                throw e;
            }
        }

        /// <summary>
        /// 等待锁
        /// </summary>
        /// <param name="lower">需等待的锁节点</param>
        /// <param name="waitTime">等待时间</param>
        /// <returns></returns>
        private bool waitForLock(string lower, TimeSpan waitTime)
        {
            var stat = zk.Exists(root + "/" + lower, true);
            //判断比自己小一个数的节点是否存在,如果不存在则无需等待锁,同时注册监听            
            if (stat != null)
            {
                Console.WriteLine("Thread " + System.Threading.Thread.CurrentThread.Name + " waiting for " + root + "/" + lower);
                autoevent = new AutoResetEvent(false);
                //阻止当前线程，直到当前实例收到信号，使用 TimeSpan 度量时间间隔并指定是否在等待之前退出同步域
                bool r = autoevent.WaitOne(waitTime);
                autoevent.Dispose();
                autoevent = null;
                return r;
            }
            else return true;
        }

        /// <summary>
        /// 解除锁
        /// </summary>
        public virtual void unlock()
        {
            try
            {
                Console.WriteLine("unlock " + myZnode);
                zk.Delete(myZnode, -1);
                myZnode = null;
                zk.Dispose();
            }
            catch (KeeperException e)
            {
                throw e;
            }
        }
    }
}
