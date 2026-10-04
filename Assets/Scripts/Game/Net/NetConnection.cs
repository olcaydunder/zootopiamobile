using System;
using System.Collections.Generic;

/// <summary>
/// One end of a UDP conversation (plain C#, used by both the game server and the phone).
/// Carries two kinds of messages in each datagram:
///  - reliable: numbered, resent until acknowledged, delivered once and in order
///    (joins, hits, deaths, loot, doors...);
///  - unreliable: sent once, may be lost (movement and snapshots, 20 per second, the next one replaces it).
/// Datagram: 'Z' 'M' kind=Data, token(4), ack(2), reliableCount(1) {seq(2) len(2) bytes}, unreliableCount(1) {len(2) bytes}.
/// "ack" is the last reliable sequence number received in order.
/// </summary>
public sealed class NetConnection
{
    public const byte Magic0 = (byte)'Z';
    public const byte Magic1 = (byte)'M';
    public const byte KindHello = 1, KindWelcome = 2, KindReject = 3, KindData = 4, KindBye = 5;

    /// <summary>Datagrams stay under the usual Internet MTU (no IP fragmentation).</summary>
    public const int MaxPacket = 1200;
    /// <summary>Largest single message (bigger data is split by the caller).</summary>
    public const int MaxMessage = 1000;
    private const int HeaderSize = 2 + 1 + 4 + 2;
    private const double ResendAfter = 0.22;
    private const double KeepAliveAfter = 0.1;
    private const int MaxPacketsPerFlush = 8;
    private const int ReceiveWindow = 4096;

    private sealed class Pending
    {
        public ushort seq;
        public byte[] data;
        public double lastSent = -1;
        public int sends;
    }

    public uint Token;
    public double LastReceive;
    public double LastSend = -1;
    /// <summary>Most times any single reliable message was sent (a dead link shows as a growing number).</summary>
    public int WorstResends { get; private set; }
    public int PendingReliable { get { return pending.Count; } }

    private ushort nextSendSeq;
    private ushort nextRecvSeq;
    private bool ackDirty;
    private readonly List<Pending> pending = new List<Pending>();
    private readonly Dictionary<ushort, byte[]> early = new Dictionary<ushort, byte[]>();
    private readonly List<byte[]> unreliable = new List<byte[]>();
    private readonly byte[] packet = new byte[MaxPacket];

    public NetConnection(uint token, double now)
    {
        Token = token;
        LastReceive = now;
    }

    /// <summary>Wrapping comparison of 16-bit sequence numbers: true when a comes before b.</summary>
    public static bool SeqBefore(ushort a, ushort b)
    {
        return (short)(a - b) < 0;
    }

    public void SendReliable(byte[] message)
    {
        if (message == null || message.Length == 0)
            return;
        if (message.Length > MaxMessage)
            throw new ArgumentException("reliable message too large: " + message.Length);
        pending.Add(new Pending { seq = nextSendSeq++, data = message });
    }

    public void SendUnreliable(byte[] message)
    {
        if (message == null || message.Length == 0 || message.Length > MaxMessage)
            return;
        unreliable.Add(message);
    }

    /// <summary>
    /// Writes due datagrams through <paramref name="output"/>(buffer, length): new and timed-out reliable
    /// messages, the queued unreliable ones and, when needed, a bare acknowledgement / keep-alive.
    /// </summary>
    public void Flush(double now, Action<byte[], int> output)
    {
        int nextPending = 0;
        int nextUnreliable = 0;
        int packets = 0;
        bool keepAlive = ackDirty || LastSend < 0 || now - LastSend >= KeepAliveAfter;

        while (packets < MaxPacketsPerFlush)
        {
            int len = WriteHeader();
            int relCountAt = len++;
            int relCount = 0;
            for (; nextPending < pending.Count && relCount < 255; nextPending++)
            {
                var p = pending[nextPending];
                if (p.lastSent >= 0 && now - p.lastSent < ResendAfter)
                    continue;
                if (len + 4 + p.data.Length + 1 > MaxPacket)
                    break;   // next datagram
                packet[len++] = (byte)p.seq;
                packet[len++] = (byte)(p.seq >> 8);
                packet[len++] = (byte)p.data.Length;
                packet[len++] = (byte)(p.data.Length >> 8);
                Buffer.BlockCopy(p.data, 0, packet, len, p.data.Length);
                len += p.data.Length;
                p.lastSent = now;
                p.sends++;
                if (p.sends > WorstResends)
                    WorstResends = p.sends;
                relCount++;
            }
            packet[relCountAt] = (byte)relCount;

            int unrelCountAt = len++;
            int unrelCount = 0;
            for (; nextUnreliable < unreliable.Count && unrelCount < 255; nextUnreliable++)
            {
                var u = unreliable[nextUnreliable];
                if (len + 2 + u.Length > MaxPacket)
                    break;
                packet[len++] = (byte)u.Length;
                packet[len++] = (byte)(u.Length >> 8);
                Buffer.BlockCopy(u, 0, packet, len, u.Length);
                len += u.Length;
                unrelCount++;
            }
            packet[unrelCountAt] = (byte)unrelCount;

            if (relCount == 0 && unrelCount == 0 && !keepAlive)
                break;
            output(packet, len);
            packets++;
            keepAlive = false;
            ackDirty = false;
            LastSend = now;

            bool moreReliable = false;
            for (int i = nextPending; i < pending.Count; i++)
            {
                if (pending[i].lastSent < 0 || now - pending[i].lastSent >= ResendAfter)
                {
                    moreReliable = true;
                    break;
                }
            }
            if (!moreReliable && nextUnreliable >= unreliable.Count)
                break;
        }
        unreliable.Clear();   // anything that did not fit is dropped (it is unreliable)
    }

