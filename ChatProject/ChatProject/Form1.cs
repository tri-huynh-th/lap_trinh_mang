﻿using ChatProject.model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ChatProject
{
    public partial class Form1 : Form
    {
        private TcpListener server;
        private List<ClientInfo> clients;
        private Thread serverThread;
        private string expectedKey = "secret_key"; // Khóa mà server mong đợi

        public Form1()
        {
            InitializeComponent();
            clients = new List<ClientInfo>();
        }

        private void Form_load(object sender, EventArgs e)
        {
            showLocalIp();
            startSever();
            startListeningForClients();
        }

        private void startSever()
        {
            server = new TcpListener(IPAddress.Any, 9000);
            server.Start();
            serverThread = new Thread(acceptClients);
            serverThread.Start();
            appendMessage("Sever sẵn sàng kết nối");
        }

        private void startListeningForClients()
        {
            Thread udpThread = new Thread(() =>
            {
                UdpClient udpServer = new UdpClient(9001);
                IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

                while (true)
                {
                    byte[] clientRequest = udpServer.Receive(ref clientEndPoint);
                    string request = Encoding.UTF8.GetString(clientRequest);

                    if (request.StartsWith("DISCOVER_SERVER_REQUEST"))
                    {
                        string clientKey = request.Substring("DISCOVER_SERVER_REQUEST".Length);

                        // Kiểm tra key
                        if (clientKey == expectedKey)
                        {
                            byte[] serverResponse = Encoding.UTF8.GetBytes(getLocalIpAddress());
                            udpServer.Send(serverResponse, serverResponse.Length, clientEndPoint);
                        }
                        else
                        {
                            byte[] errorResponse = Encoding.UTF8.GetBytes("INVALID_KEY");
                            udpServer.Send(errorResponse, errorResponse.Length, clientEndPoint);
                        }
                    }
                }
            });

            udpThread.IsBackground = true;
            udpThread.Start();
        }

        private void acceptClients()
        {
            while (true)
            {
                var client = server.AcceptTcpClient();
                var clientInfo = new ClientInfo { tpc = client };
                clients.Add(clientInfo);
                var clientThread = new Thread(handleClient);
                clientThread.Start(clientInfo);
            }
        }

        private void handleClient(object obj)
        {
            var clientInfo = (ClientInfo)obj;
            var stream = clientInfo.tpc.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            clientInfo.name = reader.ReadLine();
            Invoke((Action)(() =>
            {
                connectTextBox.AppendText(clientInfo.name + Environment.NewLine);
            }));
            appendMessage($"{clientInfo.name} đã kết nối");

            while (clientInfo.tpc.Connected)
            {
                try
                {
                    var message = reader.ReadLine();
                    if (message != null)
                    {
                        appendMessage($"{clientInfo.name}: {message}");
                        broadcastMessage($"{clientInfo.name}: {message}", clientInfo);
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }

            clients.Remove(clientInfo);
            clientInfo.tpc.Close();
            Invoke((Action)(() =>
            {
                connectTextBox.Text = string.Empty;
                foreach (var client in clients)
                {
                    connectTextBox.AppendText(client.name + Environment.NewLine);
                }
            }));
            appendMessage($"{clientInfo.name} đã ngắt kết nối");
        }

        private void appendMessage(string message)
        {
            Invoke((Action)(() =>
            {
                messageBox.AppendText(message + Environment.NewLine);
            }));
        }

        private void broadcastMessage(string message, ClientInfo sender)
        {
            foreach (var client in clients)
            {
                if (client != sender)
                {
                    try
                    {
                        var writer = new StreamWriter(client.tpc.GetStream(), Encoding.UTF8) { AutoFlush = true };
                        writer.WriteLine(message);
                    }
                    catch (Exception)
                    {
                        // Ignore any exceptions
                    }
                }
            }
        }

        private void showLocalIp()
        {
            Invoke((Action)(() =>
            {
                string ip = getLocalIpAddress();
                ipTextBox.AppendText(ip);
            }));
        }

        private String getLocalIpAddress()
        {
            var hostName = Dns.GetHostName();
            var addresses = Dns.GetHostAddresses(hostName);

            foreach (var ip in addresses)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            var message = sendBox.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                appendMessage("Server: " + message);
                broadcastMessage("Server: " + message, null);
                sendBox.Clear();
            }
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }
    }
}
