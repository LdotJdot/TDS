using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TDSAot.State
{
    internal class ActionState : IDisposable
    {
        internal List<(Task task, CancellationTokenSource ct)> tasks = [];

        public ActionState()
        {
        }
                
        public void Start(Task task, CancellationTokenSource cts)
        {
            tasks.Add((task, cts));
            task.Start();
        }

        public void Dispose()
        {
            foreach (var tk in this.tasks)
            {
                tk.ct.Cancel();
                tk.task.Dispose();
            }
        }
    }
}