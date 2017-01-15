using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using System.Data.SqlClient;
using System.Threading;

namespace TwitchChat
{
    public class IRCBot
    {
        private TcpClient irc = new TcpClient("irc.chat.twitch.tv", 6667);
        private NetworkStream networkStream;
        private StreamReader streamReader;
        private StreamWriter streamWriter;
        private Random random = new Random();
        private int threadNumber;
        private List<string> thisThreadStreamers = new List<string>();
        private Queue<string> receivedMessages = new Queue<string>();
        private int processedMessages = 0;
        //Thank god for channels renaming their custom bots...in time I need to identify a better way of doing this, because this is going to slow down performance slightly...
        

        public IRCBot(List<string> channels, int threadNumber)
        {
            this.threadNumber = threadNumber; //For tracking where we are
            try
            {
                networkStream = irc.GetStream();
                streamReader = new StreamReader(networkStream, Encoding.Default);
                streamWriter = new StreamWriter(networkStream);
                long nickNumber = LongRandom(10000000000000, 99999999999999, random);
                streamWriter.WriteLine("NICK justinfan" + nickNumber); //We need a unique name for each thread, so why not get a random number between 10 Trillion and 100 Trillion? I'm a fan of risks and want the .00001% chance I hit an uncaught error here.
                streamWriter.Flush();
                string data = streamReader.ReadLine();
                foreach (string streamer in channels)
                {
                    streamWriter.WriteLine("JOIN #" + streamer); //The IRC command is #join <channel>, we have 25 to join here...and we will report that in the console.
                    thisThreadStreamers.Add(streamer);
                }
                streamWriter.Flush();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        internal void handleData()
        {
            Logging.WriteToConsole("IRC Thread #" + threadNumber + " has been started.");
            string data;
            while ((data = streamReader.ReadLine()) != null)
            {
                string[] splitData = data.Split(' '); //Will need this to determine the message type we have received
                if (data != string.Empty && splitData.Length > 0)
                {
                    if (data.Trim() != string.Empty && splitData.Length > 1 && splitData[1].Equals("PRIVMSG")) //PRIVMSG = A new message in the channel TODO: Figure out why the splitData.Length > 1 check was needed here and why it wasn't working about when we needed to check > 0 ?
                    {
                        receivedMessages.Enqueue(data);
                    }
                    else if (splitData[0].Equals("PING")) //Every ~5 minutes IRC sends a ping which must be responded with a pong
                    {
                        streamWriter.WriteLine("PONG " + splitData[1]);
                        streamWriter.Flush();
                    }
                }
            }
        }

        internal void handleMessages()
        {
            Logging.WriteToConsole("Message Handler #" + threadNumber + " has been started.");
            while (true)
            {
                if (receivedMessages.Count > 200 && receivedMessages.Peek() != null)
                {
                    processedMessages += receivedMessages.Count;
                    MessageLoadBalancer loadBalancer = new MessageLoadBalancer(receivedMessages);
                    Thread clearThisQueue = new Thread(() => loadBalancer.ClearTheQueue());
                    receivedMessages.Clear();
                    clearThisQueue.Start();
                }
                if (receivedMessages.Count > 0 && receivedMessages.Peek() != null)
                {
                    string data = receivedMessages.Dequeue();
                    if (data != null)
                    {
                        string[] splitData = data.Split(' ');
                        try
                        {
                            string commentSubmitter = splitData[0].Substring(1, splitData[0].IndexOf('!') - 1); //Each line from the stream begins ':username' and ends with '!username', so we just get the username before the checkmark without the :
                            string submissionTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fffffff"); //Super specific timing, just going to be useful in the future for analysis [MPS, MPMS, etc.]
                            string message = data.Substring(data.IndexOf(':', data.IndexOf(':') + 1) + 1); //Each message starts with the 1st ':' in the read line, we need to get the substring between that and the 2nd :
                            if (message.Contains("'"))
                            {
                                message = message.Replace("'", "''");
                            }
                            if (!Operations.channelBots.Contains<string>(commentSubmitter))
                            {
                                using (SqlConnection connection = new SqlConnection(Constants.connectionString))
                                {
                                    connection.Open();
                                    int submitterId = -1, streamerId = -1;
                                    using (SqlCommand command = new SqlCommand("SELECT TOP 1 * FROM Chatter where ChatterName='" + commentSubmitter + "'", connection))
                                    {
                                        using (SqlDataReader reader = command.ExecuteReader())
                                        {
                                            while (reader.Read())
                                            {
                                                submitterId = reader.GetInt32(0);
                                            }
                                        }
                                    }
                                    using (SqlCommand command = new SqlCommand("SELECT TOP 1 * FROM Channel where ChannelName='" + splitData[2].Replace("#", "") + "'", connection))
                                    {
                                        using (SqlDataReader reader = command.ExecuteReader())
                                        {
                                            while (reader.Read())
                                            {
                                                streamerId = reader.GetInt32(0);
                                            }
                                        }
                                    }
                                    if (submitterId == -1)
                                    {
                                        using (SqlCommand command = new SqlCommand("INSERT INTO Chatter(ChatterName) VALUES('" + commentSubmitter + "')", connection))
                                        {
                                            command.ExecuteNonQuery();
                                        }
                                    }
                                    if (streamerId == -1)
                                    {
                                        using (SqlCommand command = new SqlCommand("INSERT INTO Channel(ChannelName) VALUES('" + splitData[2].Replace("#", "") + "')", connection))
                                        {
                                            command.ExecuteNonQuery();
                                        }
                                    }

                                    using (SqlCommand command = new SqlCommand("SELECT TOP 1 * FROM Chatter where ChatterName='" + commentSubmitter + "'", connection))
                                    {
                                        using (SqlDataReader reader = command.ExecuteReader())
                                        {
                                            while (reader.Read())
                                            {
                                                submitterId = reader.GetInt32(0);
                                            }
                                        }
                                    }
                                    using (SqlCommand command = new SqlCommand("SELECT TOP 1 * FROM Channel where ChannelName='" + splitData[2].Replace("#", "") + "'", connection))
                                    {
                                        using (SqlDataReader reader = command.ExecuteReader())
                                        {
                                            while (reader.Read())
                                            {
                                                streamerId = reader.GetInt32(0);
                                            }
                                        }
                                    }

                                    if (streamerId != -1 && submitterId != -1)
                                    {
                                        using (SqlCommand command = new SqlCommand("INSERT INTO Message(ChatterId, ChannelId, SubmissionTime, MessageContent) VALUES('" + submitterId + "', '" + streamerId + "', '" + submissionTime + "', '" + message + "')", connection))
                                        {
                                            command.ExecuteNonQuery();
                                        }
                                    }
                                    else
                                    {
                                        Logging.WriteToConsole("Something's not right with the database");
                                    }
                                    processedMessages++;
                                }
                            }
                        }
                        catch (Exception exc)
                        {
                            Logging.WriteToConsole("Database Error: " + exc.Message);
                        }
                    }
                }
            }
        }

        public int getQueueLength()
        {
            return receivedMessages.Count;
        }

        public int getProcessedMessages()
        {
            return processedMessages;
        }

        public int getThreadNbr()
        {
            return threadNumber;
        }

        public List<string> getStreamerList()
        {
            return thisThreadStreamers;
        }

        /*
         * LongRandom Method
         * This is used to generate a random 14 digit string of numbers between the two given parameters. Since this number is so long (haha...), int's can't be used so this work around from StackOverflow does the trick.
         */
        private static long LongRandom(long min, long max, Random rand)
        {
            byte[] buf = new byte[8];
            rand.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);

            return (Math.Abs(longRand % (max - min)) + min);
        }
    }
}
