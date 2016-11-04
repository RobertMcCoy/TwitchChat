using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TwitchChat
{
    class ThreadMonitor
    {
        private List<Thread> monitoredThreads;

        public ThreadMonitor(List<Thread> monitoredThreads)
        {
            this.monitoredThreads = monitoredThreads;
        }

        public void monitorThreads()
        {
            Logging.WriteToConsole("Beginning thread monitoring. Total threads to monitor: " + monitoredThreads.Count);
            while (true)
            {
                foreach (Thread thread in monitoredThreads)
                {
                    if (thread.IsAlive)
                        continue;
                    else
                    {
                        Logging.WriteToConsole("Thread found that was not alive. Thread #" + thread.Name);
                        thread.Abort();
                        thread.Start();
                    }
                }
                Thread.Sleep(5000);
            }
        }
    }
}
