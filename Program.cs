using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace SocketTcpClient
{
    class Program
    {
        static int numberOfRequestsExecuted = 0;
        static int port = 2013;
        static string address = "88.212.241.115";    //Порт и IP-адресс
        static StringBuilder key = null;
        static IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(address), port);
        static bool flagChange = true;
        static bool flagKey = false;
        static Stack<int> errors; // Стек для отлова ошибок
        const int n = 2018;
        static bool[] answerRecived = new bool[n];         //Получили значение от сервера 
        static int[] arrayOfResponses = new int[n];   //Массив с обработанными ответами от сервера
        static Regex regex = new Regex(@"\d*");    //Регулярное выражение для поиска чисел


        static void FlagChange()
        {
            Thread.Sleep(10000);
            flagChange = true;
        }
        static async void FlagChangeAsync()
        {
            await Task.Run(() => FlagChange());
        }
        static void SendMessage(ref Socket socket, string message)
        {
            Encoding encodingType = Encoding.GetEncoding("US-ASCII");
            Byte[] data = encodingType.GetBytes(message);
            socket.Send(data);
        }
        static StringBuilder RecieveMessage(ref Socket socket)
        {
            Byte[] data = new byte[256];
            StringBuilder builder = new StringBuilder();
            int bytes = 0;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding encodingType = Encoding.GetEncoding("koi8-r");

            do
            {
                bytes = socket.Receive(data, data.Length, 0);
                builder.Append(encodingType.GetString(data, 0, bytes));
            }
            while (builder[builder.Length - 1] != '\n' && bytes > 0);
            return builder;
        }
        static async void GetKeyAsync()
        {
            await Task.Run(() => GetKey());
        }
        static void GetKey()
        {
            while (!flagKey)
            {
                Socket socket = null;
                try
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Connect(ipPoint);
                    SendMessage(ref socket, "Register\n");
                    key = RecieveMessage(ref socket);
                    key.Remove(0, 3);
                    if (key.ToString().IndexOf("Rate limit. Please wait some time then repeat.") != -1) Thread.Sleep(2000);
                    else
                    {
                        flagKey = true;
                        FlagChangeAsync();
                    }
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
                catch
                {
                    socket.Dispose();
                    flagChange = false;
                    flagKey = false;
                    GetKeyAsync();
                    return;
                }
                finally {
                    socket.Dispose();
                }
            }
        }
        static int ParseString(string response)
        {
            int answer = 0;                              //Полученное число
            MatchCollection matches = regex.Matches(response);      //Набор успешных совпадений
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                    if (match.Value != "") answer = int.Parse(match.Value);
            }
            return answer;
        }    //Парсим строку, пришедшую с сервера
        static void ServerAnswer(int number)
        {
            while (!answerRecived[number-1])
            {
                Socket socket = null;
                try
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    int answer = 0;
                    socket.Connect(ipPoint);
                    if (flagKey)
                    {
                        StringBuilder message = new StringBuilder(key.ToString());
                        string temp = "|" + number.ToString() + "\n";
                        message.Insert(message.Length - 4, temp);
                        SendMessage(ref socket, message.ToString());
                        StringBuilder builder = RecieveMessage(ref socket);
                        answer = ParseString(builder.ToString());
                        if (answer != 0 && (builder[builder.Length - 1] == '\n' || builder[builder.Length - 1] == ' ' || builder[builder.Length - 1] == '.'))
                        {
                            arrayOfResponses[number - 1] = answer;
                            answerRecived[number - 1] = true;
                        }
                        else if (builder.ToString().IndexOf("Key has") != -1)
                        {
                            socket.Shutdown(SocketShutdown.Both);
                            socket.Close();
                            throw new Exception("Key has expired");
                        }
                        socket.Shutdown(SocketShutdown.Both);
                        socket.Close();
                    }
                    else Thread.Sleep(2000);
                }
                catch (Exception ex)
                {
                    if (ex.ToString().IndexOf("Key has expired") != -1 && flagChange)
                    {
                        flagChange = false;
                        flagKey = false;
                        GetKeyAsync();
                    }
                    socket.Dispose();
                    errors.Push(number);
                    return;
                }
                finally { socket.Dispose(); }
            }
        }
        static void StartTasks(int leftBorder, int rightBorder) {
            for (int i = leftBorder + 1; i <= rightBorder; ++i)
            {
                while (!flagKey)
                {
                    flagChange = false;
                    GetKey();
                }
                ServerAnswerAsync(i);
            }
        }
        static void WaitingForAllAnswers(int leftBorder, int rightBorder)
        {
            while (true)
            {
                Thread.Sleep(1000);
                numberOfRequestsExecuted = 0;
                while (errors.Count != 0)
                {
                    Thread.Sleep(100);
                    int temp = errors.Pop();
                    ServerAnswerAsync(temp);
                }
                for (int i = leftBorder; i < rightBorder; ++i)
                    if (answerRecived[i]) numberOfRequestsExecuted++;
                if (numberOfRequestsExecuted == rightBorder - leftBorder) break;
            }
        }
        static void SendAnswer(double answer) {
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(ipPoint);

                string message = "Check_Advanced " + answer.ToString() + "\n"; 
                SendMessage(ref socket, message);
                StringBuilder builder = RecieveMessage(ref socket);
                Console.WriteLine(builder.ToString());
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch
            {
                socket.Dispose();
            }
        }
        static async void ServerAnswerAsync(int number)
        {
            await Task.Run(() => ServerAnswer(number));
        }

        static void Main(string[] args)
        {
            GetKey();
            for (int j = 0; j < 4; ++j)
            {
                errors = new Stack<int>();
                int leftBorder = j * 505;
                int rightBorder;

                if (j == 3) rightBorder = 2018;
                else rightBorder = (j + 1) * 505;

                StartTasks(leftBorder, rightBorder);

                WaitingForAllAnswers(leftBorder, rightBorder);
                
                flagKey = false;
                flagChange = false;
                GetKey();
            }
            Array.Sort(arrayOfResponses);
            
            double answer = arrayOfResponses[1009] + arrayOfResponses[1008];
            answer /= 2;

            SendAnswer(answer);
            Console.ReadKey();
        }
    }
}