    private int WriteHeader()
    {
        packet[0] = Magic0;
        packet[1] = Magic1;
        packet[2] = KindData;
        packet[3] = (byte)Token;
        packet[4] = (byte)(Token >> 8);
        packet[5] = (byte)(Token >> 16);
        packet[6] = (byte)(Token >> 24);
        ushort ack = (ushort)(nextRecvSeq - 1);
        packet[7] = (byte)ack;
        packet[8] = (byte)(ack >> 8);
        return HeaderSize;
    }

    /// <summary>Reads the token of a datagram (0 if it is not a data datagram).</summary>
    public static uint PeekToken(byte[] data, int length)
    {
        if (length < HeaderSize || data[0] != Magic0 || data[1] != Magic1 || data[2] != KindData)
            return 0;
        return (uint)(data[3] | (data[4] << 8) | (data[5] << 16) | (data[6] << 24));
    }

    /// <summary>
    /// Handles one data datagram. <paramref name="deliver"/>(buffer, offset, length, reliable) is called
    /// for every message that is new: reliable ones strictly in order, unreliable ones as they come.
    /// Returns false for a malformed datagram.
    /// </summary>
    public bool Receive(byte[] data, int length, double now, Action<byte[], int, int, bool> deliver)
    {
        if (PeekToken(data, length) != Token)
            return false;
        LastReceive = now;
        int pos = 7;
        ushort ack = (ushort)(data[pos] | (data[pos + 1] << 8));
        pos += 2;
        // Everything up to "ack" arrived: stop resending it.
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (!SeqBefore(ack, pending[i].seq))
                pending.RemoveAt(i);
        }

        if (pos >= length)
            return false;
        int relCount = data[pos++];
        for (int r = 0; r < relCount; r++)
        {
            if (pos + 4 > length)
                return false;
            ushort seq = (ushort)(data[pos] | (data[pos + 1] << 8));
            int len = data[pos + 2] | (data[pos + 3] << 8);
            pos += 4;
            if (len <= 0 || pos + len > length)
                return false;
            ackDirty = true;   // answer soon, even if it is a duplicate (our ack may have been lost)
            if (seq == nextRecvSeq)
            {
                nextRecvSeq++;
                deliver(data, pos, len, true);
                byte[] next;
                while (early.TryGetValue(nextRecvSeq, out next))
                {
                    early.Remove(nextRecvSeq);
                    nextRecvSeq++;
                    deliver(next, 0, next.Length, true);
                }
            }
            else if (SeqBefore(nextRecvSeq, seq) && (ushort)(seq - nextRecvSeq) < ReceiveWindow && !early.ContainsKey(seq))
            {
                var copy = new byte[len];
                Buffer.BlockCopy(data, pos, copy, 0, len);
                early[seq] = copy;
            }
            pos += len;
        }

        if (pos >= length)
            return false;
        int unrelCount = data[pos++];
        for (int u = 0; u < unrelCount; u++)
        {
            if (pos + 2 > length)
                return false;
            int len = data[pos] | (data[pos + 1] << 8);
            pos += 2;
            if (len <= 0 || pos + len > length)
                return false;
            deliver(data, pos, len, false);
            pos += len;
        }
        return true;
    }
}
