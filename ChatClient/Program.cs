using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatClient
{
    class Program
    {
        static string nickname = "";
        static string currentInput = "";
        static object inputLock = new object();

        static void Main(string[] args)
        {
            Console.WriteLine("=== Chat İstemcisi ===");

            Console.Write("Takma adın:");
            nickname = Console.ReadLine();

            Console.Write("Sunucu IP adresi (Enter = localhost): ");
            string serverIP = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(serverIP))
                serverIP = "127.0.0.1";

            Socket clientSocket = new Socket(AddressFamily.InterNetwork,
                                            SocketType.Stream,
                                            ProtocolType.Tcp);

            try
            {
                IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), 5000);
                Console.WriteLine("Sunucuya bağlanılıyor...");
                clientSocket.Connect(serverEndPoint);
                Console.WriteLine("Sunucuya bağlandı!");

                byte[] nicknameData = Encoding.UTF8.GetBytes($"NICK:{nickname}");
                clientSocket.Send(nicknameData);

                Console.WriteLine("Mesaj göndermek için yazın (Çıkmak için 'exit' yazın)\n");

                Thread receiveThread = new Thread(() => ReceiveMessages(clientSocket));
                receiveThread.Start();

                while (true)
                {
                    Console.Write($"{nickname}: ");

                    currentInput = "";

                    while (true)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(intercept: true);

                        if (key.Key == ConsoleKey.Enter)
                        {
                            Console.WriteLine();
                            break;
                        }
                        else if (key.Key == ConsoleKey.Backspace)
                        {
                            lock (inputLock)
                            {
                                if (currentInput.Length > 0)
                                {
                                    currentInput = currentInput.Substring(0, currentInput.Length - 1);
                                    Console.Write("\b \b");
                                }
                            }
                        }
                        else if (!char.IsControl(key.KeyChar))
                        {
                            lock (inputLock)
                            {
                                currentInput += key.KeyChar;
                                Console.Write(key.KeyChar);
                            }
                        }
                    }

                    string message = currentInput;

                    if (message.ToLower().Trim() == "exit")
                    {
                        Console.WriteLine("Bağlantı kapatılıyor...");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    byte[] data = Encoding.UTF8.GetBytes($"{nickname}: {message}");
                    clientSocket.Send(data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
            }
            finally
            {
                clientSocket.Close();
                Console.WriteLine("Bağlantı kapatıldı.");
            }
        }

        static void ReceiveMessages(Socket socket)
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int bytesRead = socket.Receive(buffer);

                    if (bytesRead == 0)
                    {
                        Console.WriteLine("\nSunucu bağlantıyı kapattı.");
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    lock (inputLock)
                    {
                        // Mevcut satırı tamamen sil
                        int currentLineCursor = Console.CursorTop;
                        Console.SetCursorPosition(0, currentLineCursor);
                        Console.Write(new string(' ', Console.WindowWidth - 1));
                        Console.SetCursorPosition(0, currentLineCursor);

                        // Gelen mesajı yazdır
                        Console.WriteLine(message);

                        // Kullanıcının yazdığını geri yükle
                        Console.Write($"{nickname}: {currentInput}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAlma hatası: {ex.Message}");
            }
        }
    }
}