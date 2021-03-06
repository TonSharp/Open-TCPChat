﻿using System;
using System.Text;
using System.Threading;
using TCPChat.Tools;
using TCPChat.Messages;

namespace TCPChat.Network
{
    public class NetworkManager
    {
        public User User;
        public readonly Cmd Cmd;

        private Thread listenThread;
        private Thread receiveThread;

        private string id;

        public readonly Connector Connector;
        
        private bool receiveMessage;
        
        private readonly Action notification;

        public NetworkManager(Action notification)
        {
            Cmd = new Cmd();
            this.notification = notification;
            
            Connector = new Connector();
        }

        public string Process()
        {
            return Cmd.ReadLine(User);      //Always read command and parse it in main thread
        }

        protected internal void RegisterUser()
        {
            tryOnceMore:
            Console.Write("Enter your name: ");
            var userName = Console.ReadLine();
            if(userName != null && userName.Length > 16)
            {
                Console.WriteLine("Name is too long");
                goto tryOnceMore;
            }

            Console.Title = userName!;             //Set title for user with him userName

            Console.Write("Enter your color (white): ");
            var color = Console.ReadLine();

            Console.Clear();

            User = new User(userName, ColorParser.GetColorFromString(color));   //Parse color from string and create user
        }

        public bool StartClient()
        {
            Connector.StartClient(receiveThread, listenThread);
            
            var joiningMessage = new ConnectionMessage(Connection.Connect, User);
            SendMessage(joiningMessage);

            Cmd.WriteLine("Successfully connected to the server");
                                                
            receiveMessage = true;

            receiveThread = new Thread(ReceiveMessage);    //Starting receive message thread
            receiveThread.Start();
            
            return true;
        }

