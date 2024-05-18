using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SVNTools
{
	class Program
	{
		/// <summary>
		/// 目录
		/// </summary>
		private static string TargetPath = "";
		/// <summary>
		/// 单次提交上限
		/// </summary>
		private static int TotalCommitCount = 4999;
		/// <summary>
		/// 提交描述
		/// </summary>
		private static string CommitMessage = "合并代码";

		static void Main(string[] args)
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			TargetPath = Directory.GetCurrentDirectory();

			//args = new string[]{"limit=100", "message=\"aabb\"", "logfile=1", "loglv=1" };
			//解析传入参数
			if (args.Length > 0)
			{
				ParseArgs(args);
			}

			//执行
			Exec();

			stopwatch.Stop();
			string totalTime = FormatElapsedTime(stopwatch.Elapsed);
			Log.Info($"*******************process done，totoal time({totalTime})*******************");
			Log.Close();

			Console.WriteLine("please press any key to finish!!!");
			Console.ReadKey();
		}

		private static void Exec()
		{
			//第一步：获得总目录文件数
			Log.Info("----------GetDirectoryFileCount----------");
			int totalCount = 0;
			GetDirectoryFileCount(TargetPath, true, ref totalCount);
			totalCount += 1;//本身目录也算上
			Log.Info($"total files count:{totalCount}");

			//第二步：执行svn add操作
			Log.Info("----------AddFiles----------");
			int succeedCount = 0;
			int handleCount = 0;
			{
				AddFiles(TargetPath, true, ref succeedCount, ref handleCount, in totalCount);
				Log.Info($"AddFiles succeedCount:{succeedCount}, totalCount:{totalCount}, remainCount:{totalCount - handleCount}");
			}
			succeedCount = 0;
			handleCount = 0;

			//第三步：提交目录
			Log.Info("----------CommitPaths----------");
			List<string> currFiles = new List<string>(TotalCommitCount);
			currFiles.Add(TargetPath);
			{//目录
				CommitPaths(TargetPath, currFiles, true, ref handleCount, in totalCount);
				//可能有剩余
				if (currFiles.Count > 0)
				{
					handleCount += currFiles.Count;
					Log.Info($"SvnCommitPaths count:{currFiles.Count}, handleCount:{handleCount}, totalCount:{totalCount}, remainCount:{totalCount - handleCount}");
					SVNUtils.SvnCommitPaths(currFiles.ToArray(), CommitMessage);
					currFiles.Clear();
				}
			}

			//第四步：提交文件
			Log.Info("----------CommitFiles----------");
			{//文件
				CommitFiles(TargetPath, currFiles, true, ref handleCount, in totalCount);
				//可能有剩余
				if (currFiles.Count > 0)
				{
					handleCount += currFiles.Count;
					Log.Info($"SvnCommitFiles count:{currFiles.Count}, handleCount:{handleCount}, totalCount:{totalCount}, remainCount:{totalCount - handleCount}");
					SVNUtils.SvnCommitFiles(currFiles.ToArray(), CommitMessage);
					currFiles.Clear();
				}
			}

			//第五步：提交最外层目录
			Log.Info("----------SvnCommitPathAndFiles----------");
			SVNUtils.SvnCommitPathAndFiles(TargetPath, CommitMessage);
		}

		/// <summary>
		/// 解析传入参数，如path="c:/aaa",limit=123,message="aabb",logfile=1,loglv=1
		/// path	目录路径
		/// limit	同时提交文件数量
		/// message 备注
		/// logfile 是否写入文件，0不写入，非0写入
		/// loglv	日志等级，可以是0，1，2，3，4；0所有日志激活，4所有日志关闭
		/// </summary>
		private static void ParseArgs(string[] args)
		{
			string pathPattern = @"path=(.*)";
			string limitPattern = @"limit=(\d+)";
			string messagePattern = @"message=(.*)";
			string logfilePattern = @"logfile=(\d+)";
			string loglvPattern = @"loglv=(\d+)";
			for (int i = 0; i < args.Length; ++i)
			{
				string arg = args[i];
				Log.Info($"input arg:{arg}");

				Match match = Regex.Match(arg, pathPattern);
				if (match.Success)
				{
					Log.Info($"match path:{arg}");
					TargetPath = match.Groups[1].Value;

					continue;
				}

				match = Regex.Match(arg, limitPattern);
				if (match.Success)
				{
					Log.Info($"match limit:{arg}");
					string limitValue = match.Groups[1].Value;
					if (int.TryParse(limitValue, out var result) && result > 0 && result < 10000)
						TotalCommitCount = result;
					else
						Log.Error($"input arg error:{arg}，valid range(0,10000)");

					continue;
				}

				match = Regex.Match(arg, messagePattern);
				if (match.Success)
				{
					Log.Info($"match message:{arg}");
					CommitMessage = match.Groups[1].Value;

					continue;
				}

				match = Regex.Match(arg, logfilePattern);
				if (match.Success)
				{
					Log.Info($"match logfile:{arg}");
					string v = match.Groups[1].Value;
					Log.EnableLog2File(v == "0" ? false : true);

					continue;
				}

				match = Regex.Match(arg, loglvPattern);
				if (match.Success)
				{
					Log.Info($"match loglv:{arg}");
					string v = match.Groups[1].Value;
					if (Enum.TryParse<LogLv>(v, out var logLv))
					{
						Log.SetLogLv(logLv);
					}

					continue;
				}
			}
		}

		/// <summary>
		/// 执行Add
		/// </summary>
		private static void AddFiles(string path, bool isTopDir, ref int succeedCount, ref int handleCount, in int totalCount)
		{
			handleCount++;
			if (SVNUtils.SvnAddFile(path))
			{
				succeedCount++;
				if (succeedCount % TotalCommitCount == 0)
					Log.Info($"AddFiles succeedCount:{succeedCount}, totalCount:{totalCount}, remainCount:{totalCount - handleCount}");
			}

			string[] subDirectories = Directory.GetDirectories(path);
			foreach (string directory in subDirectories)
			{
				string fileName = Path.GetFileName(directory);
				if (isTopDir && IsIgnoreFile(fileName))
					continue;

				//再添加子目录
				AddFiles(directory, false, ref succeedCount, ref handleCount, in totalCount);
			}

			//最后添加文件
			string[] fileEntries = Directory.GetFiles(path);
			foreach (string file in fileEntries)
			{
				string fileName = Path.GetFileName(file);
				if (isTopDir && IsIgnoreFile(fileName))
					continue;

				handleCount++;
				if (SVNUtils.SvnAddFile(file))
				{
					succeedCount++;
					if (succeedCount % TotalCommitCount == 0)
						Log.Info($"AddFiles succeedCount:{succeedCount}, totalCount:{totalCount}, remainCount:{totalCount - handleCount}");
				}
			}
		}

		/// <summary>
		/// 执行commit
		/// </summary>
		private static void CommitPaths(string path, List<string> currFiles, bool isTopDir, ref int handleCount, in int totalCount)
		{
			string[] subDirectories = Directory.GetDirectories(path);
			foreach (string directory in subDirectories)
			{
				string fileName = Path.GetFileName(directory);
				if (isTopDir && IsIgnoreFile(fileName))
					continue;

				//先添加目录
				currFiles.Add(directory);
				if (currFiles.Count >= TotalCommitCount)
				{
					handleCount += currFiles.Count;
					Log.Info($"SvnCommitPaths count:{currFiles.Count}, handleCount:{handleCount}, totalCount:{totalCount}, remainCount:{totalCount - handleCount}");
					SVNUtils.SvnCommitPaths(currFiles.ToArray(), CommitMessage);
					currFiles.Clear();
				}

				//添加子目录
				CommitPaths(directory, currFiles, false, ref handleCount, in totalCount);
			}
		}

		/// <summary>
		/// 执行commit
		/// </summary>
		private static void CommitFiles(string path, List<string> currFiles, bool isTopDir, ref int handleCount, in int totalCount)
		{
			string[] subDirectories = Directory.GetDirectories(path);
			foreach (string directory in subDirectories)
			{
				string fileName = Path.GetFileName(directory);
				if (isTopDir && IsIgnoreFile(fileName))
					continue;

				string[] fileEntries = Directory.GetFiles(directory);
				foreach (string file in fileEntries)
				{
					fileName = Path.GetFileName(file);
					if (isTopDir && IsIgnoreFile(fileName))
						continue;

					currFiles.Add(file);
					if (currFiles.Count >= TotalCommitCount)
					{
						handleCount += currFiles.Count;
						Log.Info($"SvnCommitFiles count:{currFiles.Count}, handleCount:{handleCount}, totalCount:{totalCount}, remainCount:{totalCount - handleCount}");
						SVNUtils.SvnCommitFiles(currFiles.ToArray(), CommitMessage);
						currFiles.Clear();
					}
				}

				//添加子目录
				CommitFiles(directory, currFiles, false, ref handleCount, in totalCount);
			}
		}

		/// <summary>
		/// 是否属于忽视文件
		/// </summary>
		/// <param name="fileName">文件名，包括扩展名</param>
		private static bool IsIgnoreFile(string fileName)
		{
			string[] ignoreFiles = { "svnlog", ".svn", ".vs" };
			if (ignoreFiles.Contains(fileName))
				return true;
			if (fileName.StartsWith("SVNTools") || fileName.StartsWith("SharpSvn") || fileName.StartsWith("SharpPlink"))
				return true;
			return false;
		}
		private static void GetDirectoryFileCount(string path, bool isTopDir, ref int filesCount)
		{
			try
			{
				// 遍历当前目录下的文件和文件夹
				foreach (string file in Directory.GetFiles(path))
				{
					var fileName = Path.GetFileName(file);
					if (isTopDir && IsIgnoreFile(fileName))
						continue;
					filesCount++;
				}
				// 递归遍历子目录
				foreach (string dir in Directory.GetDirectories(path))
				{
					var fileName = Path.GetFileName(dir);
					if (isTopDir && IsIgnoreFile(fileName))
						continue;

					filesCount++;
					GetDirectoryFileCount(dir, false, ref filesCount);
				}
			}
			catch (Exception ex)
			{
				Log.Error($"GetDirectoryFileCount error：{ex.Message}");
			}
		}
		private static string FormatElapsedTime(TimeSpan ts)
		{
			return String.Format("{0:00}h:{1:00}m:{2:00}s", ts.Hours, ts.Minutes, ts.Seconds);
		}
	}
}