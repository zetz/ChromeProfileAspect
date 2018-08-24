using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ChromeProfileAspect;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ChromeProfileBuilder
{
	class Program
	{
		static bool Inject_ChromeProfileScope(AssemblyDefinition assemblyDefinition, MethodDefinition method)
		{
			if (!method.HasCustomAttributes)
				return false;

			var aspectAttr = method.CustomAttributes.FirstOrDefault(ca => ca.AttributeType.Name.StartsWith(nameof(ChromeProfileAttribute)));
			if (aspectAttr == null)
				return false;

			if (method.Body == null)
				return false;

			if (method.IsSetter || method.IsGetter)
				return false;

			var getCurrentMethod = typeof(System.Reflection.MethodBase).GetMethod(nameof(System.Reflection.MethodBase.GetCurrentMethod));
			var getCurrentMethodRef = assemblyDefinition.MainModule.ImportReference(getCurrentMethod);

			var logginAspectBefore = typeof(ChromeProfileManager).GetMethod(nameof(ChromeProfileManager.Enter), new[] { typeof(object), typeof(MethodBase) });
			var logginAspectBeforeRef = assemblyDefinition.MainModule.ImportReference(logginAspectBefore);

			var logginAspectAfter = typeof(ChromeProfileManager).GetMethod(nameof(ChromeProfileManager.Leave), new[] { typeof(object), typeof(MethodBase) });
			var logginAspectAfterRef = assemblyDefinition.MainModule.ImportReference(logginAspectAfter);

			var il = method.Body.GetILProcessor();
			var first = il.Body.Instructions.First();
			{
				il.InsertBefore(first,
					method.Body.ThisParameter != null
						? Instruction.Create(OpCodes.Ldarg_0)
						: Instruction.Create(OpCodes.Ldnull));
				il.InsertBefore(first, Instruction.Create(OpCodes.Call, getCurrentMethodRef));
				il.InsertBefore(first, Instruction.Create(OpCodes.Call, logginAspectBeforeRef));
			}

			{
				var last = il.Body.Instructions.Last();
				last.OpCode = OpCodes.Nop;  // old Ret -> Nop

				il.InsertAfter(last, Instruction.Create(OpCodes.Ret)); // new Ret
				last = il.Body.Instructions.Last();
				il.InsertBefore(last,
					method.Body.ThisParameter != null
						? Instruction.Create(OpCodes.Ldarg_0)
						: Instruction.Create(OpCodes.Ldnull));
				il.InsertBefore(last, Instruction.Create(OpCodes.Call, getCurrentMethodRef));
				il.InsertBefore(last, Instruction.Create(OpCodes.Call, logginAspectAfterRef));
			}


			method.CustomAttributes.Remove(aspectAttr);
			return true;
		}

		static void Main(string[] args)
		{
			string targetPath = null;
			if (args.Length > 0) {
				targetPath = args[0];
			} 

			if (string.IsNullOrEmpty(targetPath)) {
				Show();
				return;
			}

			if (System.IO.Directory.Exists(targetPath) == false) {
				Show();
				return;
			}

			var backupDirectory = System.IO.Directory.GetCurrentDirectory();
			var fullPath = System.IO.Path.GetFullPath(targetPath);
			System.IO.Directory.SetCurrentDirectory(fullPath);
			var assemblies = new List<string>();
			{
				assemblies.AddRange(System.IO.Directory.GetFiles(fullPath, "*.dll"));
				assemblies.AddRange(System.IO.Directory.GetFiles(fullPath, "*.exe"));
			}

			try {
				foreach (var filename in assemblies) {
					try {
						var rp = new ReaderParameters();
						rp.ReadSymbols = false;
						rp.ReadWrite = true;
						rp.InMemory = true;

						var assemblyDef = AssemblyDefinition.ReadAssembly(filename, rp);

						bool changed = false;
						foreach (var module in assemblyDef.Modules) {
							foreach (var type in module.Types) {
								if (type.Name == "<Module>") continue;
								foreach (var method in type.Methods) {

									if (!method.HasCustomAttributes)
										continue;

									var aspectAttr = method.CustomAttributes.FirstOrDefault(ca => ca.AttributeType.Name.StartsWith(nameof(ChromeProfileAttribute)));
									if (aspectAttr == null)
										continue;

									bool injected = Inject_ChromeProfileScope(assemblyDef, method);
									if (injected) {
										Console.WriteLine($"Inject!! {method.DeclaringType.Name}.{method.Name}");
									}
									changed |= injected;
								}
							}
						}

						if (changed) {
							// save to file
							Console.WriteLine($"Overwriting!! {filename}");
							assemblyDef.Write(filename, new WriterParameters() {
								WriteSymbols = false,

							});
							Console.WriteLine($"Success");
						}
					} catch (Exception e) {
						Console.WriteLine("{0}\r\n{1}", filename, e);

					}
				}
			} catch (Exception e) {
				Console.WriteLine(e);
			}
			System.IO.Directory.SetCurrentDirectory(backupDirectory);

		}

		static void Show()
		{
			Console.WriteLine("========= ChromeProfileBuilder ===============");
			Console.WriteLine("usage : ChromeProfileBuilder.exe [TARGET_PATH]");
		}
	}
}