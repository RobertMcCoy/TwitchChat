using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;

namespace TwitchChat
{
    class IRCBot
    {
        private TcpClient irc = new TcpClient("irc.chat.twitch.tv", 6667);
        private NetworkStream networkStream;
        private StreamReader streamReader;
        private StreamWriter streamWriter;
        private Random random = new Random();
        private int threadNumber;
        List<string> thisThreadStreamers = new List<string>();
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
            Logging.WriteToConsole("Thread #" + threadNumber + " has been started.");
            //Byte[] bytes = new Byte[1024]; //TODO: Understand this more in the future - this could be the issue with the initial thread start leaking information
            //int i;
            //while ((i = networkStream.Read(bytes, 0, bytes.Length)) != 0) //TODO: See if this can be converted to another stream reader -- threading appears to cause overlap on initial start ???
            string data;
            while ((data = streamReader.ReadLine()) != null)
            {
                //string data = Encoding.ASCII.GetString(bytes, 0, i); //Convert the network stream line to an actual string of ASCII characters [Might want to do unicode for foriegn characters?]
                string[] splitData = data.Split(' '); //Will need this to determine the message type we have received
                if (data != string.Empty && splitData.Length > 0)
                {
                    if (data.Trim() != string.Empty && splitData.Length > 1 && splitData[1].Equals("PRIVMSG")) //PRIVMSG = A new message in the channel TODO: Figure out why the splitData.Length > 1 check was needed here and why it wasn't working about when we needed to check > 0 ?
                    {
                        Operations.receivedMessages.Enqueue(data);
                    }
                    else if (splitData[0].Equals("PING")) //Every ~5 minutes IRC sends a ping which must be responded with a pong
                    {
                        streamWriter.WriteLine("PONG " + splitData[1]);
                        streamWriter.Flush();
                    }
                }
            }
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
