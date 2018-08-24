using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp
{

	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");

			var objects = new List<GameObject>();
			for (int i = 0; i < 100; ++i) {
				objects.Add(new GameObject());
			}


			var fiveSecondsTask = Task.Factory.StartNew(() => {
				System.Threading.Thread.Sleep(5000);
			});

			while (fiveSecondsTask.IsCompleted == false) {

				var result = Parallel.ForEach(objects, obj => {
					obj.Update(0);
				});

			}
		}
	}
}
