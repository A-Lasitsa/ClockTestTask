using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public static class NTPTime
{
    public static DateTime GetNTPTime(string ntpServer)
    {
        var ntpData = new byte[48];
        ntpData[0] = 0x1B; // 00 011 011 in hex

        // Get source address and create a socket
        var addresses = Dns.GetHostEntry(ntpServer).AddressList;
        var remoteEP = new IPEndPoint(addresses[0], 123);
        var socket = new Socket(addresses[0].AddressFamily, SocketType.Dgram, ProtocolType.Udp);

        // Get time data from server and close socket
        try
        {
            socket.Connect(remoteEP);
            socket.ReceiveTimeout = 2000;
            socket.Send(ntpData);
            socket.Receive(ntpData);
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
            socket.Close();
            return DateTime.MinValue;
        }
        socket.Close();

        // Convert received data to DateTime format
        uint seconds = (uint)ntpData[40] << 24 | (uint)ntpData[41] << 16 | (uint)ntpData[42] << 8 | ntpData[43];
        uint fract = (uint)ntpData[44] << 24 | (uint)ntpData[45] << 16 | (uint)ntpData[46] << 8 | ntpData[47];
        
        var timeSec = seconds + fract / uint.MaxValue;
        var dateTime = new DateTime(1900, 1, 1).AddSeconds(timeSec);

        return dateTime;
    }
}
