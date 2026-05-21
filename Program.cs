using Newtonsoft.Json;
using RestSharp;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

string hardcodedJwtSecret = "THIS_IS_A_SUPER_SECRET_KEY_123456";
string hardcodedPassword = "AdminPassword123!";
string connectionString =
    "Server=localhost;Database=ProdDb;User Id=sa;Password=SuperPassword123;TrustServerCertificate=true";

// Health endpoint
app.MapGet("/", () =>
{
    return Results.Ok("Vulnerable API Running");
});

// Good one
app.MapGet("/health", () =>
{
    return Results.Ok("Healthy2 ");
}); 

// SQL Injection
app.MapGet("/users", async (string username) =>
{
    using var connection = new SqlConnection(connectionString);

    await connection.OpenAsync();

    string sql =
        $"SELECT * FROM Users WHERE Username = '{username}'";

    using var command = new SqlCommand(sql, connection);

    using var reader = await command.ExecuteReaderAsync();

    var results = new List<object>();

    while (await reader.ReadAsync())
    {
        results.Add(new
        {
            Id = reader["Id"],
            Username = reader["Username"]
        });
    }

    return Results.Ok(results);
});


// Command Injection
app.MapGet("/ping", (string host) =>
{
    var process = new Process();

    process.StartInfo.FileName = "cmd.exe";
    process.StartInfo.Arguments = $"/c ping {host}";
    process.StartInfo.RedirectStandardOutput = true;
    process.Start();

    string output = process.StandardOutput.ReadToEnd();

    return Results.Text(output);
});


// SSRF
app.MapGet("/fetch", async (string url) =>
{
    var client = new RestClient(url);

    var request = new RestRequest();

    var response = await client.ExecuteAsync(request);

    return Results.Text(response.Content ?? "");
});


// Path Traversal
app.MapGet("/file", (string path) =>
{
    var content = File.ReadAllText(path);

    return Results.Text(content);
});


// Weak Crypto
app.MapGet("/md5", (string value) =>
{
    using var md5 = MD5.Create();

    var bytes = Encoding.UTF8.GetBytes(value);

    var hash = md5.ComputeHash(bytes);

    return Convert.ToHexString(hash);
});


// XXE
app.MapPost("/xml", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);

    string xml = await reader.ReadToEndAsync();

    XmlDocument doc = new XmlDocument();

    doc.XmlResolver = new XmlUrlResolver();

    doc.LoadXml(xml);

    return Results.Ok(doc.InnerText);
});


// Insecure Deserialization
app.MapPost("/deserialize", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);

    string body = await reader.ReadToEndAsync();

    var obj = JsonConvert.DeserializeObject<object>(
        body,
        new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

    return Results.Ok(obj);
});


// Open Redirect
app.MapGet("/redirect", (string url) =>
{
    return Results.Redirect(url);
});


// Sensitive Data Exposure
app.MapGet("/debug", () =>
{
    return Results.Ok(new
    {
        Password = hardcodedPassword,
        JwtSecret = hardcodedJwtSecret,
        ConnectionString = connectionString,
        MachineName = Environment.MachineName,
        User = Environment.UserName,
        CurrentDirectory = Environment.CurrentDirectory
    });
});


// Hardcoded Credentials
app.MapGet("/login", (string username, string password) =>
{
    if (username == "admin" &&
        password == "Password123")
    {
        return Results.Ok("Logged in");
    }

    return Results.Unauthorized();
});


// Dangerous Reflection
app.MapGet("/type", (string type) =>
{
    var loadedType = Type.GetType(type);

    return Results.Ok(loadedType?.FullName);
});


// Dangerous File Upload
app.MapPost("/upload", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    var file = form.Files[0];

    var uploadPath =
        Path.Combine("uploads", file.FileName);

    using var stream =
        File.Create(uploadPath);

    await file.CopyToAsync(stream);

    return Results.Ok(uploadPath);
});


// Information Disclosure
app.MapGet("/env", () =>
{
    return Results.Ok(Environment.GetEnvironmentVariables());
});

app.Run();