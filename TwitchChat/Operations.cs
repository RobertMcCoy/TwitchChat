using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using System.Data.SqlClient;

namespace TwitchChat
{
    public class Operations
    {
        private const int SCALE_DOWN_CONSTANT = 100;
        private const int STREAMS_PER_THREAD = 25;
        private static List<List<string>> channelsToJoin = new List<List<string>>();
        public static List<string> fullListOfChannels = new List<string>();
        public static List<Thread> ircHandlers = new List<Thread>();
        public static List<Thread> messageHandlers = new List<Thread>();
        public static List<IRCBot> ircBots = new List<IRCBot>();

        public static string[] channelBots = { "gspbot", "tuckusruckusbot", "missychatbot", "6seven8_bot", "spindigbot", "jenningsbot", "lasskeepobot", "jogabot", "og_walkbot",
                                         "cinbot", "clickerheroesbot", "xnoobbot", "drunkafbot", "zmcbeastbot", "lucidfoxxbot", "moobot", "hnlbot", "scamazbot", "revobot",
                                         "vaneiobot", "korgek_bot", "gorobot", "toez_bot", "flpbot", "priestbot", "xanbot", "drangrybot", "phantombot", "coebot", "wizebot", "branebot",
                                         "vivbot", "revlobot", "ankhbot", "deepbot", "nightbot", "ohbot", "koalabot", "quorrabot" };

        public static int createBots()
        {
            string jsonResponse = generateResults("https://api.twitch.tv/kraken/streams?access_token=m36ctuloofijrvkx16rt2cjkbw6ndd&client_id=i3fyf84w4iies7v78jov1jp2zmwdbpa&limit=100");
            if (jsonResponse != string.Empty)
            {
                RootObject streamWrapper = JsonConvert.DeserializeObject<RootObject>(jsonResponse);
                int currentTotalStreams = streamWrapper._total / SCALE_DOWN_CONSTANT;
                int totalThreadsRequired = (streamWrapper._total / SCALE_DOWN_CONSTANT / STREAMS_PER_THREAD);
                int currentThreadCounter = 0;
                for (int i = 0; i < totalThreadsRequired; i++)
                {
                    channelsToJoin.Add(new List<string>());
                }
                for (int i = 100; i < currentTotalStreams + 100; i += 100)
                {
                    if (streamWrapper != null)
                    {
                        for (int j = 0; j < streamWrapper.streams.Count; j++)
                        {
                            if (currentThreadCounter >= totalThreadsRequired - 1)
                            {
                                currentThreadCounter = 0;
                                channelsToJoin[currentThreadCounter].Add(streamWrapper.streams[j].channel.name);
                            }
                            else
                            {
                                channelsToJoin[currentThreadCounter].Add(streamWrapper.streams[j].channel.name);
                                currentThreadCounter++;
                            }
                        }
                        Logging.WriteToConsole("A new set of " + streamWrapper.streams.Count + " has been parsed.");
                    }
                    else
                    {
                        break;
                    }
                    jsonResponse = generateResults("https://api.twitch.tv/kraken/streams?access_token=m36ctuloofijrvkx16rt2cjkbw6ndd&client_id=i3fyf84w4iies7v78jov1jp2zmwdbpa&limit=100&offset=" + i);
                    streamWrapper = JsonConvert.DeserializeObject<RootObject>(jsonResponse);
                    fullListOfChannels = combinedLists(channelsToJoin, fullListOfChannels, (i - 100) / 100);
                }
                string combinedChannelsString = "All Channel Names: ";
                foreach (string channel in fullListOfChannels)
                {
                    combinedChannelsString += channel + ", ";
                }
                Logging.WriteToConsole(combinedChannelsString);
                handleChannelName();
                int counter = 0;
                foreach (List<string> stringList in channelsToJoin)
                {
                    IRCBot newBot = new IRCBot(stringList, counter);
                    ircBots.Add(newBot);
                    ircHandlers.Add(new Thread(() => newBot.handleData()));
                    messageHandlers.Add(new Thread(() => newBot.handleMessages()));
                    ircHandlers[counter].Name = "IRCBot" + (counter).ToString(); //This name is used for reference in the ThreadMonitor
                    messageHandlers[counter].Name = "MessageHandler" + (counter).ToString();
                    counter++;
                }
                int botsStarted = startBots();
                ThreadMonitor ircThreadMonitor = new ThreadMonitor(ircHandlers);
                Thread threadForIRCThreadMonitor = new Thread(() => ircThreadMonitor.monitorThreads());
                threadForIRCThreadMonitor.Start();
                ThreadMonitor handleMessageThreadMonitor = new ThreadMonitor(messageHandlers);
                Thread threadForHandleMessageMonitor = new Thread(() => handleMessageThreadMonitor.monitorThreads());
                threadForHandleMessageMonitor.Start();
                return botsStarted;
            }
            else
            {
                Console.WriteLine("Twitch response was blank for get streamer list.");
                return -1;
            }
        }

