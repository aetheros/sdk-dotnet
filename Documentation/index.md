# Aetheros oneM2M .NET Api Documentation

## <xref:api_index>

Markup test:

````c#
using var con = new HttpConnection("https://cse.local/");
using var app = await con.FindApplicationAsync("IN_CSE", "my.app.id");
var container = await app.EnsureContainerAsync("my_container");

using var subscription = 
  (await container.ObserveAsync<ContentInstanceType>("https://localhost/notify"))
	.Subscribe(ci => {
		Console.WriteLine($"New content instance: {ci.Data}");
	});
	
var contentInstance = await app.AddContentInstanceAsync("my_container", new ContentInstanceType { Data = "hello" });

````
