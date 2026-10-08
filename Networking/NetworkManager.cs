using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace FabledomMultiplayer.Networking
{
    public enum ConnectionState
    {
        Disconnected,
        Hosting,
        Connecting,
        Connected
    }

    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public bool IsHost => State == ConnectionState.Hosting || (State == ConnectionState.Connected && _isHost);
        public bool IsConnected => State == ConnectionState.Connected;

        public string StatusMessage { get; private set; } = "Отключено";

        private bool _isHost = false;
        private TcpListener _server;
        private TcpClient _client;
        private NetworkStream _stream;
        private Thread _networkThread;
        private volatile bool _running = false;

        private readonly ConcurrentQueue<byte[]> _incomingPackets = new ConcurrentQueue<byte[]>();

        public event Action<byte[]> OnPacketReceived;
        public event Action OnConnected;
        public event Action OnDisconnected;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartHost(int port = 7777)
        {
            Stop();
            _isHost = true;
            State = ConnectionState.Hosting;
            StatusMessage = $"Сервер запущен (порт {port}). Ожидание подключения...";
            MultiplayerPlugin.Logger.LogInfo(StatusMessage);

            _running = true;
            _networkThread = new Thread(() => ServerListenLoop(port)) { IsBackground = true };
            _networkThread.Start();
        }

        public void ConnectToHost(string host, int port = 7777)
        {
            Stop();
            _isHost = false;
            State = ConnectionState.Connecting;
            StatusMessage = $"Подключение к {host}:{port}...";
            MultiplayerPlugin.Logger.LogInfo(StatusMessage);

            _running = true;
            _networkThread = new Thread(() => ClientConnectLoop(host, port)) { IsBackground = true };
            _networkThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try
            {
                _stream?.Close();
                _client?.Close();
                _server?.Stop();
            }
            catch { }

            _stream = null;
            _client = null;
            _server = null;

            State = ConnectionState.Disconnected;
            StatusMessage = "Отключено";
        }

        public void SendPacket(byte[] data)
        {
            if (_stream == null || !_stream.CanWrite) return;
            try
            {
                byte[] lengthBytes = BitConverter.GetBytes(data.Length);
                _stream.Write(lengthBytes, 0, 4);
                _stream.Write(data, 0, data.Length);
                _stream.Flush();
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.Logger.LogWarning($"Ошибка отправки пакета: {ex.Message}");
                Disconnect();
            }
        }

        private void ServerListenLoop(int port)
        {
            try
            {
                _server = new TcpListener(IPAddress.Any, port);
                _server.Start();

                while (_running)
                {
                    if (_server.Pending())
                    {
                        _client = _server.AcceptTcpClient();
                        _client.NoDelay = true;
                        _stream = _client.GetStream();
                        State = ConnectionState.Connected;
                        StatusMessage = "Напарник подключился!";
                        MultiplayerPlugin.Logger.LogInfo(StatusMessage);
                        ReadPacketsLoop();
                        break;
                    }
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                if (_running)
                {
                    StatusMessage = $"Ошибка хоста: {ex.Message}";
                    MultiplayerPlugin.Logger.LogError(StatusMessage);
                }
            }
            finally
            {
                _server?.Stop();
            }
        }

        private void ClientConnectLoop(string host, int port)
        {
            try
            {
                _client = new TcpClient();
                var result = _client.BeginConnect(host, port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10));

                if (!success || !_client.Connected)
                {
                    StatusMessage = "Не удалось подключиться к серверу (таймаут).";
                    State = ConnectionState.Disconnected;
                    return;
                }

                _client.EndConnect(result);
                _client.NoDelay = true;
                _stream = _client.GetStream();
                State = ConnectionState.Connected;
                StatusMessage = "Успешное подключение к лобби!";
                MultiplayerPlugin.Logger.LogInfo(StatusMessage);

                ReadPacketsLoop();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка подключения: {ex.Message}";
                MultiplayerPlugin.Logger.LogError(StatusMessage);
                State = ConnectionState.Disconnected;
            }
        }

        private void ReadPacketsLoop()
        {
            byte[] lengthBuffer = new byte[4];
            while (_running && _stream != null && _stream.CanRead)
            {
                try
                {
                    int bytesRead = ReadExact(lengthBuffer, 4);
                    if (bytesRead < 4) break;

                    int packetLength = BitConverter.ToInt32(lengthBuffer, 0);
                    if (packetLength <= 0 || packetLength > 5 * 1024 * 1024) break;

                    byte[] packetData = new byte[packetLength];
                    if (ReadExact(packetData, packetLength) < packetLength) break;

                    _incomingPackets.Enqueue(packetData);
                }
                catch
                {
                    break;
                }
            }

            Disconnect();
        }

        private int ReadExact(byte[] buffer, int totalBytes)
        {
            int read = 0;
            while (read < totalBytes && _stream != null)
            {
                int r = _stream.Read(buffer, read, totalBytes - read);
                if (r <= 0) return read;
                read += r;
            }
            return read;
        }

        private void Disconnect()
        {
            if (State != ConnectionState.Disconnected)
            {
                State = ConnectionState.Disconnected;
                StatusMessage = "Соединение разорвано.";
                MultiplayerPlugin.Logger.LogWarning(StatusMessage);
            }
        }

        private void Update()
        {
            while (_incomingPackets.TryDequeue(out var packet))
            {
                try
                {
                    OnPacketReceived?.Invoke(packet);
                }
                catch (Exception ex)
                {
                    MultiplayerPlugin.Logger.LogError($"Ошибка обработки пакета: {ex}");
                }
            }
        }

        private void OnDestroy()
        {
            Stop();
        }
    }
}
