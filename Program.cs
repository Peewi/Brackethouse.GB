// See https://aka.ms/new-console-template for more information
using Brackethouse.GB;
using SDL3;

Console.WriteLine("Laus GB emulator!");
if (!SDL.Init(SDL.InitFlags.Video))
{
	SDL.LogError(SDL.LogCategory.System, $"SDL could not initialize: {SDL.GetError()}");
	return;
}
if (!SDL.CreateWindowAndRenderer("Laus GB emulator!", 480, 432, 0, out var window, out var renderer))
{
	SDL.LogError(SDL.LogCategory.Application, $"Error creating window and rendering: {SDL.GetError()}");
	return;
}
SDL.SetRenderLogicalPresentation(renderer, 160, 144, SDL.RendererLogicalPresentation.Letterbox);


GB boy = new GB(args[0], renderer);
bool running = true;
while (running)
{
	while (SDL.PollEvent(out var e))
	{
		if ((SDL.EventType)e.Type == SDL.EventType.Quit)
		{
			running = false;
		}
	}
	boy.Step();
}