        public static int startBots()
        {
            int botsStarted = 0;
            if (ircHandlers.Count != 0)
            {
                foreach (Thread thread in ircHandlers)
                {
                    try
                    {
                        thread.Start();
                        botsStarted++;
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine("IRC Bot thread failed to start:\n" + exc.Message);
                    }
                }
            }
            if (messageHandlers.Count != 0)
            {
                foreach (Thread thread in messageHandlers)
                {
                    try
                    {
                        thread.Start();
                        botsStarted++;
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine("Queue thread failed to start:\n" + exc.Message);
                    }
                }
            }
            return botsStarted;
        }

        private static List<string> combinedLists(List<List<string>> list1, List<string> list2, int list1Index)
        {
            foreach (string s in list1[list1Index])
                list2.Add(s);
            return list2;
        }

        public static string generateResults(string apiCall)
        {
            HttpWebRequest generateRequest = (HttpWebRequest)WebRequest.Create(apiCall); //For now we are using the v1 authentication, I'm not certain if there is a difference between this and the v4 authentication. I will check up with Seth on this.
            generateRequest.Method = "GET";
            return apiResponse(generateRequest);
        }

        public static string apiResponse(HttpWebRequest req) //All API calls will have some sort of response and it will be a string with JSON information, so this is just a global method to get that information
        {
            try
            {
                HttpWebResponse response = (HttpWebResponse)req.GetResponse();
                if (response.StatusDescription == "OK") //We should only continue if the status is OK. Almost all other responses will result in an exception, but this is just added security
                {
                    var returnData = response.GetResponseStream();
                    string responseStr = string.Empty;
                    using (StreamReader sr = new StreamReader(returnData))
                    {
                        responseStr = sr.ReadToEnd();
                        sr.Close();
                        returnData.Close();
                        response.Close();
                    }
                    if (!responseStr.Equals(""))
                    {
                        return responseStr;
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (WebException exc)
            {
                return string.Empty;
            }
        }

        public static void handleChannelName()
        {
            foreach (string str in fullListOfChannels)
            {
                using (SqlConnection connection = new SqlConnection(Constants.connectionString))
                {
                    connection.Open();
                    int streamerId = -1;
                    using (SqlCommand command = new SqlCommand("SELECT TOP 1 * FROM Channel where ChannelName='" + str + "'", connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                streamerId = reader.GetInt32(0);
                            }
                        }
                    }
                    if (streamerId == -1)
                    {
                        using (SqlCommand command = new SqlCommand("INSERT INTO Channel(ChannelName) VALUES('" + str + "')", connection))
                        {
                            Logging.WriteToConsole("A new channel has been created within the database: " + str);
                            command.ExecuteNonQuery();
                        }
                    }
                    connection.Close();
                }
            }
        }

        
    }
}
