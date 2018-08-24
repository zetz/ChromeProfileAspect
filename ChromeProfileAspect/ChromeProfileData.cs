using System;
using System.Collections.Generic;

namespace ChromeProfileAspect
{
	public class ChromeProfileData
	{
		[Serializable]
		public struct Record
		{
			public string name;
			//public string cat;
			public string ph;
			public long ts;
			public long dur;
			public int pid;
			public int tid;

			[Serializable]
			public struct Args
			{
				public string path;
				//public long instanceId;
				//public long sampleId;
			}
			public Args args;
		}

		public readonly List<Record> traceEvents = new List<Record>(10000);


		public struct StackInfo
		{
			public string methodName;
			public long ts;
		}

		[NonSerialized]
		public readonly Stack<StackInfo> callStack = new Stack<StackInfo>(10);
	}
}
