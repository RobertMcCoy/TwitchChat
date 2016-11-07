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
        public static List<Thread> ircBots = new List<Thread>();
        public static Queue<string> receivedMessages = new Queue<string>();

        private static string[] channelBots = { "gspbot", "tuckusruckusbot", "missychatbot", "6seven8_bot", "spindigbot", "jenningsbot", "lasskeepobot", "jogabot", "og_walkbot",
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
                    IRCBot newBot = new IRCBot(stringList, counter++);
                    ircBots.Add(new Thread(() => newBot.handleData()));
                    ircBots[counter - 1].Name = (counter - 1).ToString(); //This name is used for reference in the ThreadMonitor
                }
                int botsStarted = startBots();
                ThreadMonitor threadMonitor = new ThreadMonitor(ircBots);
                Thread threadForThreadMonitor = new Thread(() => threadMonitor.monitorThreads());
                threadForThreadMonitor.Start();
                Thread threadForHandleMessages = new Thread(() => handleMessages());
                threadForHandleMessages.Start();
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
            if (ircBots.Count != 0)
            {
                foreach (Thread thread in ircBots)
                {
                    try
                    {
                        thread.Start();
                        botsStarted++;
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine("Bot failed to start:\n" + exc.Message);
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

        public static void handleMessages()
        {
            while (true)
            {
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
                            if (!channelBots.Contains<string>(commentSubmitter))
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
                                }
                            }
                        }
                        catch (Exception exc)
                        {
                            Logging.WriteToConsole("Database Error: " + exc.Message);
                            continue;
                        }
                    }
                }
            }
        }
    }
}
