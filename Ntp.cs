using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Daemaged.NTP
{
  /// <summary>
  /// NTP leap second indicator used by <see cref="T:Daemaged.NTP.NtpPacket" />.
  /// </summary>
  public enum NtpLeapIndicator
  {
    NoLeap,
    OneSecondMore,
    OneSecondLess,
    AlarmCondition
  }

  /// <summary>
  /// NTP modes (non-reserved only) used by <see cref="T:Daemaged.NTP.NtpPacket" />.
  /// </summary>
  public enum NtpMode : byte
  {
    /// <summary>
    /// Broadcast mode. This component never operates in
    /// this mode - it is included for completeness only.
    /// </summary>
    Broadcast = 5,
    /// <summary>
    /// NTP/SNTP client mode. NTP packets sent by this
    /// component have this mode.
    /// </summary>
    Client = 3,
    /// <summary>
    /// NTP/SNTP server mode. NTP packets received by this
    /// component have this mode.
    /// </summary>
    Server = 4,
    /// <summary>
    /// Symmetric active NTP server mode. This component
    /// never operates in this mode - it is included for
    /// completeness only.
    /// </summary>
    SymmetricActive = 1,
    /// <summary>
    /// Symmetric passive NTP server mode. This component
    /// never operates in this mode - it is included for
    /// completeness only.
    /// </summary>
    SymmetricPassive = 2
  }

  /// <summary>
  /// Represents an NTP/SNTP error.
  /// </summary>
  public class NtpException : Exception
  {
    internal NtpException(string msg) : base(msg) { }

    internal NtpException(string msg, Exception inner) : base(msg, inner) { }
  }

  /// <summary>
  /// Provides methods for communication with SNTP/NTP servers.
  /// Supported protocol versions are 3 and 4.
  /// </summary>
  public class Ntp
  {
    /// <summary>
    /// Default NTP port number (DefaultPort).
    /// </summary>
    public const int DefaultPort = 123;

    private readonly IPEndPoint _remoteEndPoint;
    private int _timeout;
    private byte _versionNumber;

    /// <summary>
    /// Creates an instance of <see cref="T:Daemaged.NTP.Ntp" /> class and binds it to the specified NTP/SNTP time server.
    /// </summary>
    /// <param name="serverName">The time server hostname.</param>
    public Ntp(string serverName) : this(serverName, DefaultPort) {}

    /// <summary>
    /// Creates an instance of <see cref="T:Daemaged.NTP.Ntp" /> class and binds it to the specified NTP/SNTP time server.
    /// </summary>
    /// <param name="serverName">The time server hostname.</param>
    /// <param name="serverPort">The time server port.</param>
    public Ntp(string serverName, int serverPort)
    {
      if (serverName == null) {
        throw new ArgumentNullException("serverName");
      }
      if (serverName == string.Empty) {
        throw new ArgumentException("_remoteEndPoint");
      }
      if ((serverPort < 1) || (serverPort > 0xffff)) {
        throw new ArgumentException("Invalid port", "serverPort");
      }
      _remoteEndPoint = GetRemoteServer(serverName, serverPort);
      _timeout = 30000;
      _versionNumber = 3;
    }

    /// <summary>
    /// The maximum amount of time the client waits for response from
    /// the time server (before giving up and reporting failure).
    /// </summary>
    /// <value>Time in milliseconds. Must be positive, default is 30000 (30 seconds).</value>
    public int Timeout
    {
      get { return _timeout; }
      set
      {
        if (value <= 0)
          throw new ArgumentException(string.Format("Timeout must be a positive number.", value));
        _timeout = value;
      }
    }

    /// <summary>
    /// NTP/SNTP protocol version number.
    /// </summary>
    /// <value>1 to 4. The current NTP version is 3, the SNTP version is 4. Default is 3.</value>
    public int VersionNumber
    {
      get { return _versionNumber; }
      set {
        if ((value < 1) || (value > 4))
          throw new ArgumentException(string.Format("Protocol version number must be between 1 and 4 (not {0}).", value));
        _versionNumber = (byte) value;
      }
    }

    private static IPEndPoint GetRemoteServer(string hostName, int port)
    {
      var point = ToEndPoint(hostName, port);
      if (point != null)
        return point;
      var hostEntry = Dns.GetHostEntry(hostName);
      if (hostEntry.AddressList.Length == 0)
        throw new NtpException(string.Format("Host {0} has no IP address.", hostName));
      return ToEndPoint(hostEntry, port);
    }

    /// <summary>
    /// Sends a request for time to the NTP/SNTP server passed to the constructor
    /// and waits for the answer that contains the time values to calculate the offset
    /// between the local and server time.
    /// </summary>
    /// <returns>The server's response. Use its <see cref="P:Daemaged.NTP.NtpResponse.TimeOffset" /> to determine the time offset.</returns>
    public NtpResponse GetTime()
    {
      using (var socket = new Socket(_remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp)) {
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, _timeout);
        Send(socket);
        return new NtpResponse(ReceiveFrom(socket));
      }
    }

    private NtpPacket ReceiveFrom(Socket s)
    {
      int num;
      var packet = new NtpPacket();
      EndPoint remoteEP = new IPEndPoint(_remoteEndPoint.Address, _remoteEndPoint.Port);
      try {
        num = s.ReceiveFrom(packet.Data, ref remoteEP);
      }
      catch (SocketException exception) {
        var nativeErrorCode = exception.NativeErrorCode;
        switch ((SocketError) nativeErrorCode) {
          case SocketError.ConnectionReset:
            throw new NtpException("No time server at the specified address.", exception);

          case SocketError.TimedOut:
            throw new NtpException(string.Format("Request timed out - no answer in {0} ms.", _timeout), exception);
        }
        throw new NtpException("Socket error " + nativeErrorCode + " occurred.", exception);
      }
      packet.SetDestinationTimestamp(NtpTimestamp.Now);
      if (num < 0x30) {
        throw new NtpException(string.Format("Invalid NTP packet size of {0} bytes only.", num));
      }
      if (packet.Mode != NtpMode.Server) {
        throw new NtpException(string.Format("Invalid NTP mode {0}.", (int) packet.Mode));
      }
      return packet;
    }

    private void Send(Socket s)
    {
      var packet = new NtpPacket(NtpMode.Client, _versionNumber, NtpTimestamp.Now);
      s.SendTo(packet.Data, _remoteEndPoint);
    }

    private static IPEndPoint ToEndPoint(IPHostEntry hostEntry, int port)
    {
      for (var i = 0; i < hostEntry.AddressList.Length; i++) {
        var address = hostEntry.AddressList[i];
        if (address.AddressFamily == AddressFamily.InterNetwork)
          return new IPEndPoint(address, port);
      }
      if (hostEntry.AddressList[0].AddressFamily != AddressFamily.InterNetworkV6) {
        throw new InvalidOperationException("Unsupported address family.");
      }
      return new IPEndPoint(hostEntry.AddressList[0], port);
    }

    private static IPEndPoint ToEndPoint(string host, int port)
    {
      if (host.IndexOf(":") >= 0) {
        IPAddress address;
        if (!IPAddress.TryParse(host, out address))
          return null;
        return address.AddressFamily != AddressFamily.InterNetworkV6 ? null : new IPEndPoint(address, port);
      }
      var match = new Regex(@"^\s*([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\s*$").Match(host);
      if (!match.Success || (match.Groups.Count != 5)) {
        return null;
      }
      uint ip = 0;
      for (var i = 0; i < 4; i++) {
        var tmp = uint.Parse(match.Groups[i + 1].Value);
        if (tmp > 0xff) {
          return null;
        }
        ip |= tmp << (8*i);
      }
      return new IPEndPoint(ip, port);
    }
  }
}