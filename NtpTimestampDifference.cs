using System;
using System.Runtime.InteropServices;

namespace Daemaged.NTP
{
  /// <summary>
  /// The difference between two <see cref="T:Daemaged.NTP.NtpTimestamp" /> values.
  /// This is a value type.
  /// </summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct NtpTimestampDifference : IComparable
  {
    private readonly long _difference;

    internal NtpTimestampDifference(long d)
    {
      _difference = d;
    }

    /// <summary>
    /// Compares this instance to a specified object and returns an indication of their relative values.
    /// </summary>
    /// <param name="value">An object to compare, or null.</param>
    /// <returns>A 32-bit signed integer that indicates the relative order of the objects being compared. See <see cref="M:System.IComparable.CompareTo(System.Object)" /> for details.</returns>
    public int CompareTo(object value)
    {
      if (value is NtpTimestamp) {
        return _difference.CompareTo(((NtpTimestampDifference) value)._difference);
      }
      return _difference.CompareTo(value);
    }

    /// <summary>
    /// Indicates whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">An object to compare with this instance.</param>
    /// <returns>true if <c>obj</c> is a NtpTimestampDifference that represents the same
    /// time difference as this instance; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
      return ((obj is NtpTimestampDifference) && _difference.Equals(((NtpTimestampDifference) obj)._difference));
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>The 32 least significant bits of the time difference.</returns>
    public override int GetHashCode()
    {
      return (int) (_difference & 0x7fffffffL);
    }

    /// <summary>
    /// Accessor for the sign of the time difference.
    /// </summary>
    /// <value>True for positive differences and zero; false for negative
    /// differences.</value>
    public bool Positive { get { return (_difference >= 0L); } }

    internal long ToInt64()
    { return _difference; }

    internal long ToTicks()
    {
      ulong unixDiff;
      if (_difference >= 0L) {
        unixDiff = (ulong) (_difference >> 32);
      }
      else
        unixDiff = (ulong) (-_difference >> 32);
      var fraction = GetFraction();
      var ticks = (long)(unixDiff * (NtpTimestamp.TickToSecScale));
      ticks += (long)((fraction * NtpTimestamp.TickToSecScale) >> 32);
      if (_difference >= 0L) {
        return ticks;
      }
      return -ticks;
    }

    /// <summary>
    /// Gets the <see cref="T:System.TimeSpan" /> instance that represents the time difference.
    /// </summary>
    /// <value>A <see cref="T:System.TimeSpan" /> instance.</value>
    public TimeSpan ToTimeSpan()
    { return new TimeSpan(ToTicks()); }

    /// <summary>
    /// Converts the value of this instance to its string representation.
    /// </summary>
    /// <returns>String representation of time difference.</returns>
    public override string ToString()
    { return ToTimeSpan().ToString(); }

    /// <summary>
    /// Gets the value of this instance expressed in whole and fractional seconds.
    /// </summary>
    /// <remarks>The value of this instance expressed in whole and fractional seconds.</remarks>
    public double TotalSeconds { get { return ToTimeSpan().TotalSeconds; } }

    /// <summary>
    /// The sub-second part of the time difference, on microsecond scale.
    /// </summary>
    /// <value>A number between -10^6+1 and 10^6-1.</value>
    public int Microsecond {
      get {
        var scaledFraction = GetScaledFraction(0xf4240L);
        if (_difference >= 0L)
          return scaledFraction;
        return -scaledFraction;
      }
    }

    private ulong GetFraction()
    {
      ulong diff;
      if (_difference >= 0L)
        diff = (ulong) _difference;
      else
        diff = (ulong) -_difference;
      return (diff & 0xffffffffL);
    }

    private int GetScaledFraction(ulong scale)
    {
      return (int) (GetFraction()*scale >> 32);
    }

    /// <summary>
    /// Divides the time difference by 2.
    /// </summary>
    /// <returns>A time difference with the same sign as this instance and an absolute
    /// value one half of the absolute value of this instance.</returns>
    internal NtpTimestampDifference Halve()
    { return new NtpTimestampDifference(_difference/2L); }

    /// <summary>
    /// Changes the sign of the time difference (positive to negative and vice-versa).
    /// </summary>
    /// <returns>A time difference with the same absolute
    /// value as this instance and an opposite sign.</returns>
    public NtpTimestampDifference Negate()
    { return new NtpTimestampDifference(-_difference); }

    /// <summary>
    /// Compare two instances of NtpTimestampDifference.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <returns>True if equal; false if not equal.</returns>
    public static bool operator ==(NtpTimestampDifference a, NtpTimestampDifference b)
    { return a.Equals(b); }

    /// <summary>
    /// Compare two instances of NtpTimestampDifference.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <returns>Flase if equal; true if not equal.</returns>
    public static bool operator !=(NtpTimestampDifference a, NtpTimestampDifference b)
    { return !a.Equals(b); }

    /// <summary>
    /// Indicates whether a specified NtpTimestampDifference is less than
    /// another specified NtpTimestampDifference.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <returns>true if the value of <c>a</c> is less than
    /// the value of <c>b</c>; otherwise false.</returns>
    public static bool operator <(NtpTimestampDifference a, NtpTimestampDifference b)
    { return (a._difference < b._difference); }

    /// <summary>
    /// Indicates whether a specified NtpTimestampDifference is greater than
    /// another specified NtpTimestampDifference.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <returns>true if the value of <c>a</c> is greater than
    /// the value of <c>b</c>; otherwise false.</returns>
    public static bool operator >(NtpTimestampDifference a, NtpTimestampDifference b)
    { return (a._difference > b._difference); }

    /// <summary>
    /// Indicates whether a specified NtpTimestampDifference is less than
    /// or equal to another specified NtpTimestampDifference.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <returns>true if the value of <c>a</c> is less than
    /// or equal to the value of <c>b</c>; otherwise false.</returns>
    public static bool operator <=(NtpTimestampDifference a, NtpTimestampDifference b)
    { return (a._difference <= b._difference); }

    /// <summary>
    /// Indicates whether a specified NtpTimestampDifference is greater than
    /// or equal to another specified NtpTimestampDifference.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <returns>true if the value of <c>a</c> is greater than
    /// or equal to the value of <c>b</c>; otherwise false.</returns>
    public static bool operator >=(NtpTimestampDifference a, NtpTimestampDifference b)
    { return (a._difference >= b._difference); }

    /// <summary>
    /// Adds a time difference to a timestamp.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <returns>A timestamp plus a time difference.</returns>
    public static NtpTimestampDifference operator +(NtpTimestampDifference a, NtpTimestampDifference b)
    { return a.Add(b); }

    /// <summary>
    /// Subtracts a time difference from a timestamp.
    /// </summary>
    /// <param name="a">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <param name="b">An <see cref="T:Daemaged.NTP.NtpTimestampDifference" />.</param>
    /// <returns>A timestamp minus a time difference.</returns>
    public static NtpTimestampDifference operator -(NtpTimestampDifference a, NtpTimestampDifference b)
    { return a.Subtract(b); }

    /// <summary>
    /// Adds the specified time difference to this instance.
    /// </summary>
    /// <param name="value">A time difference to add.</param>
    /// <returns>A time difference plus a time difference.</returns>
    public NtpTimestampDifference Add(NtpTimestampDifference value)
    {
      NtpTimestampDifference difference;
      try {
        difference = new NtpTimestampDifference(_difference + value._difference);
      }
      catch (OverflowException) {
        throw new OverflowException(string.Format("Cannot add {1} to {0}.", this, value));
      }
      return difference;
    }

    /// <summary>
    /// Subtracts the specified time difference from this instance.
    /// </summary>
    /// <param name="value">A time difference to subtract.</param>
    /// <returns>A time difference plus a time difference.</returns>
    public NtpTimestampDifference Subtract(NtpTimestampDifference value)
    {
      NtpTimestampDifference diff;
      try {
        diff = new NtpTimestampDifference(_difference - value._difference);
      }
      catch (OverflowException) {
        throw new OverflowException(string.Format("Cannot subtract {1} from {0}.", this, value));
      }
      return diff;
    }
  }
}