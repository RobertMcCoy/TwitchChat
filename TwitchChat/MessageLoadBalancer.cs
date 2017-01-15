using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchChat
{
    class MessageLoadBalancer
    {
        private Queue<string> _messages;

        public MessageLoadBalancer(Queue<string> messages)
        {
            _messages = messages;
        }

        public void ClearTheQueue()
        {
            while (_messages.Count > 0)
            {
                string data = _messages.Dequeue();
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
}
