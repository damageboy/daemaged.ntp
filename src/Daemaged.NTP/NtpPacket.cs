using System;

namespace Daemaged.NTP
{
  /// <summary>
  /// Represents a NTP packet.
  /// </summary>
  /// <remarks>
  /// See RFC 1305 for the packet specification. Note that
  /// this implementation does not use any optional fields.
  /// </remarks>
  public class NtpPacket
  {
    readonly byte[] _data;
    NtpTimestamp _destinationTimestamp;
    const int INDEX_BITFIELD = 0;
    const int INDEX_ORIGINATE_TIMESTAMP = 0x18;
    const int INDEX_POLL = 2;
    const int INDEX_PRECISION = 3;
    const int INDEX_RECEIVE_TIMESTAMP = 32;
    const int INDEX_REFERENCE_IDENTIFIER = 12;
    const int INDEX_REFERENCE_TIMESTAMP = 16;
    const int INDEX_ROOT_DELAY = 4;
    const int INDEX_ROOT_DISPERSION = 8;
    const int INDEX_STRATUM = 1;
    const int INDEX_TRANSMIT_TIMESTAMP = 40;
    const int LENGTH_LI = 2;
    const int LENGTH_MODE = 3;
    const int LENGTH_VN = 3;
    const int OFFSET_LI = 6;
    const int OFFSET_MODE = 0;
    const int OFFSET_VN = 3;

    /// <summary>
    /// Minimal size of an NTP packet (they can be larger,
    /// but the optional fields are at the end and this
    /// class ignores them).
    /// </summary>
    internal const int MinPacketSize = 0x30;

    /// <summary>
    /// Constructs an empty (i.e. with all bits set to 0) packet.
    /// </summary>
    internal NtpPacket()
    {
      _data = new byte[MinPacketSize];
      _destinationTimestamp = NtpTimestamp.Now;
    }

    /// <summary>
    /// Constructs a client packet.
    /// </summary>
    internal NtpPacket(NtpMode mode, byte versionNumber, NtpTimestamp transmitTimestamp)
      : this()
    {
      _data[0] = SetBits(_data[0], OFFSET_VN, LENGTH_MODE, versionNumber);
      _data[0] = SetBits(_data[0], INDEX_BITFIELD, LENGTH_MODE, (byte)mode);
      CopyTimestampToData(transmitTimestamp, INDEX_TRANSMIT_TIMESTAMP);
    }

    void CheckPlacement(int offset, int length)
    {
      if (offset < 0)
        throw new ArgumentException($"Invalid offset {offset} - cannot be negative.");
      if (length <= 0)
        throw new ArgumentException($"Invalid length {length} - must be positive.");
      if ((offset + length) > 8)
        throw new ArgumentException("Invalid offset/length.");
    }

    void CopyTimestampToData(NtpTimestamp ts, int index)
    {
      UpdateData(ts.ToArray(), index, 8);
    }

    byte[] DuplicateData(int index, int length)
    {
      var destinationArray = new byte[length];
      Array.Copy(_data, index, destinationArray, 0, length);
      return destinationArray;
    }

    byte GetBits(byte field, int offset, int length)
    {
      CheckPlacement(offset, length);
      return (byte)((field >> offset) & GetMask(length));
    }

    int GetMask(int length)
    {
      return ((1 << length) - 1);
    }

    /// <summary>
    /// Reference Identifier.
    /// </summary>
    /// <value>4 bytes - see RFC 2030 for their interpretation.</value>
    public byte[] GetReferenceIdentifier()
    { return DuplicateData(INDEX_REFERENCE_IDENTIFIER, 4); }

    /// <summary>
    /// Root Delay.
    /// </summary>
    /// <value>4 bytes - see RFC 2030 for their interpretation.</value>
    public byte[] GetRootDelay()
    { return DuplicateData(INDEX_ROOT_DELAY, 4); }

    /// <summary>
    /// Root Dispersion.
    /// </summary>
    /// <value>4 bytes - see RFC 2030 for their interpretation.</value>
    public byte[] GetRootDispersion()
    { return DuplicateData(INDEX_ROOT_DISPERSION, 4); }

    byte SetBits(byte field, int offset, int length, byte v)
    {
      CheckPlacement(offset, length);
      var mask = GetMask(length);
      if (v > mask)
        throw new ArgumentException($"Value {v} won't fit into {length} bits - maximum is {mask}.", "v");
      mask = mask << offset;
      return (byte)((field & ~mask) | (v << offset));
    }

    internal void SetDestinationTimestamp(NtpTimestamp timestamp)
    { _destinationTimestamp = timestamp; }

    /// <summary>
    /// Accessor for the raw packet _data.
    /// </summary>
    /// <value>The array size is <see cref="F:Daemaged.NTP.NtpPacket.MinPacketSize" /> bytes.</value>
    public byte[] ToArray()
    {
      return (byte[])_data.Clone();
    }

