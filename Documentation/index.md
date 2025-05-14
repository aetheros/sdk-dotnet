# Aetheros oneM2M .NET Api Documentation

## <xref:api_index>


````c#
// connect to the IN-CSE
using var con = new HttpConnection(new Uri("https://cse.local/"));

// find an existing IN-AE (by AE-ID)
using var app = await con.FindApplicationAsync("Cmy-ae");

// or create a new one...
if (app == null) {
	var config = new ApplicationConfiguration {
		AppId = "Nmy-app-id",
		AppName = "my-ae",
		CredentialId = "...",
		PoaUrl = "http://my-server:21300/notify",
	};
	app = await connection.RegisterApplicationAsync(config);
	Console.WriteLine($"New AE-ID: {app.AE_ID}");
}

// create a container under the IN-AE
var container = await app.EnsureContainerAsync("my_container");

// listen for new contentInstance creation
using var observable = await container.ObserveContentInstanceAsync<ContentInstanceType>(container.ResourceName);
using var subscription = observable.Subscribe(
	ci => {
		// handle notification...
		Console.WriteLine($"New content instance: {ci.Data}");
	}
);

// create a new contentInstance
var contentInstance = await app.AddContentInstanceAsync(
	container.ResourceName,
	new ContentInstanceType { Data = "hello" }
);

````
