using System;
using System.Net;
using System.Net.Sockets;

/// <summary>Non-blocking UDP socket polled from the main thread (plain C#; no threads, no packages).</summary>
public sealed class NetSocket : IDisposable
{
    private Socket socket;
    private readonly byte[] receiveBuffer = new byte[2048];
    private EndPoint any;

    public bool IsOpen { get { return socket != null; } }
    public int LocalPort { get; private set; }

    /// <summary>Server: listens on <paramref name="port"/>. Phone: port 0 picks a free one.</summary>
    public void Open(int port, AddressFamily family = AddressFamily.InterNetwork)
    {
        Close();
        socket = new Socket(family, SocketType.Dgram, ProtocolType.Udp);
        socket.Blocking = false;
        try { socket.ReceiveBufferSize = 1 << 18; } catch (SocketException) { }
        try { socket.SendBufferSize = 1 << 18; } catch (SocketException) { }
        var bindAddress = family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
        socket.Bind(new IPEndPoint(bindAddress, port));
        LocalPort = ((IPEndPoint)socket.LocalEndPoint).Port;
        any = new IPEndPoint(bindAddress, 0);
    }

    /// <summary>
    /// Next waiting datagram, or false when there is none. The data stays valid until the next call.
    /// Errors (e.g. ICMP "port unreachable" bounced back to us) are skipped.
    /// </summary>
    public bool TryReceive(out byte[] data, out int length, out EndPoint from)
    {
        data = receiveBuffer;
        length = 0;
        from = null;
        if (socket == null)
            return false;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                if (socket.Available <= 0 && !socket.Poll(0, SelectMode.SelectRead))
                    return false;
                EndPoint ep = any;
                length = socket.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref ep);
                from = ep;
                return length > 0;
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.WouldBlock)
                    return false;
                // ConnectionReset / MessageSize etc.: drop this one and keep reading.
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
        return false;
    }

    public void Send(byte[] data, int length, EndPoint to)
    {
        if (socket == null || to == null)
            return;
        try
        {
            socket.SendTo(data, 0, length, SocketFlags.None, to);
        }
        catch (SocketException)
        {
            // Network gone for a moment (switching Wi-Fi / mobile data): the time-out handles it.
        }
        catch (ObjectDisposedException) { }
    }

    public void Close()
    {
        if (socket == null)
            return;
        try { socket.Close(); } catch (Exception) { }
        socket = null;
    }

    public void Dispose()
    {
        Close();
    }

    /// <summary>
    /// Resolves a host name or IP address, preferring IPv4. Null if it cannot be resolved.
    /// (Blocking DNS: called once while connecting.)
    /// </summary>
    public static IPEndPoint Resolve(string host, int port)
    {
        IPAddress ip;
        if (IPAddress.TryParse(host, out ip))
            return new IPEndPoint(ip, port);
        try
        {
            IPAddress[] all = Dns.GetHostAddresses(host);
            foreach (var a in all)
                if (a.AddressFamily == AddressFamily.InterNetwork)
                    return new IPEndPoint(a, port);
            if (all.Length > 0)
                return new IPEndPoint(all[0], port);
        }
        catch (Exception) { }
        return null;
    }
}