        public bool StartServer()
        {
            try
            {
                if(Connector.ConnectionType == ConnectionType.Server) listenThread?.Interrupt();
            
                Connector.StartServer(Cmd, notification);

                listenThread = new Thread(Connector.Server.Listen);
                listenThread.Start();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ReceiveMessage()
        {
            while(receiveMessage)
            {
                try
                {
                    var message = GetMessage();

                    if (message == null || message.Length <= 0) continue;
                    var msg = IMessageDeserializable.Parse(message);    //Lets get it
                    ParseMessage(msg);                      //And parse
                }
                catch (ThreadInterruptedException) { return; }
                catch (System.IO.IOException) { return; }       //If Thread was interrupted or something
                catch (Exception e)
                {
                    Console.WriteLine("Can't receive message: " + e.Message);   //If something went wrong

                }
            }
        }

        private byte[] GetMessage()
        {
            try
            {
                var data = new byte[64];
                var builder = new StringBuilder();
                int bytes;

                if (Connector.Stream == null) return null;
                do
                {
                    bytes = Connector.Stream.Read(data, 0, data.Length);
                    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                } while (Connector.Stream.DataAvailable);         //Lets read this while stream is available

                return Encoding.Unicode.GetBytes(builder.ToString());   //Return message bytes array

            }
            catch (NullReferenceException) { return null; }

            catch (System.IO.IOException)                       //If stream was stopped
            { 
                Cmd.WriteLine("You are disconnected"); 
                Cmd.SwitchToPrompt();
                
                Connector.Disconnect(receiveThread, listenThread);
                return null;
            }

            catch (Exception e)                                 //If something went wrong
            {
                Cmd.WriteLine("Can't get message from thread: " + e.Message);

                return null;
            }
        }

        public Input GetInputType(string input)
        {
            if (input.Trim().Length < 1) return Input.Empty;
            return input[0] == '/' ? Input.Command : Input.Message;
        }

        public string[] GetCommandArgs(string input)
        {
            if (GetInputType(input) == Input.Command)
            {
                var lower = input.ToLower();
                var args = lower.Split(" ");
                args[0] = args[0].Substring(1);

                return args;
            }

            return new string[0];
        }

        public bool IsConnectedToServer()
        {
            return (Connector.Client != null || Connector.Stream != null) && Connector.ConnectionType == ConnectionType.Client;
        }

        public bool TryCreateRoom(string port)
        {
            try
            {
                Connector.Port = Convert.ToInt32(port);

                return StartServer();
            }
            catch
            {
                return false;
            }
        }
        public bool TryJoin(params string[] joinCommand)
        {
            try
            {
                switch (joinCommand.Length)
                {
                    case 2:
                    {
                        var data = joinCommand[1].Split(":");

                        Connector.Port = Convert.ToInt32(data[1]);
                        Connector.Host = data[0];
                        
                        break;
                    }
                    case 3:
                        Connector.Host = joinCommand[0];
                        Connector.Port = Convert.ToInt32(joinCommand[1]);
                        
                        break;
                    
                    default:
                        return false;
                }

                return StartClient();
            }
            catch
            {
                return false;
            }
        }
        public void TryDisconnect()
        {
            Connector.Disconnect(receiveThread, listenThread);
        }

        /// <summary>
        /// Sends messages with PostCodes between 1-4
        /// </summary>
        /// <param name="msg"></param>
        public void SendMessage(Message msg)
        {
            try
            {
                if(Connector.ConnectionType == ConnectionType.None) 
                {
                    Connector.Disconnect(receiveThread, listenThread);
                    return;
                }
                
                var data = msg.Serialize();

                if(data.Length > 0)             //If message is not empty
                {
                    Connector.Stream.Write(data, 0, data.Length); //Send it
                }
            }
            catch(Exception e)
            {
                Cmd.WriteLine("Can't send message: " + e.Message);  //If something went wrong ))
            }
        }

        private void SendServerMessage(Message message)
        {
            try
            {
                if (message.PostCode >= 1 && message.PostCode <= 4)
                {
                    var msg = message as SimpleMessage;
                    Cmd.UserWriteLine(msg?.SendData, User);
                }
                
                Connector.Server.BroadcastFromServer(message);                            //Broadcast message to all clients
            }
            catch(Exception e)
            {
                Cmd.WriteLine("Can't send message from the server: " + e.Message);
            }
        }

        private void StopClient()
        {
            if (receiveMessage)
            {
                receiveMessage = false;
                receiveThread.Interrupt();
                receiveThread = null;
            }
        }

        private void DisconnectClient()
        {
            var msg = new ConnectionMessage(Connection.Disconnect, User);     //Send message to server about disconnecting
            Connector.Stream.Write(msg.Serialize());       //Disconnect this client from server

            StopClient();
        }

        private void ParseMessage(Message message)
        {
            switch (message.PostCode)
            {
                case {} i when (i >= 1 && i <= 4):
                {
                    var simpleMessage = message as SimpleMessage;
                    Cmd.UserWriteLine(simpleMessage?.SendData, simpleMessage?.Sender);
                    notification();
                    
                    break;
                }
                case 5:
                {
                    var idMessage = message as IDMessage;
                    if (idMessage?.Method == Method.Send)
                        {
                            id = idMessage.SendData;
                            Cmd.WriteLine($"Your id is: {id}");
                        }
                        

                    break;
                }
                case 7:
                {
                    var connectionMessage = message as ConnectionMessage;
                    if(connectionMessage?.Connection == Connection.Connect)
                        Cmd.ConnectionMessage(connectionMessage.Sender, "has joined");
                    else
                        Cmd.ConnectionMessage(connectionMessage?.Sender, "has disconnected");
                    
                    break;
                }
                case 10:
                {
                    DisconnectClient();                 //If server sends us message about stopping
                    Cmd.WriteLine("Server was stopped"); //We are decide to write this
                    
                    break;
                }
                case 11:
                {
                    DisconnectClient();
                    Cmd.WriteLine("Hash sum is not correct");
                    
                    break;
                }
                default: return;
            }

            Cmd.SwitchToPrompt();                           //Lets go back to console
        }
    }
}
