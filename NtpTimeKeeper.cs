using System;
using System.Threading;

namespace Daemaged.NTP
{
  /// <summary>
  /// Utility class that spins up a background thread to keep the NTP time offset updated
  /// </summary>
  public static class NtpTimeKeeper
  {
    private static int _sleepPeriod;
    private static Thread _keeperThread;   
    private static long _timeDiff;
    private static int _stratum;    
    private static DateTime _lastTransmit;
    private static Ntp _ntp;
    private static readonly ManualResetEvent _stopEvent;


    static NtpTimeKeeper()
    {
      _stopEvent = new ManualResetEvent(false);
    }

    /// <summary>
    /// Start collecting NTP time differences
    /// </summary>
    /// <param name="ntpHost">the hostname for the NTP server</param>
    /// <param name="everySeconds">update the time offset every N seconds</param>
    public static bool Start(string ntpHost, int everySeconds = 60)
    {
      Stop();
      _sleepPeriod = everySeconds * 1000;      

      _ntp = new Ntp(ntpHost) { VersionNumber = 4 };

      // Do it once to kick-start everything
      try {
        Keep();
      }
      catch (Exception e) {
        return false;
      }

      _stopEvent.Reset();
      
      // Assuming this worked at least once, lets do it again...
      _keeperThread = new Thread(KeepNtpTime) {
        IsBackground = true,
        Name = "NtpTimeKeeper"
      };
      _keeperThread.Start();
      return true;
    }

    /// <summary>
    /// Stop collecting time offsets
    /// </summary>
    public static void Stop()
    {
      _stopEvent.Set();
      if (_keeperThread == null)
        return;
      _keeperThread.Join();
      _keeperThread = null;
    }

    private static void KeepNtpTime()
    {
      // Keep waiting until the even is actually signal, then just GTFO
      while (!_stopEvent.WaitOne(_sleepPeriod)) {
        try {
          Keep();
        }
        catch {
          if (NtpError != null)
            NtpError(null, null);
        }
      }
    }

    private static void Keep() 
    {
      var resp = _ntp.GetTime();
      _timeDiff = resp.TimeOffset.ToTicks();
      _lastTransmit = resp.Packet.TransmitTimestamp.ToUniversalTime();
      _stratum = resp.Stratum;
    }

    public static EventHandler NtpError;

    /// <summary>
    /// UtcNow  adjusted to the latest recovered time offset in ticks
    /// </summary>
    public static long AdjustedUtcNowTicks { get { return DateTime.UtcNow.Ticks + _timeDiff; } }
    /// <summary>
    /// UtcNow adjusted to the latest recovered time offset
    /// </summary>
    public static DateTime AdjustedUtcNow { get { return DateTime.UtcNow.AddTicks(_timeDiff); } }
    /// <summary>
    /// The latest recovered time offset in ticks
    /// </summary>
    public static long LastRecordedDiffTime { get { return _timeDiff; } }
    /// <summary>
    /// The NTP server's Stratum value
    /// </summary>
    public static int Stratum { get { return _stratum; } } 
    /// <summary>
    /// The time-stamp when the last successful transition from the server was made
    /// </summary>
    public static DateTime LastTransmitTime { get { return _lastTransmit; } }
  }
}
