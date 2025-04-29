// See https://aka.ms/new-console-template for more information
using Brackethouse.GB;
using SDL3;
using System.Runtime.InteropServices;

const int TargetFrameDurationNS = 16740000;
const int TargetFrameDurationNS60Hz = 16666667;
bool Running = true;
const string ProgramName = "Peewi's GB emulator!";
GB? boy = null;
Console.WriteLine(ProgramName);
if (!SDL.Init(SDL.InitFlags.Video))
{
	SDL.LogError(SDL.LogCategory.System, $"SDL could not initialize: {SDL.GetError()}");
	return;
}
if (!SDL.CreateWindowAndRenderer(ProgramName, 480, 432, 0, out var window, out var renderer))
{
	SDL.LogError(SDL.LogCategory.Application, $"Error creating window and rendering: {SDL.GetError()}");
	return;
}
SDL.SetRenderLogicalPresentation(renderer, 160, 144, SDL.RendererLogicalPresentation.Letterbox);
SDL.SetWindowResizable(window, true);

string gamePath = "";
string savePath = "";
if (args.Length > 0)
{
	gamePath = args[0];
}
if (gamePath == "")
{
	void cb(nint userdata, nint filelist, int filter)
	{
		if (filelist != IntPtr.Zero)
		{
			nint strPtr = Marshal.ReadIntPtr(filelist);
			string? filepath = Marshal.PtrToStringUTF8(strPtr);
			if (filepath != null)
			{
				gamePath = filepath;
				StartGB(gamePath, renderer);
			}
			else
			{
				SDL.ShowSimpleMessageBox(SDL.MessageBoxFlags.Error, "No file chosen", "No ROM file chosen. Shutting down.", window);
				Running = false;
			}
		}
		else
		{
			SDL.ShowSimpleMessageBox(SDL.MessageBoxFlags.Error, "Error", SDL.GetError(), window);
		}
	}
	SDL.DialogFileFilter gameBoyFilter = new("Game Boy ROM", "gb;gbc");
	SDL.DialogFileFilter allFilter = new("all files", "*");
	SDL.ShowOpenFileDialog(cb, 0, window, [gameBoyFilter, allFilter], 2, null, false);
}
else
{
	StartGB(gamePath, renderer);
}

ulong sleepCompensation = 0;
while (Running)
{
	ulong frameStart = SDL.GetTicksNS();
	boy?.Step();
	while (SDL.PollEvent(out var e))
	{
		SDL.EventType t = (SDL.EventType)e.Type;
		if (t == SDL.EventType.Quit)
		{
			Running = false;
		}
	}
	ulong frameEnd = SDL.GetTicksNS();
	ulong frameDuration = frameEnd - frameStart;
	if (frameDuration < TargetFrameDurationNS)
	{
		ulong desiredSleep = TargetFrameDurationNS - frameDuration - sleepCompensation;
		SDL.DelayNS(desiredSleep);
		ulong actualSleep = SDL.GetTicksNS() - frameEnd;
		sleepCompensation = actualSleep - desiredSleep;
	}
}
boy?.Save(savePath);

void StartGB(string path, nint renderer)
{
	string dir = Path.GetDirectoryName(path) ?? string.Empty;
	string f = (Path.GetFileNameWithoutExtension(path) ?? string.Empty) + ".sav";
	savePath = Path.Combine(dir, f);
	boy = new GB(gamePath, savePath, renderer);
	SDL.SetWindowTitle(window, $"{ProgramName} {boy.GameTitle}");
}