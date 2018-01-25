using System;

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
    public int Stratum => Packet.Stratum;

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

    public void Validate(int maxStratum = 15, TimeSpan? maxPollInterval = null, TimeSpan? maxDispersion = null)
    {
      maxPollInterval = maxPollInterval ?? TimeSpan.FromHours(36);
      maxDispersion = maxDispersion ?? TimeSpan.FromSeconds(16);

      if (Packet.Mode != NtpMode.Server && Packet.Mode != NtpMode.Broadcast)
        throw new NtpException("Invalid mode");

      if (Packet.LeapIndicator == NtpLeapIndicator.AlarmCondition)
        throw new NtpException("Invalid leap indicator");

      if (Packet.Stratum < 1 || Packet.Stratum > maxStratum)
        throw new NtpException("Invalid stratum");

      TimeSpan freshness = (Packet.TransmitTimestamp - Packet.ReferenceTimestamp).ToTimeSpan();

      if (freshness > maxPollInterval)
        throw new NtpException("Server clock not fresh");

      var lambda = Packet.RootDelay / 2 + Packet.RootDispersion;

      if (lambda > maxDispersion.Value.TotalMilliseconds)
        throw new NtpException("Invalid dispersion");

      if (Packet.TransmitTimestamp < Packet.ReferenceTimestamp)
        throw new NtpException("Invalid time");
    }
  }
}