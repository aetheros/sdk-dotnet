# Aetheros oneM2M .NET Api Documentation

## <xref:api_index>


````c#
// connect to the IN-CSE
using var con = new HttpConnection(new Uri("https://cse.local/"));

// find an existing IN-AE
using var app = await con.FindApplicationAsync(new ApplicationConfiguration { AppId = "my.app.id" });

// create a container under the IN-AE
var container = await app.EnsureContainerAsync("my_container");

// listen for new contentInstance creation
using var subscription = 
  (await container.ObserveContentInstanceAsync<ContentInstanceType>("https://localhost/notify"))
	.Subscribe(ci => {
		Console.WriteLine($"New content instance: {ci.Data}");
	});

// create a new contentInstance
var contentInstance = await app.AddContentInstanceAsync("my_container", new ContentInstanceType { Data = "hello" });

````
