using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace ChromeProfileAspect
{
	public static class ChromeProfileManager
	{
		static readonly Stopwatch _global_sw;
		// ThreadLocal 사용하지 못해서 임의로 구현
		static readonly Dictionary<int, ChromeProfileData> _profilesPerThread = new Dictionary<int, ChromeProfileData>();

		const double TracingThreshold = 0;  // micro seconds

		static bool _enabled = false;

		static readonly int _pid = 0;

		static string _log_fileName;

		static Thread _backgroundSaver;

		static HashSet<Thread> _threads = new HashSet<Thread>();

		static ChromeProfileManager()
		{
			_global_sw = Stopwatch.StartNew();
			_pid = Process.GetCurrentProcess().Id;

			_log_fileName = $"chrome_tracing_{DateTime.Now.ToString("yyMMdd-HHmmss")}";
		}

		static string GetProfileName(MethodBase method)
		{
			return method.DeclaringType != null
				? $"{method.DeclaringType.Name}.{method.Name}()"
				: $"{method.Name}()";
		}

		public static void Enter(object caller, MethodBase method)
		{
			_enabled = true;

			var tid = System.Threading.Thread.CurrentThread.ManagedThreadId;

			lock (_threads) {
				_threads.Add(System.Threading.Thread.CurrentThread);
			}

			ChromeProfileData data;
			lock (_profilesPerThread) {
				if (_profilesPerThread.TryGetValue(tid, out data) == false) {
					data = new ChromeProfileData();
					_profilesPerThread.Add(tid, data);
				}
			}

			data.callStack.Push(new ChromeProfileData.StackInfo() {
				methodName = GetProfileName(method),
				ts = _global_sw.ElapsedTicks * 1000000 / Stopwatch.Frequency,
			});
		}

		public static void Leave(object caller, MethodBase method)
		{
			var tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
			ChromeProfileData data;
			lock (_profilesPerThread) {
				if (_profilesPerThread.TryGetValue(tid, out data) == false) {
					data = new ChromeProfileData();
					_profilesPerThread.Add(tid, data);
				}
			}

			var curr_ts = _global_sw.ElapsedTicks * 1000000 / Stopwatch.Frequency;
			var path = string.Join("/", data.callStack.Select(x => x.methodName).Reverse().ToArray());
			var stackInfo = data.callStack.Pop();
			var dur = curr_ts - stackInfo.ts;
			if (dur > TracingThreshold) {
				data.traceEvents.Add(new ChromeProfileData.Record() {
					name = stackInfo.methodName,
					ts = stackInfo.ts,
					pid = _pid,
					tid = tid,
					ph = "X",
					dur = dur,
					args = {
						path = path
					}
				});
			}

			StratBackgroundSaver();
		}

		private static void AppendToFile(string filename = "chrome_profile.json")
		{
			lock (_profilesPerThread) {
				string headline;
				if (System.IO.File.Exists(filename)) {
					using (TextReader file = new StreamReader(filename)) {
						headline = file.ReadToEnd();
					}
					headline = headline.Replace("]}", ",");
				} else {
					headline = "{\"traceEvents\":[";
				}

				using (TextWriter file = new StreamWriter(filename)) {
					file.Write(headline);
					foreach (var data in _profilesPerThread.Values) {
						foreach (var n in data.traceEvents) {
							file.Write($"{{\"pid\":{n.pid},\"tid\":{n.tid},\"ph\":\"{n.ph}\",\"ts\":{n.ts},\"dur\":{n.dur},\"name\":\"{n.name}\",\"args\":{{\"path\":\"{n.args.path}\"}}}},\n");
						}
						data.traceEvents.Clear();
					}
					file.Write("{}]}");
				}
			}
		}

		private static bool SaveToFile(string name, int split_index, int skipLogCount)
		{
			lock (_profilesPerThread) {
				var count = _profilesPerThread.Values.Aggregate(0, (i, data) => i + data.traceEvents.Count);
				if (count < skipLogCount) {
					return false;
				}

				var filename = $"{name}_{split_index}.json";
				using (TextWriter file = new StreamWriter(filename)) {
					file.Write("{\"traceEvents\":[\n");
					foreach (var data in _profilesPerThread.Values) {
						foreach (var n in data.traceEvents) {
							file.Write($"{{\"pid\":{n.pid},\"tid\":{n.tid},\"ph\":\"{n.ph}\",\"ts\":{n.ts},\"dur\":{n.dur},\"name\":\"{n.name}\",\"args\":{{\"path\":\"{n.args.path}\"}}}},\n");
						}
						data.traceEvents.Clear();
					}
					file.Write("{}]}");
					return true;
				}
			}
		}

		private static DateTime _lastSaveToFile;
		private static int _split_index = 0;
		public static void EndFrame()
		{
			if (_enabled == false)
				return;

			var elapsedSpan = System.DateTime.Now - _lastSaveToFile;
			if (elapsedSpan.TotalSeconds > 10) {
				if (SaveToFile(_log_fileName, _split_index, 10000)) {        // 로그 수량이 10000건 이상일때 파일 덤프
					_split_index++;
				}
				_lastSaveToFile = DateTime.Now;
			}
		}

		public static void Exit()
		{
			if (_enabled == false)
				return;

			SaveToFile(_log_fileName, _split_index, 0);
		}


		private static void SaveToFile() 
		{


		}

		private static void StratBackgroundSaver()
		{
			if (_backgroundSaver != null)
				return;

			_backgroundSaver = new Thread(BackgroundSaver) {
				Priority = ThreadPriority.Lowest,
				IsBackground = true,
			};
		}

		private static void BackgroundSaver()
		{
			while (true)
			{
				lock (_threads) {
					// 로그를 남겼던 스레드들이 모두 종료 되었으면 백그라운드 로거도 종료하자.
					if (_threads.All(thd => thd.IsAlive == false)) {
						break;
					}
					_threads.Clear();
				}
				EndFrame();
				Thread.Sleep(1000);
			}

			_backgroundSaver = null;
		}
	}
}