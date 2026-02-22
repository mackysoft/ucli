using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddSingleton<GreetingService>();

using var provider = services.BuildServiceProvider();

var app = ConsoleApp.Create();

app.Add("hello", (string name) =>
{
    var greeter = provider.GetRequiredService<GreetingService>();
    Console.WriteLine(greeter.Greet(name));
});

app.Run(args);

sealed class GreetingService
{
    public string Greet (string name) => $"Hello, {name}!";
}