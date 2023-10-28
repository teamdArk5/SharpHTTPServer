using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SharpServer
{
    public class Server
    {
        private Socket Socket;
        //Define an available port you'd like to listen HERE.
        private readonly int Port = 80;
        private readonly int Backlog = 10;
        private byte[] Buffer = new byte[1024];
        private List<Socket> Clients = new List<Socket>();

        //Simply create the server by creating a Socket object.
        public Server()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        //Start the server and listen to the socket.
        public void Start()
        {
            Socket.Bind(new IPEndPoint(IPAddress.Any, Port));
            Socket.Listen(Backlog);
        }

        //Ah, we received a request so we must accept it with joy!
        public void Accept()
        {
            Socket.BeginAccept(new AsyncCallback(AcceptCallback), null);
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            Socket CurrentClient = Socket.EndAccept(ar);
            //Not much reason to do that here since we ourselves are the clients, but having a list of clients is a must.
            Clients.Add(CurrentClient);
            CurrentClient.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), CurrentClient);
            //After we BeginReceive we need to start Accepting other clients
            Accept();
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            //We pass CurrentClient as the state so we could make further actions with it, hardcast is needed.
            var client = (Socket)ar.AsyncState;
            var receivedSize = client.EndReceive(ar);
            var receivedBytes = new byte[receivedSize];
            Array.Copy(Buffer, receivedBytes, receivedSize);
            var data = Encoding.ASCII.GetString(receivedBytes);
            Console.WriteLine("Data received: \n" + data);
            //Looks nasty and unecessary but we need to distinguish GET's from other methods.
            var headers = data.Split(' ');
            if (headers[0].ToUpper() == "GET")
            {
                string requestedPath = headers[1];
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, requestedPath.Trim('/'));

                if (File.Exists(fullPath))
                {
                    if (File.GetAttributes(fullPath).HasFlag(FileAttributes.Directory))
                    {
                        // 如果是目录，则列出文件
                        var files = Directory.GetFiles(fullPath);
                        var response = GenerateDirectoryListingResponse(requestedPath, files);
                        var responseBytes = Encoding.ASCII.GetBytes(response);
                        client.BeginSend(responseBytes, 0, responseBytes.Length, SocketFlags.None, new AsyncCallback(SendCallback), client);
                    }
                    else
                    {
                        // 如果是文件，则支持下载
                        //var responseBytes = File.ReadAllBytes(fullPath);
                        //client.BeginSend(responseBytes, 0, responseBytes.Length, SocketFlags.None, new AsyncCallback(SendCallback), client);

                        if (File.Exists(fullPath))
                        {
                            Stream fileStream = File.OpenRead(fullPath);
                            var response = GenerateFileDownloadResponse(Path.GetFileName(fullPath), fileStream.Length);
                            var responseBytes = Encoding.ASCII.GetBytes(response);
                            client.BeginSend(responseBytes, 0, responseBytes.Length, SocketFlags.None, asyncResult =>
                            {
                                client.EndSend(asyncResult);
                                BeginSendFileData(client, fileStream);
                            }, client);
                        }

                    }
                }
                else if (requestedPath == "/")
                {
                    // 如果请求路径是根目录 "/", 则列出当前工作目录中的所有文件和目录
                    var files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory);
                    var directories = Directory.GetDirectories(AppDomain.CurrentDomain.BaseDirectory);
                    var response = GenerateRootDirectoryListingResponse(requestedPath, directories, files);
                    var responseBytes = Encoding.ASCII.GetBytes(response);
                    client.BeginSend(responseBytes, 0, responseBytes.Length, SocketFlags.None, new AsyncCallback(SendCallback), client);
                }
                else
                {
                    // 文件或目录不存在
                    Console.WriteLine("File or directory not found: " + fullPath);
                    var notFoundResponse = GenerateNotFoundResponse();
                    var responseBytes = Encoding.ASCII.GetBytes(notFoundResponse);
                    client.BeginSend(responseBytes, 0, responseBytes.Length, SocketFlags.None, new AsyncCallback(SendCallback), client);
                }
            }
            else
            {
                //Something is wrong here...
                Console.WriteLine("Only GET method is supported");
                var responseBytes = Encoding.ASCII.GetBytes("Only GET method is supported");
                client.BeginSend(responseBytes, 0, responseBytes.Length, SocketFlags.None, new AsyncCallback(SendCallback), client);
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            Socket client = (Socket)ar.AsyncState;
            //Turn the lights off after leaving the room!
            client.Shutdown(SocketShutdown.Send);
        }

        private string GetIndexFile()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"index.html");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            return "<h1>404 Index page somehow not found... ¯\\_(ツ)_/¯<h1>";
        }

        // 生成目录文件列表的HTML响应
        private string GenerateDirectoryListingResponse(string path, string[] files)
        {
            var response = new StringBuilder();
            response.AppendLine("HTTP/1.1 200 OK");
            response.AppendLine("Content-Type: text/html; charset=UTF-8");
            response.AppendLine();
            response.AppendLine("<html><body>");
            response.AppendLine("<h1>Directory Listing: " + path + "</h1>");
            response.AppendLine("<ul>");

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                response.AppendLine($"<li><a href=\"{path}/{fileName}\">{fileName}</a></li>");
            }

            response.AppendLine("</ul>");
            response.AppendLine("</body></html>");

            return response.ToString();
        }

        // 生成文件或目录不存在的HTML响应
        private string GenerateNotFoundResponse()
        {
            var response = new StringBuilder();
            response.AppendLine("HTTP/1.1 404 Not Found");
            response.AppendLine("Content-Type: text/html; charset=UTF-8");
            response.AppendLine();
            response.AppendLine("<html><body>");
            response.AppendLine("<h1>404 Not Found</h1>");
            response.AppendLine("</body></html>");

            return response.ToString();
        }

        // 生成根目录的文件和目录列表的HTML响应
        private string GenerateRootDirectoryListingResponse(string path, string[] directories, string[] files)
        {
            var response = new StringBuilder();
            response.AppendLine("HTTP/1.1 200 OK");
            response.AppendLine("Content-Type: text/html; charset=UTF-8");
            response.AppendLine();
            response.AppendLine("<html><body>");
            response.AppendLine("<h1>Root Directory Listing</h1>");
            response.AppendLine("<ul>");

            foreach (var directory in directories)
            {
                var directoryName = Path.GetFileName(directory);
                response.AppendLine($"<li><a href=\"/{directoryName}/\">{directoryName}/</a></li>");
            }

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                response.AppendLine($"<li><a href=\"/{fileName}\">{fileName}</a></li>");
            }

            response.AppendLine("</ul>");
            response.AppendLine("</body></html>");

            return response.ToString();
        }

        private string GenerateFileDownloadResponse(string fileName, long fileSize)
        {
            var response = new StringBuilder();
            response.AppendLine("HTTP/1.1 200 OK");
            response.AppendLine("Content-Type: application/octet-stream");
            response.AppendLine($"Content-Disposition: attachment; filename=\"{fileName}\"");
            response.AppendLine($"Content-Length: {fileSize}");
            response.AppendLine();
            return response.ToString();
        }

        private void BeginSendFileData(Socket client, Stream fileStream)
        {
            byte[] buffer = new byte[1024];
            int bytesRead;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                byte[] chunk = new byte[bytesRead];
                Array.Copy(buffer, chunk, bytesRead);
                client.BeginSend(chunk, 0, chunk.Length, SocketFlags.None, asyncResult =>
                {
                    client.EndSend(asyncResult);
                }, client);
            }
            fileStream.Close();
            client.Shutdown(SocketShutdown.Send);
        }

    }

}