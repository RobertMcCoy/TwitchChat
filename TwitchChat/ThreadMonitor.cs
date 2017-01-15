using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TwitchChat
{
    class ThreadMonitor
    {
        private readonly List<Thread> monitoredThreads;

        public ThreadMonitor(List<Thread> monitoredThreads)
        {
            this.monitoredThreads = monitoredThreads;
        }

        public void monitorThreads()
        {
            Logging.WriteToConsole("Beginning thread monitoring. Total threads to monitor: " + monitoredThreads.Count);
            int intervalsCounter = 0;
            var activeThreads = new List<string>();
            while (true)
            {
                Thread.Sleep(5000);
                foreach (Thread thread in monitoredThreads)
                {
                    if (thread.IsAlive)
                    {
                        continue;
                    }
                    else
                    {
                        Logging.WriteToConsole("Thread found that was not alive. Thread '" + thread.Name + "'");
                        thread.Start();
                    }
                }
                intervalsCounter++;
                if (intervalsCounter == 5)
                {
                    Thread statisticsThread = new Thread(reportOnStatistics);
                    statisticsThread.Start();
                    intervalsCounter = 0;
                }
            }
        }

        private void reportOnStatistics()
        {
            foreach (IRCBot ircBot in Operations.ircBots)
            {
                Logging.WriteToConsole("REPORT: \tTHREAD #" + ircBot.getThreadNbr() + " has processed " + ircBot.getProcessedMessages() + " messages. | Queue Length: " + ircBot.getQueueLength());
                if (ircBot.getProcessedMessages() == 0)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    foreach (string str in ircBot.getStreamerList())
                    {
                        stringBuilder.Append(str);
                    }
                    //Logging.WriteToConsole("REPORT: \tTHREAD #" + ircBot.getThreadNbr() + " has reported 0 messages processed. Streamers in that thread: " + stringBuilder.ToString());
                }
            }
        }
    }
}
