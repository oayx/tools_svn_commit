using SharpSvn;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace SVNTools
{
	public class SVNUtils
    {
        static SvnClient client = new SvnClient();

        /// <summary>
        /// add单个文件或目录
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool SvnAddFile(string file)
        {
            SvnAddArgs args = new SvnAddArgs();
            args.Depth = SvnDepth.Empty;

			try
            {
                if (client.Add(file, args))
				{
					return true;
				}
			}
            catch (Exception ex)when (!ex.Message.Contains("is already under version control"))
            {
                Log.Error($"SvnAddFile:{file}, Error {ex.InnerException.Message}");
                return false;
            }
            catch 
            {
			}
			return false;
		}
        /// <summary>
        /// 只提交文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static bool SvnCommitPaths(string[] paths, string message)
        {
            SvnCommitArgs args = new SvnCommitArgs();
            args.Depth = SvnDepth.Empty;
			args.LogMessage = message;

            try
            {
                if (!client.Commit(paths, args))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"SvnCommitPaths Error {ex.InnerException.Message}");
                return false;
            }

            return true;
        }
        /// <summary>
        /// 只提交文件
        /// </summary>
        /// <param name="files"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static bool SvnCommitFiles(string[] files, string message)
        {
            SvnCommitArgs args = new SvnCommitArgs();
            args.Depth = SvnDepth.Files;
            args.LogMessage = message;

            try
            {
                if (!client.Commit(files, args))
                {
                    return false;
                }
			}
			catch (Exception ex)
            {
                Log.Error($"SvnCommitFiles Error {ex.InnerException.Message}");
                return false;
            }

            return true;
		}
		/// <summary>
		/// 提交文件夹和里面的文件
		/// </summary>
		/// <param name="path"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		public static bool SvnCommitPathAndFiles(string path, string message)
		{
			SvnCommitArgs args = new SvnCommitArgs();
			args.Depth = SvnDepth.Infinity;
			args.LogMessage = message;

			try
			{
				if (!client.Commit(path, args))
				{
					return false;
				}
			}
			catch (Exception ex)
			{
				Log.Error($"SvnCommitPathAndFiles:{path}, Error {ex.InnerException.Message}");
				return false;
			}

			return true;
		}

		public static bool SvnUpdata(string path, ref List<string> files)
        {
            bool result = true;

            try
            {
                Collection<SvnStatusEventArgs> statuses;
                client.GetStatus(path, out statuses);
                foreach (var item in statuses)
                {
                    if (item.Conflicted)
                    {
                        result = false;
                        string dirPath = Path.GetDirectoryName(item.Path);
                        client.CleanUp(dirPath);
                        SvnRevertArgs arg = new SvnRevertArgs() { Depth = SvnDepth.Infinity };
                        client.Revert(item.Path, arg);
                    }
                }
            }
            catch (Exception ex)
            {
				Log.Error($"Svn GetStatus Error {ex.Message}");
				return false;
            }

            if (!result)
            {
                return false;
            }

            List<string> files_ = new List<string>();
            SvnUpdateArgs ua = new SvnUpdateArgs();
            ua.Notify += delegate (object sender, SvnNotifyEventArgs e) {
                if (e.Action != SvnNotifyAction.UpdateStarted && e.Action != SvnNotifyAction.UpdateCompleted)
                {
                    Log.Info($"Svn Change {e.Action}, {e.Path}");
                    files_.Add(e.FullPath);
                }
            };

            try
            {
                if (!client.Update(path, ua))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Svn Update Error {ex.Message}");
                return false;
            }


            files.AddRange(files_);
            return true;
        }

        public static bool SvnClean(string path)
        {
            Log.Info("Svn Clean {path}");

            client.CleanUp(path);
            client.Resolve(path, SvnAccept.TheirsFull);
            return true;
        }

        public static int ExcuteCmdAtPath(string pathToCmd, string args, string pathToWorkDir)
        {
            Log.Info($"Exec Cmd: {DateTime.Now.ToString()} {pathToCmd} {args}");

            var currentPath = Directory.GetCurrentDirectory();

            if (pathToWorkDir != null)
            {
                Directory.SetCurrentDirectory(pathToWorkDir);
            }


            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = pathToCmd;

            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.ErrorDialog = true;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.Arguments = args;

            Process process = Process.Start(info);
            process.WaitForExit();

            Directory.SetCurrentDirectory(currentPath);
            Log.Info($"Exec Cmd Done: {pathToCmd} {process.ExitCode}");
            if(process.ExitCode != 0)
            {
                Log.Error("Unity Exe 执行失败");
            }
            return process.ExitCode;
        }
    }
}
