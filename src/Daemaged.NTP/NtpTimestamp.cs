using System;
using System.Runtime.InteropServices;

namespace Daemaged.NTP
{

  /// <summary>
  /// NTP timestamp implementation for <see cref="T:Daemaged.NTP.Ntp" />.
  /// This is a value type.
  /// </summary>
  /// <remarks>See RFC 1305 for the definition of the timestamp format.
  /// Note that it will stop working (overflow) in 2036 - see RFC 2030
  /// for a proposed solution.</remarks>
  [StructLayout(LayoutKind.Sequential)]
  public struct NtpTimestamp : IComparable
  {
    /// <summary>
    /// <code>new DateTime(1900, 1, 1, 0, 0, 0).ToUniversalTime().Ticks</code>
    /// plus a magical value, because the expression is off by 1 hour (perhaps
    /// due to daylight savings offset).
    /// </summary>
    const long EPOCH_TICKS = 599266080000000000;
    internal const long TickToSecScale = 10000000;
    /// <summary>
    /// Seconds from the NTP Epoch (00:00:00 on 1 January 1900) to the Unix
    /// Epoch (00:00:00 UTC on 1 January 1970)
    /// </summary>
    const long NTP2_UNIX_EPOCH_SECONDS = 0x83aa7e80L;

    static readonly Random randomGenerator;
    ulong _timestamp;
    /// <summary>
    /// Constructs the timestamp from .NET time.
    /// </summary>
    /// <param name="dt">The local time.</param>
    public NtpTimestamp(DateTime dt)
    {
      var unixTicks = dt.ToUniversalTime().Ticks - EPOCH_TICKS;
      if (unixTicks < 0L)
        throw new NtpException("Date " + dt + " is before epoch.");

      var ntpSecs = (ulong)(unixTicks / TickToSecScale);
      var netSecsRem = (ulong)(unixTicks % TickToSecScale);
      var ntpFrac = (netSecsRem << 32) / TickToSecScale;
      _timestamp = (ntpSecs << 32) | ntpFrac;
    }

    /// <summary>
    /// Gets the current system time.
    /// </summary>
    /// <value>Current system time.</value>
    /// <remarks>
    /// The precision of <see cref="T:Daemaged.NTP.NtpTimestamp" /> is higher
    /// than the precision of <see cref="T:System.DateTime" />. As recommended
    /// by RFC 1305, the low-order bits of the new timestamp which
    /// cannot be initialized from DateTime are randomized.
    /// </remarks>
    public static NtpTimestamp Now
    {
      get {
        var timestamp = new NtpTimestamp(DateTime.Now);
        timestamp._timestamp ^= (ulong)(randomGenerator.Next() & 0xFF);
        return timestamp;
      }
    }
    /// <summary>
    /// Constructs the timestamp from the raw NTP format.
    /// </summary>
    /// <param name="data">The buffer containing the 8-byte timestamp.</param>
    /// <param name="index">The starting position of the timestamp in the buffer.</param>
    public NtpTimestamp(byte[] data, int index)
    {
      ulong ntpSecs = GetNumber(data, index);
      ulong ntpFrac = GetNumber(data, index + 4);
      _timestamp = (ntpSecs << 0x20) | ntpFrac;
    }

    NtpTimestamp(ulong t)
    {
      _timestamp = t;
    }

    /// <summary>
    /// Compares this instance to a specified object and returns an indication of their relative values.
    /// </summary>
    /// <param name="value">An object to compare, or null.</param>
    /// <returns>A 32-bit signed integer that indicates the relative order of the objects being compared. See <see cref="M:System.IComparable.CompareTo(System.Object)" /> for details.</returns>
    public int CompareTo(object value)
    {
      if (value is NtpTimestamp)
        return _timestamp.CompareTo(((NtpTimestamp)value)._timestamp);
      throw new ArgumentException($"must be of type {nameof(NtpTimestamp)}", nameof(value));
    }

    /// <summary>
    /// Indicates whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">An object to compare with this instance.</param>
    /// <returns>true if <c>obj</c> is a NtpTimestamp that represents the same
    /// time as this instance; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
      return ((obj is NtpTimestamp) && _timestamp.Equals(((NtpTimestamp)obj)._timestamp));
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>The 32 least significant bits of the timestamp.</returns>
    public override int GetHashCode()
    {
      return (int)(_timestamp & 0x7fffffffL);
    }

