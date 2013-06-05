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
    private readonly byte[] _data;
    private NtpTimestamp _destinationTimestamp;
    private const int INDEX_BITFIELD = 0;
    private const int INDEX_ORIGINATE_TIMESTAMP = 0x18;
    private const int INDEX_POLL = 2;
    private const int INDEX_PRECISION = 3;
    private const int INDEX_RECEIVE_TIMESTAMP = 32;
    private const int INDEX_REFERENCE_IDENTIFIER = 12;
    private const int INDEX_REFERENCE_TIMESTAMP = 16;
    private const int INDEX_ROOT_DELAY = 4;
    private const int INDEX_ROOT_DISPERSION = 8;
    private const int INDEX_STRATUM = 1;
    private const int INDEX_TRANSMIT_TIMESTAMP = 40;
    private const int LENGTH_LI = 2;
    private const int LENGTH_MODE = 3;
    private const int LENGTH_VN = 3;
    private const int OFFSET_LI = 6;
    private const int OFFSET_MODE = 0;
    private const int OFFSET_VN = 3;
    internal const int MinPacketSize = 0x30;
    /// <summary>
    /// Minimal size of an NTP packet (they can be larger,
    /// but the optional fields are at the end and this
    /// class ignores them).
    /// </summary>
    


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

    private void CheckPlacement(int offset, int length)
    {
      if (offset < 0)
        throw new ArgumentException(string.Format("Invalid offset {0} - cannot be negative.", offset));
      if (length <= 0)
        throw new ArgumentException(string.Format("Invalid length {0} - must be positive.", length));
      if ((offset + length) > 8)
        throw new ArgumentException("Invalid offset/length.");
    }

    private void CopyTimestampToData(NtpTimestamp ts, int index)
    {
      UpdateData(ts.ToArray(), index, 8);
    }

    private byte[] DuplicateData(int index, int length)
    {
      var destinationArray = new byte[length];
      Array.Copy(_data, index, destinationArray, 0, length);
      return destinationArray;
    }

    private byte GetBits(byte field, int offset, int length)
    {
      CheckPlacement(offset, length);
      return (byte)((field >> offset) & GetMask(length));
    }

    private int GetMask(int length)
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

    private byte SetBits(byte field, int offset, int length, byte v)
    {
      CheckPlacement(offset, length);
      var mask = GetMask(length);
      if (v > mask)
        throw new ArgumentException(string.Format("Value {0} won't fit into {1} bits - maximum is {2}.", v, length, mask), "v");
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

    private void UpdateData(byte[] d, int index, int length)
    {
      if (d.Length != length)
        throw new ArgumentException(string.Format("Data length ({0}) must be equal to the length parameter value ({1}).", d.Length, length));
      d.CopyTo(_data, index);
    }

    internal byte[] Data
    { get { return _data; } }

    /// <summary>
    /// Destination Timestamp: the time at which the reply arrived at the client.
    /// </summary>
    /// <value>Timestamp in <see cref="T:Daemaged.NTP.NtpTimestamp">NTP format</see>.</value>
    public NtpTimestamp DestinationTimestamp { get { return _destinationTimestamp; } }

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
    public NtpMode Mode { get { return (NtpMode)GetBits(_data[0], OFFSET_MODE, LENGTH_MODE); } }

    /// <summary>
    /// Originate Timestamp: the time at which the request departed the client for the server.
    /// </summary>
    /// <value>Timestamp in <see cref="T:Daemaged.NTP.NtpTimestamp">NTP format</see>.</value>
    public NtpTimestamp OriginateTimestamp { get { return new NtpTimestamp(_data, INDEX_ORIGINATE_TIMESTAMP); } }

    /// <summary>
    /// Poll Interval, in seconds to the nearest power of two.
    /// </summary>
    /// <value>Maximum range is 4 to 14, normal range 6 to 10.</value>
    public int Poll { get { return _data[INDEX_POLL]; } }

    /// <summary>
    /// Precision of the packet originator's clock, in seconds to
    /// the nearest power of two.
    /// </summary>
    /// <value>Normal range is -6 to -20.</value>
    public int Precision { get { return _data[INDEX_PRECISION]; } }

    /// <summary>
    /// Receive Timestamp: the time at which the request arrived at the server.
    /// </summary>
    /// <value>Timestamp in <see cref="T:Daemaged.NTP.NtpTimestamp">NTP format</see>.</value>
    public NtpTimestamp ReceiveTimestamp { get { return new NtpTimestamp(_data, INDEX_RECEIVE_TIMESTAMP); } }

    /// <summary>
    /// Reference Timestamp: the time at which the local clock was last
    /// set or corrected.
    /// </summary>
    /// <value>Timestamp in <see cref="T:Daemaged.NTP.NtpTimestamp">NTP format</see>.</value>
    public NtpTimestamp ReferenceTimestamp { get { return new NtpTimestamp(_data, INDEX_REFERENCE_TIMESTAMP); } }

    /// <summary>
    /// Stratum level (time quality) of the packet originator's clock.
    /// </summary>
    /// <value>0 is unspecified or undefined, 1 primary (for Internet
    /// NTP servers that generally means an atomic clock, within Windows
    /// local networks it's the best time source in the network), higher
    /// numbers mean increasing distance from the primary source.</value>
    public int Stratum { get { return _data[INDEX_STRATUM]; } }

    /// <summary>
    /// Transmit Timestamp: the time at which the reply departed the server for the client.
    /// </summary>
    /// <value>Timestamp in <see cref="T:Daemaged.NTP.NtpTimestamp">NTP format</see>.</value>
    public NtpTimestamp TransmitTimestamp { get { return new NtpTimestamp(_data, INDEX_TRANSMIT_TIMESTAMP); } }

    /// <summary>
    /// NTP/SNTP protocol version number.
    /// </summary>
    /// <value>Protocol version number (NTP is 3, SNTP is 4).</value>
    public int VersionNumber { get { return GetBits(_data[0], OFFSET_VN, LENGTH_VN); } }
  }
}