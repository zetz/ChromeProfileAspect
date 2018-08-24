using System;
using ChromeProfileAspect;

namespace ConsoleApp
{
	public class GameObject
	{
		private Random _random = new Random();

		[ChromeProfileAttribute]
		public void Update(float elapsed) {
			// 
			ProcessMessages();

			ProcessPackets();

			ProcessInputs();
		}

		[ChromeProfileAttribute]
		private void ProcessMessages() {
			// 
			System.Threading.Thread.Sleep(_random.Next(5, 10));
		}

		[ChromeProfileAttribute]
		private void ProcessPackets() {
			System.Threading.Thread.Sleep(_random.Next(5, 10));
		}

		[ChromeProfileAttribute]
		private void ProcessInputs() {
			System.Threading.Thread.Sleep(_random.Next(5, 10));
		}
	}
}