    /// <summary>
    /// Accessor for the timestamp in NTP format.
    /// </summary>
    /// <value>The 8-byte timestamp. Note that the array is not writeable -
    /// that is, it is possible to write into it, but that won't change
    /// the value of the NtpTimestamp instance.</value>
    public byte[] ToArray()
    {
      var buffer = new byte[8];
      var timestamp = _timestamp;
      for (var i = 7; i >= 0; i--)
      {
        var x = timestamp / 256;
        buffer[i] = (byte)(timestamp - (256 * x));
        timestamp = x;
      }
      return buffer;
    }

    /// <summary>
    /// Minimal validity check according to RFC 1305.
    /// </summary>
    /// <value>True for all timestamps except 0.</value>
    public bool IsValid => (_timestamp != 0L);

    /// <summary>
    /// Converts timestamp to tick count (without epoch).
    /// </summary>
    internal long ToTicks()
    {
      var ntpSecs = _timestamp >> 32;
      var fraction = GetFraction();
      var secstoTicks = (long)(ntpSecs * TickToSecScale);
      return (secstoTicks + ((long)((fraction * TickToSecScale) >> 0x20)));
    }

    ulong GetFraction() { return (_timestamp & 0xffffffffL); }

    /// <summary>
    /// Compare two instances of NtpTimestamp.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <returns>True if equal; false if not equal.</returns>
    public static bool operator ==(NtpTimestamp a, NtpTimestamp b)
    { return a.Equals(b); }

    /// <summary>
    /// Compare two instances of NtpTimestamp.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <returns>False if equal; true if not equal.</returns>
    public static bool operator !=(NtpTimestamp a, NtpTimestamp b)
    { return !a.Equals(b); }

    /// <summary>
    /// Indicates whether a specified NtpTimestamp is less than
    /// another specified NtpTimestamp.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <returns>true if the value of <c>a</c> is less than
    /// the value of <c>b</c>; otherwise false.</returns>
    public static bool operator <(NtpTimestamp a, NtpTimestamp b)
    { return (a._timestamp < b._timestamp); }

    /// <summary>
    /// Indicates whether a specified NtpTimestamp is greater than
    /// another specified NtpTimestamp.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <returns>true if the value of <c>a</c> is greater than
    /// the value of <c>b</c>; otherwise false.</returns>
    public static bool operator >(NtpTimestamp a, NtpTimestamp b)
    { return (a._timestamp > b._timestamp); }

    /// <summary>
    /// Indicates whether a specified NtpTimestamp is less than
    /// or equal to another specified NtpTimestamp.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <returns>true if the value of <c>a</c> is less than
    /// or equal to the value of <c>b</c>; otherwise false.</returns>
    public static bool operator <=(NtpTimestamp a, NtpTimestamp b)
    { return (a._timestamp <= b._timestamp); }

    /// <summary>
    /// Indicates whether a specified NtpTimestamp is greater than
    /// or equal to another specified NtpTimestamp.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <returns>true if the value of <c>a</c> is greater than
    /// or equal to the value of <c>b</c>; otherwise false.</returns>
    public static bool operator >=(NtpTimestamp a, NtpTimestamp b)
    { return (a._timestamp >= b._timestamp); }

    /// <summary>
    /// Adds a time difference to a timestamp.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <param name="b">A <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <returns>A timestamp plus a time difference.</returns>
    public static NtpTimestamp operator +(NtpTimestamp a, NtpTimestampDifference b)
    { return a.Add(b); }

    /// <summary>
    /// Subtracts a time difference from a timestamp.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestamp" />.</param>
    /// <param name="b">A <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <returns>A timestamp minus a time difference.</returns>
    public static NtpTimestamp operator -(NtpTimestamp a, NtpTimestampDifference b)
    { return a.Subtract(b); }

