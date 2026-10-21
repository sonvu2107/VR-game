using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>One LAN peer, length-prefixed UTF-8. Workers never call Unity APIs.</summary>
public sealed class LanTransport : IDisposable
{
    public const int MaxMessageBytes = 4 * 1024 * 1024;
    private const int QueueLimit = 128;
    private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> events = new ConcurrentQueue<string>();
    private readonly AutoResetEvent writeSignal = new AutoResetEvent(false);
    private readonly object peerLock = new object();
    private TcpListener listener;
    private TcpClient peer;
    private volatile bool disposed;
    public bool Connected { get { lock (peerLock) return peer != null && !disposed; } }

    public void Host(int port)
    {
        ValidatePort(port);
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start(2);
        Task.Run(() =>
        {
            while (!disposed)
            {
                try
                {
                    TcpClient accepted = listener.AcceptTcpClient();
                    lock (peerLock)
                    {
                        if (peer != null || disposed) { accepted.Close(); continue; }
                        Attach(accepted);
                    }
                }
                catch (Exception) { if (!disposed) events.Enqueue("error:Không thể nhận kết nối LAN."); break; }
            }
        });
    }

    public void Join(string address, int port)
    {
        ValidatePort(port);
        if (!IPAddress.TryParse(address, out IPAddress ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Nhập địa chỉ IPv4 của máy chủ.");
        Task.Run(async () =>
        {
            TcpClient connecting = new TcpClient();
            try
            {
                Task connect = connecting.ConnectAsync(ip, port);
                if (await Task.WhenAny(connect, Task.Delay(5000)) != connect)
                    throw new TimeoutException();
                await connect;
                lock (peerLock)
                {
                    if (disposed) { connecting.Close(); return; }
                    Attach(connecting);
                }
            }
            catch (Exception ex)
            {
                connecting.Close();
                if (!disposed)
                {
                    string detail = ex is TimeoutException
                        ? "Hết thời gian chờ (5 giây). Kiểm tra IPv4 Wi-Fi của máy chủ, tường lửa và mạng Wi-Fi có cô lập thiết bị không."
                        : ex is SocketException socket && socket.SocketErrorCode == SocketError.ConnectionRefused
                            ? "Máy chủ chưa mở phòng hoặc sai cổng."
                            : "Kiểm tra IPv4 Wi-Fi của máy chủ, cổng và tường lửa.";
                    events.Enqueue("error:Không kết nối được " + address + ":" + port + ". " + detail);
                }
            }
        });
    }

    private void Attach(TcpClient connection)
    {
        connection.NoDelay = true;
        connection.ReceiveTimeout = 15000;
        connection.SendTimeout = 5000;
        peer = connection;
        events.Enqueue("connected");
        Task.Run(() => ReadLoop(connection));
        Task.Run(() => WriteLoop(connection));
    }

    public bool Send(string message)
    {
        if (!Connected || message == null) return false;
        if (outgoing.Count >= QueueLimit || Encoding.UTF8.GetByteCount(message) > MaxMessageBytes)
        { DisconnectPeer(); return false; }
        outgoing.Enqueue(message);
        writeSignal.Set();
        return true;
    }

    public bool TryReceive(out string message) => incoming.TryDequeue(out message);
    public bool TryEvent(out string value) => events.TryDequeue(out value);

    private void ReadLoop(TcpClient connection)
    {
        try
        {
            using (BinaryReader reader = new BinaryReader(connection.GetStream(), Encoding.UTF8, true))
            {
                while (!disposed && IsPeer(connection))
                {
                    int length = reader.ReadInt32();
                    if (length <= 0 || length > MaxMessageBytes) throw new InvalidDataException("Invalid LAN frame.");
                    byte[] bytes = reader.ReadBytes(length);
                    if (bytes.Length != length) throw new EndOfStreamException();
                    if (incoming.Count >= QueueLimit) throw new InvalidDataException("LAN receive queue full.");
                    incoming.Enqueue(Encoding.UTF8.GetString(bytes));
                }
            }
        }
        catch (Exception) { }
        finally { Drop(connection); }
    }

    private void WriteLoop(TcpClient connection)
    {
        try
        {
            using (BinaryWriter writer = new BinaryWriter(connection.GetStream(), Encoding.UTF8, true))
            {
                while (!disposed && IsPeer(connection))
                {
                    if (!outgoing.TryDequeue(out string message)) { writeSignal.WaitOne(100); continue; }
                    byte[] bytes = Encoding.UTF8.GetBytes(message);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                    writer.Flush();
                }
            }
        }
        catch (Exception) { }
        finally { Drop(connection); }
    }

    private bool IsPeer(TcpClient connection) { lock (peerLock) return ReferenceEquals(peer, connection); }
    private void Drop(TcpClient connection)
    {
        lock (peerLock)
        {
            if (!ReferenceEquals(peer, connection)) return;
            peer = null;
            connection.Close();
            while (outgoing.TryDequeue(out _)) { }
            while (incoming.TryDequeue(out _)) { }
            if (!disposed) events.Enqueue("disconnected");
        }
    }
    public void DisconnectPeer() { lock (peerLock) { if (peer != null) Drop(peer); } }
    public void Dispose()
    {
        disposed = true;
        listener?.Stop();
        DisconnectPeer();
        writeSignal.Set();
        // Workers can still be waking up; do not dispose their signal underneath them.
    }
    public static void ValidatePort(int port)
    {
        if (port < 1024 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Cổng phải từ 1024 đến 65535.");
    }
}
