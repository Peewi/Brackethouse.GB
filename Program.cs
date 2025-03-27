// See https://aka.ms/new-console-template for more information
using Brackethouse.GB;

Console.WriteLine("Hello, World!");
GB boy = new GB(args[0]);
while (true)
{
	boy.Step();
}