    void UpdateData(byte[] d, int index, int length)
    {
      if (d.Length != length)
        throw new ArgumentException($"Data length ({d.Length}) must be equal to the length parameter value ({length}).");
      d.CopyTo(_data, index);
    }

    internal byte[] Data => _data;

    /// <summary>
    /// Destination Timestamp: the time at which the reply arrived at the client.
    /// </summary>
    /// <value>Timestamp in <see cref="T:Daemaged.NTP.NtpTimestamp">NTP format</see>.</value>
    public NtpTimestamp DestinationTimestamp => _destinationTimestamp;

    /// <summary>
    /// Returns the leap second indicator.
    /// </summary>
    /// <value>Leap indicator.</value>
    public NtpLeapIndicator LeapIndicator {
      get {
        switch (GetBits(_data[0], OFFSET_LI, LENGTH_LI))
        {
          case 0: return NtpLeapIndicator.NoLeap;
          case 1: return NtpLeapIndicator.OneSecondMore;
          case 2: return NtpLeapIndicator.OneSecondLess;
        }
        return NtpLeapIndicator.AlarmCondition;
      }
    }

    /// <summary>
    /// NTP packet mode.
    /// </summary>
    /// <value>3 bits. Possible values are described in <see cref="T:Daemaged.NTP.NtpMode" />.</value>
    public NtpMode Mode => (NtpMode)GetBits(_data[0], OFFSET_MODE, LENGTH_MODE);

    /// <summary>
    /// Originate Timestamp: the time at which the request departed the client for the server.
    /// </summary>
    /// <value>Timestamp in <see cref="T:Daemaged.NTP.NtpTimestamp">NTP format</see>.</value>
    public NtpTimestamp OriginateTimestamp => new NtpTimestamp(_data, INDEX_ORIGINATE_TIMESTAMP);

    /// <summary>
    /// Poll Interval, in seconds to the nearest power of two.
    /// </summary>
    /// <value>Maximum range is 4 to 14, normal range 6 to 10.</value>
    public int Poll => _data[INDEX_POLL];

    /// <summary>
    /// Precision of the packet originator's clock, in seconds to
    /// the nearest power of two.
    /// </summary>
    /// <value>Normal range is -6 to -20.</value>
    public int Precision => _data[INDEX_PRECISION];

    /// <summary>
    /// Receive Timestamp: the time at which the request arrived at the server.
    /// </summary>
    /// <value>Timestamp in <see cref="T:Daemaged.NTP.NtpTimestamp">NTP format</see>.</value>
    public NtpTimestamp ReceiveTimestamp => new NtpTimestamp(_data, INDEX_RECEIVE_TIMESTAMP);

    /// <summary>
    /// Reference Timestamp: the time at which the local clock was last
    /// set or corrected.
    /// </summary>
    /// <value>Timestamp in <see cref="T:Daemaged.NTP.NtpTimestamp">NTP format</see>.</value>
    public NtpTimestamp ReferenceTimestamp => new NtpTimestamp(_data, INDEX_REFERENCE_TIMESTAMP);

    /// <summary>
    /// Stratum level (time quality) of the packet originator's clock.
    /// </summary>
    /// <value>0 is unspecified or undefined, 1 primary (for Internet
    /// NTP servers that generally means an atomic clock, within Windows
    /// local networks it's the best time source in the network), higher
    /// numbers mean increasing distance from the primary source.</value>
    public int Stratum => _data[INDEX_STRATUM];

    /// <summary>
    /// Transmit Timestamp: the time at which the reply departed the server for the client.
    /// </summary>
    /// <value>Timestamp in <see cref="T:Daemaged.NTP.NtpTimestamp">NTP format</see>.</value>
    public NtpTimestamp TransmitTimestamp => new NtpTimestamp(_data, INDEX_TRANSMIT_TIMESTAMP);

    /// <summary>
    /// RootDelay: the server's estimated aggregate round-trip-time delay to 
    /// the stratum 1 server.
    /// </summary>
    /// <value>Duration in milliseconds.</value>
    public double RootDelay {
      get {
        byte[] rootDelayBuffer = GetRootDelay();

        return 1000 * ((double)(256 * (256 * (256 * rootDelayBuffer[0] + rootDelayBuffer[1]) + rootDelayBuffer[2]) + rootDelayBuffer[3]) / 0x10000);
      }
    }

    /// <summary>
    /// RootDispersion: the server's estimated maximum measurement error relative to 
    /// the stratum 1 server.
    /// </summary>
    /// <value>Duration in milliseconds.</value>
    public double RootDispersion {
      get {
        byte[] rootDispersionBuffer = GetRootDispersion();

        return 1000 * ((double)(256 * (256 * (256 * rootDispersionBuffer[0] + rootDispersionBuffer[1]) + rootDispersionBuffer[2]) + rootDispersionBuffer[3]) / 0x10000);
      }
    }
  }
}