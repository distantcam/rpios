using System.Diagnostics;

pvc.Task("assemble", () => {
	pvc.Source("source\\*.s")
		.Pipe((streams) => {
			foreach (var stream in streams) {
				Process.Start("arm-none-eabi-as", 
					string.Format(
						"-I source\\ {0} -o build\\{1}.o", 
						stream.StreamName, 
						Path.GetFileNameWithoutExtension(stream.StreamName)))
				.WaitForExit();
			}
			return new PvcStream[0];
		});
});

pvc.Task("link", () => {
	pvc.Source("build\\*.o")
		.Pipe((streams) => {

			var objectFiles = string.Join(" ", streams
				.Select(s => s.StreamName));
			
			Process.Start("arm-none-eabi-ld", 
				string.Format(
					"--no-undefined {0} -Map kernel.map -o build\\kernel.elf -T kernel.ld", 
					objectFiles))
			.WaitForExit();
			
			return new PvcStream[0];
		});
}).Requires("assemble");

pvc.Task("compile", () => {
	pvc.Source("source\\*.c")
		.Pipe((streams) => {
			Directory.CreateDirectory("build");

			var sourceFiles = string.Join(" ", streams
				.Select(s => s.StreamName));

			Process.Start("arm-none-eabi-gcc", 
				string.Format(
					"-O2 -mfpu=vfp -mfloat-abi=hard -march=armv6zk -mtune=arm1176jzf-s -nostartfiles {0} -o build\\kernel.elf", 
					sourceFiles))
			.WaitForExit();

			return new PvcStream[0];
		});
});

pvc.Task("image", () => {
	pvc.Source("build\\*.elf")
		.Pipe((streams) => {
			foreach (var stream in streams) {
				Process.Start("arm-none-eabi-objcopy", 
					string.Format(
						"{0} -O binary {1}.img",
						stream.StreamName, 
						Path.GetFileNameWithoutExtension(stream.StreamName)))
					.WaitForExit();
			}
			return new PvcStream[0];
		});
}).Requires("compile", "link");

pvc.Task("list", () => {
	pvc.Source("build\\*.elf")
		.Pipe((streams) => {
			var enviromentPath = Environment.GetEnvironmentVariable("PATH");

			var exePath = enviromentPath.Split(';')
				.Select(x => Path.Combine(x, "arm-none-eabi-objdump.bat"))
				.FirstOrDefault(x => File.Exists(x));

			var outStreams = new List<PvcStream>(streams.Count());

			foreach (var stream in streams) {
				var fileName = Path.GetFileNameWithoutExtension(stream.StreamName);

				var process = new Process();
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.FileName = exePath;
				process.StartInfo.Arguments = "-d build\\" + fileName + ".elf";
				process.Start();
				var output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				outStreams.Add(PvcUtil.StringToStream(output, fileName + ".list"));
			}

			return outStreams;
		})
		.Save(".");
}).Requires("compile", "link");

pvc.Task("default", () => { Directory.CreateDirectory("build"); }).Requires("image", "list");

pvc.Task("clean", () => {
	File.Delete("kernel.img");
	File.Delete("kernel.list");
	File.Delete("kernel.map");

	Directory.Delete("build", true);
});