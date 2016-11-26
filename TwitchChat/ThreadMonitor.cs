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
                Thread.Sleep(5000);
                List<string> activeThreads = new List<string>();
                foreach (Thread thread in monitoredThreads)
                {
                    if (thread.IsAlive)
                    {
                        activeThreads.Add(thread.Name);
                        continue;
                    }
                    else
                    {
                        Logging.WriteToConsole("Thread found that was not alive. Thread #" + thread.Name);
                        thread.Abort();
                        thread.Start();
                    }
                }
                string activeThreadsStr = "Active threads: ";
                foreach (string s in activeThreads)
                {
                    activeThreadsStr += s + ", ";
                }
                activeThreadsStr += " | Queue Length: " + Operations.receivedMessages.Count;
                Logging.WriteToConsole(activeThreadsStr);
            }
        }
    }
}