    /// <summary>
    /// Subtracts a timestamp from a timestamp, calculating the time difference between the timestamps.
    /// </summary>
    /// <param name="a">A timestamp.</param>
    /// <param name="b">A timestamp.</param>
    /// <returns>A time difference (positive or negative).</returns>
    public static NtpTimestampDifference operator -(NtpTimestamp a, NtpTimestamp b)
    { return a.Subtract(b); }

    /// <summary>
    /// Adds the specified time difference to this instance.
    /// </summary>
    /// <param name="value">A time difference.</param>
    /// <returns>A timestamp plus a time difference.</returns>
    public NtpTimestamp Add(NtpTimestampDifference value)
    {
      bool flag;
      var t = _timestamp + ((ulong)value.ToInt64());
      if (value.Positive)
        flag = t < _timestamp;
      else
        flag = t > _timestamp;
      if (flag)
        throw new OverflowException(string.Format("Cannot add {1} to {0}.", this, value));
      return new NtpTimestamp(t);
    }

    /// <summary>
    /// Subtracts the specified time difference from this instance.
    /// </summary>
    /// <param name="value">A time difference.</param>
    /// <returns>A timestamp plus a time difference.</returns>
    public NtpTimestamp Subtract(NtpTimestampDifference value)
    {
      bool flag;
      var t = _timestamp - ((ulong)value.ToInt64());
      if (value.Positive)
        flag = t > _timestamp;
      else
        flag = t < _timestamp;
      if (flag)
        throw new OverflowException(string.Format("Cannot subtract {1} from {0}.", this, value));
      return new NtpTimestamp(t);
    }

    /// <summary>
    /// Subtracts the specified timestamp from this instance, calculating the time difference between the two timestamps.
    /// </summary>
    /// <param name="value">A timestamp.</param>
    /// <returns>A time difference (positive or negative).</returns>
    public NtpTimestampDifference Subtract(NtpTimestamp value)
    {
      long d;

      if (_timestamp >= value._timestamp)
        d = (long) (_timestamp - value._timestamp);
      else
        d = -(long)(value._timestamp - _timestamp);
      return new NtpTimestampDifference(d);
    }

    /// <summary>
    /// Converts the value of this instance to string representation
    /// (analogically to DateTime.ToString).
    /// </summary>
    /// <returns>String representation of UTC time.</returns>
    public override string ToString()
    {
      return ToUniversalTime().ToString();
    }

    /// <summary>
    /// Accessor for the timestamp in .NET time format.
    /// Converts to local time.
    /// </summary>
    /// <value>The local time.</value>
    public DateTime ToLocalTime()
    {
      return ToUniversalTime().ToLocalTime();
    }

    /// <summary>
    /// Accessor for the timestamp in .NET time format.
    /// </summary>
    /// <value>The timestamp as UTC <see cref="T:System.DateTime">DateTime</see>.
    /// </value>
    public DateTime ToUniversalTime()
    {
      return new DateTime(EPOCH_TICKS + ToTicks());
    }

    /// <summary>
    /// Accessor for the timestamp in Unix time format.
    /// </summary>
    /// <remarks>Rounds down.</remarks>
    /// <value>
    /// The number of the whole seconds of the timestamp, counted from Unix
    /// Epoch (00:00:00 UTC, January 1, 1970).
    /// </value>
    public int ToUnixTime()
    {
      var unixTime = _timestamp >> 32;
      if (unixTime < NTP2_UNIX_EPOCH_SECONDS)
        throw new NtpException("Timestamp is before the Unix epoch.");
      return (int)(unixTime - NTP2_UNIX_EPOCH_SECONDS);
    }

    /// <summary>
    /// The sub-second part of the timestamp, on microsecond scale.
    /// </summary>
    /// <remarks>Rounds down.</remarks>
    /// <value>A number between 0 and 10^6-1.</value>
    public int Microsecond => GetScaledFraction(0xf4240L);

    int GetScaledFraction(ulong scale)
    {
      return (int)(GetFraction() * scale >> 32);
    }

    static uint GetNumber(byte[] data, int offset)
    {
      uint x = 0;
      for (var i = 0; i < 4; i++)
        x = (256 * x) + data[offset + i];
      return x;
    }

    static NtpTimestamp()
    { randomGenerator = new Random(); }
  }
}
