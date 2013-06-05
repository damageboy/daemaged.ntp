namespace Daemaged.NTP
{
  /// <summary>
  /// Represents a NTP response.
  /// </summary>
  public class NtpResponse
  {
    internal NtpResponse(NtpPacket packet) { Packet = packet; }

    /// <summary>
    /// Gets the NTP packet with additional information. For advanced users only.
    /// </summary>
    /// <value>NTP packet.</value>
    public NtpPacket Packet { get; private set; }

    /// <summary>
    /// Stratum level (time quality) of the packet originator's clock.
    /// </summary>
    /// <value>0 is unspecified or undefined, 1 primary (for Internet
    /// NTP servers that generally means an atomic clock, within Windows
    /// local networks it is the best time source in the network), higher
    /// numbers mean increasing distance from the primary source.</value>
    public int Stratum { get { return Packet.Stratum; } }

    /// <summary>
    /// Gets the difference between the server time and time on the local machine.
    /// </summary>
    /// <returns>The time difference which should be added to system time to get the corrected time.</returns>
    public NtpTimestampDifference TimeOffset
    {
      get {
        var difference = (Packet.ReceiveTimestamp - Packet.OriginateTimestamp) + (Packet.TransmitTimestamp - Packet.DestinationTimestamp);
        return difference.Halve();
      }
    }
  }
}