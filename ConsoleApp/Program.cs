using System;
using System.Threading;
using System.Threading.Tasks;
using ChromeProfileAspect;


namespace ConsoleApp
{
	
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");

			ChromeProfileManager.Enter(null, System.Reflection.MethodBase.GetCurrentMethod());

			Thread.Sleep(1000);

			ChromeProfileManager.Leave(null, System.Reflection.MethodBase.GetCurrentMethod());


			Task.Factory.StartNew( () => {

				ChromeProfileManager.Enter(null, System.Reflection.MethodBase.GetCurrentMethod());

				Thread.Sleep(1000);

				ChromeProfileManager.Leave(null, System.Reflection.MethodBase.GetCurrentMethod());


			}).Wait();


		}
	}
}
