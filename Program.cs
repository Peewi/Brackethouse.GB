// See https://aka.ms/new-console-template for more information
using Brackethouse.GB;
using SDL3;

const int TargetFrameDurationNS = 16740000;
const int TargetFrameDurationNS60Hz = 16666667;
const string ProgramName = "Peewi's GB emulator!";
GB boy = null;
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
if (args.Length > 0)
{
	gamePath = args[0];
}
if (gamePath == "")
{

}
else
{
	boy = new GB(gamePath, renderer);
	SDL.SetWindowTitle(window, $"{ProgramName} {boy.GameTitle}");
}

bool running = true;
ulong sleepCompensation = 0;
while (running)
{
	ulong frameStart = SDL.GetTicksNS();
	boy?.Step();
	while (SDL.PollEvent(out var e))
	{
		SDL.EventType t = (SDL.EventType)e.Type;
		if (t == SDL.EventType.Quit)
		{
			running = false;
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