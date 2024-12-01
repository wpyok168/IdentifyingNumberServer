using HPSocket;
using HPSocket.Tcp;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace IdentifyingNumberServer
{
    internal class HpsocketServer
    {
        private readonly ITcpServer _server;
        private readonly string _storagePath = "ServerFiles";
        private readonly ConcurrentDictionary<string, long> _progressTracker = new ConcurrentDictionary<string, long>(); // 存储文件进度

        public HpsocketServer()
        {
            _server = new TcpServer();
            _server.SocketBufferSize = 4096;
            _server.OnReceive += _server_OnReceive; ;
            _server.OnClose += _server_OnClose;
        }

        public void Start(string address, ushort port)
        {
            Directory.CreateDirectory(_storagePath); // 创建存储文件夹
            _server.Address = address;
            _server.Port = port;

            if (_server.Start())
            {
                Console.WriteLine($"Server started at {address}:{port}");
            }
            else
            {
                Console.WriteLine("Failed to start server.");
            }
        }

        private HandleResult _server_OnReceive(IServer sender, IntPtr connId, byte[] data)
        {
            string json = Encoding.UTF8.GetString(data);
            var request = JsonConvert.DeserializeObject<FileRequest>(json);

            if (request == null)
            {
                SendResponse(connId, new FileResponse { Status = "error", Message = "Invalid JSON request" });
                return HandleResult.Ok;
            }

            switch (request.Command)
            {
                case "upload":
                    _ = HandleUpload(connId, request);
                    break;
                case "download":
                    _ = HandleDownload(connId, request);
                    break;
                case "push":
                    _ = PushFile(connId, request.FileName);
                    break;
                default:
                    SendResponse(connId, new FileResponse { Status = "error", Message = "Unknown command" });
                    break;
            }

            return HandleResult.Ok;
        }

        private HandleResult _server_OnClose(IServer sender, IntPtr connId, SocketOperation socketOperation, int errorCode)
        {
            Console.WriteLine($"Connection closed: {connId}");
            return HandleResult.Ok;
        }

        private async Task HandleUpload(IntPtr connId, FileRequest request)
        {
            string filePath = Path.Combine(_storagePath, request.FileName);
            long offset = request.Offset;

            try
            {
                byte[] fileData = Convert.FromBase64String(request.Data);

                using (var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    await fs.WriteAsync(fileData, 0, fileData.Length);
                    _progressTracker[request.FileName] = fs.Length; // 更新进度
                }

                SendResponse(connId, new FileResponse
                {
                    Status = "success",
                    Message = "上传完成",//Upload completed
                    Offset = _progressTracker[request.FileName]
                });
            }
            catch (Exception ex)
            {
                SendResponse(connId, new FileResponse { Status = "error", Message = $"Upload failed: {ex.Message}" });
            }
        }

        private async Task HandleDownload(IntPtr connId, FileRequest request)
        {
            string filePath = Path.Combine(_storagePath, request.FileName);

            if (!File.Exists(filePath))
            {
                SendResponse(connId, new FileResponse { Status = "error", Message = "File not found" });
                return;
            }

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(request.Offset, SeekOrigin.Begin);

                    byte[] buffer = new byte[8192]; // 每次发送 8KB 数据
                    int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);

                    SendResponse(connId, new FileResponse
                    {
                        Status = "success",
                        Message = "File chunk",//"File chunk
                        Data = Convert.ToBase64String(buffer, 0, bytesRead),
                        Offset = fs.Position
                    });
                    if (fs.Position==fs.Length)
                    {
                        SendResponse(connId, new FileResponse
                        {
                            Status = "success",
                            Message = "下载完成",//Download Complete
                            Data = null,
                            Offset = fs.Length
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                SendResponse(connId, new FileResponse { Status = "error", Message = $"Download failed: {ex.Message}" });
            }
        }

        private async Task PushFile(IntPtr connId, string fileName)
        {
            string filePath = Path.Combine(_storagePath, fileName);

            if (!File.Exists(filePath))
            {
                SendResponse(connId, new FileResponse { Status = "error", Message = "File not found" });
                return;
            }

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;

                    while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        SendResponse(connId, new FileResponse
                        {
                            Status = "success",
                            Message = "File chunk",
                            Data = Convert.ToBase64String(buffer, 0, bytesRead),
                            Offset = fs.Position
                        });
                    }

                    Console.WriteLine($"File {fileName} pushed to client.");
                }
            }
            catch (Exception ex)
            {
                SendResponse(connId, new FileResponse { Status = "error", Message = $"Push failed: {ex.Message}" });
            }
        }

        private void SendResponse(IntPtr connId, FileResponse response)
        {
            string json = JsonConvert.SerializeObject(response);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            _server.Send(connId, bytes, bytes.Length);
        }
    }
    public class FileRequest
    {
        public string Command { get; set; }
        public string FileName { get; set; }
        public long Offset { get; set; }
        public string Data { get; set; }
    }

    public class FileResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public long Offset { get; set; }
        public string Data { get; set; }
    }
}
