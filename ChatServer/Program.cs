using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatServer
{
    class Program
    {
        private static List<Socket> clientSockets = new List<Socket>(); // Clientların tutulduğu liste
        private static Dictionary<Socket, string> clientNicknames = new Dictionary<Socket, string>(); // Nicknamelerin tutulduğu liste
        private static object lockObj = new object();

        static void Main(string[] args)
        {
            Console.WriteLine("=== Chat Sunucusu ===");

            Socket serverSocket = new Socket(AddressFamily.InterNetwork,// Ipv4 kullan
                                            SocketType.Stream,          // Sürekli bağlantı (TCP)
                                            ProtocolType.Tcp);          // TCP protokolü


            // IPAddress.Any: Tüm ağ kartlarını dinler
            // 5000: port numarası
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 5000);

            try
            {
                serverSocket.Bind(endPoint);// Socketi porta bağla
                serverSocket.Listen(10);    // 10 bağlantıya kadar kuyrukta beklet

                Console.WriteLine("Sunucu tüm ağ arayüzlerinde 5000 portundan dinleniyor...");
                Console.WriteLine($"Yerel IP adresiniz: {GetLocalIPAddress()}");
                Console.WriteLine("İstemci bağlantıları bekleniyor...\n");

                while (true)
                {
                    // Accept() → Client bağlanana kadar bekle, sonra kabul et
                    Socket clientSocket = serverSocket.Accept();

                    // RemoteEndPoint → Karşı tarafın IP bilgisi
                    string clientIP = ((IPEndPoint)clientSocket.RemoteEndPoint).Address.ToString();
                    Console.WriteLine($"[+] Yeni bağlantı: {clientIP}");

                    // lock bir thread bitmeden diğerinin çalışmasını engeller
                    lock (lockObj)
                    {
                        clientSockets.Add(clientSocket);
                    }

                    Thread clientThread = new Thread(() => HandleClient(clientSocket));
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
            }
        }

        static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "IP bulunamadı";
        }

        static void HandleClient(Socket clientSocket)
        {
            byte[] buffer = new byte[1024]; // 1kb'lık veri deposu oluştur
            string clientIP = ((IPEndPoint)clientSocket.RemoteEndPoint).Address.ToString();

            try
            {
                while (true)
                {
                    int bytesRead = clientSocket.Receive(buffer);   // Clienttan veriyi al kaç byte geldiğini döndür

                    if (bytesRead == 0) // 0 ise bağlantı kapanmıştır
                        break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    // İlk mesaj nickname ise kaydet
                    if (message.StartsWith("NICK:"))
                    {
                        string nickname = message.Substring(5);
                        lock (lockObj)
                        {
                            clientNicknames[clientSocket] = nickname;
                        }
                        Console.WriteLine($"[+] {clientIP} kullanıcı adını ayarladı: {nickname}");
                        continue; // Bu mesajı broadcast etme
                    }
                    // Nickname'i al
                    string clientName = clientNicknames.ContainsKey(clientSocket)
                        ? clientNicknames[clientSocket]
                        : "Bilinmeyen";
                    Console.WriteLine($"[{clientIP} - {clientName}]: {message}");
                    BroadcastMessage(message, clientSocket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"İstemci hatası ({clientIP}): {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"[-] Bağlantı kesildi: {clientIP}");

                lock (lockObj)
                {
                    clientSockets.Remove(clientSocket);
                    clientNicknames.Remove(clientSocket);
                }

                clientSocket.Close();
            }
        }

        static void BroadcastMessage(string message, Socket senderSocket)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            lock (lockObj)
            {
                foreach (Socket client in clientSockets) // Tüm clientları dolaş gönderen hariç herkese datayı gönder
                {
                    try
                    {
                        if (client != senderSocket)
                        {
                            client.Send(data);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}