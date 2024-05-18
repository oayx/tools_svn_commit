using System;
using System.IO;

namespace SVNTools
{
	public enum LogLv
	{
		Debug = 0,
		Info = 1,
		Warning = 2,
		Error = 3,
		Max
	}
	public static class Log
	{
#if DEBUG
		private static LogLv _logLv = LogLv.Debug;
#else
		private static LogLv _logLv = LogLv.Info;
#endif
		private static StreamWriter _logWriter;

		public static void SetLogLv(LogLv lv)
		{
			_logLv = lv;
		}

		public static void Debug(string message)
		{
			if (_logLv > LogLv.Debug)
				return;
			Console.WriteLine(message);
			Log2File(message);
		}
		public static void Info(string message)
		{
			if (_logLv > LogLv.Info)
				return;
			message = $"{System.DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")} {message}";
			Console.WriteLine(message);
			Log2File(message);
		}
		public static void Waring(string message)
		{
			if (_logLv > LogLv.Warning)
				return;
			message = $"{System.DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")} {message}";
			Console.WriteLine(message);
			Log2File(message);
		}
		public static void Error(string message)
		{
			if (_logLv > LogLv.Error)
				return;
			message = $"{System.DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")} {message}";
			Console.WriteLine(message);
			Log2File(message);
		}

		public static bool EnableLog2File(bool b = true)
		{
			Console.WriteLine($"enable write log to file:{b}");
			if(b)
			{
				if (_logWriter != null)
					return false;
				try
				{
					if (!Directory.Exists("svnlog"))
						Directory.CreateDirectory("svnlog");

					_logWriter = new StreamWriter(File.Open($"svnlog/{System.DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")}.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
					return true;
				}
				catch (System.Exception ex)
				{
					_logWriter = null;
					Console.WriteLine(ex.ToString());
					return false;
				}
			}
			else
			{
				Close();
				return true;
			}
		}
		private static int _cacheCount = 0;
		public static void Log2File(string message)
		{
			if(_logWriter != null)
			{
				_logWriter.WriteLine(message);

				_cacheCount++;
				if(_cacheCount > 10)
				{
					_cacheCount = 0;
					_logWriter.Flush();
				}
			}
		}

		public static void Close()
		{
			if(_logWriter != null)
			{
				Console.WriteLine("request close log file handle");
				try
				{
					_logWriter.Close();
					_logWriter.Dispose();
				}
				catch (System.Exception ex)
				{
					_logWriter = null;
					Console.WriteLine("close log file handle error:" + ex.Message);
				}
				_logWriter = null;
			}
		}
	}
}
