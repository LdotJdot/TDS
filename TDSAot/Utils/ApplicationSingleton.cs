using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TDS.Utils
{
    internal class ApplicationSingleton
    {
        const string appId = "MyApp-12345678-1234-1234-1234-123456789abc";
        internal static Mutex? appMutex;
        internal static bool Check()
        {

            bool mutexAcquired = false;

            try
            {
                // 创建但不立即获取所有权
                appMutex = new Mutex(false, $"Global\\{appId}");

                // 尝试获取锁（立即返回）
                mutexAcquired = appMutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                // 如果之前的实例异常退出，我们会获得被遗弃的锁
                mutexAcquired = true;
            }

            if (!mutexAcquired)
            {
                appMutex?.Dispose();
            }
            return mutexAcquired;
        }
    }
}
